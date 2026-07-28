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
