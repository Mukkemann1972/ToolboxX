using System.IO;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>Ein im Papierkorb gefundener Eintrag mit dem Pfad zur zugehörigen $R-Datei (dem
/// eigentlichen Dateiinhalt) - getrennt vom reinen $I-Metadaten-Datensatz (PapierkorbEintrag),
/// weil der hier zusätzlich den echten Dateisystem-Pfad kennt.</summary>
public record PapierkorbFund(string OriginalPfad, string OriginalName, long GroesseBytes, DateTime? GeloeschtAm, string RDateiPfad);

/// <summary>
/// Papierkorb-Prüfung (Free) - der schnellste, genaueste erste Schritt vor der reinen Byte-Suche
/// (DatenrettungService): Windows behält gelöschte Dateien mit Original-Namen im Papierkorb, bis
/// er geleert wird. Braucht keine erhöhten Rechte - der eigene Papierkorb-Ordner (SID-Unterordner)
/// ist für den angemeldeten Nutzer normal lesbar, andere SID-Ordner (fremde Konten) werden beim
/// Zugriffsfehler übersprungen statt die ganze Suche abzubrechen.
/// </summary>
public class PapierkorbService
{
    public Task<IReadOnlyList<PapierkorbFund>> DurchsuchenAsync(IReadOnlyList<char> laufwerksbuchstaben) => Task.Run(() =>
    {
        var ergebnis = new List<PapierkorbFund>();

        foreach (var buchstabe in laufwerksbuchstaben)
        {
            var papierkorbOrdner = $"{char.ToUpperInvariant(buchstabe)}:\\$Recycle.Bin";
            if (!Directory.Exists(papierkorbOrdner)) continue;

            foreach (var sidOrdner in SicherAuflisten(() => Directory.EnumerateDirectories(papierkorbOrdner)))
            {
                foreach (var iDatei in SicherAuflisten(() => Directory.EnumerateFiles(sidOrdner, "$I*")))
                {
                    var eintrag = SicherLesen(iDatei);
                    if (eintrag is null) continue;

                    // $R-Datei traegt denselben Zufalls-Suffix wie die $I-Datei, nur das Kuerzel
                    // am Anfang unterscheidet sich ("$I" vs "$R") - der eigentliche Dateiinhalt.
                    var rDatei = Path.Combine(Path.GetDirectoryName(iDatei)!, "$R" + Path.GetFileName(iDatei)[2..]);
                    if (!File.Exists(rDatei)) continue;

                    ergebnis.Add(new PapierkorbFund(eintrag.OriginalPfad, eintrag.OriginalName, eintrag.GroesseBytes, eintrag.GeloeschtAm, rDatei));
                }
            }
        }

        return (IReadOnlyList<PapierkorbFund>)ergebnis.OrderByDescending(f => f.GeloeschtAm).ToList();
    });

    public Task<(int Wiederhergestellt, int Fehlgeschlagen)> WiederherstellenAsync(IReadOnlyList<PapierkorbFund> funde, string zielOrdner) => Task.Run(() =>
    {
        Directory.CreateDirectory(zielOrdner);
        var ok = 0;
        var fehlgeschlagen = 0;

        foreach (var fund in funde)
        {
            try
            {
                File.Copy(fund.RDateiPfad, EindeutigerZielpfad(Path.Combine(zielOrdner, fund.OriginalName)));
                ok++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                fehlgeschlagen++;
            }
        }

        return (ok, fehlgeschlagen);
    });

    /// <summary>Hängt "(2)", "(3)", ... an, statt eine gleichnamige, bereits im Zielordner
    /// vorhandene Datei zu überschreiben - z.B. wenn zwei Papierkorb-Funde zufällig denselben
    /// Original-Namen tragen (aus verschiedenen Ordnern gelöscht).</summary>
    private static string EindeutigerZielpfad(string pfad)
    {
        if (!File.Exists(pfad)) return pfad;

        var ordner = Path.GetDirectoryName(pfad)!;
        var name = Path.GetFileNameWithoutExtension(pfad);
        var endung = Path.GetExtension(pfad);
        for (var i = 2; ; i++)
        {
            var kandidat = Path.Combine(ordner, $"{name} ({i}){endung}");
            if (!File.Exists(kandidat)) return kandidat;
        }
    }

    private static PapierkorbEintrag? SicherLesen(string iDatei)
    {
        try
        {
            return PapierkorbIDatei.Lesen(File.ReadAllBytes(iDatei));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SicherAuflisten(Func<IEnumerable<string>> quelle)
    {
        IEnumerator<string> enumerator;
        try
        {
            enumerator = quelle().GetEnumerator();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                bool weiter;
                try
                {
                    weiter = enumerator.MoveNext();
                }
                catch (Exception e) when (e is UnauthorizedAccessException or IOException)
                {
                    yield break;
                }
                if (!weiter) yield break;
                yield return enumerator.Current;
            }
        }
    }
}
