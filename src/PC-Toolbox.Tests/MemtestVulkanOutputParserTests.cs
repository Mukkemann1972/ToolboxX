using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class MemtestVulkanOutputParserTests
{
    [Fact]
    public void Parse_MehrereFehlerfreieIterationen_ZaehltUndSummiertKorrekt()
    {
        var zeilen = new[]
        {
            "https://github.com/GpuZelenograd/memtest_vulkan v0.5.0 by GpuZelenograd",
            "1: Bus=0x00:00 DevId=0x9A49   8GB Intel(R) Iris(R) Xe Graphics",
            "Testing 1: Bus=0x00:00 DevId=0x9A49   8GB Intel(R) Iris(R) Xe Graphics",
            "      1 iteration. Since last report passed 271.3561ms      written     1.8GB, read:     3.5GB     19.3GB/sec",
            "      2 iteration. Since last report passed 250.1000ms      written     2.0GB, read:     3.7GB     20.1GB/sec",
        };

        var ergebnis = MemtestVulkanOutputParser.Parse(zeilen);

        Assert.Equal(2, ergebnis.IterationenAbgeschlossen);
        Assert.Equal(3.8, ergebnis.GbGeschriebenGesamt, 2);
        Assert.Equal(7.2, ergebnis.GbGelesenGesamt, 2);
        Assert.Equal(20.1, ergebnis.LetzterDurchsatzGbProSek);
        Assert.Equal(0, ergebnis.FehlerAnzahl);
        Assert.False(ergebnis.Abgestuerzt);
        Assert.False(ergebnis.ToolMeldetExplizitBestanden);
    }

    [Fact]
    public void Parse_FehlerZeile_ErkanntUndDetailGespeichert()
    {
        var zeilen = new[]
        {
            "      1 iteration. Since last report passed 271.3561ms      written     1.8GB, read:     3.5GB     19.3GB/sec",
            "Error found. Mode NEXT_RE_READ, total errors 0x3C7EC3 out of 0x3C000000 (0.39384872%)",
        };

        var ergebnis = MemtestVulkanOutputParser.Parse(zeilen);

        Assert.Equal(1, ergebnis.FehlerAnzahl);
        Assert.Single(ergebnis.FehlerDetails);
        Assert.Contains("NEXT_RE_READ", ergebnis.FehlerDetails[0]);
        Assert.Equal(1, ergebnis.IterationenAbgeschlossen); // Iteration zaehlt trotzdem, Fehler kam danach
    }

    [Fact]
    public void ZeileVerarbeiten_MitZeitpunkt_LandetInFehlerZeitpunkte()
    {
        var ergebnis = new PCToolboxCore.Models.VramTestParseResult();
        var zeitpunkt = new DateTime(2026, 1, 1, 12, 0, 0);

        MemtestVulkanOutputParser.ZeileVerarbeiten(
            "Error found. Mode NEXT_RE_READ, total errors 0x1 out of 0x3C000000 (0.0000027%)", ergebnis, zeitpunkt);

        Assert.Single(ergebnis.FehlerZeitpunkte);
        Assert.Equal(zeitpunkt, ergebnis.FehlerZeitpunkte[0]);
    }

    [Fact]
    public void ZeileVerarbeiten_OhneZeitpunkt_FehlerZeitpunkteBleibtLeer()
    {
        var ergebnis = new PCToolboxCore.Models.VramTestParseResult();

        MemtestVulkanOutputParser.ZeileVerarbeiten(
            "Error found. Mode NEXT_RE_READ, total errors 0x1 out of 0x3C000000 (0.0000027%)", ergebnis);

        Assert.Empty(ergebnis.FehlerZeitpunkte);
    }

    [Fact]
    public void Parse_MehrereFehler_ZaehltJedenEinzeln()
    {
        var zeilen = new[]
        {
            "Error found. Mode NEXT_RE_READ, total errors 0x1 out of 0x3C000000 (0.0000027%)",
            "Error found. Mode NEXT_RE_READ, total errors 0x2 out of 0x3C000000 (0.0000053%)",
        };

        var ergebnis = MemtestVulkanOutputParser.Parse(zeilen);

        Assert.Equal(2, ergebnis.FehlerAnzahl);
        Assert.Equal(2, ergebnis.FehlerDetails.Count);
    }

    [Fact]
    public void Parse_RuntimeError_MarkiertAlsAbgestuerzt()
    {
        var zeilen = new[]
        {
            "      1 iteration. Since last report passed 271.3561ms      written     1.8GB, read:     3.5GB     19.3GB/sec",
            "Runtime error: ERROR_DEVICE_LOST while getting () in context wait_for_fences",
        };

        var ergebnis = MemtestVulkanOutputParser.Parse(zeilen);

        Assert.True(ergebnis.Abgestuerzt);
        Assert.Contains("ERROR_DEVICE_LOST", ergebnis.AbsturzMeldung);
    }

    [Fact]
    public void Parse_ExpliziteBestanden_Meldung_WirdErkannt()
    {
        var zeilen = new[]
        {
            "      1 iteration. Since last report passed 271.3561ms      written     1.8GB, read:     3.5GB     19.3GB/sec",
            "memtest_vulkan: no any errors, testing PASSed.",
            "  press any key to continue...",
        };

        var ergebnis = MemtestVulkanOutputParser.Parse(zeilen);

        Assert.True(ergebnis.ToolMeldetExplizitBestanden);
        Assert.Equal(0, ergebnis.FehlerAnzahl);
    }

    [Fact]
    public void Parse_LeereOderUnbekannteZeilen_WerdenIgnoriert()
    {
        var zeilen = new[]
        {
            "",
            "   ",
            "https://github.com/GpuZelenograd/memtest_vulkan v0.5.0 by GpuZelenograd",
            "To finish testing use Ctrl+C",
        };

        var ergebnis = MemtestVulkanOutputParser.Parse(zeilen);

        Assert.Equal(0, ergebnis.IterationenAbgeschlossen);
        Assert.Equal(0, ergebnis.FehlerAnzahl);
        Assert.False(ergebnis.Abgestuerzt);
    }

    [Fact]
    public void ZeileVerarbeiten_InkrementellAufrufbar_GleichesErgebnisWieParse()
    {
        var ergebnis = new PCToolboxCore.Models.VramTestParseResult();
        MemtestVulkanOutputParser.ZeileVerarbeiten(
            "      1 iteration. Since last report passed 271.3561ms      written     1.8GB, read:     3.5GB     19.3GB/sec",
            ergebnis);
        MemtestVulkanOutputParser.ZeileVerarbeiten(
            "Error found. Mode NEXT_RE_READ, total errors 0x1 out of 0x3C000000 (0.0000027%)",
            ergebnis);

        Assert.Equal(1, ergebnis.IterationenAbgeschlossen);
        Assert.Equal(1, ergebnis.FehlerAnzahl);
    }
}
