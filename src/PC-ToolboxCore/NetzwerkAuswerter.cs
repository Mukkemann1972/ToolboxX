using PCToolbox.Models;
using PCToolboxCore.Models;

namespace PCToolboxCore;

/// <summary>Ein Prüfschritt der Netzwerkdiagnose mit Ergebnis und Klartext.</summary>
public class NetzwerkSchritt
{
    public string Titel { get; init; } = "";
    public AmpelStatus Ampel { get; init; }

    /// <summary>Das gemessene Ergebnis in einer Zeile (z.B. die IP-Adresse).</summary>
    public string Ergebnis { get; init; } = "";

    public string WasEsIst { get; init; } = "";
    public string Empfehlung { get; init; } = "";

    /// <summary>True, wenn dieser Schritt nur deshalb scheitert, weil vorher schon etwas kaputt war.</summary>
    public bool IstFolgefehler { get; init; }
}

/// <summary>
/// Beantwortet die häufigste Frage überhaupt: "Das Internet geht nicht - woran liegt's?"
///
/// Der Trick ist die Reihenfolge. Das Netzwerk ist eine Kette: Netzwerkkarte → Adresse
/// vom Router → Router erreichbar → Namen werden aufgelöst → Internet. Reisst die Kette
/// an einer Stelle, meldet danach ALLES einen Fehler. Genau deshalb sind die üblichen
/// Diagnose-Tools so nutzlos: Sie zeigen fünf rote Kreuze, und man weiss trotzdem nicht,
/// wo man anfangen soll.
///
/// Hier bekommt nur die ERSTE gerissene Stelle ein rotes Kreuz und die Handlungsanweisung.
/// Alles danach wird ehrlich als Folgefehler ausgewiesen - eine Baustelle, nicht fünf.
/// </summary>
public static class NetzwerkAuswerter
{
    /// <summary>Adressbereich, den Windows sich selbst gibt, wenn kein Router antwortet.</summary>
    private const string SelbstvergebenerBereich = "169.254.";

