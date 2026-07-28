using System.Management;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest Laufwerksgröße/Belegung (ohne Admin) und SMART-Rohwerte (root\WMI, braucht Admin).
/// Gibt bei fehlenden Rechten IMMER ein Ergebnis mit Erklärung zurück statt einer Exception.
/// </summary>
public class SmartService
{
    public async Task<List<DiskInfo>> GetDisksAsync()
    {
        return await Task.Run(() =>
        {
            var disks = new List<DiskInfo>();

            // 1) Kapazität + freier Platz je logischem Laufwerksbuchstaben (kein Admin nötig)
            using var logical = new ManagementObjectSearcher(
                "SELECT DeviceID, Size, FreeSpace, DriveType FROM Win32_LogicalDisk WHERE DriveType=3 OR DriveType=2");
            foreach (var o in logical.Get())
            {
                var sizeBytes = Convert.ToDouble(o["Size"] ?? 0);
                var freeBytes = Convert.ToDouble(o["FreeSpace"] ?? 0);
                var disk = new DiskInfo
                {
                    DeviceId = o["DeviceID"]?.ToString() ?? "",
                    MediaType = Convert.ToInt32(o["DriveType"]) == 2 ? "USB/Wechseldatenträger" : "Fest",
                    SizeGb = Math.Round(sizeBytes / 1024 / 1024 / 1024, 1),
                    FreeGb = Math.Round(freeBytes / 1024 / 1024 / 1024, 1),
                };
                disk.SpeicherplatzAmpel = disk.FreePercent switch
                {
                    > 20 => AmpelStatus.Gruen,
                    >= 10 => AmpelStatus.Gelb,
                    _ => AmpelStatus.Rot
                };
                disks.Add(disk);
            }

            // 2) Physische Laufwerke über die echte WMI-Verknüpfung zuordnen.
            //
            // Früher (bis 27.07.2026) wurde das Modell einfach an "das erste noch
            // unbenannte Laufwerk" gehängt. Auf Alex' PC mit 18 Laufwerksbuchstaben
            // kam dabei Unsinn heraus: C: (eine NVMe) wurde als "External USB3.0
            // USB Device" bezeichnet. Jetzt wird die Kette
            //   Win32_DiskDrive -> Win32_DiskDriveToDiskPartition -> Win32_DiskPartition
            //                   -> Win32_LogicalDiskToPartition   -> Win32_LogicalDisk
            // wirklich abgelaufen. Laufwerke ohne physische Entsprechung (virtuelle
            // oder Netzlaufwerke, z.B. eingebundener Cloud-Speicher) bleiben bewusst
            // ohne Modellnamen statt einen falschen zu bekommen.
            try
            {
                using var physical = new ManagementObjectSearcher(
                    "SELECT Model, MediaType, DeviceID, PNPDeviceID FROM Win32_DiskDrive");
                foreach (var o in physical.Get())
                {
                    var model = o["Model"]?.ToString()?.Trim() ?? "";
                    var physId = o["DeviceID"]?.ToString() ?? "";
                    var pnpId = o["PNPDeviceID"]?.ToString() ?? "";
                    if (physId.Length == 0) continue;

                    foreach (var buchstabe in LogischeLaufwerkeZu(physId))
                    {
                        var ziel = disks.FirstOrDefault(d =>
                            d.DeviceId.Equals(buchstabe, StringComparison.OrdinalIgnoreCase));
                        if (ziel == null) continue;

                        ziel.Model = model;
                        ziel.PnpDeviceId = pnpId;
                        ziel.PhysischesLaufwerk = physId;
                    }
                }
            }
            catch
            {
                // Modellname/Zuordnung ist "nice to have" – kein Abbruch, falls eine
                // der WMI-Klassen fehlt. Dann bleibt das Modell eben leer.
            }

            // 3) SMART-Rohwerte aus root\WMI (braucht i.d.R. Administrator-Rechte)
            try
            {
                var scope = new ManagementScope(@"root\WMI");
                scope.Connect();

                using var statusSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus"));
                using var dataSearcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT InstanceName, VendorSpecific FROM MSStorageDriver_FailurePredictData"));

                var vorausfallProInstanz = new Dictionary<string, bool>();
                foreach (var o in statusSearcher.Get())
                {
                    var instanz = o["InstanceName"]?.ToString() ?? "";
                    vorausfallProInstanz[instanz] = Convert.ToBoolean(o["PredictFailure"] ?? false);
                }

                // Bekannte PNPDeviceIDs aus Schritt 2 - darüber wird zugeordnet.
                var bekanntePnpIds = disks
                    .Select(d => d.PnpDeviceId)
                    .Where(p => p.Length > 0)
                    .Distinct()
                    .ToList();

                foreach (var o in dataSearcher.Get())
                {
                    var instanz = o["InstanceName"]?.ToString() ?? "";
                    var raw = o["VendorSpecific"] as byte[];
                    if (raw == null) continue;

                    // Kern der Korrektur: das Laufwerk wird über die Geräte-Kennung
                    // gefunden, NICHT über die Reihenfolge. Vorher konnte die
                    // Ausfallwarnung einer Platte am falschen Laufwerk erscheinen -
                    // auf Alex' PC bekam C: die Warnung von D: angezeigt. Bei
                    // "Backup machen, Laufwerk ersetzen" ist das keine Kleinigkeit.
                    var pnpId = SmartInstanzZuordnung.FindePnpId(instanz, bekanntePnpIds);
                    if (pnpId == null) continue;   // keine eindeutige Zuordnung -> lieber nichts

                    var attrs = SmartAttributeParser.Parse(raw);
                    var vorausfall = vorausfallProInstanz
                        .FirstOrDefault(kv => SmartInstanzZuordnung.Normalisieren(kv.Key)
                                              == SmartInstanzZuordnung.Normalisieren(instanz)).Value;

                    // Ein physisches Laufwerk kann mehrere Laufwerksbuchstaben tragen -
                    // die SMART-Werte gelten dann für alle davon.
                    foreach (var target in disks.Where(d =>
                                 d.PnpDeviceId.Equals(pnpId, StringComparison.OrdinalIgnoreCase)))
                    {
                        target.SmartVerfuegbar = true;
                        target.SmartVorausfallStatus = vorausfall;
                        target.ReallocatedSectors = attrs.GetValueOrDefault((byte)0x05);
                        target.CurrentPendingSectors = attrs.GetValueOrDefault((byte)0xC5);
                        target.PowerOnHours = attrs.GetValueOrDefault((byte)0x09);
                        var tempRaw = attrs.GetValueOrDefault((byte)0xC2);
                        target.TemperaturCelsius = tempRaw > 0 && tempRaw < 150 ? tempRaw : null;

                        (target.SmartAmpel, target.Erklaerung) = BewerteSmart(target);
                    }
                }

                // Laufwerke, die gar keine SMART-Werte liefern, bekommen eine Erklärung
                // statt eines nackten Fragezeichens ohne Text (so war es vorher zu sehen).
                MarkiereSmartAlsGesperrt(disks, Loc.T(
                    "Dieses Laufwerk liefert keine SMART-Werte. Häufig bei USB-Gehäusen (die Brücke gibt sie nicht durch), " +
                    "bei manchen NVMe-Chipsätzen und bei virtuellen Laufwerken. Kein Hinweis auf einen Defekt - " +
                    "es lässt sich darüber nur nichts sagen.",
                    "This drive doesn't report SMART values. Common with USB enclosures (the bridge chip doesn't pass them through), " +
                    "some NVMe chipsets, and virtual drives. Not a sign of a fault - there's simply nothing to report here."));
            }
            catch (UnauthorizedAccessException)
            {
                MarkiereSmartAlsGesperrt(disks, Loc.T(
                    "SMART-Zugriff verweigert → Toolbox als Administrator neu starten.",
                    "SMART access denied → restart Toolbox X as Administrator."));
            }
            catch (ManagementException)
            {
                MarkiereSmartAlsGesperrt(disks, Loc.T(
                    "SMART-Werte nicht verfügbar (WMI-Klasse fehlt, z.B. bei NVMe über manche Chipsätze oder virtuellen Datenträgern).",
                    "SMART values not available (WMI class missing, e.g. for NVMe on some chipsets or virtual drives)."));
            }
            catch (Exception ex)
            {
                MarkiereSmartAlsGesperrt(disks, Loc.T($"SMART-Abfrage fehlgeschlagen: {ex.Message}", $"SMART query failed: {ex.Message}"));
            }

            return disks;
        });
    }

