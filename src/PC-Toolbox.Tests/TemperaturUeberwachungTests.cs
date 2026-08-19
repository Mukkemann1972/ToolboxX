using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class TemperaturUeberwachungTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void Auswerten_KeineUeberschreitung_KeinePerioden()
    {
        var messungen = new[]
        {
            new TemperaturMessung(T0, "CPU", 50),
            new TemperaturMessung(T0.AddSeconds(5), "CPU", 55),
        };

        var auswertung = TemperaturUeberwachung.Auswerten(messungen, grenzwertCelsius: 80);

        Assert.Empty(auswertung.UeberschreitungsPerioden);
        Assert.Equal(55, auswertung.HoechstwertJeBauteil["CPU"]);
    }

    [Fact]
    public void Auswerten_KurzeUeberschreitung_WirdAlsAbgeschlossenePeriodeErkannt()
    {
        var messungen = new[]
        {
            new TemperaturMessung(T0, "CPU", 70),
            new TemperaturMessung(T0.AddSeconds(5), "CPU", 90),
            new TemperaturMessung(T0.AddSeconds(10), "CPU", 95),
            new TemperaturMessung(T0.AddSeconds(15), "CPU", 60),
        };

        var auswertung = TemperaturUeberwachung.Auswerten(messungen, grenzwertCelsius: 80);

        var periode = Assert.Single(auswertung.UeberschreitungsPerioden);
        Assert.Equal("CPU", periode.Bauteil);
        Assert.Equal(T0.AddSeconds(5), periode.Start);
        Assert.Equal(T0.AddSeconds(15), periode.Ende);
        Assert.Equal(95, periode.SpitzenwertCelsius);
        Assert.Equal(TimeSpan.FromSeconds(10), periode.Dauer);
    }

    [Fact]
    public void Auswerten_NochLaufendeUeberschreitung_HatKeinEnde()
    {
        var messungen = new[]
        {
            new TemperaturMessung(T0, "GPU", 60),
            new TemperaturMessung(T0.AddSeconds(5), "GPU", 90),
        };

        var auswertung = TemperaturUeberwachung.Auswerten(messungen, grenzwertCelsius: 80);

        var periode = Assert.Single(auswertung.UeberschreitungsPerioden);
        Assert.Null(periode.Ende);
        Assert.Null(periode.Dauer);
    }

    [Fact]
    public void Auswerten_MehrereBauteileGleichzeitig_WerdenGetrenntAusgewertet()
    {
        var messungen = new[]
        {
            new TemperaturMessung(T0, "CPU", 90),
            new TemperaturMessung(T0, "GPU", 40),
            new TemperaturMessung(T0.AddSeconds(5), "CPU", 60),
            new TemperaturMessung(T0.AddSeconds(5), "GPU", 40),
        };

        var auswertung = TemperaturUeberwachung.Auswerten(messungen, grenzwertCelsius: 80);

        Assert.Single(auswertung.UeberschreitungsPerioden);
        Assert.Equal("CPU", auswertung.UeberschreitungsPerioden[0].Bauteil);
        Assert.Equal(90, auswertung.HoechstwertJeBauteil["CPU"]);
        Assert.Equal(40, auswertung.HoechstwertJeBauteil["GPU"]);
    }

    [Fact]
    public void Auswerten_FehlendeMessungenWerdenIgnoriert()
    {
        var messungen = new[]
        {
            new TemperaturMessung(T0, "Laufwerk C:", null),
            new TemperaturMessung(T0.AddSeconds(5), "Laufwerk C:", 45),
        };

        var auswertung = TemperaturUeberwachung.Auswerten(messungen, grenzwertCelsius: 80);

        Assert.Equal(2, auswertung.AnzahlMessungen);
        Assert.Equal(45, auswertung.HoechstwertJeBauteil["Laufwerk C:"]);
    }
}
