using System.Diagnostics;
using System.Management;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Erkennt installierte Drucker und ihren Status über WMI (Win32_Printer) - neues Free-Modul
/// aus Hermes' 23.07-Liste ("Drucker erkennen + Status", lesend). Keine Admin-Rechte nötig,
/// dieselbe WMI-Klasse, die auch die Windows-Einstellungen-App unter der Haube abfragt.
/// </summary>
public class DruckerService
{
    /// <summary>
    /// Der Klassiker unter den Drucker-Fixes: ein hängender/verklemmter Druckauftrag blockiert
    /// die ganze Warteschlange, der Drucker "reagiert nicht mehr" oder verschwindet scheinbar.
    /// Löscht NUR noch nicht gedruckte Aufträge (die Spooler-Warteschlange), rührt Treiber,
    /// Anschluss-Einstellungen oder den Drucker selbst nicht an - deshalb auch in der Free
    /// sicher aufgehoben. Der Spooler-Dienst muss dafür kurz gestoppt werden (sonst sind die
    /// Warteschlangen-Dateien gesperrt) und läuft danach automatisch wieder.
    /// </summary>
    public Task<(bool Erfolg, string Meldung)> WarteschlangeLeerenAsync() => Task.Run(() =>
    {
        const string skript =
            "$ErrorActionPreference = 'Stop'; " +
            "Stop-Service -Name Spooler -Force; " +
            "Get-ChildItem -Path \"$env:WINDIR\\System32\\spool\\PRINTERS\" -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue; " +
            "Start-Service -Name Spooler";

        try
        {
            var start = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                $"\"try {{ {skript} }} catch {{ Write-Error $_; exit 1 }}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var prozess = Process.Start(start);
            if (prozess is null)
                return (false, Loc.T("Der Vorgang konnte nicht gestartet werden.", "The operation could not be started."));

            prozess.WaitForExit();

            return prozess.ExitCode == 0
                ? (true, Loc.T(
                      "Warteschlange geleert und der Druckerspooler-Dienst neu gestartet. Noch nicht " +
                      "gedruckte Aufträge sind weg, der Drucker selbst und seine Einstellungen sind " +
                      "unverändert - probier jetzt einen Testdruck oder ob der Drucker wieder erkannt wird.",
                      "Queue cleared and the print spooler service restarted. Unprinted jobs are gone, " +
                      "the printer itself and its settings are unchanged - try a test print now, or " +
                      "whether the printer is recognized again."))
                : (false, Loc.T(
                      "Windows hat den Vorgang nicht ausgeführt. Es wurde nichts verändert.",
                      "Windows did not carry out the operation. Nothing was changed."));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, Loc.T(
                "Ohne die Windows-Bestätigung (Administratorrechte) geht das nicht. Es wurde nichts verändert.",
                "This isn't possible without the Windows confirmation (administrator rights). Nothing was changed."));
        }
    });

    public Task<List<DruckerInfo>> GetDruckerAsync() => Task.Run(() =>
    {
        var ergebnis = new List<DruckerInfo>();

        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT Name, Default, Network, PortName, DriverName, PrinterStatus, DetectedErrorState, WorkOffline FROM Win32_Printer");

            foreach (var drucker in suche.Get())
            {
                var printerStatus = ToInt(drucker["PrinterStatus"]);
                var errorState = ToInt(drucker["DetectedErrorState"]);
                var workOffline = drucker["WorkOffline"] as bool? ?? false;

                var befund = DruckerDeutung.GesamtBefund(printerStatus, errorState, workOffline);

                ergebnis.Add(new DruckerInfo
                {
                    Name = drucker["Name"]?.ToString() ?? Loc.T("(unbenannt)", "(unnamed)"),
                    IstStandard = drucker["Default"] as bool? ?? false,
                    IstNetzwerkdrucker = drucker["Network"] as bool? ?? false,
                    AnschlussName = drucker["PortName"]?.ToString() ?? "",
                    TreiberName = drucker["DriverName"]?.ToString() ?? "",
                    BefundText = befund.Text,
                    Ampel = befund.Status
                });
            }
        }
        catch (ManagementException)
        {
            // WMI-Spooler-Abfrage fehlgeschlagen (z.B. Druckerspooler-Dienst steht) -
            // leere Liste, das UI zeigt dafuer einen ehrlichen "keine Drucker gefunden"-Hinweis.
        }

        return ergebnis.OrderByDescending(d => d.IstStandard).ThenBy(d => d.Name).ToList();
    });

    private static int ToInt(object? wert) => wert is null ? 0 : Convert.ToInt32(wert);
}
