using System.Diagnostics;
using System.IO;
using PCToolbox.Models;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Services;

/// <summary>
/// Steuert ImDisk (LTRData/ImDisk, echtes GPL-2.0, github.com/LTRData/ImDisk) als Motor
/// fuer die RAM-Disk. Der offizielle, unveraenderte Installer liegt bei (vendor/imdisk/,
/// s. HERKUNFT.txt) - anders als bei memtest_vulkan aber NICHT still im Hintergrund
/// ausgefuehrt: ein Kernel-Treiber-Setup startet erst auf ausdruecklichen Klick
/// ("ImDisk installieren"), der Installer-Dialog + UAC laeuft dann ganz normal fuer sich,
/// genau wie beim manuellen Download von der Webseite - nur eben schon im Paket dabei.
/// </summary>
public class RamDiskService
{
    private static readonly string[] BekanntePfade =
    [
        @"C:\Program Files\ImDisk\imdisk.exe",
        @"C:\Windows\System32\imdisk.exe"
    ];

    public bool IstImDiskInstalliert() => ImDiskPfadFinden() is not null;

    private static string MitgelieferterInstallerPfad =>
        Path.Combine(AppContext.BaseDirectory, "ramdisk-tools", "imdisk", "imdiskinst.exe");

    public bool InstallerVorhanden() => File.Exists(MitgelieferterInstallerPfad);

    /// <summary>
    /// Startet den mitgelieferten, offiziellen ImDisk-Installer ganz normal (kein
    /// stiller Modus, kein automatisches "runas" von uns - der Installer bringt sein
    /// eigenes Manifest/UAC-Verhalten mit, exakt wie bei einem manuellen Doppelklick).
    /// </summary>
    public bool InstallerStarten()
    {
        if (!InstallerVorhanden()) return false;

        try
        {
            Process.Start(new ProcessStartInfo(MitgelieferterInstallerPfad) { UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // UAC-Abfrage abgelehnt oder Start verweigert
            return false;
        }
    }

    public async Task<RamDiskStatus> GetStatusAsync(char laufwerksbuchstabe)
    {
        return await Task.Run(() =>
        {
            var status = new RamDiskStatus
            {
                Laufwerksbuchstabe = laufwerksbuchstabe,
                ImDiskInstalliert = IstImDiskInstalliert()
            };

            // Bewusst ueber DriveInfo statt ImDisk-eigener Text-Ausgabe (s. Kommentar
            // in ImDiskCommandBuilder) - .NET liest hier ganz normal ein Windows-
            // Laufwerk, egal ob es von ImDisk oder sonst wem stammt.
            var laufwerk = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.Name.StartsWith($"{char.ToUpperInvariant(laufwerksbuchstabe)}:",
                    StringComparison.OrdinalIgnoreCase) && d.IsReady);

            if (laufwerk is not null)
            {
                status.IstAktiv = true;
                status.GroesseGb = Math.Round(laufwerk.TotalSize / 1024.0 / 1024 / 1024, 2);
                status.FreiGb = Math.Round(laufwerk.AvailableFreeSpace / 1024.0 / 1024 / 1024, 2);
            }

            return status;
        });
    }

    public async Task<(bool Erfolg, string Meldung)> ErstellenAsync(RamDiskSettings einstellungen)
    {
        var pfad = ImDiskPfadFinden();
        if (pfad is null)
            return (false, Loc.T(
                "ImDisk ist nicht installiert. Bitte zuerst den offiziellen Treiber " +
                "installieren: https://github.com/LTRData/ImDisk/releases",
                "ImDisk is not installed. Please install the official driver first: " +
                "https://github.com/LTRData/ImDisk/releases"));

        var argumente = ImDiskCommandBuilder.BuildErstellenArgumente(einstellungen);
        var (erfolg, exitCode) = await ProzessAusfuehrenAsync(pfad, argumente);

        if (!erfolg)
            return (false, ExitCodeErklaeren(exitCode));

        return (true, Loc.T(
            $"RAM-Disk {char.ToUpperInvariant(einstellungen.Laufwerksbuchstabe)}: mit {einstellungen.GroesseMb} MB angelegt.",
            $"RAM disk {char.ToUpperInvariant(einstellungen.Laufwerksbuchstabe)}: created with {einstellungen.GroesseMb} MB."));
    }

    public async Task<(bool Erfolg, string Meldung)> EntfernenAsync(char laufwerksbuchstabe)
    {
        var pfad = ImDiskPfadFinden();
        if (pfad is null)
            return (false, Loc.T("ImDisk ist nicht installiert.", "ImDisk is not installed."));

        var argumente = ImDiskCommandBuilder.BuildEntfernenArgumente(laufwerksbuchstabe);
        var (erfolg, exitCode) = await ProzessAusfuehrenAsync(pfad, argumente);

        if (!erfolg)
            return (false, ExitCodeErklaeren(exitCode));

        return (true, Loc.T(
            $"RAM-Disk {char.ToUpperInvariant(laufwerksbuchstabe)}: entfernt. " +
            "Alle Daten darauf sind jetzt weg (das ist normal, RAM-Disks sind flüchtig).",
            $"RAM disk {char.ToUpperInvariant(laufwerksbuchstabe)}: removed. " +
            "All data on it is now gone (this is normal, RAM disks are volatile)."));
    }

    private static string? ImDiskPfadFinden()
    {
        foreach (var pfad in BekanntePfade)
            if (File.Exists(pfad)) return pfad;

        // Zusaetzlich im PATH suchen (der offizielle Installer traegt sich meist ein)
        var pathVerzeichnisse = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
        foreach (var verzeichnis in pathVerzeichnisse)
        {
            var kandidat = Path.Combine(verzeichnis.Trim(), "imdisk.exe");
            if (File.Exists(kandidat)) return kandidat;
        }

        return null;
    }

    // Verb "runas" (UAC-Abfrage) UND Ausgabe-Umleitung (RedirectStandardOutput/Error)
    // schliessen sich in .NET/Windows gegenseitig aus (ShellExecute-Start unterstuetzt
    // keine Pipe-Umleitung) - deshalb hier bewusst NUR der Exit-Code ausgewertet, keine
    // Konsolen-Ausgabe von imdisk.exe eingefangen. Toolbox X selbst laeuft ohne
    // Admin-Manifest (geringstmoegliche Rechte), UAC-Abfrage kommt gezielt nur fuer
    // diese eine Aktion, nicht fuer die ganze App.
    private static async Task<(bool Erfolg, int ExitCode)> ProzessAusfuehrenAsync(string pfad, string argumente)
    {
        try
        {
            var start = new ProcessStartInfo(pfad, argumente)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var prozess = Process.Start(start);
            if (prozess is null) return (false, -1);

            await prozess.WaitForExitAsync();
            return (prozess.ExitCode == 0, prozess.ExitCode);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Tritt u.a. auf, wenn die UAC-Abfrage (Verb "runas") abgelehnt wird
            return (false, -1);
        }
    }

    private static string ExitCodeErklaeren(int exitCode) => exitCode switch
    {
        -1 => Loc.T("Ohne Administratorrechte (UAC-Bestätigung) kann keine RAM-Disk angelegt/entfernt werden.",
                    "A RAM disk cannot be created/removed without administrator rights (UAC confirmation)."),
        _ => Loc.T($"ImDisk meldete einen Fehler (Code {exitCode}).", $"ImDisk reported an error (code {exitCode}).")
    };
}
