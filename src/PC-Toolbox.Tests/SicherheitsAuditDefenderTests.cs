using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SicherheitsAuditDefenderTests
{
    [Fact]
    public void Bewerten_AllesInOrdnung_GibtGruen()
    {
        var befund = SicherheitsAuditDefender.Bewerten(true, true, 2, true);
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }

    [Fact]
    public void Bewerten_AntivirusUnbekannt_GibtGelb()
    {
        var befund = SicherheitsAuditDefender.Bewerten(null, null, null, null);
        Assert.Equal(AmpelStatus.Gelb, befund.Status);
    }

    [Fact]
    public void Bewerten_KeinAntivirus_GibtRot()
    {
        var befund = SicherheitsAuditDefender.Bewerten(false, false, null, null);
        Assert.Equal(AmpelStatus.Rot, befund.Status);
    }

    [Fact]
    public void Bewerten_EchtzeitschutzAus_GibtRot()
    {
        var befund = SicherheitsAuditDefender.Bewerten(true, false, 1, true);
        Assert.Equal(AmpelStatus.Rot, befund.Status);
    }

    [Fact]
    public void Bewerten_SignaturenVeraltet_GibtGelb()
    {
        var befund = SicherheitsAuditDefender.Bewerten(true, true, 20, true);
        Assert.Equal(AmpelStatus.Gelb, befund.Status);
    }

    [Fact]
    public void Bewerten_ManipulationsschutzAus_GibtGelb()
    {
        var befund = SicherheitsAuditDefender.Bewerten(true, true, 1, false);
        Assert.Equal(AmpelStatus.Gelb, befund.Status);
    }

    [Fact]
    public void Bewerten_SignaturenGrenzwertVierzehnTage_GibtNochGruen()
    {
        var befund = SicherheitsAuditDefender.Bewerten(true, true, 14, true);
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }
}
