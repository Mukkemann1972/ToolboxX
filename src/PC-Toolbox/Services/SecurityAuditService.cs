using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Sammelt die Rohdaten fuer die 6 Security-Audit-Checks (s. Hermes-Vorschlag 19.08.2026) -
/// rein lesend, kein Admin noetig (bis auf die ehrliche Einschraenkung beim Sicherheits-
/// Ereignisprotokoll, s. EreignisseAsync). Die eigentliche Bewertung (Ampel/Erklaertext) liegt
/// in PCToolboxCore (Sicherheits*-Klassen), hier nur das Einsammeln.
/// </summary>
public class SecurityAuditService
{
    private static readonly TimeSpan Zeitraum = TimeSpan.FromDays(14);

    // ---------- 1. Defender ----------

    public Task<(bool? AntivirusAktiv, bool? EchtzeitschutzAktiv, int? SignaturAlterTage, bool? ManipulationsschutzAktiv)> DefenderStatusAsync() =>
        Task.Run(() =>
        {
            try
            {
                var start = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -Command \"Get-MpComputerStatus | ConvertTo-Json\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var prozess = Process.Start(start)!;
                var ausgabe = prozess.StandardOutput.ReadToEnd();
                prozess.WaitForExit(15000);

                using var doc = JsonDocument.Parse(ausgabe);
                var wurzel = doc.RootElement;

                bool? antivirusAktiv = LiesBool(wurzel, "AntivirusEnabled");
                bool? echtzeitAktiv = LiesBool(wurzel, "RealTimeProtectionEnabled");
                bool? manipulationsschutz = LiesBool(wurzel, "IsTamperProtected");

                int? signaturAlterTage = null;
                if (wurzel.TryGetProperty("AntivirusSignatureLastUpdated", out var datumEl))
                {
                    var rohtext = datumEl.GetString();
                    if (rohtext != null && DateTime.TryParse(rohtext.Replace("/Date(", "").Replace(")/", ""), out var datum))
                        signaturAlterTage = (int)(DateTime.Now - datum).TotalDays;
                }

                return (antivirusAktiv, echtzeitAktiv, signaturAlterTage, manipulationsschutz);
            }
            catch
            {
                return ((bool?)null, (bool?)null, (int?)null, (bool?)null);
            }
        });

