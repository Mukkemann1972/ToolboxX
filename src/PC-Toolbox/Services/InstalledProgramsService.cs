using Microsoft.Win32;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

public class InstalledProgramsService
{
    private static readonly (RegistryKey Hive, string HiveKurz, string Pfad)[] Quellen =
    {
        (Registry.LocalMachine, "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, "HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, "HKCU", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    };

    // Stichworte fuer Komponenten, von denen andere installierte Programme abhaengen koennten
    // (Laufzeiten/Treiber) - werden NICHT ausgeblendet, aber mit Warnung markiert.
    private static readonly string[] SystemnaheStichworte =
    {
        "Microsoft Visual C++", "Microsoft .NET", ".NET Runtime", ".NET Desktop Runtime",
        "Windows Driver", "NVIDIA Graphics Driver", "NVIDIA Display", "Intel Graphics",
        "AMD Software", "Realtek", "Update for Microsoft", "Security Update",
    };

    public Task<List<InstalledProgram>> GetInstalledProgramsAsync() =>
        Task.Run(() =>
        {
            var ergebnis = new List<InstalledProgram>();

            foreach (var (hive, hiveKurz, pfad) in Quellen)
            {
                using var basisSchluessel = hive.OpenSubKey(pfad);
                if (basisSchluessel is null) continue;

                foreach (var unterName in basisSchluessel.GetSubKeyNames())
                {
                    using var eintrag = basisSchluessel.OpenSubKey(unterName);
                    if (eintrag is null) continue;

                    var anzeigeName = eintrag.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(anzeigeName)) continue; // kein sichtbares Programm

                    // Registry-eigenes Flag: rein interne Komponenten (z.B. einzelne Update-Patches)
                    // tauchen in "Programme und Features" gar nicht erst auf - hier genauso.
                    var registrySystemKomponente = (eintrag.GetValue("SystemComponent") as int?) == 1;
                    var uninstallString = eintrag.GetValue("UninstallString") as string ?? "";
                    if (registrySystemKomponente || string.IsNullOrWhiteSpace(uninstallString)) continue;

                    var programm = new InstalledProgram
                    {
                        DisplayName = anzeigeName,
                        Publisher = eintrag.GetValue("Publisher") as string ?? "",
                        DisplayVersion = eintrag.GetValue("DisplayVersion") as string ?? "",
                        InstallDateRoh = eintrag.GetValue("InstallDate") as string ?? "",
                        EstimatedSizeKb = eintrag.GetValue("EstimatedSize") as int?,
                        UninstallString = uninstallString,
                        QuietUninstallString = eintrag.GetValue("QuietUninstallString") as string ?? "",
                        InstallLocation = eintrag.GetValue("InstallLocation") as string ?? "",
                        RegistryKeyPfad = $@"{hiveKurz}\{pfad}\{unterName}",
                    };

                    EinordnenUndErklaeren(programm);
                    ergebnis.Add(programm);
                }
            }

            return ergebnis
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });

    private static void EinordnenUndErklaeren(InstalledProgram programm)
    {
        var systemnah = SystemnaheStichworte.Any(s =>
            programm.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase));

        if (systemnah)
        {
            programm.SystemKomponente = true;
            programm.Ampel = AmpelStatus.Gelb;
            programm.Erklaerung = Loc.T(
                "Systemnahe Komponente (Laufzeit/Treiber) - andere installierte " +
                "Programme könnten davon abhängen. Nur entfernen, wenn du sicher bist, dass nichts " +
                "mehr diese Version braucht.",
                "System-level component (runtime/driver) - other installed " +
                "programs might depend on it. Only remove if you're sure nothing " +
                "still needs this version.");
        }
        else
        {
            programm.Ampel = AmpelStatus.Gruen;
            programm.Erklaerung = Loc.T("Normales Programm, über den eigenen Deinstaller entfernbar.",
                                        "Normal program, removable via its own uninstaller.");
        }
    }
}
