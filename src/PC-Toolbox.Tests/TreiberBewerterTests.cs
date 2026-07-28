using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class TreiberBewerterTests
{
    private static readonly DateTime Heute = new(2026, 7, 26);

    [Fact]
    public void KeinFehler_UndAktuellerTreiber_IstGruen()
    {
        var geraet = new GeraetBefund { Name = "Realtek Audio", TreiberDatum = new DateTime(2024, 3, 1) };

        var bewertung = TreiberBewerter.Bewerten(geraet, Heute);

        Assert.Equal(AmpelStatus.Gruen, bewertung.Ampel);
    }

    [Fact]
    public void FehlenderTreiber_Code28_IstRotUndErklaertWasZuTunIst()
    {
        var geraet = new GeraetBefund { Name = "Unbekanntes Gerät", FehlerCode = 28 };

        var bewertung = TreiberBewerter.Bewerten(geraet, Heute);

        Assert.Equal(AmpelStatus.Rot, bewertung.Ampel);
        Assert.Contains("Hersteller", bewertung.Empfehlung);
    }

    [Theory]
    [InlineData(22)] // vom Nutzer abgeschaltet
    [InlineData(24)] // gerade nicht angesteckt
    [InlineData(45)] // Karteileiche
    public void GewollteZustaende_SindGelbNichtRot(int code)
    {
        // Ein absichtlich abgeschaltetes Geraet als roten Fehler zu melden, waere
        // genau die Panikmache, die wir anderen Tools vorwerfen.
        var bewertung = TreiberBewerter.Bewerten(new GeraetBefund { FehlerCode = code }, Heute);

        Assert.Equal(AmpelStatus.Gelb, bewertung.Ampel);
    }

    [Fact]
    public void AlterTreiberOhneFehler_IstNurEinHinweisNiemalsRot()
    {
        var geraet = new GeraetBefund { Name = "Alte Tastatur", TreiberDatum = new DateTime(2015, 1, 1) };

        var bewertung = TreiberBewerter.Bewerten(geraet, Heute);

        Assert.Equal(AmpelStatus.Gelb, bewertung.Ampel);
        Assert.Contains("Kein Handlungsbedarf", bewertung.Empfehlung);
    }

    [Fact]
    public void SiebenJahreAlterTreiber_IstNochKeinHinweisWert()
    {
        // Bewusst grosszuegige Grenze: "alt" ist bei funktionierender Hardware normal.
        var geraet = new GeraetBefund { TreiberDatum = new DateTime(2019, 8, 1) };

        Assert.Equal(AmpelStatus.Gruen, TreiberBewerter.Bewerten(geraet, Heute).Ampel);
    }

    [Fact]
    public void UnbekannterFehlerCode_BekommtEhrlicheStandardantwort()
    {
        var bewertung = TreiberBewerter.Bewerten(new GeraetBefund { FehlerCode = 9999 }, Heute);

        Assert.Equal(AmpelStatus.Rot, bewertung.Ampel);
        Assert.Contains("9999", bewertung.Kurzform);
        Assert.Contains("neu starten", bewertung.Empfehlung);
    }

    [Fact]
    public void AlleBekanntenCodes_HabenEineEchteHandlungsanweisung()
    {
        foreach (var code in new[] { 1, 3, 10, 12, 14, 18, 19, 21, 22, 24, 28, 29, 31, 32, 33, 37, 38, 39, 43, 45, 48, 52 })
        {
            Assert.True(GeraeteFehlerCodes.IstBekannt(code), $"Code {code} fehlt in der Tabelle.");

            var erklaerung = GeraeteFehlerCodes.Erklaeren(code);
            Assert.False(string.IsNullOrWhiteSpace(erklaerung.Kurzform));
            Assert.True(erklaerung.Empfehlung.Length > 40, $"Code {code} erklärt zu wenig.");
        }
    }

    [Fact]
    public void Zusammenfassung_NennntDieAnzahlDerEchtenProbleme()
    {
        Assert.Equal("120 Geräte geprüft - Windows meldet bei keinem ein Problem.",
            TreiberBewerter.Zusammenfassung(120, 0, 0));

        Assert.Equal("120 Geräte geprüft - bei 1 Gerät meldet Windows ein Problem.",
            TreiberBewerter.Zusammenfassung(120, 1, 3));
    }

    [Theory]
    [InlineData("20210513000000.000000+000", 2021, 5, 13)]
    [InlineData("20160101000000.******+***", 2016, 1, 1)] // Treiberdaten kommen oft so
    public void WmiDatum_WirdRichtigGelesen(string wmiWert, int jahr, int monat, int tag)
    {
        Assert.Equal(new DateTime(jahr, monat, tag), WmiDatum.Parse(wmiWert));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("keine Ahnung")]
    [InlineData("2021")]
    public void WmiDatum_UnbrauchbareWerte_GebenNullStattAbsturz(string? wert)
    {
        Assert.Null(WmiDatum.Parse(wert));
    }
}
