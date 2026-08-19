using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class VerwaisteOrdnerErkennungTests
{
    [Fact]
    public void IstBekannterSystemOrdner_CommonFilesIstTrue()
    {
        Assert.True(VerwaisteOrdnerErkennung.IstBekannterSystemOrdner("Common Files"));
    }

    [Fact]
    public void IstBekannterSystemOrdner_GrossKleinschreibungEgal()
    {
        Assert.True(VerwaisteOrdnerErkennung.IstBekannterSystemOrdner("microsoft"));
    }

    [Fact]
    public void IstBekannterSystemOrdner_UnbekannterOrdnerIstFalse()
    {
        Assert.False(VerwaisteOrdnerErkennung.IstBekannterSystemOrdner("IrgendeineAlteSoftware"));
    }

    [Fact]
    public void IstErklaertDurch_ExakterNameTreffer()
    {
        var programme = new[] { ("CCleaner", "Piriform") };

        Assert.True(VerwaisteOrdnerErkennung.IstErklaertDurch("CCleaner", programme));
    }

    [Fact]
    public void IstErklaertDurch_TreffertUeberHersteller()
    {
        var programme = new[] { ("CCleaner", "Piriform") };

        Assert.True(VerwaisteOrdnerErkennung.IstErklaertDurch("Piriform", programme));
    }

    [Fact]
    public void IstErklaertDurch_KeinProgrammPasstIstFalse()
    {
        var programme = new[] { ("CCleaner", "Piriform"), ("Firefox", "Mozilla") };

        Assert.False(VerwaisteOrdnerErkennung.IstErklaertDurch("AlteSoftwareGmbH", programme));
    }

    [Fact]
    public void IstErklaertDurch_LeereListeIstFalse()
    {
        Assert.False(VerwaisteOrdnerErkennung.IstErklaertDurch("Irgendwas", Array.Empty<(string, string)>()));
    }

    [Fact]
    public void IstVerdaechtig_UnbekannterOrdnerOhnePassendesProgrammIstTrue()
    {
        var programme = new[] { ("CCleaner", "Piriform") };

        Assert.True(VerwaisteOrdnerErkennung.IstVerdaechtig("AlteSoftwareGmbH", programme));
    }

    [Fact]
    public void IstVerdaechtig_BekannterSystemOrdnerIstNieVerdaechtig()
    {
        Assert.False(VerwaisteOrdnerErkennung.IstVerdaechtig("Common Files", Array.Empty<(string, string)>()));
    }

    [Fact]
    public void IstVerdaechtig_DurchInstalliertesProgrammErklaertIstNichtVerdaechtig()
    {
        var programme = new[] { ("CCleaner", "Piriform") };

        Assert.False(VerwaisteOrdnerErkennung.IstVerdaechtig("CCleaner", programme));
    }
}
