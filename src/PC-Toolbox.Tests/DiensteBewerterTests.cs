using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DiensteBewerterTests
{
    private static DienstInfo Dienst(string name, string starttyp = "Auto") =>
        new() { Name = name, Anzeigename = name, Starttyp = starttyp };

    [Theory]
    [InlineData("RpcSs")]
    [InlineData("Winmgmt")]
    [InlineData("EventLog")]
    [InlineData("WlanSvc")]
    [InlineData("Spooler")]
    public void KritischeDienste_SindGeschuetztUndNichtAenderbar(string name)
    {
        var bewertung = DiensteBewerter.Bewerten(Dienst(name));

        Assert.True(bewertung.Geschuetzt);
        Assert.Equal(AmpelStatus.Gruen, bewertung.Ampel);
    }

    [Fact]
    public void TreiberNaheDienste_SindImmerGeschuetzt_AuchWennUnbekannt()
    {
        // Boot/System = Treiber-Ebene. Daran darf nie jemand drehen, egal wie der Dienst heisst.
        var bewertung = DiensteBewerter.Bewerten(Dienst("IrgendeinTreiber", "Boot"));

        Assert.True(bewertung.Geschuetzt);
    }

    [Fact]
    public void Telemetriedienst_AufAutomatisch_IstGelbUndWirdEhrlichBeschrieben()
    {
        var bewertung = DiensteBewerter.Bewerten(Dienst("DiagTrack"));

        Assert.False(bewertung.Geschuetzt);
        Assert.Equal(AmpelStatus.Gelb, bewertung.Ampel);
        // Keine falschen Versprechen ueber Geschwindigkeit.
        Assert.Contains("schneller", bewertung.Empfehlung);
    }

    [Fact]
    public void BereitsAufBedarfGestellt_IstGruenUndKeinHandlungsbedarf()
    {
        var bewertung = DiensteBewerter.Bewerten(Dienst("DiagTrack", "Manual"));

        Assert.Equal(AmpelStatus.Gruen, bewertung.Ampel);
        Assert.Contains("alles gut", bewertung.Kurzurteil);
    }

    [Fact]
    public void UnbekannterDienst_WirdInRuheGelassenUndErklaert()
    {
        var bewertung = DiensteBewerter.Bewerten(Dienst("HerstellerHilfsdienst"));

        Assert.False(bewertung.Geschuetzt);
        Assert.Equal(AmpelStatus.Gruen, bewertung.Ampel);
        // Kernaussage: im Zweifel "Manuell" statt "Abschalten".
        Assert.Contains("Manuell", bewertung.Empfehlung);
    }

    [Fact]
    public void SysMain_WarntVorDemAbschaltenBeiKlassischerFestplatte()
    {
        var bewertung = DiensteBewerter.Bewerten(Dienst("SysMain"));

        Assert.Contains("Festplatte", bewertung.Empfehlung);
    }

    // ================= Befehle =================

    [Theory]
    [InlineData("Auto", "auto")]
    [InlineData("Manual", "demand")]
    [InlineData("Disabled", "disabled")]
    public void StarttypSetzen_UebersetztInDieScSchreibweise(string starttyp, string erwartet)
    {
        var befehl = DiensteBefehle.StarttypSetzen("DiagTrack", starttyp);

        // Das Leerzeichen NACH dem Gleichheitszeichen ist Pflicht - sonst tut sc.exe nichts.
        Assert.Equal($"config \"DiagTrack\" start= {erwartet}", befehl);
    }

    [Fact]
    public void StarttypSetzen_UnbekannterWert_WirdAbgelehnt()
    {
        Assert.Throws<ArgumentException>(() => DiensteBefehle.StarttypSetzen("DiagTrack", "vielleicht"));
    }

    [Fact]
    public void Stoppen_SetztDenNamenInAnfuehrungszeichen()
    {
        Assert.Equal("stop \"Windows Update\"", DiensteBefehle.Stoppen("Windows Update"));
    }

    [Fact]
    public void Zusammenfassung_ZaehltNurEchteVorschlaege()
    {
        Assert.Equal("250 Dienste geprüft - bei 3 Diensten lohnt ein Blick.",
            DiensteBewerter.Zusammenfassung(250, 3));

        Assert.Equal("250 Dienste geprüft - es gibt nichts, was wir dir zum Umstellen vorschlagen würden.",
            DiensteBewerter.Zusammenfassung(250, 0));
    }
}
