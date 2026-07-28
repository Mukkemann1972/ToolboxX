namespace PCToolboxCore;

/// <summary>
/// Holt aus einer Autostart-Befehlszeile die eigentliche Programmdatei heraus.
///
/// Klingt trivial, ist es aber nicht: In der Registry stehen Befehle wie
///   "C:\Program Files\Firma\tool.exe" -minimized     (mit Anfuehrungszeichen)
///   C:\Program Files\Firma\tool.exe -minimized       (OHNE - trotz Leerzeichen im Pfad!)
/// Wer hier stumpf am ersten Leerzeichen trennt, landet bei "C:\Program" und meldet
/// dann faelschlich "Datei fehlt". Genau solche Fehlalarme sind der Grund, warum viele
/// Aufraeum-Tools Eintraege als Leichen anzeigen, die voellig in Ordnung sind.
///
/// Deshalb wird bei fehlenden Anfuehrungszeichen an jeder Leerstelle geprueft, ob es
/// die Datei bis dahin wirklich gibt. Die Pruefung wird hereingereicht (Func), damit
/// diese Logik plattformneutral bleibt und auf dem Pi echt getestet werden kann.
/// </summary>
public static class BefehlszeileZerleger
{
    public static string ProgrammPfadErmitteln(string? befehl, Func<string, bool> existiert)
    {
        var text = (befehl ?? "").Trim();
        if (text.Length == 0) return "";

        if (text.StartsWith('"'))
        {
            var ende = text.IndexOf('"', 1);
            return ende > 0 ? text[1..ende] : text.Trim('"');
        }

        if (existiert(text)) return text;

        var pos = -1;
        while ((pos = text.IndexOf(' ', pos + 1)) >= 0)
        {
            var kandidat = text[..pos];
            if (existiert(kandidat)) return kandidat;
            if (existiert(kandidat + ".exe")) return kandidat + ".exe";
        }

        // Nichts gefunden: den Teil vor dem ersten Leerzeichen zurueckgeben. Die
        // Bewertung meldet dann "Datei fehlt" - was in diesem Fall auch stimmt.
        var ersteLeerstelle = text.IndexOf(' ');
        return ersteLeerstelle < 0 ? text : text[..ersteLeerstelle];
    }
}
