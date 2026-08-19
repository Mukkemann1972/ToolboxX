using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class ProduktSchluesselFormatTests
{
    [Theory]
    [InlineData("ABCDE-FGHJK-MPQRT-VWXY2-34678")]
    [InlineData("12345-12345-12345-12345-12345")]
    public void SiehtGueltigAus_FuenfGruppenZuFuenf_GibtTrue(string schluessel)
    {
        Assert.True(ProduktSchluesselFormat.SiehtGueltigAus(schluessel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDE-FGHJK-MPQRT-VWXY2")]
    [InlineData("ABCDE-FGHJK-MPQRT-VWXY2-34678-9")]
    [InlineData("ABC-DE-FGHJK-MPQRT-VWXY2")]
    [InlineData("nur ein Text ohne Bindestriche")]
    public void SiehtGueltigAus_FalschesFormat_GibtFalse(string? schluessel)
    {
        Assert.False(ProduktSchluesselFormat.SiehtGueltigAus(schluessel));
    }
}
