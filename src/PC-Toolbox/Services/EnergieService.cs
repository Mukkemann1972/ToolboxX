using System.Diagnostics;
using System.Text;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Energie-Diagnose (Free): "powercfg /lastwake" - kein Admin nötig, anders als die Pro-Ergänzungen
/// (Standby-Blocker, Energy Report), die beide selbst zum Lesen Administratorrechte brauchen.
/// "powercfg.exe" schreibt (live geprüft, anders als dism.exe/sfc.exe) sauberes UTF-8, deshalb
/// genügt UTF8Encoding ohne Umweg über /English oder Codepage-Tricks.
/// </summary>
public class EnergieService
{
    public async Task<AufweckVerlaufErgebnis> AufweckVerlaufAsync()
    {
        var start = new ProcessStartInfo("powercfg.exe", "/lastwake")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false)
        };

        using var prozess = Process.Start(start);
        if (prozess is null) return new AufweckVerlaufErgebnis(null, "");

        var ausgabe = await prozess.StandardOutput.ReadToEndAsync();
        await prozess.WaitForExitAsync();

        return AufweckVerlauf.Parsen(ausgabe);
    }
}