    /// <summary>
    /// Liefert die Laufwerksbuchstaben, die auf einem physischen Laufwerk liegen -
    /// über die echte WMI-Verknüpfung statt über Vermutungen.
    /// </summary>
    private static IEnumerable<string> LogischeLaufwerkeZu(string physischeDeviceId)
    {
        var ergebnis = new List<string>();

        // Die DeviceID (z.B. \\.\PHYSICALDRIVE0) wird hier BEWUSST unverändert
        // eingesetzt und NICHT maskiert. Die naheliegende Annahme, Backslashes
        // müssten für WQL verdoppelt werden, ist an dieser Stelle falsch: mit
        // verdoppelten Backslashes antwortet WMI mit "Nicht gefunden", mit den
        // rohen funktioniert die Abfrage. Genau daran ist die Zuordnung beim
        // ersten Versuch stillschweigend gescheitert (der catch unten hat den
        // Fehler geschluckt, und dann galt für JEDES Laufwerk "kein SMART").
        try
        {
            using var partitionen = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{physischeDeviceId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
            foreach (var p in partitionen.Get())
            {
                var partitionId = p["DeviceID"]?.ToString();
                if (string.IsNullOrEmpty(partitionId)) continue;

                using var logische = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                foreach (var l in logische.Get())
                {
                    var buchstabe = l["DeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(buchstabe)) ergebnis.Add(buchstabe);
                }
            }
        }
        catch
        {
            // Verknüpfung nicht abfragbar (z.B. exotischer Treiber): dann bleibt dieses
            // physische Laufwerk ohne Zuordnung - besser als eine geratene.
        }

        return ergebnis;
    }

    private static void MarkiereSmartAlsGesperrt(List<DiskInfo> disks, string hinweis)
    {
        foreach (var d in disks.Where(d => !d.SmartVerfuegbar))
        {
            d.SmartAmpel = AmpelStatus.Unbekannt;
            d.Erklaerung = hinweis;
        }
    }

    private static (AmpelStatus, string) BewerteSmart(DiskInfo d)
    {
        if (d.SmartVorausfallStatus)
            return (AmpelStatus.Rot, Loc.T(
                "Hersteller meldet drohenden Ausfall → JETZT Backup machen, Laufwerk zeitnah ersetzen.",
                "Manufacturer reports an impending failure → back up NOW and replace the drive soon."));

        if ((d.ReallocatedSectors ?? 0) > 20 || (d.CurrentPendingSectors ?? 0) > 0)
            return (AmpelStatus.Gelb, Loc.T(
                "Erhöhte Reallocated/Pending Sectors. Mögliche Ursachen: (1) SATA-Kabel locker/defekt → Kabel prüfen oder tauschen " +
                "(2) Controller-Problem → anderen Port testen (3) Laufwerk altersschwach → Backup machen, Ersatz einplanen.",
                "Elevated Reallocated/Pending sector counts. Possible causes: (1) loose/faulty SATA cable → check or replace it " +
                "(2) controller issue → try a different port (3) drive nearing end of life → back up and plan a replacement."));

        if ((d.ReallocatedSectors ?? 0) > 0)
            return (AmpelStatus.Gelb, Loc.T(
                "Vereinzelte Reallocated Sectors vorhanden – im Auge behalten, noch kein Grund zur Panik.",
                "A few reallocated sectors present - worth keeping an eye on, no reason to panic yet."));

        return (AmpelStatus.Gruen, Loc.T("Alle SMART-Werte im grünen Bereich.", "All SMART values are in the green."));
    }
}

/// <summary>
/// Minimaler Parser für die 512-Byte VendorSpecific-SMART-Tabelle (ATA-Standardformat: 30 Attribute à 12 Byte, ab Offset 2).
/// </summary>
internal static class SmartAttributeParser
{
    public static Dictionary<byte, long> Parse(byte[] raw)
    {
        var result = new Dictionary<byte, long>();
        const int attributeStart = 2;
        const int attributeLength = 12;
        for (int i = 0; i < 30; i++)
        {
            int offset = attributeStart + i * attributeLength;
            if (offset + attributeLength > raw.Length) break;

            byte id = raw[offset];
            if (id == 0) continue;

            // Rohwert liegt (ATA-üblich) in den Bytes 5-10 der Attribut-Struktur, hier vereinfacht auf 6 Byte little-endian.
            long rawValue = 0;
            for (int b = 0; b < 6; b++)
                rawValue |= (long)raw[offset + 5 + b] << (8 * b);

            result[id] = rawValue;
        }
        return result;
    }
}
