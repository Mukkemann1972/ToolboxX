using System.Management;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest die verbaute Hardware aus - Speicherriegel, Laufwerke und optische Laufwerke,
/// so ähnlich wie man es sonst nur im BIOS sieht.
///
/// Bewusst **reine Anzeige**, nichts Schreibendes und bewusst klein gehalten: Es geht
/// darum, auf einen Blick zu wissen, was drinsteckt (und wo es beim Hersteller Hilfe
/// dazu gibt) - kein zweites Modul neben den schon vorhandenen Laufwerks- und
/// GPU-Modulen. Läuft ohne Admin-Rechte.
/// </summary>
public class HardwareInfoService
{
    public Task<List<SpeicherRiegel>> SpeicherAsync() => Task.Run(Speicher);

    private static List<SpeicherRiegel> Speicher()
    {
        var riegel = new List<SpeicherRiegel>();

        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT DeviceLocator, BankLabel, Capacity, Speed, ConfiguredClockSpeed, " +
                "Manufacturer, PartNumber, SMBIOSMemoryType, FormFactor FROM Win32_PhysicalMemory");

            foreach (var o in suche.Get())
            {
                var bytes = ZahlOderNull(o["Capacity"]);

                riegel.Add(new SpeicherRiegel
                {
                    Steckplatz = o["DeviceLocator"]?.ToString()?.Trim()
                                 ?? o["BankLabel"]?.ToString()?.Trim() ?? "?",
                    GroesseGb = bytes is > 0 ? Math.Round(bytes.Value / 1024d / 1024 / 1024, 1) : 0,
                    Typ = HardwareKlartext.SpeicherTyp((int)(ZahlOderNull(o["SMBIOSMemoryType"]) ?? 0)),
                    Bauform = HardwareKlartext.Bauform((int)(ZahlOderNull(o["FormFactor"]) ?? 0)),
                    // ConfiguredClockSpeed ist der Takt, mit dem der Riegel WIRKLICH läuft -
                    // "Speed" ist nur das, was er könnte. Beides weicht oft voneinander ab
                    // (z.B. wenn XMP im BIOS nicht eingeschaltet ist).
                    TaktMhz = (int?)(ZahlOderNull(o["ConfiguredClockSpeed"]) ?? ZahlOderNull(o["Speed"])),
                    Hersteller = Bereinigen(o["Manufacturer"]?.ToString()),
                    Teilenummer = Bereinigen(o["PartNumber"]?.ToString())
                });
            }
        }
        catch (ManagementException)
        {
            // Manche Notebooks/virtuelle Maschinen geben hier nichts heraus.
        }

        return riegel.OrderBy(r => r.Steckplatz, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public Task<List<LaufwerkInfo>> LaufwerkeAsync() => Task.Run(Laufwerke);

    private static List<LaufwerkInfo> Laufwerke()
    {
        var laufwerke = new List<LaufwerkInfo>();

        try
        {
            using var platten = new ManagementObjectSearcher(
                "SELECT Model, Size, InterfaceType, MediaType FROM Win32_DiskDrive");

            foreach (var o in platten.Get())
            {
                var bytes = ZahlOderNull(o["Size"]);
                var modell = o["Model"]?.ToString()?.Trim() ?? Loc.T("Unbekanntes Laufwerk", "Unknown drive");

                laufwerke.Add(new LaufwerkInfo
                {
                    Modell = modell,
                    GroesseGb = bytes is > 0 ? Math.Round(bytes.Value / 1024d / 1024 / 1024, 1) : null,
                    Schnittstelle = o["InterfaceType"]?.ToString(),
                    Art = HardwareKlartext.LaufwerksArt(o["MediaType"]?.ToString(),
                                                        o["InterfaceType"]?.ToString(), modell)
                });
            }
        }
        catch (ManagementException)
        {
            // ohne Plattenliste weiter - die optischen Laufwerke kommen trotzdem
        }

        try
        {
            using var optische = new ManagementObjectSearcher(
                "SELECT Name, Drive, MediaType FROM Win32_CDROMDevice");

            foreach (var o in optische.Get())
                laufwerke.Add(new LaufwerkInfo
                {
                    Modell = o["Name"]?.ToString()?.Trim() ?? Loc.T("Optisches Laufwerk", "Optical drive"),
                    Art = OptischeArt(o["Name"]?.ToString()),
                    Laufwerksbuchstabe = o["Drive"]?.ToString(),
                    IstOptisch = true
                });
        }
        catch (ManagementException)
        {
            // kein optisches Laufwerk vorhanden oder nicht auslesbar
        }

        return laufwerke;
    }

    /// <summary>
    /// Ob ein Laufwerk Blu-ray oder nur DVD kann, verrät Windows nicht direkt - es steht
    /// aber fast immer im Modellnamen (z.B. "BD-RE", "DVD±RW").
    /// </summary>
    private static string OptischeArt(string? name)
    {
        var n = (name ?? "").ToUpperInvariant();

        if (n.Contains("BD") || n.Contains("BLU")) return Loc.T("Blu-ray-Laufwerk", "Blu-ray drive");
        if (n.Contains("DVD")) return Loc.T("DVD-Laufwerk", "DVD drive");
        if (n.Contains("CD")) return Loc.T("CD-Laufwerk", "CD drive");

        return Loc.T("Optisches Laufwerk", "Optical drive");
    }

    private static long? ZahlOderNull(object? wert)
    {
        try
        {
            return wert is null ? null : Convert.ToInt64(wert);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>Windows trägt bei unbekannten Angaben gern Platzhalter ein - die helfen niemandem.</summary>
    private static string? Bereinigen(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert)) return null;

        var text = wert.Trim();
        string[] platzhalter = ["unknown", "unbekannt", "to be filled by o.e.m.", "none", "not specified", "000000"];

        return platzhalter.Any(p => text.Equals(p, StringComparison.OrdinalIgnoreCase)) ? null : text;
    }
}
