namespace PCToolboxCore.Models;

/// <summary>
/// Die reinen Messergebnisse der Ziel-Diagnose (eine einzelne, vom Nutzer angegebene Adresse) -
/// ohne jede Bewertung. Getrennt gehalten aus demselben Grund wie <see cref="NetzwerkMesswerte"/>:
/// die Denkarbeit (<see cref="ZielDiagnoseAuswerter"/>) soll ohne echtes Netzwerk testbar sein.
/// </summary>
public class ZielMesswerte
{
    public string Ziel { get; set; } = "";

    /// <summary>Vom Namensdienst gelieferte Adressen, leer wenn die Auflösung fehlschlug.</summary>
    public string[] AufgeloesteAdressen { get; set; } = [];

    public bool NamensaufloesungOk => AufgeloesteAdressen.Length > 0;

    /// <summary>Adresse, auf die die Hosts-Datei dieses PCs "Ziel" fest umleitet - null wenn kein Eintrag.</summary>
    public string? HostsAdresse { get; set; }

    public bool ProxyAktiv { get; set; }
    public string? ProxyServer { get; set; }

    public int GeprueftePort { get; set; } = 443;
    public bool TcpErreichbar { get; set; }
    public long? TcpZeitMs { get; set; }
    public string? TcpFehler { get; set; }
}
