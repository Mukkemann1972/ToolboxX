using PCToolbox.Models;
using PCToolboxCore;
using PCToolboxCore.Models;
using Xunit;

namespace PCToolboxCore.Tests;

public class ZielDiagnoseAuswerterTests
{
    private static ZielMesswerte AllesGut() => new()
    {
        Ziel = "mail.firma.de",
        AufgeloesteAdressen = ["192.168.178.119"],
        HostsAdresse = null,
        ProxyAktiv = false,
        GeprueftePort = 443,
        TcpErreichbar = true,
        TcpZeitMs = 12
    };

    private static NetzwerkSchritt Schritt(List<NetzwerkSchritt> schritte, string titel) =>
        schritte.Single(s => s.Titel == titel);

    [Fact]
    public void AllesInOrdnung_VierGrunePunkte()
    {
        var schritte = ZielDiagnoseAuswerter.Auswerten(AllesGut());

        Assert.Equal(4, schritte.Count);
        Assert.All(schritte, s => Assert.Equal(AmpelStatus.Gruen, s.Ampel));
    }

    [Fact]
    public void NamensaufloesungFehlgeschlagen_IstRotUndTcpWirdUebersprungen()
    {
        var messwerte = AllesGut();
        messwerte.AufgeloesteAdressen = [];

        var schritte = ZielDiagnoseAuswerter.Auswerten(messwerte);

        Assert.Equal(AmpelStatus.Rot, Schritt(schritte, "Namensauflösung").Ampel);
        var tcp = Schritt(schritte, "Verbindung zu Port 443");
        Assert.True(tcp.IstFolgefehler);
        Assert.Equal(AmpelStatus.Gelb, tcp.Ampel);
    }

    [Fact]
    public void HostsUeberschreibung_IstGelbNieRotUndZeigtDieUmgeleiteteAdresse()
    {
        var messwerte = AllesGut();
        messwerte.HostsAdresse = "10.0.0.9";

        var schritte = ZielDiagnoseAuswerter.Auswerten(messwerte);
        var hosts = Schritt(schritte, "Eigene Umleitung (Hosts-Datei)");

        Assert.Equal(AmpelStatus.Gelb, hosts.Ampel);
        Assert.Contains("10.0.0.9", hosts.Ergebnis);
    }

    [Fact]
    public void ProxyAktiv_IstGelbNieRotUndZeigtDenServer()
    {
        var messwerte = AllesGut();
        messwerte.ProxyAktiv = true;
        messwerte.ProxyServer = "proxy.firma.de:8080";

        var schritte = ZielDiagnoseAuswerter.Auswerten(messwerte);
        var proxy = Schritt(schritte, "Proxy-Einstellungen");

        Assert.Equal(AmpelStatus.Gelb, proxy.Ampel);
        Assert.Contains("proxy.firma.de:8080", proxy.Ergebnis);
    }

    [Fact]
    public void DnsOkAberTcpScheitert_GenauDasWarDerHeutigeRealeFall()
    {
        // Exakt das Szenario der Brave-Diagnose: DNS klappt, TCP-Verbindung zum Zielport
        // scheitert trotzdem - das muss klar als der eigentliche Befund markiert werden.
        var messwerte = AllesGut();
        messwerte.TcpErreichbar = false;
        messwerte.TcpZeitMs = null;
        messwerte.TcpFehler = "TimedOut";

        var schritte = ZielDiagnoseAuswerter.Auswerten(messwerte);
        var tcp = Schritt(schritte, "Verbindung zu Port 443");

        Assert.Equal(AmpelStatus.Rot, tcp.Ampel);
        Assert.False(tcp.IstFolgefehler);
        Assert.Contains("TimedOut", tcp.Ergebnis);
    }

    [Fact]
    public void JederSchritt_HatErklaerungUndHandlungsanweisung()
    {
        var schritte = ZielDiagnoseAuswerter.Auswerten(new ZielMesswerte { Ziel = "test.de" });

        Assert.All(schritte, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.WasEsIst));
            Assert.False(string.IsNullOrWhiteSpace(s.Empfehlung));
        });
    }
}
