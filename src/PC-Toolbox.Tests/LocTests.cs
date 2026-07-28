using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

// Wichtig: diese Tests setzen Loc.Current und muessen deshalb NACHEINANDER
// laufen (xUnit fuehrt Tests in einer Klasse standardmaessig nicht parallel
// zu Tests in anderen Klassen aus derselben Assembly aus, aber Loc ist ein
// globaler statischer Zustand - jeder Test setzt ihn deshalb selbst am Ende
// wieder zurueck, um Reihenfolge-Abhaengigkeiten zu anderen Test-Dateien zu
// vermeiden, falls Loc.Current je woanders gelesen wird).
public class LocTests
{
    [Fact]
    public void T_GibtDeutschZurueck_WennAktuelleSpracheDeutsch()
    {
        Loc.Load(Sprache.Deutsch);
        try
        {
            Assert.Equal("Übersicht", Loc.T("Übersicht", "Overview"));
        }
        finally { Loc.Load(Sprache.Deutsch); }
    }

    [Fact]
    public void T_GibtEnglischZurueck_WennAktuelleSpracheEnglisch()
    {
        Loc.Load(Sprache.Englisch);
        try
        {
            Assert.Equal("Overview", Loc.T("Übersicht", "Overview"));
        }
        finally { Loc.Load(Sprache.Deutsch); }
    }

    [Fact]
    public void Load_SetztCurrentDauerhaftBisZumNaechstenLoad()
    {
        Loc.Load(Sprache.Englisch);
        try
        {
            Assert.Equal(Sprache.Englisch, Loc.Current);
            Assert.Equal("Settings", Loc.T("Einstellungen", "Settings"));
        }
        finally { Loc.Load(Sprache.Deutsch); }
    }

    [Theory]
    [InlineData("de", Sprache.Deutsch)]
    [InlineData("de-DE", Sprache.Deutsch)]
    [InlineData("de-AT", Sprache.Deutsch)]
    [InlineData("DE", Sprache.Deutsch)]
    [InlineData("en", Sprache.Englisch)]
    [InlineData("en-US", Sprache.Englisch)]
    [InlineData("fr", Sprache.Englisch)]
    [InlineData("", Sprache.Englisch)]
    public void ErkenneAusSystemsprache_DeutschNurBeiDe(string kuerzel, Sprache erwartet)
    {
        // Alles ausser Deutsch faellt auf Englisch zurueck statt auf eine
        // dritte, nicht uebersetzte Sprache - besser eine funktionierende
        // Uebersetzung als eine kaputte in der Landessprache.
        Assert.Equal(erwartet, Loc.ErkenneAusSystemsprache(kuerzel));
    }
}
