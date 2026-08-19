using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class ZuverlaessigkeitsVerlaufTests
{
    [Theory]
    [InlineData("Application Error")]
    [InlineData("Application Hang")]
    public void Klassifizieren_AbsturzQuellen_ErgibtAbsturz(string quelle)
    {
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren(quelle, 1000, "irgendein Text");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Absturz, grad);
    }

    [Fact]
    public void Klassifizieren_WindowsUpdateErfolg_ErgibtInfo()
    {
        // Echtes Muster von diesem Rechner: EventID 19 = "Installation erfolgreich".
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren(
            "Microsoft-Windows-WindowsUpdateClient", 19, "Installation erfolgreich: ...");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Info, grad);
    }

    [Fact]
    public void Klassifizieren_WindowsUpdateFehlgeschlagen_ErgibtFehler()
    {
        // Echtes Muster von diesem Rechner: EventID 20 = "Installationsfehler".
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren(
            "Microsoft-Windows-WindowsUpdateClient", 20, "Installationsfehler: ...0x80073D02");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Fehler, grad);
    }

    [Fact]
    public void Klassifizieren_MsiErfolgsstatusNull_ErgibtInfo()
    {
        // Echter Nachrichtentext von diesem Rechner (MsiInstaller, Logi Tune).
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren(
            "MsiInstaller", 1033,
            "Das Produkt wurde durch Windows Installer installiert. ... Erfolg- bzw. Fehlerstatus der Installation: 0.");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Info, grad);
    }

    [Fact]
    public void Klassifizieren_MsiErfolgsstatusUngleichNull_ErgibtFehler()
    {
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren(
            "MsiInstaller", 1034,
            "Installation fehlgeschlagen. Erfolg- bzw. Fehlerstatus der Installation: 1603.");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Fehler, grad);
    }

    [Fact]
    public void Klassifizieren_TreiberSchluesselwortInUnbekannterQuelle_ErgibtFehler()
    {
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren(
            "Microsoft-Windows-DriverFrameworks-UserMode", 10111, "Treiber konnte nicht geladen werden");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Fehler, grad);
    }

    [Fact]
    public void Klassifizieren_UnbekannteQuelleOhneAuffaelligkeit_ErgibtInfo()
    {
        var grad = ZuverlaessigkeitsVerlauf.Klassifizieren("EventLog", 6013, "Systemuptime ...");

        Assert.Equal(ZuverlaessigkeitSchweregrad.Info, grad);
    }

    [Fact]
    public void NachTagGruppieren_MehrereEintraegeAmSelbenTag_WerdenGezaehlt()
    {
        var tag = new DateTime(2026, 8, 9, 10, 0, 0);
        var eintraege = new[]
        {
            new ZuverlaessigkeitEintrag(tag, "Application Error", "x", "m", ZuverlaessigkeitSchweregrad.Absturz),
            new ZuverlaessigkeitEintrag(tag.AddHours(2), "MsiInstaller", "y", "m", ZuverlaessigkeitSchweregrad.Info),
            new ZuverlaessigkeitEintrag(tag.AddHours(4), "MsiInstaller", "z", "m", ZuverlaessigkeitSchweregrad.Info),
        };

        var tage = ZuverlaessigkeitsVerlauf.NachTagGruppieren(eintraege);

        var einzigerTag = Assert.Single(tage);
        Assert.Equal(1, einzigerTag.AnzahlAbsturz);
        Assert.Equal(2, einzigerTag.AnzahlInfo);
        Assert.Equal(3, einzigerTag.Gesamt);
    }

    [Fact]
    public void NachTagGruppieren_NeuesterTagZuerst()
    {
        var alt = new DateTime(2026, 8, 1, 12, 0, 0);
        var neu = new DateTime(2026, 8, 9, 12, 0, 0);
        var eintraege = new[]
        {
            new ZuverlaessigkeitEintrag(alt, "MsiInstaller", "x", "m", ZuverlaessigkeitSchweregrad.Info),
            new ZuverlaessigkeitEintrag(neu, "MsiInstaller", "y", "m", ZuverlaessigkeitSchweregrad.Info),
        };

        var tage = ZuverlaessigkeitsVerlauf.NachTagGruppieren(eintraege);

        Assert.Equal(DateOnly.FromDateTime(neu), tage[0].Tag);
        Assert.Equal(DateOnly.FromDateTime(alt), tage[1].Tag);
    }

    [Fact]
    public void NachTagGruppieren_LeereListe_GibtLeereListeZurueck()
    {
        var tage = ZuverlaessigkeitsVerlauf.NachTagGruppieren(Array.Empty<ZuverlaessigkeitEintrag>());

        Assert.Empty(tage);
    }
}
