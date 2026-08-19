using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class NvmeZuverlaessigkeitBewertungTests
{
    [Fact]
    public void Bewerten_HoheAbnutzung_IstRot()
    {
        var (status, _) = NvmeZuverlaessigkeitBewertung.Bewerten(new NvmeZuverlaessigkeitWerte(95, 40, 1000, 0));
        Assert.Equal(AmpelStatus.Rot, status);
    }

    [Fact]
    public void Bewerten_UnkorrigierbareFehler_IstGelb()
    {
        var (status, _) = NvmeZuverlaessigkeitBewertung.Bewerten(new NvmeZuverlaessigkeitWerte(10, 40, 1000, 3));
        Assert.Equal(AmpelStatus.Gelb, status);
    }

    [Fact]
    public void Bewerten_MittlereAbnutzung_IstGelb()
    {
        var (status, _) = NvmeZuverlaessigkeitBewertung.Bewerten(new NvmeZuverlaessigkeitWerte(75, 40, 1000, 0));
        Assert.Equal(AmpelStatus.Gelb, status);
    }

    [Fact]
    public void Bewerten_AllesGesund_IstGruen()
    {
        var (status, _) = NvmeZuverlaessigkeitBewertung.Bewerten(new NvmeZuverlaessigkeitWerte(5, 40, 1000, 0));
        Assert.Equal(AmpelStatus.Gruen, status);
    }

    [Fact]
    public void Bewerten_KeineWerte_IstGruen()
    {
        var (status, _) = NvmeZuverlaessigkeitBewertung.Bewerten(new NvmeZuverlaessigkeitWerte(null, null, null, null));
        Assert.Equal(AmpelStatus.Gruen, status);
    }
}
