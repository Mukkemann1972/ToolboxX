using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest aktuell angeschlossene Mäuse über WMI (Win32_PnPEntity, PNPClass = "Mouse") -
/// neues Free-Modul, Auftrag vom 04.08. ("Maus-Diagnose"-Kachel + Brücke zu MausWerk).
/// Empirisch auf einem echten Windows-Testsystem geprüft: "Mouse" ist die einzige PNPClass,
/// die eine reale HID-Maus als solche kennzeichnet (die übrigen Sub-Interfaces eines
/// zusammengesetzten Maus-Geräts - z.B. programmierbare Zusatztasten - laufen unter der
/// generischen "HIDClass" und würden das Ergebnis verfälschen, wenn man danach mitfiltert).
/// Nutzt dieselbe Fehlercode-Deutung wie USB-Geräte/Treiber-Check (TreiberBewerter/
/// GeraeteFehlerCodes) - ein Problem an einer Maus ist technisch derselbe Fall wie an
/// jedem anderen Gerät.
/// </summary>
public class MausGeraeteService
{
    public Task<List<GeraetBefund>> LadeAsync() => Task.Run(() =>
    {
        var geraete = new List<GeraetBefund>();

        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPClass, ConfigManagerErrorCode, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Mouse'");

            foreach (var o in suche.Get())
            {
                var name = o["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                geraete.Add(new GeraetBefund
                {
                    Name = name,
                    Hersteller = Bereinigen(o["Manufacturer"]?.ToString()),
                    Klasse = o["PNPClass"]?.ToString(),
                    FehlerCode = ZahlOderNull(o["ConfigManagerErrorCode"]),
                    GeraeteId = o["PNPDeviceID"]?.ToString()
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
