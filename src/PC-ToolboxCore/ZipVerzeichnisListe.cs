namespace PCToolboxCore;

/// <summary>
/// Baut aus den flachen Eintragsnamen eines ZIP-Archivs (wie sie ZipArchive.Entries liefert)
/// die "Kinder" eines bestimmten inneren Pfads - genau das, was man braucht, um ein ZIP wie
/// einen Ordner durchzublättern, ohne dass die Windows-seitige ZIP-Bibliothek selbst
/// hierher muss. Bewusst reine Textarbeit über eine Liste von (Pfad, Größe)-Paaren, damit
/// das auch auf dem Pi testbar ist, ohne eine echte ZIP-Datei zu öffnen.
///
/// ZIP-Einträge sind flach ("Ordner/Unterordner/Datei.txt") - Ordner tauchen darin oft gar
/// nicht als eigener Eintrag auf, sondern ergeben sich nur aus den Pfaden der enthaltenen
/// Dateien. Manche ZIP-Werkzeuge legen zusätzlich einen expliziten Ordnereintrag an (Pfad
/// endet auf "/", Größe 0) - beide Fälle werden hier gleich behandelt.
/// </summary>
public static class ZipVerzeichnisListe
{
    public sealed record ZipQuellEintrag(string Pfad, long Groesse);

    public sealed record ZipEintrag(string Name, bool IstOrdner, long? GroesseBytes);

    public static List<ZipEintrag> KindEintraege(IEnumerable<ZipQuellEintrag> alleEintraege, string aktuellerPfad)
    {
        var praefix = string.IsNullOrEmpty(aktuellerPfad) ? "" : aktuellerPfad.Trim('/') + "/";

        var ordnerNamen = new HashSet<string>(StringComparer.Ordinal);
        var dateiGroessen = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var eintrag in alleEintraege)
        {
            var pfad = eintrag.Pfad.Replace('\\', '/');
            if (!pfad.StartsWith(praefix, StringComparison.Ordinal)) continue;

            var rest = pfad[praefix.Length..];
            if (rest.Length == 0) continue; // der aktuelle Ordner selbst, kein Kind

            var istExpliziterOrdnerEintrag = rest.EndsWith('/');
            if (istExpliziterOrdnerEintrag) rest = rest[..^1];
            if (rest.Length == 0) continue;

            var trenner = rest.IndexOf('/');
            if (trenner < 0)
            {
                if (istExpliziterOrdnerEintrag) ordnerNamen.Add(rest);
                else dateiGroessen[rest] = eintrag.Groesse;
            }
            else
            {
                // Liegt tiefer als ein direktes Kind - macht aber den ersten Namensteil zu einem Unterordner hier.
                ordnerNamen.Add(rest[..trenner]);
            }
        }

        var ergebnis = new List<ZipEintrag>();
        ergebnis.AddRange(ordnerNamen.Select(name => new ZipEintrag(name, true, null)));

        // Ein Name, der sowohl als Ordner als auch als Datei auftaucht (untypisch, aber ein
        // ZIP könnte inkonsistent gepackt sein), zählt als Ordner - das ist die sicherere Deutung.
        ergebnis.AddRange(dateiGroessen
            .Where(kv => !ordnerNamen.Contains(kv.Key))
            .Select(kv => new ZipEintrag(kv.Key, false, kv.Value)));

        return ergebnis
            .OrderBy(e => !e.IstOrdner)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
