using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class HostsDateiAuswerterTests
{
    [Fact]
    public void KeinEintrag_GibtNullZurueck()
    {
        var inhalt = "127.0.0.1 localhost\n::1 localhost\n";

        Assert.Null(HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }

    [Fact]
    public void PassenderEintrag_WirdGefunden()
    {
        var inhalt = "127.0.0.1 localhost\n192.168.178.119 mail.firma.de\n";

        Assert.Equal("192.168.178.119", HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }

    [Fact]
    public void GrossKleinschreibungSpieltKeineRolle()
    {
        var inhalt = "192.168.178.119 Mail.Firma.DE\n";

        Assert.Equal("192.168.178.119", HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }

    [Fact]
    public void AuskommentierterEintrag_WirdIgnoriert()
    {
        var inhalt = "# 192.168.178.119 mail.firma.de\n";

        Assert.Null(HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }

    [Fact]
    public void KommentarHinterEintrag_WirdAbgeschnittenEintragBleibtGueltig()
    {
        var inhalt = "192.168.178.119 mail.firma.de # eigener Testeintrag\n";

        Assert.Equal("192.168.178.119", HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }

    [Fact]
    public void MehrereNamenProZeile_JederWirdGefunden()
    {
        var inhalt = "10.0.0.5 intern.firma.de mail.firma.de\n";

        Assert.Equal("10.0.0.5", HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }

    [Fact]
    public void LeereDateiOderNurLeerzeilen_GibtNullZurueck()
    {
        Assert.Null(HostsDateiAuswerter.SucheEintrag("\n\n  \n", "mail.firma.de"));
        Assert.Null(HostsDateiAuswerter.SucheEintrag("", "mail.firma.de"));
    }

    [Fact]
    public void AndereDomain_WirdNichtFaelschlichGetroffen()
    {
        var inhalt = "192.168.178.119 andere-domain.de\n";

        Assert.Null(HostsDateiAuswerter.SucheEintrag(inhalt, "mail.firma.de"));
    }
}
