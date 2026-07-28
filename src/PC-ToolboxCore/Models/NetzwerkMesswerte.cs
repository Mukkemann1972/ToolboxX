namespace PCToolboxCore.Models;

/// <summary>
/// Die reinen Messergebnisse der Netzwerkpruefung - ohne jede Bewertung.
/// Getrennt gehalten, damit die eigentliche Denkarbeit (<see cref="NetzwerkAuswerter"/>)
/// ohne echte Netzwerkkarte testbar ist: Wir koennen jeden Ausfall durchspielen,
/// ohne dafuer wirklich ein Kabel zu ziehen.
/// </summary>
public class NetzwerkMesswerte
{
    public bool AdapterVorhanden { get; set; }
    public string? AdapterName { get; set; }
    public bool IstWlan { get; set; }

    /// <summary>Erste IPv4-Adresse des aktiven Adapters, z.B. 192.168.178.42.</summary>
    public string? IpAdresse { get; set; }

    /// <summary>Adresse des Routers (Standardgateway).</summary>
    public string? Gateway { get; set; }

    public string[] DnsServer { get; set; } = [];

    public bool GatewayErreichbar { get; set; }
    public long? GatewayAntwortzeitMs { get; set; }

    /// <summary>Namensauflösung hat funktioniert (aus einem Namen wurde eine Adresse).</summary>
    public bool NamensaufloesungOk { get; set; }

    /// <summary>Eine öffentliche Adresse hat auf einen Ping geantwortet.</summary>
    public bool PingInsInternetOk { get; set; }

    /// <summary>Nur gesetzt, wenn der aktive Adapter WLAN ist und gerade verbunden ist.</summary>
    public int? WlanSignalProzent { get; set; }

    /// <summary>Namen weiterer aktiver Adapter (VPN/virtuell) - erkannt, aber nicht der Haupt-Pruefpfad.</summary>
    public string[] WeitereAdapter { get; set; } = [];
}
