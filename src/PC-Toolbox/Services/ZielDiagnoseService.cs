using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.Win32;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Services;

/// <summary>
/// Prüft eine einzelne, vom Nutzer angegebene Adresse End-zu-Ende - genau die Schritte, die man
/// sonst von Hand durchklicken müsste, wenn "diese eine Seite" nicht lädt, obwohl der Rest des
/// Internets funktioniert (Namensauflösung, Hosts-Datei, Proxy, echter Verbindungsaufbau).
/// Die Bewertung übernimmt PCToolboxCore.ZielDiagnoseAuswerter, hier steckt nur das
/// Windows-spezifische Messen.
///
/// Rein lesend: es wird nichts an Hosts-Datei, Proxy oder sonstwo verändert.
/// </summary>
public class ZielDiagnoseService
{
    private const int ZeitlimitMs = 4000;

    private static readonly string HostsPfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    public async Task<ZielMesswerte> PruefenAsync(string ziel, int port = 443)
    {
        var messwerte = new ZielMesswerte { Ziel = ziel, GeprueftePort = port };

        messwerte.AufgeloesteAdressen = await NamenAufloesenAsync(ziel);
        messwerte.HostsAdresse = HostsPruefen(ziel);
        (messwerte.ProxyAktiv, messwerte.ProxyServer) = ProxyErmitteln();

        if (messwerte.AufgeloesteAdressen.Length > 0)
        {
            var (erreichbar, zeit, fehler) = await TcpTestenAsync(messwerte.AufgeloesteAdressen[0], port);
            messwerte.TcpErreichbar = erreichbar;
            messwerte.TcpZeitMs = zeit;
            messwerte.TcpFehler = fehler;
        }

        return messwerte;
    }

    private static async Task<string[]> NamenAufloesenAsync(string ziel)
    {
        try
        {
            using var abbruch = new CancellationTokenSource(ZeitlimitMs);
            var adressen = await Dns.GetHostAddressesAsync(ziel, abbruch.Token);
            return adressen.Select(a => a.ToString()).ToArray();
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException or ArgumentException)
        {
            return [];
        }
    }

    private static string? HostsPruefen(string ziel)
    {
        try
        {
            var inhalt = File.ReadAllText(HostsPfad);
            return HostsDateiAuswerter.SucheEintrag(inhalt, ziel);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Liest die WinINet-Proxy-Einstellungen (Windows-Einstellungen → Netzwerk und Internet →
    /// Proxy) - dieselbe Quelle, die die meisten Programme und Browser standardmäßig übernehmen.
    /// </summary>
    private static (bool Aktiv, string? Server) ProxyErmitteln()
    {
        try
        {
            using var schluessel = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");

            var aktiv = schluessel?.GetValue("ProxyEnable") is int wert && wert == 1;
            var server = schluessel?.GetValue("ProxyServer") as string;

            return (aktiv, aktiv ? server : null);
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException)
        {
            return (false, null);
        }
    }

    private static async Task<(bool Erreichbar, long? ZeitMs, string? Fehler)> TcpTestenAsync(string ip, int port)
    {
        try
        {
            using var client = new TcpClient();
            var uhr = Stopwatch.StartNew();
            using var abbruch = new CancellationTokenSource(ZeitlimitMs);

            await client.ConnectAsync(IPAddress.Parse(ip), port, abbruch.Token);

            uhr.Stop();
            return (true, uhr.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException)
        {
            return (false, null, Loc.T("Zeitüberschreitung", "Timed out"));
        }
        catch (SocketException e)
        {
            return (false, null, e.SocketErrorCode.ToString());
        }
    }
}
