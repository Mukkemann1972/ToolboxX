using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class WindowsUpdateDeutungTests
{
    [Theory]
    [InlineData("2026-07 Kumulatives Update für Windows 11 (KB5040001)", "KB5040001")]
    [InlineData("Security Intelligence-Update für Microsoft Defender Antivirus – KB2267602 (Version 1.455.382.0)", "KB2267602")]
    [InlineData("kb1234567 in Kleinbuchstaben", "KB1234567")]
    public void KbNummerAusTitel_FindetVorhandeneKbNummer(string titel, string erwartet)
    {
        Assert.Equal(erwartet, WindowsUpdateDeutung.KbNummerAusTitel(titel));
    }

    [Fact]
    public void KbNummerAusTitel_GibtNullZurueckWennKeineKbNummerImTitel()
    {
        Assert.Null(WindowsUpdateDeutung.KbNummerAusTitel("9NRZT3Q9R3DL-Microsoft.WindowsAppRuntime.2"));
    }

    [Theory]
    [InlineData(2, false)]   // Succeeded
    [InlineData(3, true)]    // SucceededWithErrors
    [InlineData(4, true)]    // Failed
    [InlineData(5, true)]    // Aborted
    [InlineData(1, true)]    // InProgress - unterbrochen ist genau der Verdachtsfall
    [InlineData(0, false)]   // NotStarted
    public void DeuteErgebnisCode_MarkiertRichtigAlsFehlgeschlagenOderNicht(int code, bool erwartetFehlgeschlagen)
    {
        var (_, istFehlgeschlagen) = WindowsUpdateDeutung.DeuteErgebnisCode(code);
        Assert.Equal(erwartetFehlgeschlagen, istFehlgeschlagen);
    }

    [Fact]
    public void DeuteErgebnisCode_GibtNieLeerenTextZurueck()
    {
        foreach (var code in new[] { -1, 0, 1, 2, 3, 4, 5, 99 })
        {
            var (text, _) = WindowsUpdateDeutung.DeuteErgebnisCode(code);
            Assert.False(string.IsNullOrWhiteSpace(text));
        }
    }

    [Theory]
    [InlineData(1, "Installation")]
    [InlineData(2, "Deinstallation")]
    [InlineData(99, "Unbekannt")]
    public void DeuteOperation_DeutetBekannteWerte(int operation, string erwartet)
    {
        Assert.Equal(erwartet, WindowsUpdateDeutung.DeuteOperation(operation));
    }
}
