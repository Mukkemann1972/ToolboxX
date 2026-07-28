using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class HardwareInfoTests
{
    [Theory]
    [InlineData(24, "DDR3")]
    [InlineData(26, "DDR4")]
    [InlineData(34, "DDR5")]
    [InlineData(0, "unbekannt")]
    public void SpeicherTyp_WirdAusDemSmbiosCodeUebersetzt(int code, string erwartet)
    {
        Assert.Equal(erwartet, HardwareKlartext.SpeicherTyp(code));
    }

    [Fact]
    public void SpeicherTyp_UnbekannterCode_WirdEhrlichAlsCodeAngezeigt()
    {
        // Lieber "Typ-Code 99" als eine erfundene Bezeichnung.
        Assert.Equal("Typ-Code 99", HardwareKlartext.SpeicherTyp(99));
    }

    [Theory]
    [InlineData(8, "DIMM (Desktop)")]
    [InlineData(12, "SO-DIMM (Notebook)")]
    [InlineData(99, "")]
    public void Bauform_WirdUebersetztOderWeggelassen(int code, string erwartet)
    {
        Assert.Equal(erwartet, HardwareKlartext.Bauform(code));
    }

    [Fact]
    public void LaufwerksArt_ErkenntSsdAmModellnamen()
    {
        // Windows meldet fuer JEDE Platte "Fixed hard disk media" - auch fuer SSDs.
        // Der Modellname verraet mehr als die offizielle Angabe.
        Assert.Equal("SSD", HardwareKlartext.LaufwerksArt("Fixed hard disk media", "SCSI", "Samsung SSD 870 EVO"));
        Assert.Contains("NVMe", HardwareKlartext.LaufwerksArt("Fixed hard disk media", "SCSI", "WD BLACK SN850X NVMe"));
        Assert.Equal("Festplatte oder SSD", HardwareKlartext.LaufwerksArt("Fixed hard disk media", "IDE", "ST2000DM008"));
    }

    [Fact]
    public void LaufwerksArt_UsbWirdAlsExternErkannt()
    {
        Assert.Contains("USB", HardwareKlartext.LaufwerksArt("Removable Media", "USB", "Generic Flash Disk"));
    }

    [Theory]
    [InlineData("NVIDIA", "NVIDIA")]
    [InlineData("Samsung Electronics", "Samsung")]
    [InlineData("WDC WD40EFRX", "Western Digital / SanDisk")]
    [InlineData("802C", "Crucial / Micron")] // RAM meldet sich oft nur als Code
    public void HerstellerLinks_FindetDieOffizielleSeite(string eingabe, string erwarteteAnzeige)
    {
        var link = HerstellerLinks.Finde(eingabe);

        Assert.NotNull(link);
        Assert.Equal(erwarteteAnzeige, link!.Value.Anzeige);
        Assert.StartsWith("https://", link.Value.Adresse);
    }

    [Fact]
    public void HerstellerLinks_UnbekannterHersteller_GibtKeinenLink()
    {
        // Ein falscher Link waere schlimmer als gar keiner.
        Assert.Null(HerstellerLinks.Finde("Irgendeine Klitsche GmbH"));
        Assert.Null(HerstellerLinks.Finde(""));
        Assert.Null(HerstellerLinks.Finde(null));
    }

    [Fact]
    public void PartitionGrenzen_WerdenAusDerPowershellAntwortGelesen()
    {
        var json = """{"SizeMin":52428800,"SizeMax":1000204886016}""";

        var grenzen = PartitionGrenzen.Lesen(json);

        Assert.NotNull(grenzen);
        Assert.Equal(52428800, grenzen!.Value.Min);
        Assert.Equal(1000204886016, grenzen.Value.Max);
    }

    [Fact]
    public void PartitionGrenzen_AlsFeld_NimmtDenErstenEintrag()
    {
        var json = """[{"SizeMin":1000,"SizeMax":2000},{"SizeMin":3000,"SizeMax":4000}]""";

        Assert.Equal((1000, 2000), PartitionGrenzen.Lesen(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein JSON")]
    [InlineData("[]")]
    [InlineData("""{"Etwas":1}""")]
    public void PartitionGrenzen_UnbrauchbareAntwort_GibtNullStattAbsturz(string? json)
    {
        Assert.Null(PartitionGrenzen.Lesen(json));
    }
}
