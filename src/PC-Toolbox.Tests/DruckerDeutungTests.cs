using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DruckerDeutungTests
{
    [Theory]
    [InlineData(3, AmpelStatus.Gruen)]
    [InlineData(4, AmpelStatus.Gruen)]
    [InlineData(5, AmpelStatus.Gelb)]
    [InlineData(6, AmpelStatus.Rot)]
    [InlineData(7, AmpelStatus.Rot)]
    [InlineData(1, AmpelStatus.Unbekannt)]
    [InlineData(99, AmpelStatus.Unbekannt)]
    public void DeutePrinterStatus_LiefertErwarteteAmpel(int status, AmpelStatus erwartet)
    {
        Assert.Equal(erwartet, DruckerDeutung.DeutePrinterStatus(status).Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void DeuteErrorState_KeinFehlerBeiUnknownOderNoError(int errorState)
    {
        Assert.Null(DruckerDeutung.DeuteErrorState(errorState));
    }

    [Theory]
    [InlineData(4, AmpelStatus.Rot)]   // Kein Papier
    [InlineData(6, AmpelStatus.Rot)]   // Kein Toner
    [InlineData(8, AmpelStatus.Rot)]   // Papierstau
    [InlineData(3, AmpelStatus.Gelb)]  // Papier wird knapp
    [InlineData(5, AmpelStatus.Gelb)]  // Toner wird knapp
    public void DeuteErrorState_LiefertErwarteteAmpelFuerBekannteFehler(int errorState, AmpelStatus erwartet)
    {
        var befund = DruckerDeutung.DeuteErrorState(errorState);
        Assert.NotNull(befund);
        Assert.Equal(erwartet, befund!.Status);
    }

    [Fact]
    public void DeuteErrorState_UnbekannterCodeBleibtEhrlichStattZuRaten()
    {
        var befund = DruckerDeutung.DeuteErrorState(999);
        Assert.NotNull(befund);
        Assert.Equal(AmpelStatus.Gelb, befund!.Status);
    }

    [Fact]
    public void GesamtBefund_OfflineArbeitenSchlaegtAllesAndereRot()
    {
        // Auch wenn PrinterStatus "Bereit" meldet: WorkOffline=true ist die eigentliche Ursache,
        // warum nichts gedruckt wird, und muss Vorrang vor einem gruenen Status haben.
        var befund = DruckerDeutung.GesamtBefund(printerStatus: 3, detectedErrorState: 2, workOffline: true);
        Assert.Equal(AmpelStatus.Rot, befund.Status);
    }

    [Fact]
    public void GesamtBefund_FehlerstatusSchlaegtPrinterStatus()
    {
        var befund = DruckerDeutung.GesamtBefund(printerStatus: 3, detectedErrorState: 4, workOffline: false);
        Assert.Equal(AmpelStatus.Rot, befund.Status);
        Assert.Contains("Papier", befund.Text);
    }

    [Fact]
    public void GesamtBefund_FaelltZurueckAufPrinterStatusWennKeinFehler()
    {
        var befund = DruckerDeutung.GesamtBefund(printerStatus: 3, detectedErrorState: 2, workOffline: false);
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }
}
