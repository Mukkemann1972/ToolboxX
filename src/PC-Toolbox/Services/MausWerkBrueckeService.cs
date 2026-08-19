using System.Diagnostics;
using System.IO;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Schlanke, rein lesende Brücke von der Maus-Diagnose zu MausWerk (ISY IEM-1001) - Auftrag
/// vom 04.08.2026. Bewusst KEIN Code-Merge von MausWerks eigentlicher Überwachungs-/
/// Alert-Engine, nur: erkennen ob eine MausWerk-kompatible Maus angeschlossen ist, erkennen
/// ob MausWerk installiert ist, und dann öffnen bzw. den Installer starten - exakt dasselbe
/// Knopf-Muster wie "ImDisk installieren" in RamDiskService.
///
/// VID/PID (04D9:8008) aus MausWerks eigenem DeviceProfile.CreateIsyStandard()
/// (isy-iem-1001/src/shared/DeviceProfile.cs) übernommen, nicht geraten.
///
/// "Installiert?"-Prüfung: Existenz von IsyIem1001Wpf.exe am bekannten Install-Ziel, NICHT
/// über den Windows-Dienst "IsyIem1001" (ursprünglicher Plan) - live am 04.08.2026 verworfen,
/// weil der Dienst aus der separaten WinForms-App (IsyIem1001Monitor.exe) kommt, die gerade
/// nicht baut (vorbestehende, unabhängige Breakage) und weil "installiert?" hier praktisch nur
/// bedeuten muss "kann der Öffnen-Knopf etwas öffnen" - dafür reicht die Datei, der Dienst ist
/// für diese Frage nicht das eigentlich relevante Signal.
///
/// Öffnen-Ziel (%LOCALAPPDATA%\IsyIem1001\IsyIem1001Wpf.exe) und Installer-Suchpfad
/// (isy-iem-1001\build\setup\ISY_IEM_1001_Setup.msi neben diesem Projekt) stammen aus
/// MausWerks aktuellem install.ps1/build-setup.ps1 - das ist die einzige der mehreren im
/// MausWerk-Code gefundenen Installations-Varianten, die tatsächlich zum aktuellen Build
/// gehört (Phase 14h, "Release Preparation"). Funktioniert damit heute nur auf einem
/// Werkstatt-/Entwicklungs-PC, auf dem beide Projekte nebeneinander liegen (wie bei Alex) -
/// für echte Endnutzer muss dieser Suchpfad später durch eine echte Release-URL ersetzt
/// werden, sobald MausWerk eine hat (bewusst kein erfundener Link, s. "keine unbackten
/// Versprechen"-Grundsatz).
/// </summary>
public class MausWerkBrueckeService
{
    private static readonly string OeffnenPfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IsyIem1001", "IsyIem1001Wpf.exe");

    // Bewusst als fester bekannter Werkstatt-Pfad (analog RamDiskService.BekanntePfade), nicht
    // relativ zu AppContext.BaseDirectory berechnet - der wäre je nach Debug/Release/Publish
    // ein anderer Ordner und würde die Rechnung jedes Mal verschieben. s. Kommentar oben.
    private const string LokalerInstallerPfad =
        @"C:\Projekte\raspi-sandbox\workspace\code\isy-iem-1001\build\setup\ISY_IEM_1001_Setup.msi";

    public bool IstMausWerkKompatibel(string? geraeteId) => MausWerkErkennung.IstKompatibel(geraeteId);

    public bool IstMausWerkInstalliert() => File.Exists(OeffnenPfad);

    public bool InstallerVorhanden() => File.Exists(LokalerInstallerPfad);

    public bool MausWerkOeffnen()
    {
        if (!File.Exists(OeffnenPfad)) return false;

        try
        {
            Process.Start(new ProcessStartInfo(OeffnenPfad) { UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public bool InstallerStarten()
    {
        if (!File.Exists(LokalerInstallerPfad)) return false;

        try
        {
            Process.Start(new ProcessStartInfo(LokalerInstallerPfad) { UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
