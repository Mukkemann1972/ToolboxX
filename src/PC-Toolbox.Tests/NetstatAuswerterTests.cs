using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class NetstatAuswerterTests
{
    // Echtes Beispiel (gekuerzt) einer deutschen netstat -ano-Ausgabe - so bleibt der Test
    // an der wirklichen Struktur dran, nicht an einer erfundenen Annahme darueber.
    private const string BeispielAusgabe = """
        Aktive Verbindungen

          Proto  Lokale Adresse         Remoteadresse          Status           PID
          TCP    0.0.0.0:135            0.0.0.0:0              ABHOEREN         1596
          TCP    127.0.0.1:5354         0.0.0.0:0              ABHOEREN         4972
          TCP    192.168.178.49:49338   160.79.104.10:443      HERGESTELLT      1440
          TCP    192.168.178.49:49160   84.46.55.204:21116     WARTEND          0
          TCP    192.168.178.49:50816   172.217.112.4:443      HERGESTELLT      15584
          UDP    0.0.0.0:123            *:*                                    9999
          TCP6   [::]:135               [::]:0                 ABHOEREN         1596
          TCP6   [::1]:5354             [::]:0                 ABHOEREN         4972
        """;

    [Fact]
    public void Parse_LiestAlleVerbindungszeilen()
    {
        var verbindungen = NetstatAuswerter.Parse(BeispielAusgabe);

        Assert.Equal(8, verbindungen.Count);
    }

    [Fact]
    public void Parse_TrenntAdresseUndPortKorrektAuchBeiIPv6()
    {
        var verbindungen = NetstatAuswerter.Parse(BeispielAusgabe);

        var ipv6 = verbindungen.Single(v => v.LokaleAdresse == "[::1]:5354");
        Assert.Equal("::", ipv6.RemoteAdresse);
        Assert.Equal(0, ipv6.RemotePort);
    }

    [Fact]
    public void Parse_LiestPidAuchOhneStatusspalteBeiUdp()
    {
        var verbindungen = NetstatAuswerter.Parse(BeispielAusgabe);

        var udp = verbindungen.Single(v => v.Protokoll == "UDP");
        Assert.Equal(9999, udp.Pid);
        Assert.Equal("*", udp.RemoteAdresse);
    }

    [Fact]
    public void Parse_IgnoriertKopfzeilenUndLeereZeilen()
    {
        var verbindungen = NetstatAuswerter.Parse("Aktive Verbindungen\n\n  Proto  Lokale Adresse\n");

        Assert.Empty(verbindungen);
    }

    [Fact]
    public void AktiveVerbindungen_FiltertLauschendeSocketsRaus()
    {
        var alle = NetstatAuswerter.Parse(BeispielAusgabe);

        var aktiv = NetstatAuswerter.AktiveVerbindungen(alle);

        Assert.All(aktiv, v => Assert.NotEqual("0.0.0.0", v.RemoteAdresse));
        Assert.All(aktiv, v => Assert.NotEqual("::", v.RemoteAdresse));
        Assert.All(aktiv, v => Assert.NotEqual("*", v.RemoteAdresse));
    }

    [Fact]
    public void AktiveVerbindungen_FiltertPidNullRaus()
    {
        var alle = NetstatAuswerter.Parse(BeispielAusgabe);

        var aktiv = NetstatAuswerter.AktiveVerbindungen(alle);

        Assert.DoesNotContain(aktiv, v => v.Pid == 0);
    }

    [Fact]
    public void AktiveVerbindungen_BehaeltEchteVerbindungen()
    {
        var alle = NetstatAuswerter.Parse(BeispielAusgabe);

        var aktiv = NetstatAuswerter.AktiveVerbindungen(alle);

        Assert.Contains(aktiv, v => v.RemoteAdresse == "160.79.104.10" && v.RemotePort == 443 && v.Pid == 1440);
        Assert.Contains(aktiv, v => v.RemoteAdresse == "172.217.112.4" && v.RemotePort == 443 && v.Pid == 15584);
    }
}
