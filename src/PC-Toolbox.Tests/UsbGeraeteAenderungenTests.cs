using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class UsbGeraeteAenderungenTests
{
    [Theory]
    [InlineData(@"USB\VID_1234&PID_5678\7&1234567&0&1", true)]
    [InlineData(@"usb\vid_1234&pid_5678\1", true)]
    [InlineData(@"HID\VID_1234&PID_5678\1", false)]
    [InlineData(@"ACPI\PNP0303\0", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IstUsbGeraet_ErkenntUsbBusKorrekt(string? pnpDeviceId, bool erwartet)
    {
        Assert.Equal(erwartet, UsbGeraeteAenderungen.IstUsbGeraet(pnpDeviceId));
    }

    [Fact]
    public void Anzeige_ZeigtNamenUndPnpIdZusammen_WennNameVorhanden()
    {
        var pnpId = @"USB\VID_1234&PID_5678\1";
        var aenderung = new GeraeteAenderung(DateTime.Now, GeraeteAenderungsArt.Erschienen, pnpId, "Mein Geraet");
        Assert.Contains("Mein Geraet", aenderung.Anzeige);
        Assert.Contains(pnpId, aenderung.Anzeige);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Anzeige_FaelltAufPnpIdZurueck_WennKeinName(string? name)
    {
        var pnpId = @"USB\VID_1234&PID_5678\1";
        var aenderung = new GeraeteAenderung(DateTime.Now, GeraeteAenderungsArt.Verschwunden, pnpId, name);
        Assert.Equal(pnpId, aenderung.Anzeige);
    }

    [Fact]
    public void ArtText_IstFuerJedeArtNieLeer()
    {
        foreach (GeraeteAenderungsArt art in Enum.GetValues<GeraeteAenderungsArt>())
        {
            Assert.False(string.IsNullOrWhiteSpace(UsbGeraeteAenderungen.ArtText(art)));
        }
    }
}
