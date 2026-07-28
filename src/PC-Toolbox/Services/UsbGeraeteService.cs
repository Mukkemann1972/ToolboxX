using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest aktuell angeschlossene USB-Geräte über WMI (Win32_PnPEntity, PNPDeviceID beginnt mit
/// "USB\") - neues Free-Modul aus Hermes' 23.07-Liste, Vorbild USBDeview, hier bewusst nur der
/// lesende Diagnose-Teil (Free = lesend). Nutzt dieselbe Fehlercode-Deutung wie der
/// Treiber-Check (TreiberBewerter/GeraeteFehlerCodes) - ein Problem an einem USB-Gerät ist
/// technisch derselbe Fall wie an jedem anderen Gerät.
///
/// Bewusst NICHT dabei (V1): die Historie früher angeschlossener, jetzt nicht mehr
/// vorhandener Geräte (USBDeviceViews eigentliche Stärke) - das braucht zuverlässiges
/// Unterscheiden zwischen "gerade nicht da" und "war nie richtig weg", was einfache
/// Win32_PnPEntity-Abfragen nicht sauber hergeben. Als offener Punkt im Backlog vermerkt.
/// </summary>
public class UsbGeraeteService
{
    public Task<List<GeraetBefund>> LadeAsync() => Task.Run(() =>
    {
        var geraete = new List<GeraetBefund>();

        try
        {
            // Bewusst OHNE "WHERE PNPDeviceID LIKE 'USB\%'" in der WQL-Abfrage selbst: der
            // Backslash macht dort Probleme (WMI meldet "Die Anfrage ist ungueltig", empirisch
            // geprueft) - stattdessen alle Geraete lesen und in C# filtern, das ist robust.
            using var suche = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPClass, ConfigManagerErrorCode, PNPDeviceID FROM Win32_PnPEntity");

            foreach (var o in suche.Get())
            {
                var geraeteId = o["PNPDeviceID"]?.ToString() ?? "";
                if (!geraeteId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)) continue;

                var name = o["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                geraete.Add(new GeraetBefund
                {
                    Name = name,
                    Hersteller = Bereinigen(o["Manufacturer"]?.ToString()),
                    Klasse = o["PNPClass"]?.ToString(),
                    FehlerCode = ZahlOderNull(o["ConfigManagerErrorCode"])
                });
            }
        }
        catch (ManagementException)
        {
            // WMI nicht verfuegbar - leere Liste, die Oberflaeche sagt das ehrlich.
        }

        return geraete.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    private static int ZahlOderNull(object? wert)
    {
        try
        {
            return wert is null ? 0 : Convert.ToInt32(wert);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return 0;
        }
    }

    private static string? Bereinigen(string? hersteller)
    {
        if (string.IsNullOrWhiteSpace(hersteller)) return null;
        var text = hersteller.Trim();
        return text.StartsWith('(') ? null : text;
    }
}
