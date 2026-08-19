using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DirectXDiagnoseTests
{
    // Ausschnitt aus dem echten "dxdiag /t"-Bericht von diesem Rechner.
    private const string RealBericht =
        "------------------\r\n" +
        "System Information\r\n" +
        "------------------\r\n" +
        "      Time of this report: 8/8/2026, 23:18:48\r\n" +
        "         Operating System: Windows 11 Pro 64-bit (10.0, Build 26200)\r\n" +
        "          DirectX Version: DirectX 12\r\n" +
        "      DX Setup Parameters: Not found\r\n" +
        "\r\n" +
        "------------\r\n" +
        "DxDiag Notes\r\n" +
        "------------\r\n" +
        "      Display Tab 1: No problems found.\r\n" +
        "        Sound Tab 1: No problems found.\r\n" +
        "        Sound Tab 2: No problems found.\r\n" +
        "          Input Tab: No problems found.\r\n" +
        "\r\n" +
        "--------------------\r\n" +
        "DirectX Debug Levels\r\n" +
        "--------------------\r\n" +
        "Direct3D:    0/4 (retail)\r\n";

    [Fact]
    public void Auswerten_EchterBericht_LiestDirectXVersion()
    {
        var ergebnis = DirectXDiagnose.Auswerten(RealBericht);

        Assert.Equal("DirectX 12", ergebnis.Version);
    }

    [Fact]
    public void Auswerten_EchterBericht_LiestAlle4Notizen()
    {
        var ergebnis = DirectXDiagnose.Auswerten(RealBericht);

        Assert.Equal(4, ergebnis.Notizen.Count);
        Assert.All(ergebnis.Notizen, n => Assert.True(n.IstUnauffaellig));
        Assert.Equal("Display Tab 1", ergebnis.Notizen[0].Bereich);
        Assert.Equal("Input Tab", ergebnis.Notizen[3].Bereich);
    }

    [Fact]
    public void Auswerten_NotizMitEchtemProblem_IstNichtUnauffaellig()
    {
        var notiz = new DirectXNotiz("Sound Tab 1", "Missing driver for device X.");

        Assert.False(notiz.IstUnauffaellig);
    }

    [Fact]
    public void Auswerten_UnerwartetesFormat_LiefertNullStattAbsturz()
    {
        var ergebnis = DirectXDiagnose.Auswerten("Irgendein komplett anderer Text ohne die erwarteten Marker");

        Assert.Null(ergebnis.Version);
        Assert.Empty(ergebnis.Notizen);
    }

    [Fact]
    public void Auswerten_LeererString_KrachtNicht()
    {
        var ergebnis = DirectXDiagnose.Auswerten("");

        Assert.Null(ergebnis.Version);
        Assert.Empty(ergebnis.Notizen);
    }
}
