using PCToolbox.Models;

namespace PCToolboxCore;

/// <summary>Ein Windows-Dienst, so wie ihn das System meldet.</summary>
public class DienstInfo
{
    /// <summary>Der technische Name, mit dem der Dienst gesteuert wird (z.B. "DiagTrack").</summary>
    public string Name { get; set; } = "";

    public string Anzeigename { get; set; } = "";
    public string? Beschreibung { get; set; }

    /// <summary>Auto, Manual, Disabled - oder Boot/System bei Treiber-nahen Diensten.</summary>
    public string Starttyp { get; set; } = "";

    public bool Laeuft { get; set; }

    public bool IstTreiberNah =>
        Starttyp.Equals("Boot", StringComparison.OrdinalIgnoreCase) ||
        Starttyp.Equals("System", StringComparison.OrdinalIgnoreCase);

    public string StarttypKlartext => Starttyp.ToLowerInvariant() switch
    {
        "auto" => Loc.T("Startet automatisch mit Windows", "Starts automatically with Windows"),
        "manual" => Loc.T("Startet nur bei Bedarf", "Starts only on demand"),
        "disabled" => Loc.T("Abgeschaltet", "Disabled"),
        "boot" or "system" => Loc.T("Startet ganz früh beim Hochfahren (Treiber-Ebene)", "Starts very early during boot (driver level)"),
        _ => Starttyp
    };
}

public class DienstBewertung
{
    public AmpelStatus Ampel { get; init; }
    public string Kurzurteil { get; init; } = "";
    public string WasEsIst { get; init; } = "";
    public string Empfehlung { get; init; } = "";

    /// <summary>True = die Toolbox bietet für diesen Dienst gar keine Änderung an.</summary>
    public bool Geschuetzt { get; init; }
}

