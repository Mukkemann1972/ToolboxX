using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class RegistryKompendiumTests
{
    [Fact]
    public void Eintraege_HatKeineDuplizierteIds()
    {
        var ids = RegistryKompendium.Eintraege.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Eintraege_JederHatPfadWertnameUndErklaerung()
    {
        Assert.NotEmpty(RegistryKompendium.Eintraege);
        Assert.All(RegistryKompendium.Eintraege, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Pfad));
            Assert.False(string.IsNullOrWhiteSpace(e.Wertname));
            Assert.False(string.IsNullOrWhiteSpace(e.Erklaerung));
            Assert.True(e.Hive is "HKLM" or "HKCU");
        });
    }

    [Fact]
    public void DeuteWert_JederEintragHatEineDeutungFuerFehlendenWert()
    {
        // "null" simuliert einen Wert, der (noch) nicht in der Registry steht -
        // jeder Eintrag muss damit umgehen koennen, ohne "Unbekannt" zu liefern,
        // wenn ein Windows-Standardverhalten dahintersteckt.
        foreach (var eintrag in RegistryKompendium.Eintraege)
        {
            var befund = RegistryKompendium.DeuteWert(eintrag.Id, null);
            Assert.False(string.IsNullOrWhiteSpace(befund.Text));
        }
    }

    [Theory]
    [InlineData("dateiendungen", 0, AmpelStatus.Gruen)]
    [InlineData("dateiendungen", 1, AmpelStatus.Gelb)]
    [InlineData("uac-stufe", 0, AmpelStatus.Rot)]
    [InlineData("uac-stufe", 5, AmpelStatus.Gruen)]
    [InlineData("autologon", "1", AmpelStatus.Rot)]
    [InlineData("autologon", "0", AmpelStatus.Gruen)]
    [InlineData("remotedesktop", 0, AmpelStatus.Gelb)]
    [InlineData("remotedesktop", 1, AmpelStatus.Gruen)]
    [InlineData("autorun", 255, AmpelStatus.Gruen)]
    public void DeuteWert_LiefertErwarteteAmpel(string id, object rohwert, AmpelStatus erwartet)
    {
        var befund = RegistryKompendium.DeuteWert(id, rohwert);
        Assert.Equal(erwartet, befund.Status);
    }

    [Fact]
    public void DeuteWert_UnbekannteIdLiefertUnbekanntStattAbsturz()
    {
        var befund = RegistryKompendium.DeuteWert("gibt-es-nicht", 42);
        Assert.Equal(AmpelStatus.Unbekannt, befund.Status);
    }

    [Fact]
    public void DeuteWert_UnerwarteterTypLiefertUnbekanntStattAbsturz()
    {
        var befund = RegistryKompendium.DeuteWert("dateiendungen", "ein Text statt einer Zahl");
        Assert.Equal(AmpelStatus.Unbekannt, befund.Status);
    }
}
