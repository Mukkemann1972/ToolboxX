using PCToolbox.Models;
using PCToolboxCore.Models;

namespace PCToolboxCore;

/// <summary>
/// Beantwortet die zweithäufigste Netzwerkfrage: "Das Internet geht, aber DIESE EINE Seite
/// nicht - woran liegt's?" Ergänzt <see cref="NetzwerkAuswerter"/> (die allgemeine Kette PC →
/// Router → Internet), die diese Frage bewusst nicht beantwortet: eine einzelne unerreichbare
/// Adresse bei sonst funktionierendem Internet sieht dort überall grün aus.
///
/// Anders als bei der Ketten-Prüfung sind Hosts-Datei und Proxy hier bewusst KEIN Fehlerstatus,
/// sondern reine Zusatzinformation (gelb = "wissenswert", nie rot) - ein Proxy oder ein
/// Hosts-Eintrag ist nicht per se falsch, aber oft die Ursache, wenn "nur diese eine Adresse"
/// nicht erreichbar ist, während alles andere funktioniert.
/// </summary>
public static class ZielDiagnoseAuswerter
{
    public static List<NetzwerkSchritt> Auswerten(ZielMesswerte m)
    {
        var schritte = new List<NetzwerkSchritt>
        {
            NamensaufloesungSchritt(m),
            HostsSchritt(m),
            ProxySchritt(m),
            TcpSchritt(m)
        };

        return schritte;
    }

    private static NetzwerkSchritt NamensaufloesungSchritt(ZielMesswerte m) => new()
    {
        Titel = Loc.T("Namensauflösung", "Name resolution"),
        Ampel = m.NamensaufloesungOk ? AmpelStatus.Gruen : AmpelStatus.Rot,
        Ergebnis = m.NamensaufloesungOk
            ? string.Join(", ", m.AufgeloesteAdressen)
            : Loc.T("Keine Adresse gefunden", "No address found"),
        WasEsIst = Loc.T(
            "Übersetzt den eingegebenen Namen in die Adresse, mit der der PC tatsächlich Kontakt aufnimmt.",
            "Translates the entered name into the address the PC actually contacts."),
        Empfehlung = m.NamensaufloesungOk
            ? Loc.T("Funktioniert.", "Works.")
            : Loc.T(
                  "Prüfe die Schreibweise der Adresse. Klappt es bei anderen Adressen (z.B. google.de), " +
                  "liegt es an dieser einen Adresse oder ihrem Namensdienst - nicht am eigenen Internet.",
                  "Check the spelling of the address. If other addresses (e.g. google.com) work, the " +
                  "problem is with this one address or its name service - not your own internet connection.")
    };

    private static NetzwerkSchritt HostsSchritt(ZielMesswerte m)
    {
        var hatUeberschreibung = m.HostsAdresse is not null;

        return new NetzwerkSchritt
        {
            Titel = Loc.T("Eigene Umleitung (Hosts-Datei)", "Local override (hosts file)"),
            Ampel = hatUeberschreibung ? AmpelStatus.Gelb : AmpelStatus.Gruen,
            Ergebnis = hatUeberschreibung
                ? Loc.T($"Dieser PC leitet \"{m.Ziel}\" fest auf {m.HostsAdresse} um",
                        $"This PC hard-redirects \"{m.Ziel}\" to {m.HostsAdresse}")
                : Loc.T("Keine eigene Umleitung eingetragen", "No local override entered"),
            WasEsIst = Loc.T(
                "Die Hosts-Datei ist eine kleine Liste in Windows, die - falls ein Eintrag vorhanden ist - " +
                "IMMER Vorrang vor der normalen Namensauflösung hat, auch nach einem Neustart oder " +
                "Browser-Cache-Löschen.",
                "The hosts file is a small list in Windows that - if an entry exists - ALWAYS takes " +
                "priority over normal name resolution, even after a restart or clearing the browser cache."),
            Empfehlung = hatUeberschreibung
                ? Loc.T(
                      "Falls diese Umleitung nicht bewusst eingerichtet wurde (z.B. von einem Programm " +
                      "oder einem früheren Test), lässt sie sich mit einem Texteditor (als Administrator) " +
                      "in C:\\Windows\\System32\\drivers\\etc\\hosts entfernen.",
                      "If this override wasn't set up on purpose (e.g. by a program or an earlier test), " +
                      "it can be removed with a text editor (as administrator) in " +
                      "C:\\Windows\\System32\\drivers\\etc\\hosts.")
                : Loc.T("Nichts zu tun.", "Nothing to do.")
        };
    }

