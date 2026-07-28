using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class NameMatcherTests
{
    [Fact]
    public void Kandidaten_NormalerName_LiefertErstesWortUndZusammengeschrieben()
    {
        var kandidaten = NameMatcher.Kandidaten("Mozilla Firefox", "Mozilla").ToList();

        Assert.Contains("Mozilla", kandidaten);
        Assert.Contains("MozillaFirefox", kandidaten);
    }

    [Fact]
    public void Kandidaten_KurzesErstesWort_WirdAusgeschlossen()
    {
        // "7-" Zip hat als "erstes Wort" (bis zum Leerzeichen) nur "7-Zip" - kein Leerzeichen drin,
        // also zaehlt "7Zip" (zusammengeschrieben). Aber ein Name wie "Go App" hat "Go" (2 Zeichen)
        // als erstes Wort - das soll NICHT als Kandidat auftauchen (Mindestlaenge 4).
        var kandidaten = NameMatcher.Kandidaten("Go App", null).ToList();

        Assert.DoesNotContain("Go", kandidaten);
    }

    [Fact]
    public void Kandidaten_DisplayNameUndPublisherGleicherName_KeineDuplikate()
    {
        // Google Chrome / Publisher "Google LLC" wuerden ohne Dedupe zweimal "Google" liefern.
        var kandidaten = NameMatcher.Kandidaten("Google Chrome", "Google LLC").ToList();

        Assert.Single(kandidaten, k => string.Equals(k, "Google", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Kandidaten_LeereWerte_LiefertLeereListe()
    {
        var kandidaten = NameMatcher.Kandidaten(null, "").ToList();

        Assert.Empty(kandidaten);
    }

    [Fact]
    public void Kandidaten_EinWortName_ErstesWortUndZusammengeschriebenGleich_NurEinmal()
    {
        // "Notepad++" hat kein Leerzeichen - erstesWort == ohneLeerzeichen (Sonderzeichen raus) fast gleich,
        // darf nicht doppelt auftauchen.
        var kandidaten = NameMatcher.Kandidaten("Notepad", null).ToList();

        Assert.Single(kandidaten);
        Assert.Equal("Notepad", kandidaten[0]);
    }
}