/// <summary>
/// Ordnet Windows-Dienste ein.
///
/// Der wichtigste Unterschied zu den „Dienste optimieren"-Anleitungen im Netz: Dort wird
/// munter auf „Deaktiviert" gestellt, und Wochen später funktioniert der Drucker oder das
/// WLAN nicht mehr, ohne dass noch jemand den Zusammenhang sieht. Deshalb hier zwei
/// Festlegungen:
///
/// 1. <b>Kritische Dienste werden gar nicht erst angeboten.</b> Kein Warnhinweis zum
///    Wegklicken - der Knopf fehlt einfach.
/// 2. <b>Empfohlen wird „Manuell", nie „Deaktiviert".</b> Bei „Manuell" startet der Dienst
///    weiterhin, wenn ihn wirklich jemand braucht - er liegt nur nicht mehr die ganze Zeit
///    im Speicher herum. Das ist fast der ganze Nutzen ohne das ganze Risiko.
/// </summary>
public static class DiensteBewerter
{
    /// <summary>
    /// Ohne diese Dienste startet Windows nicht mehr richtig, meldet sich niemand mehr an,
    /// oder das Netzwerk ist tot. Die Liste ist bewusst grosszügig - im Zweifel schützen.
    /// </summary>
    private static readonly HashSet<string> Unantastbar = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "Winmgmt", "EventLog", "PlugPlay",
        "Power", "ProfSvc", "SamSs", "gpsvc", "CryptSvc", "Schedule", "BFE", "MpsSvc",
        "Dhcp", "Dnscache", "nsi", "NlaSvc", "netprofm", "LanmanWorkstation", "LanmanServer",
        "UserManager", "SystemEventsBroker", "StateRepository", "TrustedInstaller",
        "WinDefend", "SecurityHealthService", "wscsvc", "AudioEndpointBuilder", "Audiosrv",
        "WlanSvc", "WwanSvc", "Spooler", "Themes", "ShellHWDetection", "TimeBrokerSvc",
        "CoreMessagingRegistrar", "camsvc", "AppXSvc", "ClipSVC", "EventSystem", "KeyIso"
    };

    /// <summary>
    /// Dienste, die man guten Gewissens auf „Manuell" stellen kann - mit ehrlicher Angabe,
    /// was man davon hat. Keine Versprechen über Geschwindigkeit, wo keine sind.
    /// </summary>
    private static readonly Dictionary<string, (string WasEsIst, string Empfehlung)> Bremsen =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DiagTrack"] = (
                Loc.T("Sammelt Nutzungsdaten und schickt sie an Microsoft („Benutzererfahrungen und Telemetrie\").",
                      "Collects usage data and sends it to Microsoft (\"user experiences and telemetry\")."),
                Loc.T("Kannst du auf „Manuell\" stellen. Windows funktioniert vollständig weiter - es " +
                      "werden nur weniger Daten übertragen. Spürbar schneller wird der PC davon nicht, " +
                      "das behaupten nur die Tuning-Programme.",
                      "You can set it to \"Manual\". Windows keeps working completely - just " +
                      "less data gets transmitted. The PC won't get noticeably faster from this, " +
                      "only tuning programs claim that.")),

            ["dmwappushservice"] = (
                Loc.T("Überträgt Geräteverwaltungs-Nachrichten - auf einem privaten PC ohne Firmenverwaltung ohne Funktion.",
                      "Transmits device management messages - has no function on a private PC without corporate management."),
                Loc.T("Auf „Manuell\" stellen ist unbedenklich, wenn dein PC nicht von einer Firma verwaltet wird.",
                      "Setting it to \"Manual\" is safe if your PC isn't managed by a company.")),

            ["RemoteRegistry"] = (
                Loc.T("Erlaubt es, die Registry deines PCs über das Netzwerk zu bearbeiten.",
                      "Allows editing your PC's registry over the network."),
                Loc.T("Sollte abgeschaltet bleiben - das ist ein Einfallstor, das ein Privat-PC nicht " +
                      "braucht. Bei Windows ist es normalerweise schon aus.",
                      "Should stay disabled - this is an entry point a private PC doesn't " +
                      "need. On Windows it's normally already off.")),

            ["RetailDemo"] = (
                Loc.T("Der Vorführmodus für Ausstellungsstücke im Elektromarkt.",
                      "The demo mode for display units in electronics stores."),
                Loc.T("Auf einem echten PC völlig ohne Nutzen - kann gefahrlos auf „Manuell\".",
                      "Completely useless on a real PC - can safely be set to \"Manual\".")),

            ["Fax"] = (
                Loc.T("Faxversand über ein angeschlossenes Modem.",
                      "Fax sending via a connected modem."),
                Loc.T("Wenn du kein Fax über den PC verschickst: gefahrlos auf „Manuell\".",
                      "If you don't send faxes from the PC: safely set to \"Manual\".")),

            ["MapsBroker"] = (
                Loc.T("Lädt Kartenmaterial für die Windows-Karten-App im Hintergrund herunter.",
                      "Downloads map data for the Windows Maps app in the background."),
                Loc.T("Wenn du die Karten-App nicht nutzt: auf „Manuell\" stellen.",
                      "If you don't use the Maps app: set it to \"Manual\".")),

            ["XblGameSave"] = (
                Loc.T("Speichert Spielstände von Xbox-Spielen in der Cloud.",
                      "Saves Xbox game saves to the cloud."),
                Loc.T("Ohne Xbox-Spiele auf diesem PC ohne Funktion - „Manuell\" ist unbedenklich.",
                      "No function without Xbox games on this PC - \"Manual\" is safe.")),

            ["XboxNetApiSvc"] = (
                Loc.T("Netzwerkdienst für Xbox-Spiele.",
                      "Network service for Xbox games."),
                Loc.T("Ohne Xbox-Spiele auf diesem PC ohne Funktion - „Manuell\" ist unbedenklich.",
                      "No function without Xbox games on this PC - \"Manual\" is safe.")),

            ["WMPNetworkSvc"] = (
                Loc.T("Gibt die Medienbibliothek des Windows Media Players im Netzwerk frei.",
                      "Shares the Windows Media Player media library on the network."),
                Loc.T("Wenn du keine Musik/Filme vom PC an andere Geräte im Haus streamst: auf „Manuell\".",
                      "If you don't stream music/movies from the PC to other devices at home: set it to \"Manual\".")),

            ["SysMain"] = (
                Loc.T("Merkt sich, welche Programme du oft startest, und lädt sie im Voraus in den Speicher (früher „Superfetch\").",
                      "Remembers which programs you start often and preloads them into memory (formerly \"Superfetch\")."),
                Loc.T("Bei einer SSD bringt das kaum noch etwas und sorgt für dauernde Hintergrundaktivität. " +
                      "Auf „Manuell\" ist vertretbar. Bei einer klassischen Festplatte solltest du es " +
                      "anlassen - dort hilft es wirklich.",
                      "On an SSD this barely helps anymore and causes constant background activity. " +
                      "\"Manual\" is reasonable. On a classic hard drive you should " +
                      "leave it on - it really helps there."))
        };

    public static DienstBewertung Bewerten(DienstInfo dienst)
    {
        if (dienst.IstTreiberNah || Unantastbar.Contains(dienst.Name))
            return new DienstBewertung
            {
                Ampel = AmpelStatus.Gruen,
                Geschuetzt = true,
                Kurzurteil = Loc.T("Gehört zum System - wird nicht angetastet", "Belongs to the system - not touched"),
                WasEsIst = KurzBeschreibung(dienst),
                Empfehlung = Loc.T(
                    "Diesen Dienst bietet die Toolbox bewusst nicht zum Ändern an. Ohne ihn " +
                    "läuft Windows nicht mehr richtig - und der Zusammenhang fällt oft erst " +
                    "Wochen später auf, wenn plötzlich etwas anderes nicht mehr geht.",
                    "The toolbox deliberately doesn't offer this service for changing. Without it " +
                    "Windows stops working correctly - and the connection is often only noticed " +
                    "weeks later, when something else suddenly stops working.")
            };

        if (Bremsen.TryGetValue(dienst.Name, out var eintrag))
            return new DienstBewertung
            {
                Ampel = dienst.Starttyp.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                    ? AmpelStatus.Gelb
                    : AmpelStatus.Gruen,
                Kurzurteil = dienst.Starttyp.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                    ? Loc.T("Läuft dauerhaft mit - muss es aber nicht", "Runs permanently - but doesn't have to")
                    : Loc.T("Bereits auf Bedarf gestellt - alles gut", "Already set to on-demand - all good"),
                WasEsIst = eintrag.WasEsIst,
                Empfehlung = eintrag.Empfehlung
            };

        return new DienstBewertung
        {
            Ampel = AmpelStatus.Gruen,
            Kurzurteil = dienst.StarttypKlartext,
            WasEsIst = KurzBeschreibung(dienst),
            Empfehlung = Loc.T(
                "Zu diesem Dienst haben wir keine eigene Einschätzung. Faustregel: Was du " +
                "nicht sicher zuordnen kannst, lässt du in Ruhe. Wenn du ihn ändern willst, " +
                "wähle „Manuell\" statt „Abschalten\" - dann startet er weiterhin, falls " +
                "ihn doch jemand braucht.",
                "We have no assessment of our own for this service. Rule of thumb: what you " +
                "can't confidently place, you leave alone. If you want to change it, " +
                "choose \"Manual\" instead of \"Disabled\" - then it will still start if " +
                "someone does need it after all.")
        };
    }

    private static string KurzBeschreibung(DienstInfo dienst) =>
        string.IsNullOrWhiteSpace(dienst.Beschreibung)
            ? Loc.T($"Windows-Dienst „{dienst.Anzeigename}\" ({dienst.Name}). Eine Beschreibung liefert Windows dazu nicht.",
                    $"Windows service \"{dienst.Anzeigename}\" ({dienst.Name}). Windows doesn't provide a description for it.")
            : dienst.Beschreibung!;

    /// <summary>Ist dieser Dienst einer, den wir aktiv zum Umstellen vorschlagen?</summary>
    public static bool IstVorschlag(string name) => Bremsen.ContainsKey(name);

    public static string Zusammenfassung(int gesamt, int vorschlaege)
    {
        if (gesamt == 0) return Loc.T("Es konnten keine Dienste ausgelesen werden.", "No services could be read.");

        return vorschlaege switch
        {
            0 => Loc.T($"{gesamt} Dienste geprüft - es gibt nichts, was wir dir zum Umstellen vorschlagen würden.",
                       $"{gesamt} services checked - there's nothing we'd suggest changing."),
            1 => Loc.T($"{gesamt} Dienste geprüft - bei 1 Dienst lohnt ein Blick.",
                       $"{gesamt} services checked - 1 service is worth a look."),
            _ => Loc.T($"{gesamt} Dienste geprüft - bei {vorschlaege} Diensten lohnt ein Blick.",
                       $"{gesamt} services checked - {vorschlaege} services are worth a look.")
        };
    }
}

/// <summary>Baut die sc.exe-Aufrufe zum Umstellen eines Dienstes.</summary>
public static class DiensteBefehle
{
    /// <summary>
    /// Achtung, klassische Falle: sc.exe verlangt das Leerzeichen NACH dem Gleichheitszeichen
    /// ("start= demand"). Ohne dieses Leerzeichen lehnt es den Aufruf wortlos ab.
    /// </summary>
    public static string StarttypSetzen(string dienstName, string starttyp)
    {
        var wert = starttyp.ToLowerInvariant() switch
        {
            "auto" => "auto",
            "manual" => "demand",
            "disabled" => "disabled",
            _ => throw new ArgumentException($"Unbekannter Starttyp: {starttyp}", nameof(starttyp))
        };

        return $"config \"{dienstName}\" start= {wert}";
    }

    public static string Stoppen(string dienstName) => $"stop \"{dienstName}\"";
}
