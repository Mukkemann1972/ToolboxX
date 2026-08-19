using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class AkkuGesundheitTests
{
    [Fact]
    public void GesundheitsProzent_VolleKapazitaet_Ist100()
    {
        var kapazitaet = new AkkuKapazitaet(50000, 50000, null);

        Assert.Equal(100.0, AkkuGesundheit.GesundheitsProzent(kapazitaet));
    }

    [Fact]
    public void GesundheitsProzent_HalbeKapazitaet_Ist50()
    {
        var kapazitaet = new AkkuKapazitaet(50000, 25000, null);

        Assert.Equal(50.0, AkkuGesundheit.GesundheitsProzent(kapazitaet));
    }

    [Fact]
    public void GesundheitsProzent_KeineDesignKapazitaet_IstNullStattDivisionDurchNull()
    {
        var kapazitaet = new AkkuKapazitaet(0, 1000, null);

        Assert.Equal(0.0, AkkuGesundheit.GesundheitsProzent(kapazitaet));
    }

    [Theory]
    [InlineData(50000, 45000, AmpelStatus.Gruen)]   // 90%
    [InlineData(50000, 35000, AmpelStatus.Gelb)]    // 70%
    [InlineData(50000, 15000, AmpelStatus.Rot)]     // 30%
    public void Bewerten_SchwellenwerteStimmen(long design, long voll, AmpelStatus erwartet)
    {
        var kapazitaet = new AkkuKapazitaet(design, voll, null);

        Assert.Equal(erwartet, AkkuGesundheit.Bewerten(kapazitaet));
    }

    [Fact]
    public void Erklaerung_MitLadezyklen_NenntDieAnzahl()
    {
        var kapazitaet = new AkkuKapazitaet(50000, 45000, 320);

        Assert.Contains("320", AkkuGesundheit.Erklaerung(kapazitaet));
    }

    [Fact]
    public void Erklaerung_OhneLadezyklen_KommtOhneZahlAus()
    {
        var kapazitaet = new AkkuKapazitaet(50000, 45000, null);

        var text = AkkuGesundheit.Erklaerung(kapazitaet);
        Assert.DoesNotContain("Ladezyklen", text);
    }
}
