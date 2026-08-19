using System.Globalization;
using System.Text;

namespace PCToolboxCore;

/// <summary>Ein Messpunkt des Live-Systemmonitors.</summary>
public record SystemmonitorMesswert(
    DateTime Zeitpunkt, double CpuProzent, double RamProzent, double DiskProzent, double NetzwerkKbps);

/// <summary>
/// Begrenzter Verlauf der letzten Messpunkte (FIFO, älteste fällt raus) - ein unbegrenzt
/// wachsender Verlauf würde bei minutenlangem Beobachten unnötig Speicher binden, für den
/// CSV-Export und die Live-Anzeige reicht ein Fenster der letzten N Messpunkte.
/// </summary>
public class SystemmonitorVerlauf
{
    private readonly List<SystemmonitorMesswert> _werte = new();

    public SystemmonitorVerlauf(int maxEintraege)
    {
        if (maxEintraege <= 0) throw new ArgumentOutOfRangeException(nameof(maxEintraege));
        MaxEintraege = maxEintraege;
    }

    public int MaxEintraege { get; }
    public IReadOnlyList<SystemmonitorMesswert> Werte => _werte;

    public void Hinzufuegen(SystemmonitorMesswert wert)
    {
        _werte.Add(wert);
        if (_werte.Count > MaxEintraege)
            _werte.RemoveAt(0);
    }
}

/// <summary>
/// CSV-Export des Live-Systemmonitors (Free). Semikolon als Trennzeichen (nicht Komma) + Punkt
/// als Dezimaltrennzeichen (InvariantCulture) - Semikolon ist die von deutschem Excel erwartete
/// CSV-Trennung, Punkt-Dezimalzahlen werden von Excels CSV-Import trotzdem korrekt als Zahl
/// erkannt (anders als eine Komma/Komma-Kollision, die Spalten verschieben würde).
/// </summary>
public static class SystemmonitorCsv
{
    public static string ZuCsv(IEnumerable<SystemmonitorMesswert> werte)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Zeitpunkt;CPU_Prozent;RAM_Prozent;Disk_Prozent;Netzwerk_KBps");

        foreach (var w in werte)
        {
            sb.AppendLine(string.Join(";",
                w.Zeitpunkt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                w.CpuProzent.ToString("0.0", CultureInfo.InvariantCulture),
                w.RamProzent.ToString("0.0", CultureInfo.InvariantCulture),
                w.DiskProzent.ToString("0.0", CultureInfo.InvariantCulture),
                w.NetzwerkKbps.ToString("0.0", CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }
}
