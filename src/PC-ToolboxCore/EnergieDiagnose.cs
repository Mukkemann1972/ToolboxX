namespace PCToolboxCore;

/// <summary>Ergebnis von "powercfg /lastwake" (Free, kein Admin nötig): wie oft der PC seit dem
/// letzten Zurücksetzen des Zählers durch etwas geweckt wurde, plus Windows' eigener Rohtext zu
/// den Details (wird unverändert angezeigt statt in Einzelfelder zerlegt - siehe Erklärung unten).</summary>
public record AufweckVerlaufErgebnis(int? Anzahl, string RohDetails);

/// <summary>
/// "powercfg /lastwake" hat, anders als der Batteriebericht, keine XML-Variante - die Ausgabe ist
/// reiner, sprachabhängiger Text. Nur die erste Zeile folgt einem stabilen "&lt;Text&gt; - &lt;Zahl&gt;"
/// Muster (auf Deutsch "Aktivierungsverlaufsanzahl - 0", auf Englisch "Wake History Count - 0") -
/// die Zahl danach wird sprachunabhängig per Regex-freies Split extrahiert. Der Rest der Ausgabe
/// (bei einem echten Aufweck-Ereignis: Gerät/Zeitpunkt-Details) wird bewusst NICHT in Einzelfelder
/// zerlegt - dieses Feature konnte auf keiner Maschine gegen ein echtes Aufweck-Ereignis
/// live-verifiziert werden (dieser Rechner schläft laut eigener Energierichtlinie nie, siehe
/// Standby-Blocker/Energy-Report), das exakte Detailformat ist daher nicht gesichert. Windows'
/// eigener Text wird stattdessen unverändert gezeigt - ehrlicher als ein ungeprüftes Format
/// vorzutäuschen.
/// </summary>
public static class AufweckVerlauf
{
    public static AufweckVerlaufErgebnis Parsen(string rohausgabe)
    {
        var zeilen = rohausgabe.Replace("\r\n", "\n").Split('\n');
        var ersteZeilenIndex = Array.FindIndex(zeilen, z => z.Contains('-'));
        if (ersteZeilenIndex < 0)
            return new AufweckVerlaufErgebnis(null, rohausgabe.Trim());

        var zahlText = zeilen[ersteZeilenIndex].Split('-').LastOrDefault()?.Trim();
        var anzahl = int.TryParse(zahlText, out var n) ? n : (int?)null;

        var details = string.Join("\n",
            zeilen.Skip(ersteZeilenIndex + 1).Where(z => !string.IsNullOrWhiteSpace(z))).Trim();

        return new AufweckVerlaufErgebnis(anzahl, details);
    }
}
