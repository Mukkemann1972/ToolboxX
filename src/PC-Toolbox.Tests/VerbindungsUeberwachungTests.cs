using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class VerbindungsUeberwachungTests
{
    private static DateTime T(int sekunden) => new DateTime(2026, 7, 28, 12, 0, 0).AddSeconds(sekunden);

    [Fact]
    public void Auswerten_LeereListeGibtLeereAuswertung()
    {
        var ergebnis = VerbindungsUeberwachung.Auswerten([]);

        Assert.Equal(0, ergebnis.AnzahlMessungen);
        Assert.Equal(0, ergebnis.AnzahlAusfaelle);
        Assert.Empty(ergebnis.AusfallPerioden);
    }

    [Fact]
    public void Auswerten_NurErfolgreicheMessungenKeineAusfallperiode()
    {
        var messungen = new[]
        {
            new VerbindungsMessung(T(0), true),
            new VerbindungsMessung(T(5), true),
            new VerbindungsMessung(T(10), true),
        };

        var ergebnis = VerbindungsUeberwachung.Auswerten(messungen);

        Assert.Equal(3, ergebnis.AnzahlMessungen);
        Assert.Equal(0, ergebnis.AnzahlAusfaelle);
        Assert.Empty(ergebnis.AusfallPerioden);
    }

    [Fact]
    public void Auswerten_ErkenntEinzelneAbgeschlosseneAusfallperiode()
    {
        var messungen = new[]
        {
            new VerbindungsMessung(T(0), true),
            new VerbindungsMessung(T(5), false),
            new VerbindungsMessung(T(10), false),
            new VerbindungsMessung(T(15), true),
        };

        var ergebnis = VerbindungsUeberwachung.Auswerten(messungen);

        Assert.Equal(2, ergebnis.AnzahlAusfaelle);
        var periode = Assert.Single(ergebnis.AusfallPerioden);
        Assert.Equal(T(5), periode.Start);
        Assert.Equal(T(15), periode.Ende);
        Assert.Equal(TimeSpan.FromSeconds(10), periode.Dauer);
    }

    [Fact]
    public void Auswerten_ErkenntMehrereGetrennteAusfallperioden()
    {
        var messungen = new[]
        {
            new VerbindungsMessung(T(0), true),
            new VerbindungsMessung(T(5), false),
            new VerbindungsMessung(T(10), true),
            new VerbindungsMessung(T(15), true),
            new VerbindungsMessung(T(20), false),
            new VerbindungsMessung(T(25), false),
            new VerbindungsMessung(T(30), true),
        };

        var ergebnis = VerbindungsUeberwachung.Auswerten(messungen);

        Assert.Equal(2, ergebnis.AusfallPerioden.Count);
        Assert.Equal(T(5), ergebnis.AusfallPerioden[0].Start);
        Assert.Equal(T(10), ergebnis.AusfallPerioden[0].Ende);
        Assert.Equal(T(20), ergebnis.AusfallPerioden[1].Start);
        Assert.Equal(T(30), ergebnis.AusfallPerioden[1].Ende);
    }

    [Fact]
    public void Auswerten_NochLaufenderAusfallHatKeinEnde()
    {
        var messungen = new[]
        {
            new VerbindungsMessung(T(0), true),
            new VerbindungsMessung(T(5), false),
            new VerbindungsMessung(T(10), false),
        };

        var ergebnis = VerbindungsUeberwachung.Auswerten(messungen);

        var periode = Assert.Single(ergebnis.AusfallPerioden);
        Assert.Equal(T(5), periode.Start);
        Assert.Null(periode.Ende);
        Assert.Null(periode.Dauer);
    }

    [Fact]
    public void Auswerten_SortiertMessungenVorDerAuswertung()
    {
        // Messungen absichtlich nicht in zeitlicher Reihenfolge uebergeben.
        var messungen = new[]
        {
            new VerbindungsMessung(T(10), true),
            new VerbindungsMessung(T(0), true),
            new VerbindungsMessung(T(5), false),
        };

        var ergebnis = VerbindungsUeberwachung.Auswerten(messungen);

        var periode = Assert.Single(ergebnis.AusfallPerioden);
        Assert.Equal(T(5), periode.Start);
        Assert.Equal(T(10), periode.Ende);
    }
}