    public static List<NetzwerkSchritt> Auswerten(NetzwerkMesswerte m)
    {
        var schritte = new List<NetzwerkSchritt>();
        var problemGefunden = false;

        void Hinzu(string titel, bool inOrdnung, string ergebnis, string wasEsIst, string empfehlung)
        {
            if (inOrdnung)
            {
                schritte.Add(new NetzwerkSchritt
                {
                    Titel = titel,
                    Ampel = AmpelStatus.Gruen,
                    Ergebnis = ergebnis,
                    WasEsIst = wasEsIst,
                    Empfehlung = empfehlung
                });
                return;
            }

            var folge = problemGefunden;
            problemGefunden = true;

            schritte.Add(new NetzwerkSchritt
            {
                Titel = titel,
                // Folgefehler bewusst gelb: Es ist kein eigenes Problem, sondern dasselbe
                // Problem nochmal. Fünf rote Kreuze helfen niemandem beim Anfangen.
                Ampel = folge ? AmpelStatus.Gelb : AmpelStatus.Rot,
                Ergebnis = ergebnis,
                WasEsIst = wasEsIst,
                IstFolgefehler = folge,
                Empfehlung = folge
                    ? Loc.T(
                          "Das ist aller Voraussicht nach nur eine Folge des Problems weiter oben. " +
                          "Kümmere dich zuerst um den ersten roten Punkt - erledigt sich der, ist " +
                          "dieser Punkt meist von allein mit erledigt.",
                          "This is most likely just a consequence of the problem above. " +
                          "Take care of the first red point first - once that's resolved, " +
                          "this point is usually resolved along with it.")
                    : empfehlung
            });
        }

        // --- 1. Netzwerkkarte -------------------------------------------------------
        Hinzu(Loc.T("Netzwerkverbindung", "Network connection"),
            m.AdapterVorhanden,
            m.AdapterVorhanden
                ? Loc.T($"{m.AdapterName} ({(m.IstWlan ? "WLAN" : "Kabel")})", $"{m.AdapterName} ({(m.IstWlan ? "Wi-Fi" : "cable")})")
                : Loc.T("Keine aktive Verbindung gefunden", "No active connection found"),
            Loc.T(
                "Die Netzwerkkarte ist das Bauteil, mit dem dein PC überhaupt erst mit Kabel oder " +
                "WLAN Kontakt aufnimmt.",
                "The network adapter is the component your PC uses to make contact via cable or " +
                "Wi-Fi in the first place."),
            Loc.T(
                "Prüfe bei Kabel: Steckt es auf beiden Seiten fest, leuchtet am Router die Buchse? " +
                "Prüfe bei WLAN: Ist das WLAN am PC eingeschaltet (Taste am Notebook oder " +
                "Flugmodus in Windows) und bist du mit dem richtigen Netz verbunden?",
                "For cable: Is it plugged in firmly on both ends, is the port on the router lit up? " +
                "For Wi-Fi: Is Wi-Fi turned on on the PC (a key on the notebook or " +
                "airplane mode in Windows), and are you connected to the right network?"));

        // --- 2. Adresse vom Router --------------------------------------------------
        var hatAdresse = !string.IsNullOrWhiteSpace(m.IpAdresse);
        var selbstVergeben = m.IpAdresse?.StartsWith(SelbstvergebenerBereich, StringComparison.Ordinal) == true;

        Hinzu(Loc.T("Adresse vom Router", "Address from the router"),
            hatAdresse && !selbstVergeben,
            hatAdresse ? m.IpAdresse! : Loc.T("Keine Adresse erhalten", "No address received"),
            Loc.T(
                "Dein Router teilt jedem Gerät im Haushalt eine eigene Nummer zu - ohne die kann " +
                "dein PC niemanden erreichen, so wie ein Brief ohne Absender.",
                "Your router assigns every device in the household its own number - without it, " +
                "your PC can't reach anyone, like a letter without a sender address."),
            selbstVergeben
                ? Loc.T(
                      "Dein PC hat sich eine Notfall-Adresse selbst gegeben (sie beginnt mit 169.254). " +
                      "Das heisst immer dasselbe: Der Router hat nicht geantwortet. Starte zuerst den " +
                      "Router neu (Stecker ziehen, 10 Sekunden warten, wieder rein) und danach den PC. " +
                      "Hilft das nicht, liegt es meist am Kabel oder daran, dass du im falschen WLAN bist.",
                      "Your PC has given itself an emergency address (it starts with 169.254). " +
                      "This always means the same thing: the router didn't respond. First restart the " +
                      "router (unplug it, wait 10 seconds, plug it back in) and then the PC. " +
                      "If that doesn't help, it's usually the cable or being connected to the wrong Wi-Fi.")
                : Loc.T(
                      "Dein PC hat gar keine Adresse bekommen. Starte den Router neu (Stecker ziehen, " +
                      "10 Sekunden warten) und danach den PC. Bleibt es dabei, probiere ein anderes " +
                      "Netzwerkkabel oder eine andere Buchse am Router.",
                      "Your PC didn't get an address at all. Restart the router (unplug it, " +
                      "wait 10 seconds) and then the PC. If it persists, try a different " +
                      "network cable or a different port on the router."));

        // --- 3. Router erreichbar ---------------------------------------------------
        var hatGateway = !string.IsNullOrWhiteSpace(m.Gateway);

        Hinzu(Loc.T("Router erreichbar", "Router reachable"),
            hatGateway && m.GatewayErreichbar,
            !hatGateway
                ? Loc.T("Kein Router eingetragen", "No router registered")
                : m.GatewayErreichbar
                    ? Loc.T($"{m.Gateway} antwortet{(m.GatewayAntwortzeitMs is { } ms ? $" in {ms} ms" : "")}",
                            $"{m.Gateway} responds{(m.GatewayAntwortzeitMs is { } ms2 ? $" in {ms2} ms" : "")}")
                    : Loc.T($"{m.Gateway} antwortet nicht", $"{m.Gateway} does not respond"),
            Loc.T(
                "Der Router ist die Tür nach draussen. Alles, was ins Internet geht, läuft über ihn.",
                "The router is the door to the outside. Everything going to the internet passes through it."),
            Loc.T(
                "Dein PC weiss, wo der Router ist, bekommt aber keine Antwort. Schau, ob am Router " +
                "die Lämpchen normal leuchten, und starte ihn neu (Stecker ziehen, 10 Sekunden warten). " +
                "Bei WLAN kann es auch schlicht zu weit weg sein - geh näher ran und probiere es nochmal.",
                "Your PC knows where the router is but gets no response. Check whether the router's " +
                "lights look normal, and restart it (unplug it, wait 10 seconds). " +
                "On Wi-Fi it might simply be too far away - move closer and try again."));

        // --- 4. Namensauflösung -----------------------------------------------------
        Hinzu(Loc.T("Namen werden aufgelöst", "Names are being resolved"),
            m.NamensaufloesungOk,
            m.NamensaufloesungOk ? Loc.T("Funktioniert", "Works") : Loc.T("Schlägt fehl", "Fails"),
            Loc.T(
                "Ein Namensdienst übersetzt Adressen wie \"tagesschau.de\" in die Nummern, mit denen " +
                "Computer tatsächlich arbeiten - eine Art Telefonbuch fürs Internet.",
                "A name service translates addresses like \"example.com\" into the numbers that " +
                "computers actually work with - a kind of phone book for the internet."),
            Loc.T(
                "Das ist der klassische Fall \"Internet ist da, aber keine Seite lädt\". Meist hilft " +
                "schon ein Neustart des Routers. Bleibt es dabei, ist im Router oft ein falscher oder " +
                "abgeschalteter Namensdienst eingetragen - das lässt sich in dessen Einstellungen ändern.",
                "This is the classic \"internet is there, but no page loads\" case. Usually " +
                "just restarting the router helps. If it persists, the router often has a wrong or " +
                "disabled name service configured - this can be changed in its settings."));

        // --- 5. Internet ------------------------------------------------------------
        var internetOk = m.PingInsInternetOk || m.NamensaufloesungOk;

        Hinzu(Loc.T("Internet erreichbar", "Internet reachable"),
            internetOk,
            internetOk
                ? m.PingInsInternetOk ? Loc.T("Antwort aus dem Internet erhalten", "Response received from the internet") : Loc.T("Erreichbar (Testanfragen werden geblockt)", "Reachable (test requests are blocked)")
                : Loc.T("Keine Antwort aus dem Internet", "No response from the internet"),
            Loc.T(
                "Der letzte Schritt: Kommt dein PC über den Router hinaus wirklich ins Internet?",
                "The last step: does your PC actually reach the internet beyond the router?"),
            Loc.T(
                "Dein Heimnetz funktioniert, aber hinter dem Router geht es nicht weiter. Das liegt " +
                "fast immer am Anschluss selbst, nicht an deinem PC. Prüfe am Router, ob die " +
                "Internet-Leuchte normal ist, und schau beim Anbieter nach einer Störung " +
                "(oft am Handy über das Mobilfunknetz möglich).",
                "Your home network works, but it doesn't continue past the router. This is " +
                "almost always the connection itself, not your PC. Check on the router whether the " +
                "internet light looks normal, and check with your provider for an outage " +
                "(often possible on your phone via the mobile network)."));

        return schritte;
    }

    /// <summary>Ein Satz Gesamturteil, der oben im Modul steht.</summary>
    public static string Gesamturteil(IEnumerable<NetzwerkSchritt> schritte)
    {
        var liste = schritte.ToList();
        var ersterFehler = liste.FirstOrDefault(s => s.Ampel == AmpelStatus.Rot);

        if (ersterFehler is null)
            return liste.Any(s => s.Ampel == AmpelStatus.Gelb)
                ? Loc.T("Grundsätzlich in Ordnung - einzelne Punkte solltest du dir unten ansehen.",
                        "Basically fine - you should look at a few points below.")
                : Loc.T("Alles in Ordnung: Dein PC ist im Netz und kommt ins Internet.",
                        "Everything is fine: your PC is on the network and reaches the internet.");

        return Loc.T($"Hier hakt es: {ersterFehler.Titel}. Was zu tun ist, steht unten bei diesem Punkt.",
                     $"Here's where it snags: {ersterFehler.Titel}. What to do is listed below at this point.");
    }
}