    private static bool? LiesBool(JsonElement wurzel, string name) =>
        wurzel.TryGetProperty(name, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
            ? el.GetBoolean()
            : null;

    // ---------- 2. Ereignis-Anomalien ----------

    // Rueckgabe je Kategorie + ob der Kanal (i.d.R. nur "Sicherheit") ohne Admin-Rechte nicht
    // lesbar war - dann ehrlich anzeigen statt eine leere/gruene Bewertung vorzutaeuschen.
    public Task<Dictionary<EreignisKategorie, (List<EreignisEintrag> Eintraege, bool ZugriffVerweigert)>> EreignisseAsync() =>
        Task.Run(() =>
        {
            var ergebnis = new Dictionary<EreignisKategorie, (List<EreignisEintrag>, bool)>
            {
                [EreignisKategorie.FehlgeschlageneAnmeldungen] = LiesKanal("Security", [4625]),
                [EreignisKategorie.NeuesKonto] = LiesKanal("Security", [4720]),
                [EreignisKategorie.LogGeloescht] = LiesKanal("Security", [1102]),
                [EreignisKategorie.DienstAbsturz] = LiesKanal("System", [7031, 7034]),
                [EreignisKategorie.UnerwarteterNeustart] = LiesKanal("System", [41]),
                [EreignisKategorie.UnsauberesHerunterfahren] = LiesKanal("System", [6008]),
                [EreignisKategorie.AnwendungsAbsturz] = LiesKanal("Application", [1000, 1002]),
            };
            return ergebnis;
        });

    private static (List<EreignisEintrag>, bool) LiesKanal(string kanal, int[] eventIds)
    {
        var eintraege = new List<EreignisEintrag>();
        var millisekunden = (long)Zeitraum.TotalMilliseconds;
        var idBedingung = string.Join(" or ", eventIds.Select(id => $"EventID={id}"));
        var xpath = $"*[System[({idBedingung}) and TimeCreated[timediff(@SystemTime) <= {millisekunden}]]]";

        try
        {
            var abfrage = new EventLogQuery(kanal, PathType.LogName, xpath);
            using var reader = new EventLogReader(abfrage);

            EventRecord? datensatz;
            while ((datensatz = reader.ReadEvent()) is not null)
            {
                using (datensatz)
                {
                    string beschreibung;
                    try { beschreibung = datensatz.FormatDescription() ?? $"Ereignis-ID {datensatz.Id}"; }
                    catch { beschreibung = $"Ereignis-ID {datensatz.Id}"; }

                    eintraege.Add(new EreignisEintrag(
                        datensatz.TimeCreated ?? DateTime.Now,
                        datensatz.ProviderName ?? kanal,
                        beschreibung.Length > 200 ? beschreibung[..200] : beschreibung));
                }
            }
            return (eintraege, false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or EventLogException)
        {
            return (eintraege, true);
        }
    }

    // ---------- 3. Signaturen ----------

    public Task<List<AppSignatur>> SignaturenAsync() => Task.Run(() =>
    {
        var programme = new InstalledProgramsService().GetInstalledProgramsAsync().GetAwaiter().GetResult();
        var kandidaten = new List<(string Name, string Pfad)>();

        foreach (var p in programme)
        {
            if (string.IsNullOrWhiteSpace(p.InstallLocation) || !Directory.Exists(p.InstallLocation)) continue;

            try
            {
                var exeDateien = Directory.GetFiles(p.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly);
                if (exeDateien.Length == 0) continue;

                // Bevorzugt eine .exe, deren Name zum Anzeigenamen passt - sonst einfach die erste.
                var passende = exeDateien.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Contains(p.DisplayName.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                kandidaten.Add((p.DisplayName, passende ?? exeDateien[0]));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Ordner nicht lesbar - dieses Programm einfach auslassen, kein Absturz.
            }
        }

        if (kandidaten.Count == 0) return [];

        return SignaturenPruefen(kandidaten);
    });

    private static List<AppSignatur> SignaturenPruefen(List<(string Name, string Pfad)> kandidaten)
    {
        try
        {
            var pfadListe = string.Join(",", kandidaten.Select(k => $"'{k.Pfad.Replace("'", "''")}'"));
            var start = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"Get-AuthenticodeSignature -LiteralPath {pfadListe} | Select-Object Path,Status | ConvertTo-Json\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var prozess = Process.Start(start)!;
            var ausgabe = prozess.StandardOutput.ReadToEnd();
            prozess.WaitForExit(30000);

            using var doc = JsonDocument.Parse(ausgabe);
            var elemente = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : [doc.RootElement];

            var statusJePfad = elemente
                .Where(e => e.TryGetProperty("Path", out _))
                .ToDictionary(e => e.GetProperty("Path").GetString() ?? "", e => e.GetProperty("Status").GetString() ?? "");

            return kandidaten.Select(k =>
            {
                var status = statusJePfad.GetValueOrDefault(k.Pfad, "");
                var ergebnis = status switch
                {
                    "Valid" => SignaturErgebnis.GueltigSigniert,
                    "NotSigned" => SignaturErgebnis.Unsigniert,
                    "HashMismatch" or "NotTrusted" => SignaturErgebnis.UngueltigSigniert,
                    _ => SignaturErgebnis.KeinPfadGefunden, // Incompatible/UnknownError - nicht bewerten
                };
                return new AppSignatur(k.Name, k.Pfad, ergebnis);
            }).Where(a => a.Ergebnis != SignaturErgebnis.KeinPfadGefunden).ToList();
        }
        catch
        {
            return [];
        }
    }

    // ---------- 4. Zertifikate ----------

    public Task<List<ZertifikatEintrag>> ZertifikateAsync() => Task.Run(() =>
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Cast<X509Certificate2>()
                .Select(c => new ZertifikatEintrag(c.Issuer, c.Subject, c.NotAfter))
                .OrderBy(z => z.Aussteller, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    });

    // ---------- 5. Browser-Extensions ----------

    public Task<List<ExtensionEintrag>> BrowserExtensionsAsync() => Task.Run(() =>
    {
        var ergebnis = new List<ExtensionEintrag>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        ergebnis.AddRange(ChromiumExtensionenLesen(Path.Combine(localAppData, "Google", "Chrome", "User Data"), "Chrome"));
        ergebnis.AddRange(ChromiumExtensionenLesen(Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "Edge"));
        ergebnis.AddRange(FirefoxExtensionenLesen(Path.Combine(roamingAppData, "Mozilla", "Firefox", "Profiles")));

        return ergebnis;
    });

    private static IEnumerable<ExtensionEintrag> ChromiumExtensionenLesen(string userDataOrdner, string browserName)
    {
        var treffer = new List<ExtensionEintrag>();
        if (!Directory.Exists(userDataOrdner)) return treffer;

        IEnumerable<string> profilOrdner;
        try { profilOrdner = Directory.GetDirectories(userDataOrdner).Where(d => Path.GetFileName(d) is "Default" || Path.GetFileName(d).StartsWith("Profile ")); }
        catch { return treffer; }

        foreach (var profil in profilOrdner)
        {
            var extensionsOrdner = Path.Combine(profil, "Extensions");
            if (!Directory.Exists(extensionsOrdner)) continue;

            IEnumerable<string> idOrdner;
            try { idOrdner = Directory.GetDirectories(extensionsOrdner); }
            catch { continue; }

            foreach (var idPfad in idOrdner)
            {
                try
                {
                    var versionOrdner = Directory.GetDirectories(idPfad).OrderByDescending(v => v).FirstOrDefault();
                    if (versionOrdner is null) continue;

                    var manifestPfad = Path.Combine(versionOrdner, "manifest.json");
                    if (!File.Exists(manifestPfad)) continue;

                    if (ManifestLesen(manifestPfad, browserName) is { } eintrag)
                        treffer.Add(eintrag);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    // Einzelne kaputte/gesperrte Extension ueberspringen, nicht das ganze Modul abbrechen.
                }
            }
        }
        return treffer;
    }

    private static ExtensionEintrag? ManifestLesen(string manifestPfad, string browserName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPfad));
        var wurzel = doc.RootElement;

        var name = wurzel.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
        if (name.StartsWith("__MSG_") && name.EndsWith("__"))
            name = LokalisierterNameOderRoh(Path.GetDirectoryName(manifestPfad)!, name);

        var version = wurzel.TryGetProperty("version", out var vEl) ? vEl.GetString() ?? "" : "";

        var berechtigungen = new List<string>();
        var hostBerechtigungen = new List<string>();

        if (wurzel.TryGetProperty("permissions", out var permEl) && permEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in permEl.EnumerateArray())
            {
                var wert = p.GetString() ?? "";
                if (wert.Contains("://") || wert == "<all_urls>") hostBerechtigungen.Add(wert);
                else berechtigungen.Add(wert);
            }
        }
        if (wurzel.TryGetProperty("host_permissions", out var hostEl) && hostEl.ValueKind == JsonValueKind.Array)
            hostBerechtigungen.AddRange(hostEl.EnumerateArray().Select(h => h.GetString() ?? ""));

        return new ExtensionEintrag(string.IsNullOrWhiteSpace(name) ? "(unbenannt)" : name, browserName, version, null, berechtigungen, hostBerechtigungen);
    }

    private static string LokalisierterNameOderRoh(string versionOrdner, string rohName)
    {
        try
        {
            var schluessel = rohName.Trim('_')["MSG_".Length..].TrimEnd('_');
            var localesOrdner = Path.Combine(versionOrdner, "_locales");
            if (!Directory.Exists(localesOrdner)) return rohName;

            var sprachOrdner = new[] { "en", "de" }
                .Select(s => Path.Combine(localesOrdner, s))
                .FirstOrDefault(Directory.Exists) ?? Directory.GetDirectories(localesOrdner).FirstOrDefault();
            if (sprachOrdner is null) return rohName;

            var messagesPfad = Path.Combine(sprachOrdner, "messages.json");
            if (!File.Exists(messagesPfad)) return rohName;

            using var doc = JsonDocument.Parse(File.ReadAllText(messagesPfad));
            if (doc.RootElement.TryGetProperty(schluessel, out var eintrag) && eintrag.TryGetProperty("message", out var msg))
                return msg.GetString() ?? rohName;
        }
        catch { /* Roh-Namen als Fallback behalten */ }
        return rohName;
    }

    private static IEnumerable<ExtensionEintrag> FirefoxExtensionenLesen(string profileOrdner)
    {
        var treffer = new List<ExtensionEintrag>();
        if (!Directory.Exists(profileOrdner)) return treffer;

        foreach (var profil in Directory.GetDirectories(profileOrdner))
        {
            var pfad = Path.Combine(profil, "extensions.json");
            if (!File.Exists(pfad)) continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(pfad));
                if (!doc.RootElement.TryGetProperty("addons", out var addons)) continue;

                foreach (var addon in addons.EnumerateArray())
                {
                    var name = addon.TryGetProperty("defaultLocale", out var loc) && loc.TryGetProperty("name", out var n)
                        ? n.GetString() ?? "(unbenannt)" : "(unbenannt)";

                    var apiPerms = new List<string>();
                    var hostPerms = new List<string>();
                    if (addon.TryGetProperty("userPermissions", out var up))
                    {
                        if (up.TryGetProperty("permissions", out var pEl) && pEl.ValueKind == JsonValueKind.Array)
                            apiPerms.AddRange(pEl.EnumerateArray().Select(p => p.GetString() ?? ""));
                        if (up.TryGetProperty("origins", out var oEl) && oEl.ValueKind == JsonValueKind.Array)
                            hostPerms.AddRange(oEl.EnumerateArray().Select(o => o.GetString() ?? ""));
                    }

                    var version = addon.TryGetProperty("version", out var vEl) ? vEl.GetString() ?? "" : "";
                    treffer.Add(new ExtensionEintrag(name, "Firefox", version, null, apiPerms, hostPerms));
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Dieses Profil ueberspringen, nicht das ganze Modul abbrechen.
            }
        }
        return treffer;
    }

    // ---------- 6. Autostart-Sicherheitshinweise (wiederverwendet, nicht neu gebaut) ----------

    public Task<List<SicherheitsBefund>> AutostartHinweiseAsync() => new AutostartService().WeitereOrteAsync();
}
