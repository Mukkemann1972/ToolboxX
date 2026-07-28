using System.Globalization;

namespace PCToolboxCore;

/// <summary>
/// Wandelt Datumsangaben aus WMI (Windows-Systeminformationen) in echte Datumswerte um.
///
/// WMI liefert Zeitpunkte in einem eigenen Format: "20210513000000.000000+000"
/// (Jahr, Monat, Tag, Stunde, Minute, Sekunde, dann Nachkommastellen und Zeitzone).
/// Bei Treiberdaten stehen dort oft Sternchen statt der Zeitzone - deshalb wird
/// bewusst nur der vordere, verlässliche Teil ausgewertet.
/// </summary>
public static class WmiDatum
{
    public static DateTime? Parse(string? wmiWert)
    {
        if (string.IsNullOrWhiteSpace(wmiWert)) return null;

        var text = wmiWert.Trim();
        if (text.Length < 8) return null;

        var datumsTeil = text[..8];

        return DateTime.TryParseExact(datumsTeil, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var datum)
            ? datum
            : null;
    }
}
