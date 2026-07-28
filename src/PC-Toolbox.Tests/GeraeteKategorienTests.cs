using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class GeraeteKategorienTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Klartext_FehlendeKlasseWirdSonstige(string? klasse)
    {
        Assert.Equal("Sonstige", GeraeteKategorien.Klartext(klasse));
    }

    [Theory]
    [InlineData("Net", "Netzwerk")]
    [InlineData("NET", "Netzwerk")]
    [InlineData("usb", "USB")]
    [InlineData("HIDClass", "Eingabegeräte (Tastatur/Maus)")]
    [InlineData("Bluetooth", "Bluetooth")]
    public void Klartext_UebersetztBekannteKlassenUnabhaengigVonGrossKleinschreibung(string klasse, string erwartet)
    {
        Assert.Equal(erwartet, GeraeteKategorien.Klartext(klasse));
    }

    [Fact]
    public void Klartext_UnbekannteKlasseKommtUnveraendertDurch()
    {
        Assert.Equal("EinePNPKlasseDieWirNichtKennen", GeraeteKategorien.Klartext("EinePNPKlasseDieWirNichtKennen"));
    }
}
