using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class WlanSignalDeutungTests
{
    [Fact]
    public void ProzentAusNetshAusgabe_LiestDeutscheAusgabe()
    {
        var ausgabe = """
            Name                   : WLAN
            Beschreibung            : Intel(R) Wi-Fi 6 AX200 160MHz
            Status                  : verbunden
            Signal                 : 87%
            """;

        Assert.Equal(87, WlanSignalDeutung.ProzentAusNetshAusgabe(ausgabe));
    }

    [Fact]
    public void ProzentAusNetshAusgabe_GibtNullZurueckWennGetrennt()
    {
        var ausgabe = """
            Name                   : WLAN
            Status                  : getrennt
            Funkstatus           : Hardware Aktiviert
            """;

        Assert.Null(WlanSignalDeutung.ProzentAusNetshAusgabe(ausgabe));
    }

    [Fact]
    public void ProzentAusNetshAusgabe_GibtNullBeiLeererAusgabeZurueck()
    {
        Assert.Null(WlanSignalDeutung.ProzentAusNetshAusgabe(""));
    }

    [Theory]
    [InlineData(90, AmpelStatus.Gruen)]
    [InlineData(70, AmpelStatus.Gruen)]
    [InlineData(69, AmpelStatus.Gelb)]
    [InlineData(40, AmpelStatus.Gelb)]
    [InlineData(39, AmpelStatus.Rot)]
    [InlineData(0, AmpelStatus.Rot)]
    public void Deuten_LiefertErwarteteAmpelGrenzwerte(int prozent, AmpelStatus erwartet)
    {
        var (_, status) = WlanSignalDeutung.Deuten(prozent);
        Assert.Equal(erwartet, status);
    }
}
