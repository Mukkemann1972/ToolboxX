using System.Diagnostics;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Zeigt, welches Programm gerade nach draußen redet (Rang 4 aus der NirSoft-Recherche,
/// Vorbild CurrPorts) - ergänzt die Netzwerk-Diagnose, die nur die Kette bis zum Internet
/// prüft, nicht wer sie benutzt. Reine Anzeige, nichts wird verändert oder beendet.
///
/// Motor ist `netstat -ano` (kein eigener Zugriff auf die TCP-Tabelle über iphlpapi.dll nötig -
/// die Windows-Bordmittel liefern genau das schon zuverlässig). Die Auswertung des Texts steckt
/// in PCToolboxCore.NetstatAuswerter, hier nur der Prozessaufruf und die PID→Programmname-
/// Auflösung (die zwingend Windows-spezifisch ist).
/// </summary>
public class PortProgrammService
{
    public Task<List<OffeneVerbindung>> LadeAsync() =>
        Task.Run(() =>
        {
            var ergebnis = new List<OffeneVerbindung>();

            var ausgabe = NetstatAusfuehren();
            if (ausgabe is null) return ergebnis;

            var alle = NetstatAuswerter.Parse(ausgabe);
            var aktive = NetstatAuswerter.AktiveVerbindungen(alle);

            foreach (var v in aktive)
            {
                ergebnis.Add(new OffeneVerbindung
                {
                    Programm = ProgrammNameVon(v.Pid),
                    Pid = v.Pid,
                    Protokoll = v.Protokoll,
                    RemoteAdresse = v.RemoteAdresse,
                    RemotePort = v.RemotePort
                });
            }

            // Nach Programm gruppieren wäre für 50 Verbindungen desselben Browsers
            // übersichtlicher, aber jede Zeile ist ein eigenes Ziel - genau das ist der Punkt
            // von "wer redet mit wem". Sortierung nach Programmname reicht als Übersicht.
            return ergebnis.OrderBy(v => v.Programm, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(v => v.RemoteAdresse)
                           .ToList();
        });

    private static string? NetstatAusfuehren()
    {
        try
        {
            var start = new ProcessStartInfo("netstat.exe", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var prozess = Process.Start(start);
            if (prozess is null) return null;

            var ausgabe = prozess.StandardOutput.ReadToEnd();
            prozess.WaitForExit();
            return ausgabe;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static string ProgrammNameVon(int pid)
    {
        try
        {
            return Process.GetProcessById(pid).ProcessName + ".exe";
        }
        catch (ArgumentException)
        {
            // Prozess ist zwischen netstat-Aufruf und jetzt schon beendet worden.
            return Loc.T("(bereits beendet)", "(already exited)");
        }
        catch (Exception)
        {
            return Loc.T("Unbekannt", "Unknown");
        }
    }
}
