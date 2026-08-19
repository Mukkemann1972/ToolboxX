using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class VramThermalAuswertungTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void Auswerten_KeineTemperaturdaten_LiefertKeineSpitzeUndKeineAussage()
    {
        var ergebnis = VramThermalAuswertung.Auswerten([T0], []);

        Assert.Null(ergebnis.SpitzentemperaturCelsius);
        Assert.False(ergebnis.AlleFehlerBeiHoherTemperatur);
        Assert.False(ergebnis.EinzelneFehlerBeiHoherTemperatur);
    }

    [Fact]
    public void Auswerten_FehlerGenauBeiHoherTemperatur_AlleAlsThermischErkannt()
    {
        var temperaturen = new[]
        {
            new TemperaturMesspunkt(T0, 60),
            new TemperaturMesspunkt(T0.AddSeconds(30), 90),
            new TemperaturMesspunkt(T0.AddSeconds(60), 55),
        };
        var fehler = new[] { T0.AddSeconds(31) };

        var ergebnis = VramThermalAuswertung.Auswerten(fehler, temperaturen);

        Assert.Equal(90, ergebnis.SpitzentemperaturCelsius);
        Assert.True(ergebnis.AlleFehlerBeiHoherTemperatur);
        Assert.False(ergebnis.EinzelneFehlerBeiHoherTemperatur);
    }

    [Fact]
    public void Auswerten_FehlerBeiNormalerTemperatur_KeinThermischerVerdacht()
    {
        var temperaturen = new[]
        {
            new TemperaturMesspunkt(T0, 55),
            new TemperaturMesspunkt(T0.AddSeconds(30), 58),
        };
        var fehler = new[] { T0.AddSeconds(31) };

        var ergebnis = VramThermalAuswertung.Auswerten(fehler, temperaturen);

        Assert.Equal(58, ergebnis.SpitzentemperaturCelsius);
        Assert.False(ergebnis.AlleFehlerBeiHoherTemperatur);
        Assert.False(ergebnis.EinzelneFehlerBeiHoherTemperatur);
    }

    [Fact]
    public void Auswerten_NurEinTeilDerFehlerBeiHoherTemperatur_AlsEinzelfaelleErkannt()
    {
        var temperaturen = new[]
        {
            new TemperaturMesspunkt(T0, 55),
            new TemperaturMesspunkt(T0.AddSeconds(30), 90),
        };
        var fehler = new[] { T0.AddSeconds(1), T0.AddSeconds(31) };

        var ergebnis = VramThermalAuswertung.Auswerten(fehler, temperaturen);

        Assert.Equal(1, ergebnis.AnzahlFehlerBeiHoherTemperatur);
        Assert.Equal(2, ergebnis.AnzahlFehlerGesamt);
        Assert.False(ergebnis.AlleFehlerBeiHoherTemperatur);
        Assert.True(ergebnis.EinzelneFehlerBeiHoherTemperatur);
    }

    [Fact]
    public void Auswerten_FehlerZeitpunktZuWeitVonMessungEntfernt_ZaehltNichtAlsThermisch()
    {
        var temperaturen = new[] { new TemperaturMesspunkt(T0, 95) };
        var fehler = new[] { T0.AddMinutes(5) }; // weit ausserhalb des Toleranzfensters

        var ergebnis = VramThermalAuswertung.Auswerten(fehler, temperaturen);

        Assert.Equal(0, ergebnis.AnzahlFehlerBeiHoherTemperatur);
    }

    [Fact]
    public void Auswerten_KeineFehler_LiefertTrotzdemSpitzentemperatur()
    {
        var temperaturen = new[] { new TemperaturMesspunkt(T0, 70), new TemperaturMesspunkt(T0.AddSeconds(10), 78) };

        var ergebnis = VramThermalAuswertung.Auswerten([], temperaturen);

        Assert.Equal(78, ergebnis.SpitzentemperaturCelsius);
        Assert.Equal(0, ergebnis.AnzahlFehlerGesamt);
        Assert.False(ergebnis.AlleFehlerBeiHoherTemperatur);
    }
}
