using System.Text.Json;

namespace PCToolboxCore;

/// <summary>
/// Ordnet Laufwerksbuchstaben (C:, D:, ...) ihrer physischen Platte zu - für jede Stelle, an der
/// eine ganze physische Platte per Nummer ausgewählt wird (Datenrettung, Klonen, Wischen,
/// Partitionen verwalten). "Platte 2: Kingston - 64 GB" allein sagt nichts darüber, ob das der
/// USB-Stick oder eine interne Datenplatte ist - bei mehreren angeschlossenen Platten genau die
/// Verwechslungsgefahr, die schon mal zu einer versehentlich bearbeiteten falschen Platte geführt
/// hat. Deshalb wird der Laufwerksbuchstabe direkt in der Auswahlliste mit angezeigt, nicht nur
/// auf Nachfrage.
/// </summary>
public static class LaufwerksbuchstabenProPlatte
{
    /// <summary>An die üblichen "Get-Disk ...; Write-Output TRENNER; ..."-Mehrfachabfragen
    /// anhängbar (s. z.B. FestplattenWischService) - liefert nur Partitionen, die wirklich einen
    /// Laufwerksbuchstaben haben, versteckte/reservierte Partitionen fallen von selbst raus.</summary>
    public const string Abfragebefehl =
        "Get-Partition | Where-Object { $_.DriveLetter } | Select-Object DiskNumber,DriveLetter | ConvertTo-Json -Compress";

    public static IReadOnlyDictionary<int, IReadOnlyList<char>> Lesen(string json)
    {
        var ergebnis = new Dictionary<int, List<char>>();

        foreach (var element in AlsElemente(json))
        {
            if (!element.TryGetProperty("DiskNumber", out var dn) || !dn.TryGetInt32(out var diskNummer)) continue;
            if (!element.TryGetProperty("DriveLetter", out var dl)) continue;

            var text = dl.ValueKind switch
            {
                JsonValueKind.String => dl.GetString(),
                JsonValueKind.Number => dl.ToString(),
                _ => null
            };
            if (string.IsNullOrEmpty(text)) continue;

            var buchstabe = char.ToUpperInvariant(text[0]);
            if (!ergebnis.TryGetValue(diskNummer, out var liste))
                ergebnis[diskNummer] = liste = [];
            if (!liste.Contains(buchstabe)) liste.Add(buchstabe);
        }

        foreach (var liste in ergebnis.Values) liste.Sort();
        return ergebnis.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<char>)kv.Value);
    }

    /// <summary>Fertiger Anzeigetext für die Auswahllisten, z.B. "C:, D:" oder (ohne Buchstaben,
    /// z.B. bei einem leeren/unpartitionierten USB-Stick) ein ehrlicher Platzhalter statt gar
    /// nichts - Fehlen ist hier eine Information, keine Lücke.</summary>
    public static string Anzeige(IReadOnlyList<char>? buchstaben) =>
        buchstaben is { Count: > 0 }
            ? string.Join(", ", buchstaben.Select(b => $"{b}:"))
            : Loc.T("kein Laufwerksbuchstabe", "no drive letter");

    private static IEnumerable<JsonElement> AlsElemente(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;

        JsonDocument dokument;
        try { dokument = JsonDocument.Parse(json); }
        catch (JsonException) { yield break; }

        using (dokument)
        {
            var wurzel = dokument.RootElement;
            if (wurzel.ValueKind == JsonValueKind.Object) yield return wurzel;
            else if (wurzel.ValueKind == JsonValueKind.Array)
                foreach (var el in wurzel.EnumerateArray()) yield return el;
        }
    }
}
