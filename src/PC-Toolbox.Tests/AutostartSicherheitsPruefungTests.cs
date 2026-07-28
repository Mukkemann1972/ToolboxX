using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class AutostartSicherheitsPruefungTests
{
    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("Explorer.exe")]
    [InlineData("EXPLORER.EXE")]
    public void PruefeWinlogonShell_ErkenntNormaleShellUnabhaengigVonGrossKleinschreibung(string wert)
    {
        var befund = AutostartSicherheitsPruefung.PruefeWinlogonShell(wert);
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }

    [Fact]
    public void PruefeWinlogonShell_LeererWertGiltAlsInOrdnung()
    {
        var befund = AutostartSicherheitsPruefung.PruefeWinlogonShell(null);
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }

    [Fact]
    public void PruefeWinlogonShell_FremdeShellIstRoterFund()
    {
        var befund = AutostartSicherheitsPruefung.PruefeWinlogonShell(@"C:\bad\evil.exe");
        Assert.Equal(AmpelStatus.Rot, befund.Status);
        Assert.Contains("evil.exe", befund.Text);
    }

    [Fact]
    public void IfeoUmleitung_IstImmerEinRoterFund()
    {
        var befund = AutostartSicherheitsPruefung.IfeoUmleitung("notepad.exe", @"C:\bad\evil.exe");
        Assert.Equal(AmpelStatus.Rot, befund.Status);
        Assert.Contains("notepad.exe", befund.Text);
        Assert.Contains("evil.exe", befund.Text);
    }
}
