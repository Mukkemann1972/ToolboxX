using PCToolbox.Models;
using PCToolboxCore;
using PCToolboxCore.Models;
using Xunit;

namespace PCToolboxCore.Tests;

public class NetzwerkAuswerterTests
{
    private static NetzwerkMesswerte AllesGut() => new()
    {
        AdapterVorhanden = true,
        AdapterName = "Ethernet",
        IpAdresse = "192.168.178.42",
        Gateway = "192.168.178.1",
        DnsServer = ["192.168.178.1"],
        GatewayErreichbar = true,
        GatewayAntwortzeitMs = 2,
        NamensaufloesungOk = true,
        PingInsInternetOk = true
    };

    private static NetzwerkSchritt Schritt(List<NetzwerkSchritt> schritte, string titel) =>
        schritte.Single(s => s.Titel == titel);

    [Fact]
    public void AllesInOrdnung_FuenfGrunePunkteUndFreundlichesUrteil()
    {
        var schritte = NetzwerkAuswerter.Auswerten(AllesGut());

        Assert.Equal(5, schritte.Count);
        Assert.All(schritte, s => Assert.Equal(AmpelStatus.Gruen, s.Ampel));
        Assert.Equal("Alles in Ordnung: Dein PC ist im Netz und kommt ins Internet.",
            NetzwerkAuswerter.Gesamturteil(schritte));
    }

    [Fact]
    public void KabelAb_NurEinRotesKreuzDerRestIstFolgefehler()
    {
        // Der eigentliche Mehrwert: EINE Baustelle anzeigen, nicht fuenf.
        var messwerte = new NetzwerkMesswerte { AdapterVorhanden = false };

        var schritte = NetzwerkAuswerter.Auswerten(messwerte);

        Assert.Single(schritte, s => s.Ampel == AmpelStatus.Rot);
        Assert.Equal("Netzwerkverbindung", schritte.First(s => s.Ampel == AmpelStatus.Rot).Titel);
        Assert.All(schritte.Skip(1), s => Assert.True(s.IstFolgefehler));
        Assert.All(schritte.Skip(1), s => Assert.Equal(AmpelStatus.Gelb, s.Ampel));
    }

    [Fact]
    public void NotfallAdresse_WirdErkanntUndRichtigErklaert()
    {
        var messwerte = AllesGut();
        messwerte.IpAdresse = "169.254.13.7";
        messwerte.GatewayErreichbar = false;

        var schritte = NetzwerkAuswerter.Auswerten(messwerte);
        var adresse = Schritt(schritte, "Adresse vom Router");

        Assert.Equal(AmpelStatus.Rot, adresse.Ampel);
        Assert.Contains("169.254", adresse.Empfehlung);
        Assert.Contains("Router", adresse.Empfehlung);
    }

    [Fact]
    public void NurNamensaufloesungKaputt_IstDerKlassischeFallSeitenLadenNicht()
    {
        var messwerte = AllesGut();
        messwerte.NamensaufloesungOk = false;

        var schritte = NetzwerkAuswerter.Auswerten(messwerte);

        Assert.Equal(AmpelStatus.Gruen, Schritt(schritte, "Router erreichbar").Ampel);
        Assert.Equal(AmpelStatus.Rot, Schritt(schritte, "Namen werden aufgelöst").Ampel);
        Assert.Contains("keine Seite lädt", Schritt(schritte, "Namen werden aufgelöst").Empfehlung);
        Assert.StartsWith("Hier hakt es: Namen werden aufgelöst",
            NetzwerkAuswerter.Gesamturteil(schritte));
    }

    [Fact]
    public void GeblockterPingAberNamensaufloesungGeht_GiltAlsInternetErreichbar()
    {
        // Viele Netze blocken Test-Anfragen. Daraus "kein Internet" zu machen waere
        // ein Fehlalarm - wer Namen aufloesen kann, ist online.
        var messwerte = AllesGut();
        messwerte.PingInsInternetOk = false;

        var schritte = NetzwerkAuswerter.Auswerten(messwerte);
        var internet = Schritt(schritte, "Internet erreichbar");

        Assert.Equal(AmpelStatus.Gruen, internet.Ampel);
        Assert.Contains("geblockt", internet.Ergebnis);
    }

    [Fact]
    public void RouterDaAberKeinInternet_ZeigtAufDenAnschlussNichtAufDenPc()
    {
        var messwerte = AllesGut();
        messwerte.NamensaufloesungOk = false;
        messwerte.PingInsInternetOk = false;

        var schritte = NetzwerkAuswerter.Auswerten(messwerte);
        var internet = Schritt(schritte, "Internet erreichbar");

        Assert.True(internet.IstFolgefehler);
        Assert.Equal(AmpelStatus.Gelb, internet.Ampel);
    }

    [Fact]
    public void JederSchritt_HatErklaerungUndHandlungsanweisung()
    {
        // Kernprinzip: kein Befund ohne verstaendliche Erklaerung, was zu tun ist.
        var schritte = NetzwerkAuswerter.Auswerten(new NetzwerkMesswerte { AdapterVorhanden = false });

        Assert.All(schritte, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.WasEsIst));
            Assert.True(s.Empfehlung.Length > 40, $"Schritt '{s.Titel}' erklärt zu wenig.");
        });
    }
}