    private static NetzwerkSchritt ProxySchritt(ZielMesswerte m) => new()
    {
        Titel = Loc.T("Proxy-Einstellungen", "Proxy settings"),
        Ampel = m.ProxyAktiv ? AmpelStatus.Gelb : AmpelStatus.Gruen,
        Ergebnis = m.ProxyAktiv
            ? Loc.T($"Ein Proxy ist eingetragen: {m.ProxyServer}", $"A proxy is configured: {m.ProxyServer}")
            : Loc.T("Kein Proxy eingetragen", "No proxy configured"),
        WasEsIst = Loc.T(
            "Ein Proxy ist eine Zwischenstation, über die der Internetverkehr laufen kann, statt direkt " +
            "zum Ziel zu gehen - üblich z.B. in Firmennetzen.",
            "A proxy is a relay that internet traffic can be routed through instead of going directly to " +
            "the target - common e.g. in company networks."),
        Empfehlung = m.ProxyAktiv
            ? Loc.T(
                  "Ein Proxy kann Verbindungen zu Adressen im eigenen lokalen Netzwerk (z.B. 192.168.x.x) " +
                  "stören, weil er versucht, sie erst nach draußen zu schicken. Windows-Einstellungen → " +
                  "Netzwerk und Internet → Proxy prüfen, ob das gewollt ist.",
                  "A proxy can interfere with connections to addresses on your own local network (e.g. " +
                  "192.168.x.x) because it tries to route them outward first. Check Windows Settings → " +
                  "Network & Internet → Proxy for whether this is intended.")
            : Loc.T("Nichts zu tun.", "Nothing to do.")
    };

    private static NetzwerkSchritt TcpSchritt(ZielMesswerte m)
    {
        var titel = Loc.T($"Verbindung zu Port {m.GeprueftePort}", $"Connection to port {m.GeprueftePort}");
        var wasEsIst = Loc.T(
            "Der eigentliche Verbindungsaufbau zum Ziel, so wie es ein Browser auch versuchen würde.",
            "The actual connection attempt to the target, the same way a browser would try it.");

        if (!m.NamensaufloesungOk)
        {
            return new NetzwerkSchritt
            {
                Titel = titel,
                Ampel = AmpelStatus.Gelb,
                Ergebnis = Loc.T("Übersprungen - keine Adresse zum Testen", "Skipped - no address to test"),
                IstFolgefehler = true,
                WasEsIst = wasEsIst,
                Empfehlung = Loc.T(
                    "Kann erst getestet werden, wenn die Namensauflösung oben funktioniert.",
                    "Can only be tested once name resolution above works.")
            };
        }

        return new NetzwerkSchritt
        {
            Titel = titel,
            Ampel = m.TcpErreichbar ? AmpelStatus.Gruen : AmpelStatus.Rot,
            Ergebnis = m.TcpErreichbar
                ? Loc.T($"Antwortet in {m.TcpZeitMs} ms", $"Responds in {m.TcpZeitMs} ms")
                : Loc.T($"Keine Antwort{(m.TcpFehler is null ? "" : $" ({m.TcpFehler})")}",
                        $"No response{(m.TcpFehler is null ? "" : $" ({m.TcpFehler})")}"),
            WasEsIst = wasEsIst,
            Empfehlung = m.TcpErreichbar
                ? Loc.T(
                      "Die reine Netzwerkverbindung funktioniert. Lädt eine Webseite trotzdem nicht, liegt " +
                      "es meist an einem einzelnen Programm (Browser-Erweiterung, Sicherheitssoftware, ein " +
                      "bestimmter Browser-Cache) - nicht mehr am Netzwerk selbst.",
                      "The raw network connection works. If a webpage still doesn't load, it's usually one " +
                      "specific program (browser extension, security software, a particular browser " +
                      "cache) - not the network itself anymore.")
                : Loc.T(
                      "Hier hakt es wirklich: Der PC bekommt keine Antwort von diesem Ziel auf diesem Port. " +
                      "Prüfe, ob das Ziel gerade erreichbar ist (Server läuft, richtiges Netzwerk/VPN " +
                      "aktiv), oder ob eine Firewall/Sicherheitssoftware auf diesem PC genau diese " +
                      "Verbindung blockiert.",
                      "This is where it really snags: the PC gets no response from this target on this " +
                      "port. Check whether the target is currently reachable (server running, correct " +
                      "network/VPN active), or whether a firewall/security program on this PC is blocking " +
                      "exactly this connection.")
        };
    }
}
