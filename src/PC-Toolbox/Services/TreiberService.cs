using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest alle Geräte und ihre Treiber über WMI aus - rein lesend, ohne Admin-Rechte.
///
/// Bewusst NICHT enthalten: das Herunterladen oder Installieren von Treibern. Das ist
/// die Stelle, an der die üblichen "Driver Booster"-Programme Geld verdienen und Schaden
/// anrichten. Wir sagen, welches Gerät klemmt und wo der Treiber herkommen sollte -
/// geholt wird er beim Hersteller.
/// </summary>
public class TreiberService
{
    public Task<List<GeraetBefund>> LadeGeraeteAsync() => Task.Run(LadeGeraete);

    private static List<GeraetBefund> LadeGeraete()
    {
        var geraete = new List<GeraetBefund>();
        var treiberInfos = TreiberInfosLesen();

        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, PNPClass, ConfigManagerErrorCode, PNPDeviceID FROM Win32_PnPEntity");

            foreach (var o in suche.Get())
            {
                var name = o["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var geraeteId = o["PNPDeviceID"]?.ToString() ?? "";

                var befund = new GeraetBefund
                {
                    Name = name,
                    Hersteller = Bereinigen(o["Manufacturer"]?.ToString()),
                    Klasse = o["PNPClass"]?.ToString(),
                    FehlerCode = ZahlOderNull(o["ConfigManagerErrorCode"])
                };

                if (treiberInfos.TryGetValue(geraeteId, out var treiber))
                {
                    befund.TreiberVersion = treiber.Version;
                    befund.TreiberDatum = treiber.Datum;
                }

                geraete.Add(befund);
            }
        }
        catch (ManagementException)
        {
            // WMI nicht verfügbar oder Abfrage abgelehnt - dann eben eine leere Liste,
            // die Oberfläche sagt das ehrlich.
        }

        return geraete;
    }

    private static Dictionary<string, (string? Version, DateTime? Datum)> TreiberInfosLesen()
    {
        var infos = new Dictionary<string, (string?, DateTime?)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT DeviceID, DriverVersion, DriverDate FROM Win32_PnPSignedDriver");

            foreach (var o in suche.Get())
            {
                var id = o["DeviceID"]?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                infos[id] = (o["DriverVersion"]?.ToString(), WmiDatum.Parse(o["DriverDate"]?.ToString()));
            }
        }
        catch (ManagementException)
        {
            // Ohne diese Zusatzangaben fehlen nur Version und Datum - die Fehlererkennung
            // (der eigentliche Zweck des Moduls) funktioniert trotzdem.
        }

        return infos;
    }

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

    /// <summary>
    /// Bei sehr vielen Geräten trägt Windows als Hersteller Platzhalter wie
    /// "(Standardsystemgeräte)" ein - das hilft niemandem und wird weggelassen.
    /// </summary>
    private static string? Bereinigen(string? hersteller)
    {
        if (string.IsNullOrWhiteSpace(hersteller)) return null;

        var text = hersteller.Trim();
        return text.StartsWith('(') ? null : text;
    }
}
