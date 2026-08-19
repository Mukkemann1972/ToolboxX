using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class FirmwareTypTests
{
    [Fact]
    public void RufusHinweis_Uefi_NenntGpt()
    {
        var text = FirmwareTypErklaerung.RufusHinweis(FirmwareTyp.Uefi);

        Assert.Contains("GPT", text);
        Assert.Contains("UEFI", text);
    }

    [Fact]
    public void RufusHinweis_LegacyBios_NenntMbr()
    {
        var text = FirmwareTypErklaerung.RufusHinweis(FirmwareTyp.LegacyBios);

        Assert.Contains("MBR", text);
        Assert.Contains("BIOS", text);
    }

    [Fact]
    public void RufusHinweis_Unbekannt_GibtEhrlichenHinweisOhneGptMbr()
    {
        var text = FirmwareTypErklaerung.RufusHinweis(FirmwareTyp.Unbekannt);

        Assert.DoesNotContain("GPT", text);
        Assert.DoesNotContain("MBR", text);
    }
}
