using PCToolbox.Models;
using PCToolboxCore;
using PCToolboxCore.Models;
using Xunit;

namespace PCToolboxCore.Tests;

public class AutostartBewerterTests
{
    private static AutostartEintrag Eintrag(string dateiName, string pfad = @"C:\Program Files\Test\test.exe") =>
        new()
        {
            Name = dateiName,
            DateiName = dateiName,
            ProgrammPfad = pfad,
            Befehl = $"\"{pfad}\""
        };

    [Fact]
    public void Bewerten_BekanntesSystemprogramm_IstGruenUndSagtAnlassen()
    {
        var bewertung = AutostartBewerter.Bewerten(Eintrag("securityhealthsystray"));

        Assert.Equal(AmpelStatus.Gruen, bewertung.Ampel);
        Assert.Equal(AutostartKategorie.Systemnah, bewertung.Kategorie);
        Assert.Contains("anlassen", bewertung.Kurzurteil, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bewerten_BekannteBremse_IstRot()
    {
        var bewertung = AutostartBewerter.Bewerten(Eintrag("jusched"));

        Assert.Equal(AmpelStatus.Rot, bewertung.Ampel);
        Assert.Equal(AutostartKategorie.Bremse, bewertung.Kategorie);
    }

    [Fact]
    public void Bewerten_SpieleLauncher_IstGelbNichtRot()
    {
        // Bewusst gelb: Ein Launcher ist kein Schaedling, nur Bequemlichkeit -
        // die Entscheidung gehoert dem Nutzer, nicht dem Tool.
        var bewertung = AutostartBewerter.Bewerten(Eintrag("steam"));

        Assert.Equal(AmpelStatus.Gelb, bewertung.Ampel);
        Assert.Equal(AutostartKategorie.Optional, bewertung.Kategorie);
    }

    [Fact]
    public void Bewerten_FehlendeDatei_IstKarteileicheUndNichtVerdaechtig()
    {
        var eintrag = Eintrag("altesprogramm");
        eintrag.DateiFehlt = true;

        var bewertung = AutostartBewerter.Bewerten(eintrag);

        Assert.Equal(AutostartKategorie.Karteileiche, bewertung.Kategorie);
        Assert.Equal(AmpelStatus.Gelb, bewertung.Ampel);
    }

    [Fact]
    public void Bewerten_StartAusTempOrdner_IstVerdaechtig()
    {
        var eintrag = Eintrag("kjhsdf", @"C:\Users\Alex\AppData\Local\Temp\kjhsdf.exe");

        var bewertung = AutostartBewerter.Bewerten(eintrag);

        Assert.Equal(AutostartKategorie.Verdaechtig, bewertung.Kategorie);
        Assert.Equal(AmpelStatus.Rot, bewertung.Ampel);
    }

    [Fact]
    public void Bewerten_UpdaterAusTempOrdner_WirdAlsVerdaechtigEingestuft()
    {
        // Der ungewoehnliche Ort wiegt schwerer als "ist nur ein Updater".
        var eintrag = Eintrag("superupdater", @"C:\Users\Alex\Downloads\superupdater.exe");

        var bewertung = AutostartBewerter.Bewerten(eintrag);

        Assert.Equal(AutostartKategorie.Verdaechtig, bewertung.Kategorie);
    }

    [Fact]
    public void Bewerten_UnbekannterUpdater_IstRoteBremse()
    {
        var bewertung = AutostartBewerter.Bewerten(
            Eintrag("irgendwasupdate", @"C:\Program Files\Irgendwas\irgendwasupdate.exe"));

        Assert.Equal(AutostartKategorie.Bremse, bewertung.Kategorie);
        Assert.Equal(AmpelStatus.Rot, bewertung.Ampel);
    }

    [Fact]
    public void Bewerten_VoellingUnbekannt_WirdEhrlichAlsUnbekanntGezeigtUndNichtGeraten()
    {
        var bewertung = AutostartBewerter.Bewerten(
            Eintrag("mein-eigenes-tool", @"C:\Program Files\Eigenbau\mein-eigenes-tool.exe"));

        Assert.Equal(AutostartKategorie.Unbekannt, bewertung.Kategorie);
        Assert.Equal(AmpelStatus.Gelb, bewertung.Ampel);
        // Es MUSS eine Anleitung zum selbst Herausfinden enthalten sein - das ist
        // der Kern des Versprechens "Erklaerung reicht fuer sich allein".
        Assert.Contains("neu starten", bewertung.Empfehlung, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bewerten_UnbekanntMitHerausgeber_NenntDenHerausgeberImText()
    {
        var eintrag = Eintrag("firmentool", @"C:\Program Files\Firma\firmentool.exe");
        eintrag.Herausgeber = "Beispiel GmbH";

        var bewertung = AutostartBewerter.Bewerten(eintrag);

        Assert.Contains("Beispiel GmbH", bewertung.WasEsIst);
    }

    [Fact]
    public void Zusammenfassung_MehrereBremsen_WirdRichtigGezaehlt()
    {
        var bewertungen = new[]
        {
            AutostartBewerter.Bewerten(Eintrag("jusched")),
            AutostartBewerter.Bewerten(Eintrag("adobearm")),
            AutostartBewerter.Bewerten(Eintrag("onedrive"))
        };

        var text = AutostartBewerter.Zusammenfassung(bewertungen, aktiveEintraege: 3);

        Assert.Equal("3 Programme starten automatisch mit. 2 davon bremsen den Start unnötig.", text);
    }

    [Fact]
    public void Zusammenfassung_NichtsZuTun_SagtDasAuchSo()
    {
        var bewertungen = new[] { AutostartBewerter.Bewerten(Eintrag("onedrive")) };

        var text = AutostartBewerter.Zusammenfassung(bewertungen, aktiveEintraege: 1);

        Assert.Equal("1 Programm startet automatisch mit. Nichts davon bremst unnötig - sieht gut aus.", text);
    }

    [Fact]
    public void Regeln_KennenKeineDoppeltenDateinamen()
    {
        // Zwei Regeln fuer dieselbe Datei waeren ein stiller Widerspruch im Katalog:
        // Welche gewinnt, haenge dann von der Reihenfolge ab.
        var alle = AutostartRegeln.Alle.SelectMany(r => r.DateiNamen).ToList();

        var doppelte = alle.GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
                           .Where(g => g.Count() > 1)
                           .Select(g => g.Key)
                           .ToList();

        Assert.Empty(doppelte);
    }

    [Fact]
    public void Regeln_JedeRegelHatEineEchteEmpfehlung()
    {
        // Kernprinzip: Kein Eintrag ohne verstaendliche Handlungsanweisung.
        Assert.All(AutostartRegeln.Alle, regel =>
        {
            Assert.False(string.IsNullOrWhiteSpace(regel.WasEsIst));
            Assert.True(regel.Empfehlung.Length > 40, $"Regel '{regel.Anzeige}' erklärt zu wenig.");
        });
    }
}
