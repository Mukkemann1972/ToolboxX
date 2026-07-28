using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class NetzwerkView : UserControl
{
    private readonly NetzwerkService _dienst = new();
    private readonly PortProgrammService _portDienst = new();
    private readonly VerbindungsUeberwachungService _ueberwachungDienst = new();
    private readonly List<VerbindungsMessung> _messungen = [];

    public NetzwerkView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Netzwerk-Diagnose", "Network diagnostics");
        QuelleHinweis.ToolTip = Loc.T(
            "Eigene Messungen: Ping/DNS-Abfragen und Ermittlung des Standard-Gateways (.NET-Netzwerk-APIs) - Schritt für Schritt bis zum Internet, nichts wird verändert.",
            "Our own measurements: ping/DNS queries and determining the default gateway (.NET network APIs) - step by step to the internet, nothing is changed.");
        EinleitungText.Text = Loc.T(
            "Prüft die Kette vom PC bis ins Internet Schritt für Schritt durch und sagt dir, an welcher Stelle es hakt - und was du dagegen tun kannst. Es wird nur gemessen, nichts verändert.",
            "Checks the chain from the PC to the internet step by step and tells you where it snags - and what you can do about it. Only measurements are taken, nothing is changed.");
        PruefenButton.Content = Loc.T("Erneut prüfen", "Check again");
        VerbindungenBereich.Header = Loc.T("Wer redet gerade mit draußen?", "Who's currently talking to the outside?");

        UeberwachungBereich.Header = Loc.T("Dauerhafte Verbindungsüberwachung", "Continuous connection monitoring");
        UeberwachungErklaerung.Text = Loc.T(
            "Die Prüfung oben ist eine Momentaufnahme - \"das Internet geht manchmal weg\" lässt sich damit " +
            "grundsätzlich nicht finden. Hier wird alle 5 Sekunden geprüft und protokolliert, solange dieses " +
            "Modul offen bleibt, damit auch kurze, sporadische Aussetzer sichtbar werden.",
            "The check above is a snapshot - \"the internet sometimes drops\" can't be found with that by " +
            "design. Here it's checked and logged every 5 seconds for as long as this module stays open, so " +
            "even short, sporadic dropouts become visible.");
        UeberwachungButton.Content = Loc.T("Überwachung starten", "Start monitoring");

        Loaded += async (_, _) => await PruefenAsync();
        Loaded += async (_, _) => await VerbindungenLadenAsync();
        Unloaded += (_, _) => _ueberwachungDienst.Stoppen();
    }

    private void Ueberwachung_Click(object sender, RoutedEventArgs e)
    {
        if (_ueberwachungDienst.LaeuftGerade)
        {
            _ueberwachungDienst.Stoppen();
            UeberwachungButton.Content = Loc.T("Überwachung starten", "Start monitoring");
            return;
        }

        _messungen.Clear();
        AusfallListe.ItemsSource = null;
        UeberwachungButton.Content = Loc.T("Überwachung stoppen", "Stop monitoring");
        UeberwachungStatus.Text = Loc.T("Erste Messung folgt in Kürze …", "First measurement coming shortly…");

        _ueberwachungDienst.Starten(TimeSpan.FromSeconds(5), messung =>
            Dispatcher.Invoke(() => VerarbeiteMessung(messung)));
    }

    private void VerarbeiteMessung(VerbindungsMessung messung)
    {
        _messungen.Add(messung);
        var auswertung = VerbindungsUeberwachung.Auswerten(_messungen);

        UeberwachungStatus.Text = Loc.T(
            $"{auswertung.AnzahlMessungen} Prüfung(en) seit dem Start, {auswertung.AnzahlAusfaelle} davon ohne Antwort. " +
            $"Zuletzt {messung.Zeitpunkt:HH:mm:ss}: {(messung.Erfolgreich ? "✅ erreichbar" : "⚠️ keine Antwort")}.",
            $"{auswertung.AnzahlMessungen} check(s) since starting, {auswertung.AnzahlAusfaelle} without a response. " +
            $"Last at {messung.Zeitpunkt:HH:mm:ss}: {(messung.Erfolgreich ? "✅ reachable" : "⚠️ no response")}.");

        AusfallListe.ItemsSource = auswertung.AusfallPerioden
            .Select(p => p.Dauer is { } dauer
                ? Loc.T($"Ausfall {p.Start:HH:mm:ss} – {p.Ende:HH:mm:ss} ({dauer.TotalSeconds:0} Sekunden)",
                        $"Outage {p.Start:HH:mm:ss} – {p.Ende:HH:mm:ss} ({dauer.TotalSeconds:0} seconds)")
                : Loc.T($"Ausfall seit {p.Start:HH:mm:ss} - dauert noch an",
                        $"Outage since {p.Start:HH:mm:ss} - still ongoing"))
            .Reverse()
            .ToList();
    }

    private async Task VerbindungenLadenAsync()
    {
        try
        {
            var verbindungen = await _portDienst.LadeAsync();

            if (verbindungen.Count == 0)
            {
                VerbindungenHinweis.Text = Loc.T(
                    "Gerade keine aktiven Verbindungen nach draußen gefunden.",
                    "No active connections to the outside found right now.");
                VerbindungenListe.ItemsSource = null;
                return;
            }

            VerbindungenHinweis.Text = Loc.T(
                $"{verbindungen.Count} aktive Verbindung(en) - eine Momentaufnahme, das ändert sich laufend. " +
                "Ein unbekannter Programmname bei einer fremden Adresse ist nicht automatisch verdächtig " +
                "(z.B. Cloud-Sync, Updates, Hintergrunddienste), aber ein guter Ausgangspunkt zum Nachschauen.",
                $"{verbindungen.Count} active connection(s) - a snapshot, this changes constantly. " +
                "An unfamiliar program name talking to an unfamiliar address isn't automatically suspicious " +
                "(e.g. cloud sync, updates, background services), but it's a good starting point to look into.");

            VerbindungenListe.ItemsSource = verbindungen
                .Select(v => $"{v.Programm} (PID {v.Pid})  →  {v.Protokoll}  {v.RemoteAdresse}:{v.RemotePort}")
                .ToList();
        }
        catch (Exception ex)
        {
            DiagnoseLog.Schreiben(nameof(NetzwerkView) + ".Verbindungen", ex.ToString());
        }
    }

    private async Task PruefenAsync()
    {
        PruefenButton.IsEnabled = false;
        UrteilText.Text = Loc.T("Prüfe die Verbindung …", "Checking the connection…");
        AdapterText.Text = "";

        var messwerte = await _dienst.MessenAsync();
        var schritte = NetzwerkAuswerter.Auswerten(messwerte);

        Liste.ItemsSource = schritte.Select(s => new NetzwerkZeile(s, FarbeFuer)).ToList();
        UrteilText.Text = NetzwerkAuswerter.Gesamturteil(schritte);

        var adapterZeilen = new List<string>();

        if (messwerte.DnsServer.Length > 0)
            adapterZeilen.Add(Loc.T($"Namensdienst laut Router: {string.Join(", ", messwerte.DnsServer)}",
                                    $"Name service according to the router: {string.Join(", ", messwerte.DnsServer)}"));

        if (messwerte.WlanSignalProzent is { } signal)
        {
            var (text, _) = WlanSignalDeutung.Deuten(signal);
            adapterZeilen.Add(Loc.T($"WLAN-Signalstärke: {text}", $"Wi-Fi signal strength: {text}"));
        }

        if (messwerte.WeitereAdapter.Length > 0)
            adapterZeilen.Add(Loc.T(
                $"Weitere aktive Adapter (VPN/virtuell, nicht Teil der Prüfung oben): {string.Join(", ", messwerte.WeitereAdapter)}",
                $"Other active adapters (VPN/virtual, not part of the check above): {string.Join(", ", messwerte.WeitereAdapter)}"));

        AdapterText.Text = string.Join("   ·   ", adapterZeilen);

        PruefenButton.IsEnabled = true;
    }

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private async void Pruefen_Click(object sender, RoutedEventArgs e) => await PruefenAsync();
}

public class NetzwerkZeile
{
    public NetzwerkZeile(NetzwerkSchritt schritt, Func<AmpelStatus, Brush> farbe)
    {
        Titel = schritt.Titel;
        Ergebnis = schritt.Ergebnis;
        WasEsIst = schritt.WasEsIst;
        Empfehlung = schritt.Empfehlung;
        Symbol = schritt.Ampel.Symbol();
        AmpelFarbe = farbe(schritt.Ampel);
    }

    public string Titel { get; }
    public string Ergebnis { get; }
    public string WasEsIst { get; }
    public string Empfehlung { get; }
    public string Symbol { get; }
    public Brush AmpelFarbe { get; }
}
