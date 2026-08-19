using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SicherheitsAuditEreignisseTests
{
    private static List<EreignisEintrag> Erzeuge(int anzahl) =>
        Enumerable.Range(0, anzahl).Select(i => new EreignisEintrag(DateTime.Now.AddDays(-i), "Quelle", "Beschreibung")).ToList();

    [Fact]
    public void Bewerten_FehlgeschlageneAnmeldungen_LeerListe_GibtGruen()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.FehlgeschlageneAnmeldungen, Erzeuge(0));
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }

    [Fact]
    public void Bewerten_FehlgeschlageneAnmeldungen_WenigeSindGelb()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.FehlgeschlageneAnmeldungen, Erzeuge(5));
        Assert.Equal(AmpelStatus.Gelb, befund.Status);
    }

    [Fact]
    public void Bewerten_FehlgeschlageneAnmeldungen_VieleSindRot()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.FehlgeschlageneAnmeldungen, Erzeuge(25));
        Assert.Equal(AmpelStatus.Rot, befund.Status);
    }

    [Fact]
    public void Bewerten_LogGeloescht_EinmalIstRot()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.LogGeloescht, Erzeuge(1));
        Assert.Equal(AmpelStatus.Rot, befund.Status);
    }

    [Fact]
    public void Bewerten_LogGeloescht_LeerIstGruen()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.LogGeloescht, Erzeuge(0));
        Assert.Equal(AmpelStatus.Gruen, befund.Status);
    }

    [Fact]
    public void Bewerten_NeuesKonto_EinesIstGelbNieRot()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.NeuesKonto, Erzeuge(50));
        Assert.Equal(AmpelStatus.Gelb, befund.Status);
    }

    [Fact]
    public void Bewerten_DienstAbsturz_Schwellwerte()
    {
        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.DienstAbsturz, Erzeuge(0)).Status);
        Assert.Equal(AmpelStatus.Gelb, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.DienstAbsturz, Erzeuge(3)).Status);
        Assert.Equal(AmpelStatus.Rot, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.DienstAbsturz, Erzeuge(10)).Status);
    }

    [Fact]
    public void Bewerten_UnerwarteterNeustart_Schwellwerte()
    {
        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.UnerwarteterNeustart, Erzeuge(0)).Status);
        Assert.Equal(AmpelStatus.Gelb, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.UnerwarteterNeustart, Erzeuge(2)).Status);
        Assert.Equal(AmpelStatus.Rot, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.UnerwarteterNeustart, Erzeuge(5)).Status);
    }

    [Fact]
    public void Bewerten_UnsauberesHerunterfahren_Schwellwerte()
    {
        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.UnsauberesHerunterfahren, Erzeuge(0)).Status);
        Assert.Equal(AmpelStatus.Rot, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.UnsauberesHerunterfahren, Erzeuge(4)).Status);
    }

    [Fact]
    public void Bewerten_AnwendungsAbsturz_Schwellwerte()
    {
        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.AnwendungsAbsturz, Erzeuge(0)).Status);
        Assert.Equal(AmpelStatus.Gelb, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.AnwendungsAbsturz, Erzeuge(5)).Status);
        Assert.Equal(AmpelStatus.Rot, SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.AnwendungsAbsturz, Erzeuge(15)).Status);
    }

    [Fact]
    public void Bewerten_TextEnthaeltAnzahl()
    {
        var befund = SicherheitsAuditEreignisse.Bewerten(EreignisKategorie.FehlgeschlageneAnmeldungen, Erzeuge(7));
        Assert.Contains("7", befund.Text);
    }
}
