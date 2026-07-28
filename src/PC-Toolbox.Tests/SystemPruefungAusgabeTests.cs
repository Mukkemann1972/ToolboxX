using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SystemPruefungAusgabeTests
{
    // ---------- Bereinigen: die UTF-16-Falle bei sfc ----------

    [Fact]
    public void Bereinigen_EntferntNullBytes()
    {
        // Genau so kommt sfc-Ausgabe an, wenn man sie umleitet: UTF-16, also
        // zwischen jedem Buchstaben ein Null-Byte. Ohne das Entfernen greift
        // hinterher kein einziger Textvergleich.
        var utf16Artig = "K\0e\0i\0n\0e\0";
        Assert.Equal("Keine", SystemPruefungAusgabe.Bereinigen(utf16Artig));
    }

    [Fact]
    public void Bereinigen_FasstLeerzeichenZusammenUndVereinheitlichtZeilenumbrueche()
    {
        Assert.Equal("a b\nc", SystemPruefungAusgabe.Bereinigen("  a    b\r\nc  "));
    }

    [Fact]
    public void Bereinigen_LeerUndNullSindHarmlos()
    {
        Assert.Equal("", SystemPruefungAusgabe.Bereinigen(null));
        Assert.Equal("", SystemPruefungAusgabe.Bereinigen(""));
    }

    // ---------- SFC ----------

    [Fact]
    public void Sfc_Sauber_DeutschUndEnglisch()
    {
        Assert.Equal(PruefBefund.Sauber, SystemPruefungAusgabe.SfcDeuten(
            "Der Windows-Ressourcenschutz hat keine Integritätsverletzungen gefunden.").Befund);
        Assert.Equal(PruefBefund.Sauber, SystemPruefungAusgabe.SfcDeuten(
            "Windows Resource Protection did not find any integrity violations.").Befund);
    }

    [Fact]
    public void Sfc_SauberErkenntAuchDieUtf16Rohform()
    {
        // Der realistische Fall: Ausgabe mit Null-Bytes, wie umgeleitet empfangen.
        var roh = string.Join("\0", "Der Windows-Ressourcenschutz hat keine Integritätsverletzungen gefunden.".ToCharArray());
        Assert.Equal(PruefBefund.Sauber, SystemPruefungAusgabe.SfcDeuten(roh).Befund);
    }

    [Fact]
    public void Sfc_Behoben()
    {
        Assert.Equal(PruefBefund.BeschaedigtBehoben, SystemPruefungAusgabe.SfcDeuten(
            "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden und erfolgreich repariert.").Befund);
        Assert.Equal(PruefBefund.BeschaedigtBehoben, SystemPruefungAusgabe.SfcDeuten(
            "Windows Resource Protection found corrupt files and successfully repaired them.").Befund);
    }

    [Fact]
    public void Sfc_NichtBehebbar_GehtVorDemBehobenFall()
    {
        // Diese Meldung enthaelt ebenfalls "gefunden" bzw. "found corrupt files" -
        // sie darf NICHT als Erfolg durchgehen. Genau deshalb ist die Pruefreihenfolge
        // im Deuter festgelegt.
        var deutsch = "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden, " +
                      "konnte jedoch einige von ihnen nicht reparieren.";
        var englisch = "Windows Resource Protection found corrupt files but was unable to fix some of them.";

        Assert.Equal(PruefBefund.BeschaedigtNichtBehebbar, SystemPruefungAusgabe.SfcDeuten(deutsch).Befund);
        Assert.Equal(PruefBefund.BeschaedigtNichtBehebbar, SystemPruefungAusgabe.SfcDeuten(englisch).Befund);
    }

    [Fact]
    public void Sfc_NichtBehebbar_NenntDieRichtigeReihenfolgeDismDannSfc()
    {
        // Fachlich der wichtigste Hinweis des ganzen Moduls: SFC holt Ersatzdateien
        // aus dem Komponentenspeicher. Ist der selbst kaputt, muss ZUERST DISM laufen.
        // Windows erklaert das nirgends - wenn wir es verlieren, ist das Modul wertlos.
        var e = SystemPruefungAusgabe.SfcDeuten(
            "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden, konnte jedoch einige von ihnen nicht reparieren.");
        Assert.Contains("DISM", e.NaechsterSchritt);
        Assert.Contains("SFC", e.NaechsterSchritt);
    }

    [Fact]
    public void Sfc_LeereAusgabeIstNichtGelaufen_UndNenntAdminrechte()
    {
        var e = SystemPruefungAusgabe.SfcDeuten("");
        Assert.Equal(PruefBefund.NichtGelaufen, e.Befund);
        Assert.Contains("Administrator", e.NaechsterSchritt);
    }

    [Fact]
    public void Sfc_UnbekannteAusgabeWirdNichtGeraten()
    {
        // Lieber ehrlich "kann ich nicht deuten" als eine erfundene Diagnose.
        var e = SystemPruefungAusgabe.SfcDeuten("Irgendwas völlig anderes stand hier.");
        Assert.Equal(PruefBefund.Unklar, e.Befund);
    }

    // ---------- DISM ----------

    [Fact]
    public void Dism_Sauber()
    {
        Assert.Equal(PruefBefund.Sauber, SystemPruefungAusgabe.DismDeuten(
            "Kein Komponentenspeicherfehler gefunden. Der Vorgang wurde erfolgreich beendet.").Befund);
        Assert.Equal(PruefBefund.Sauber, SystemPruefungAusgabe.DismDeuten(
            "No component store corruption detected. The operation completed successfully.").Befund);
    }

    [Fact]
    public void Dism_Reparierbar()
    {
        Assert.Equal(PruefBefund.BeschaedigtReparierbar, SystemPruefungAusgabe.DismDeuten(
            "Der Komponentenspeicher ist beschädigt. Der Speicher ist reparierbar.").Befund);
        Assert.Equal(PruefBefund.BeschaedigtReparierbar, SystemPruefungAusgabe.DismDeuten(
            "The component store is repairable.").Befund);
    }

    [Fact]
    public void Dism_Behoben_NenntSfcAlsNaechstenSchritt()
    {
        var e = SystemPruefungAusgabe.DismDeuten(
            "Der Wiederherstellungsvorgang wurde erfolgreich abgeschlossen.");
        Assert.Equal(PruefBefund.BeschaedigtBehoben, e.Befund);
        Assert.Contains("SFC", e.NaechsterSchritt);
    }

    [Fact]
    public void Dism_QuelldateienFehlen_WirdAlsFehlercodeErkannt()
    {
        var e = SystemPruefungAusgabe.DismDeuten(
            "Fehler: 0x800f081f  Die Quelldateien konnten nicht gefunden werden.");
        Assert.Equal(PruefBefund.BeschaedigtNichtBehebbar, e.Befund);
        Assert.Contains("0x800F081F", e.Klartext);
    }

    [Fact]
    public void Dism_DownloadFehlgeschlagen()
    {
        var e = SystemPruefungAusgabe.DismDeuten("Fehler: 0x800f0906");
        Assert.Equal(PruefBefund.BeschaedigtNichtBehebbar, e.Befund);
        Assert.Contains("0x800F0906", e.Klartext);
    }

    [Fact]
    public void Dism_UnbekannteAusgabeWirdNichtGeraten()
    {
        Assert.Equal(PruefBefund.Unklar, SystemPruefungAusgabe.DismDeuten("blabla").Befund);
    }

    // ---------- Grundsatz ----------

    // ---------- Ursache: das "Warum", nicht nur das "Was" ----------

    [Fact]
    public void Beschaedigung_ErklaertAuchDieUrsache()
    {
        // Alex' Punkt vom 27.07.: das Modul sagte DASS etwas kaputt ist und WIE man
        // es repariert - aber nicht, WIE ES DAZU KOMMT. Ohne das Warum repariert man
        // denselben Schaden in einem halben Jahr wieder.
        var faelle = new[]
        {
            SystemPruefungAusgabe.DismDeuten("The component store is repairable."),
            SystemPruefungAusgabe.DismDeuten("Der Wiederherstellungsvorgang wurde erfolgreich abgeschlossen."),
            SystemPruefungAusgabe.SfcDeuten(
                "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden, konnte jedoch einige von ihnen nicht reparieren."),
            SystemPruefungAusgabe.SfcDeuten(
                "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden und erfolgreich repariert.")
        };

        foreach (var e in faelle)
            Assert.False(string.IsNullOrWhiteSpace(e.Ursache), $"Ursache fehlt bei: {e.Klartext}");
    }

    [Fact]
    public void Ursache_NenntDieHaeufigstenAusloeser()
    {
        var e = SystemPruefungAusgabe.DismDeuten("The component store is repairable.");
        Assert.Contains("Update", e.Ursache);      // unterbrochenes Update = haeufigste Ursache
        Assert.Contains("WinSxS", e.Ursache);      // Tuning-Tools raeumen dort auf
        Assert.Contains("Laufwerke", e.Ursache);   // Querverweis auf unser SMART-Modul
    }

    [Fact]
    public void SauberesErgebnis_HatKeineUrsache()
    {
        // Bei "alles in Ordnung" waere eine Ursachenliste nur Verunsicherung.
        Assert.Equal("", SystemPruefungAusgabe.SfcDeuten(
            "Der Windows-Ressourcenschutz hat keine Integritätsverletzungen gefunden.").Ursache);
        Assert.Equal("", SystemPruefungAusgabe.DismDeuten(
            "No component store corruption detected.").Ursache);
    }

    [Fact]
    public void JedesErgebnis_HatKlartextUndNaechstenSchritt()
    {
        // Der Grundsatz des Projekts: kein Befund ohne Erklaerung UND ohne
        // konkreten naechsten Schritt. Sonst ist es nur eine weitere kryptische
        // Meldung - genau das, was wir Windows vorwerfen.
        var ausgaben = new[]
        {
            "Der Windows-Ressourcenschutz hat keine Integritätsverletzungen gefunden.",
            "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden und erfolgreich repariert.",
            "Der Windows-Ressourcenschutz hat beschädigte Dateien gefunden, konnte jedoch einige von ihnen nicht reparieren.",
            "", "voelliger unsinn"
        };
        foreach (var a in ausgaben)
        {
            foreach (var e in new[] { SystemPruefungAusgabe.SfcDeuten(a), SystemPruefungAusgabe.DismDeuten(a) })
            {
                Assert.False(string.IsNullOrWhiteSpace(e.Klartext));
                Assert.False(string.IsNullOrWhiteSpace(e.NaechsterSchritt));
            }
        }
    }
}
