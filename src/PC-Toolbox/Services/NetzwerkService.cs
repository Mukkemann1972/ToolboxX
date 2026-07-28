using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Services;

/// <summary>
/// Misst den Zustand der Netzwerkverbindung. Bewusst nur Messen, kein Bewerten -
/// das Einordnen macht der NetzwerkAuswerter im plattformneutralen Kern.
///
/// Alle Prüfungen sind rein lesend und kurz abgebrochen (Zeitlimits), damit das
/// Modul auch dann in wenigen Sekunden fertig ist, wenn gar nichts antwortet -
/// genau dann braucht man es ja.
/// </summary>
public class NetzwerkService
{
    /// <summary>
    /// Ziel für den Erreichbarkeitstest: Cloudflares Namensdienst. Bewusst eine feste
    /// Nummer statt eines Namens - so wird wirklich nur die Verbindung geprüft und
    /// nicht schon die Namensauflösung mit.
    /// </summary>
    private const string OeffentlicheTestAdresse = "1.1.1.1";

    /// <summary>
    /// Für die Namensauflösung: eine von der IANA für genau solche Zwecke reservierte
    /// Adresse. Kein Werbe- oder Verfolgungsrisiko, keine fremde Firma im Spiel.
    /// </summary>
    private const string TestName = "example.com";

    private const int ZeitlimitMs = 2000;

    public async Task<NetzwerkMesswerte> MessenAsync()
    {
        var messwerte = new NetzwerkMesswerte();

        AdapterErmitteln(messwerte);

        if (!string.IsNullOrWhiteSpace(messwerte.Gateway))
        {
            var (erreichbar, zeit) = await PingAsync(messwerte.Gateway!);
            messwerte.GatewayErreichbar = erreichbar;
            messwerte.GatewayAntwortzeitMs = zeit;
        }

        // Beide Aussenprüfungen parallel - das halbiert die Wartezeit im Fehlerfall.
        var internetAufgabe = PingAsync(OeffentlicheTestAdresse);
        var namenAufgabe = NamenAufloesenAsync();

        messwerte.PingInsInternetOk = (await internetAufgabe).Erreichbar;
        messwerte.NamensaufloesungOk = await namenAufgabe;

        if (messwerte.IstWlan)
            messwerte.WlanSignalProzent = await WlanSignalAsync();

        messwerte.WeitereAdapter = WeitereAdapterErmitteln(messwerte.AdapterName);

        return messwerte;
    }

    /// <summary>
    /// Signalstärke über das Windows-Bordmittel "netsh wlan show interfaces" - dieselbe
    /// Quelle, die auch das WLAN-Symbol in der Taskleiste speist. Kein direkter WLAN-API-
    /// Zugriff nötig.
    /// </summary>
    private static async Task<int?> WlanSignalAsync()
    {
        try
        {
            var start = new ProcessStartInfo("netsh.exe", "wlan show interfaces")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var prozess = Process.Start(start);
            if (prozess is null) return null;

            var ausgabe = await prozess.StandardOutput.ReadToEndAsync();
            await prozess.WaitForExitAsync();

            return WlanSignalDeutung.ProzentAusNetshAusgabe(ausgabe);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Weitere aktive Adapter (VPN/virtuell) - dieselbe Grundmenge wie in AdapterErmitteln,
    /// nur ohne den bereits gewählten Hauptadapter. Rein informativ, kein eigener Pruefpfad.
    /// </summary>
    private static string[] WeitereAdapterErmitteln(string? hauptAdapterName)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.OperationalStatus == OperationalStatus.Up)
                .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(a => !string.Equals(a.Name, hauptAdapterName, StringComparison.Ordinal))
                .Select(a => a.Name)
                .ToArray();
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    private static void AdapterErmitteln(NetzwerkMesswerte messwerte)
    {
        NetworkInterface[] adapter;
        try
        {
            adapter = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return;
        }

        // Der interessante Adapter ist der, der wirklich verbunden ist UND einen Router
        // kennt. Ohne die zweite Bedingung gewinnt sonst gern eine virtuelle Karte
        // (VirtualBox, VPN, Hyper-V), die zwar "oben" ist, aber nirgendwohin führt.
        var aktive = adapter
            .Where(a => a.OperationalStatus == OperationalStatus.Up)
            .Where(a => a.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        a.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();

        var gewaehlt = aktive.FirstOrDefault(a => GatewayVon(a) is not null) ?? aktive.FirstOrDefault();
        if (gewaehlt is null) return;

        messwerte.AdapterVorhanden = true;
        messwerte.AdapterName = gewaehlt.Name;
        messwerte.IstWlan = gewaehlt.NetworkInterfaceType is NetworkInterfaceType.Wireless80211;

        try
        {
            var eigenschaften = gewaehlt.GetIPProperties();

            messwerte.IpAdresse = eigenschaften.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address.ToString();

            messwerte.Gateway = GatewayVon(gewaehlt);

            messwerte.DnsServer = eigenschaften.DnsAddresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToArray();
        }
        catch (NetworkInformationException)
        {
            // Adapter meldet keine Details - dann bleibt es bei dem, was wir schon haben.
        }
    }

    private static string? GatewayVon(NetworkInterface adapter)
    {
        try
        {
            return adapter.GetIPProperties().GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork &&
                                     !a.Equals(IPAddress.Any))?
                .ToString();
        }
        catch (NetworkInformationException)
        {
            return null;
        }
    }

    private static async Task<(bool Erreichbar, long? ZeitMs)> PingAsync(string ziel)
    {
        try
        {
            using var ping = new Ping();
            var antwort = await ping.SendPingAsync(ziel, ZeitlimitMs);

            return antwort.Status == IPStatus.Success ? (true, antwort.RoundtripTime) : (false, null);
        }
        catch (Exception e) when (e is PingException or SocketException or ArgumentException)
        {
            return (false, null);
        }
    }

    private static async Task<bool> NamenAufloesenAsync()
    {
        try
        {
            using var abbruch = new CancellationTokenSource(ZeitlimitMs);
            var adressen = await Dns.GetHostAddressesAsync(TestName, abbruch.Token);

            return adressen.Length > 0;
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException or ArgumentException)
        {
            return false;
        }
    }
}
