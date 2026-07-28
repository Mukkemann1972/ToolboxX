using PCToolboxCore;
using PCToolboxCore.Models;
using PCToolboxCore.Services;
using Xunit;

namespace PCToolboxCore.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void Load_OhneDatei_GibtStandardwerteZurueck()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-test-{Guid.NewGuid()}.json");

        var settings = SettingsService.Load(pfad);

        Assert.Equal(AppTheme.DunkelNeon, settings.Theme);
        Assert.Equal("#22D3EE", settings.AccentColor);
    }

    [Fact]
    public void SaveUndLoad_Rundtrip_BehaeltWerte()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-test-{Guid.NewGuid()}.json");
        try
        {
            var original = new AppSettings { Theme = AppTheme.Hell, AccentColor = "#F59E0B" };

            SettingsService.Save(original, pfad);
            var geladen = SettingsService.Load(pfad);

            Assert.Equal(original.Theme, geladen.Theme);
            Assert.Equal(original.AccentColor, geladen.AccentColor);
        }
        finally
        {
            if (File.Exists(pfad)) File.Delete(pfad);
        }
    }

    [Fact]
    public void Load_KaputteDatei_FaelltAufStandardwerteZurueckStattAbzustuerzen()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-test-{Guid.NewGuid()}.json");
        File.WriteAllText(pfad, "{ das ist kein gueltiges JSON");
        try
        {
            var settings = SettingsService.Load(pfad);
            Assert.Equal(AppTheme.DunkelNeon, settings.Theme);
        }
        finally
        {
            File.Delete(pfad);
        }
    }

    [Fact]
    public void Load_OhneDatei_ErkenntSpracheStattSieUnbestimmtZuLassen()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-test-{Guid.NewGuid()}.json");

        var settings = SettingsService.Load(pfad);

        // Welcher der beiden Werte es konkret ist, haengt vom Testrechner ab (s.
        // LocTests fuer die eigentliche Erkennungslogik) - hier zaehlt nur, dass
        // beim allerersten Start ueberhaupt EINE Sprache feststeht, statt null zu
        // bleiben und irgendwo unbemerkt auf Deutsch zu defaulten.
        Assert.NotNull(settings.Sprache);
    }

    [Fact]
    public void Load_AeltereDateiOhneSprachFeld_ErgaenztSpracheStattNullZuLassen()
    {
        // Simuliert eine settings.json von VOR der Sprachumschaltung - das Feld
        // fehlt im JSON komplett. Nutzer mit englischem Windows sollen nach einem
        // Update nicht ungefragt eine deutsche Toolbox bekommen.
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-test-{Guid.NewGuid()}.json");
        File.WriteAllText(pfad, """{ "Theme": "Hell", "AccentColor": "#F59E0B" }""");
        try
        {
            var settings = SettingsService.Load(pfad);
            Assert.NotNull(settings.Sprache);
        }
        finally { File.Delete(pfad); }
    }

    [Fact]
    public void SaveUndLoad_Sprache_Rundtrip_UeberschreibtNichtMitErkennung()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-test-{Guid.NewGuid()}.json");
        try
        {
            SettingsService.Save(new AppSettings { Sprache = Sprache.Englisch }, pfad);
            var geladen = SettingsService.Load(pfad);

            Assert.Equal(Sprache.Englisch, geladen.Sprache);
        }
        finally { File.Delete(pfad); }
    }

    [Theory]
    [InlineData("#22D3EE", "#127482")]
    [InlineData("#FFFFFF", "#8C8C8C")]
    [InlineData("#000000", "#000000")]
    public void Darken_RechnetErwarteteAbdunklung(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, SettingsService.Darken(eingabe));
    }
}
