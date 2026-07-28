namespace PCToolboxCore;

// Leitet aus Anzeigename/Hersteller eines Programms markante Such-Kandidaten ab, mit denen
// LeftoverScanner nach Registry-Schluesseln/Ordnern desselben Namens sucht. Bewusst kein voller
// Wortsalat (nur die markantesten Woerter, mind. 4 Zeichen) - kurze Woerter wie "Team" oder "App"
// wuerden sonst zu viele Zufallstreffer erzeugen.
public static class NameMatcher
{
    private const int MindestLaenge = 4;

    public static IEnumerable<string> Kandidaten(string? anzeigeName, string? hersteller)
    {
        var gefunden = new List<string>();

        foreach (var quelle in new[] { anzeigeName, hersteller })
        {
            if (string.IsNullOrWhiteSpace(quelle)) continue;

            var erstesWort = quelle.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            if (erstesWort.Length >= MindestLaenge) gefunden.Add(erstesWort);

            var ohneLeerzeichen = new string(quelle.Where(char.IsLetterOrDigit).ToArray());
            if (ohneLeerzeichen.Length >= MindestLaenge &&
                !string.Equals(ohneLeerzeichen, erstesWort, StringComparison.OrdinalIgnoreCase))
                gefunden.Add(ohneLeerzeichen);
        }

        return gefunden.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
