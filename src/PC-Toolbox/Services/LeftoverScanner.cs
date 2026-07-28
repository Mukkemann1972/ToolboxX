using System.IO;
using Microsoft.Win32;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

// Phase 3 (VERIFIZIEREN) des Super-Uninstallers: sucht NACH einer Deinstallation bewusst
// konservativ nach Resten - lieber einen echten Rest uebersehen, als versehentlich etwas
// Falsches zum Loeschen vorzuschlagen. Deshalb: nur klar benannte Fundorte (kein Scan der
// gesamten Registry/Festplatte), geschuetzte Systempfade sind komplett tabu, und jeder Fund
// wird nur VORGESCHLAGEN - entfernt wird er erst, wenn der Nutzer ihn einzeln bestaetigt
// (siehe UninstallerService/SafetyEnforcer: Sichern-Phase davor macht das reversibel).
public class LeftoverScanner
{
    private static readonly string[] GeschuetztePfadAnfaenge =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Common Files"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Package Cache"),
    };

    private static readonly string[] AllgemeineWurzeln =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    };

    public Task<List<LeftoverItem>> ScanLeftoversAsync(InstalledProgram programm) =>
        Task.Run(() =>
        {
            var funde = new List<LeftoverItem>();
            RegistryResteSuchen(programm, funde);
            OrdnerResteSuchen(programm, funde);
            return funde;
        });

    private static void RegistryResteSuchen(InstalledProgram programm, List<LeftoverItem> funde)
    {
        // Der eigentliche Uninstall-Eintrag sollte nach erfolgreicher Deinstallation weg sein -
        // falls nicht, ist der Uninstaller vermutlich fehlgeschlagen oder abgebrochen worden.
        if (RegistrySchluesselExistiertNoch(programm.RegistryKeyPfad))
        {
            funde.Add(new LeftoverItem
            {
                Art = LeftoverArt.RegistrySchluessel,
                Pfad = programm.RegistryKeyPfad,
                Beschreibung = Loc.T($"Der Deinstallations-Eintrag von \"{programm.DisplayName}\" ist noch da.",
                                     $"The uninstall entry for \"{programm.DisplayName}\" is still there."),
                Erklaerung = Loc.T(
                    "Das deutet meist darauf hin, dass der Deinstaller abgebrochen oder " +
                    "übersprungen wurde. Vor dem Entfernen prüfen, ob das Programm im Startmenü " +
                    "wirklich weg ist.",
                    "This usually indicates the uninstaller was cancelled or " +
                    "skipped. Before removing, check whether the program is really " +
                    "gone from the Start menu."),
                Ampel = AmpelStatus.Rot,
            });
        }

        foreach (var (hive, hiveKurz) in new[] { (Registry.CurrentUser, "HKCU"), (Registry.LocalMachine, "HKLM") })
        {
            using var basis = hive.OpenSubKey("SOFTWARE");
            if (basis is null) continue;

            foreach (var kandidat in NameMatcher.Kandidaten(programm.DisplayName, programm.Publisher))
            {
                using var treffer = basis.OpenSubKey(kandidat);
                if (treffer is null) continue;

                var pfad = $@"{hiveKurz}\SOFTWARE\{kandidat}";
                if (PathSafety.IstGeschuetzterRegistryPfad(pfad)) continue;

                funde.Add(new LeftoverItem
                {
                    Art = LeftoverArt.RegistrySchluessel,
                    Pfad = pfad,
                    Beschreibung = Loc.T($"Registry-Schlüssel \"{kandidat}\" passt zum Namen von \"{programm.DisplayName}\".",
                                         $"Registry key \"{kandidat}\" matches the name of \"{programm.DisplayName}\"."),
                    // WICHTIG (bewusste Einschraenkung): manche Hersteller (z.B. Google, Adobe) legen
                    // EINEN gemeinsamen Schluessel fuer mehrere Produkte an - wird hier nicht erkannt.
                    // Deshalb immer nur als Vorschlag mit Warnung, nie automatisch entfernt.
                    Erklaerung = Loc.T(
                        "Meist Einstellungen/Lizenzdaten, die der Deinstaller vergessen hat. " +
                        "Manche Hersteller nutzen aber EINEN Schlüssel für mehrere Produkte - vor dem " +
                        "Entfernen sicherstellen, dass kein anderes Programm desselben Herstellers " +
                        "noch installiert ist.",
                        "Usually settings/license data that the uninstaller forgot. " +
                        "But some manufacturers use ONE key for multiple products - before " +
                        "removing, make sure no other program from the same manufacturer " +
                        "is still installed."),
                    Ampel = AmpelStatus.Gelb,
                });
            }
        }
    }

    private static void OrdnerResteSuchen(InstalledProgram programm, List<LeftoverItem> funde)
    {
        var kandidatenOrdner = new List<string>();

        if (!string.IsNullOrWhiteSpace(programm.InstallLocation))
            kandidatenOrdner.Add(programm.InstallLocation.TrimEnd('\\'));

        foreach (var basis in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        })
        {
            foreach (var name in NameMatcher.Kandidaten(programm.DisplayName, programm.Publisher))
                kandidatenOrdner.Add(Path.Combine(basis, name));
        }

        foreach (var ordner in kandidatenOrdner.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(ordner)) continue;
            if (PathSafety.IstGeschuetzterPfad(ordner, GeschuetztePfadAnfaenge) ||
                PathSafety.IstZuAllgemeinerPfad(ordner, AllgemeineWurzeln)) continue;

            var dateiAnzahl = SicherDateienZaehlen(ordner);
            funde.Add(new LeftoverItem
            {
                Art = LeftoverArt.Ordner,
                Pfad = ordner,
                Beschreibung = dateiAnzahl == 0
                    ? Loc.T($"Leerer Ordner von \"{programm.DisplayName}\".", $"Empty folder from \"{programm.DisplayName}\".")
                    : Loc.T($"Ordner von \"{programm.DisplayName}\" mit {dateiAnzahl} Datei(en) - der Deinstaller hat ihn stehen lassen.",
                            $"Folder from \"{programm.DisplayName}\" with {dateiAnzahl} file(s) - the uninstaller left it behind."),
                Erklaerung = dateiAnzahl == 0
                    ? Loc.T("Gefahrlos entfernbar - der Deinstaller hat nur vergessen, den leeren Ordner selbst zu löschen.",
                            "Safe to remove - the uninstaller just forgot to delete the empty folder itself.")
                    : Loc.T(
                          "Enthält noch Dateien (z.B. eigene Einstellungen/Logs). Vor dem Entfernen kurz reinschauen, " +
                          "falls dort etwas Persönliches lag, das du behalten willst.",
                          "Still contains files (e.g. settings/logs). Take a quick look before removing, " +
                          "in case something personal is in there that you want to keep."),
                Ampel = dateiAnzahl == 0 ? AmpelStatus.Gruen : AmpelStatus.Gelb,
            });
        }
    }

    private static bool RegistrySchluesselExistiertNoch(string registryKeyPfad)
    {
        var (hive, pfad) = AufloesenHive(registryKeyPfad);
        if (hive is null) return false;
        using var schluessel = hive.OpenSubKey(pfad);
        return schluessel is not null;
    }

    private static (RegistryKey? Hive, string Pfad) AufloesenHive(string vollerPfad)
    {
        var teile = vollerPfad.Split('\\', 2);
        if (teile.Length != 2) return (null, "");
        RegistryKey? hive = teile[0] switch
        {
            "HKLM" => Registry.LocalMachine,
            "HKCU" => Registry.CurrentUser,
            _ => null,
        };
        return (hive, teile[1]);
    }

    private static int SicherDateienZaehlen(string ordner)
    {
        try { return Directory.EnumerateFiles(ordner, "*", SearchOption.AllDirectories).Take(1000).Count(); }
        catch { return -1; } // Zugriff verweigert o.ae. - lieber vorsichtig als abstuerzen
    }
}
