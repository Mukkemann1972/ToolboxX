using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using PCToolbox.Services.Pro;
#endif

namespace PCToolbox.Modules;

public partial class NetzwerkView : UserControl
{
    private readonly NetzwerkService _dienst = new();
    private readonly PortProgrammService _portDienst = new();
    private readonly VerbindungsUeberwachungService _ueberwachungDienst = new();
    private readonly ZielDiagnoseService _zielDienst = new();
    private readonly List<VerbindungsMessung> _messungen = [];

#if PRO_BUILD
    private readonly WlanDetailsService _wlanDienst = new();
    private TextBlock _wlanStatusText = null!;
    private ItemsControl _wlanPasswortListe = null!;
    private ItemsControl _wlanKanalListe = null!;
#endif

    public NetzwerkView()
    {
        InitializeComponent();

#if PRO_BUILD
        WlanProBereichAufbauen();
#endif

        TitelText.Text = Loc.T("Netzwerk-Diagnose", "Network diagnostics");
        QuelleHinweis.ToolTip = Loc.T(
            "Eigene Messungen: Ping/DNS-Abfragen und Ermittlung des Standard-Gateways (.NET-Netzwerk-APIs) - Schritt für Schritt bis zum Internet, nichts wird verändert.",
            "Our own measurements: ping/DNS queries and determining the default gateway (.NET network APIs) - step by step to the internet, nothing is changed.");
        EinleitungText.Text = Loc.T(
            "Prüft die Kette vom PC bis ins Internet Schritt für Schritt durch und sagt dir, an welcher Stelle es hakt - und was du dagegen tun kannst. Es wird nur gemessen, nichts verändert.",
            "Checks the chain from the PC to the internet step by step and tells you where it snags - and what you can do about it. Only measurements are taken, nothing is changed.");
        PruefenButton.Content = Loc.T("Erneut prüfen", "Check again");
        VerbindungenBereich.Header = Loc.T("Wer redet gerade mit draußen?", "Who's currently talking to the outside?");

        ZielDiagnoseBereich.Header = Loc.T("Eine bestimmte Adresse prüfen", "Check a specific address");
        ZielErklaerung.Text = Loc.T(
            "Die Prüfung oben beantwortet \"geht mein Internet überhaupt\" - hier geht es um die andere, " +
            "genauso häufige Frage: \"Alles andere klappt, aber DIESE EINE Seite lädt nicht - woran " +
            "liegt's?\" Adresse eingeben (z.B. mail.deinefirma.de), es wird nur gemessen, nichts verändert.",
            "The check above answers \"is my internet working at all\" - this is the other, equally common " +
            "question: \"everything else works, but THIS ONE site won't load - why?\" Enter an address " +
            "(e.g. mail.yourcompany.com), only measurements are taken, nothing is changed.");
        ZielEingabe.Text = "";
        ZielPruefenButton.Content = Loc.T("Prüfen", "Check");

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
#if PRO_BUILD
        Loaded += async (_, _) => await WlanProLadenAsync();
#endif
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

    private async void ZielPruefen_Click(object sender, RoutedEventArgs e) => await ZielPruefenAsync();

    private async Task ZielPruefenAsync()
    {
        var eingabe = ZielEingabe.Text.Trim();
        if (eingabe.Length == 0)
        {
            ZielUrteilText.Text = Loc.T("Bitte eine Adresse eingeben.", "Please enter an address.");
            return;
        }

        var ziel = ZielAusEingabe(eingabe);

        ZielPruefenButton.IsEnabled = false;
        ZielUrteilText.Text = Loc.T($"Prüfe {ziel} …", $"Checking {ziel}…");
        ZielErgebnisListe.ItemsSource = null;

        try
        {
            var messwerte = await _zielDienst.PruefenAsync(ziel);
            var schritte = ZielDiagnoseAuswerter.Auswerten(messwerte);

            ZielErgebnisListe.ItemsSource = schritte.Select(s => new NetzwerkZeile(s, FarbeFuer)).ToList();
            ZielUrteilText.Text = NetzwerkAuswerter.Gesamturteil(schritte);
        }
        catch (Exception ex)
        {
            DiagnoseLog.Schreiben(nameof(NetzwerkView) + ".ZielDiagnose", ex.ToString());
            ZielUrteilText.Text = Loc.T("Prüfung fehlgeschlagen - siehe Diagnose-Log.", "Check failed - see diagnostic log.");
        }
        finally
        {
            ZielPruefenButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Erlaubt neben der nackten Adresse ("mail.firma.de") auch eine komplette URL
    /// ("https://mail.firma.de/login") - genau so, wie man sie sich meist aus dem
    /// Browser kopiert. DNS/TCP brauchen nur den reinen Hostnamen.
    /// </summary>
    private static string ZielAusEingabe(string eingabe)
    {
        if (Uri.TryCreate(eingabe, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return uri.Host;

        var ohneSchema = eingabe.Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace("https://", "", StringComparison.OrdinalIgnoreCase);
        var trennerIndex = ohneSchema.IndexOfAny(['/', '\\', '?']);
        return trennerIndex >= 0 ? ohneSchema[..trennerIndex] : ohneSchema;
    }

#if PRO_BUILD
    /// <summary>
    /// Baut den WLAN-Pro-Bereich zur Laufzeit in ProZusatzBereich ein, statt ihn fest in
    /// NetzwerkView.xaml zu verdrahten - so bleibt die XAML-Datei fuer Free/Pro identisch,
    /// genau wie beim USB-Geraete-Modul.
    /// </summary>
    private void WlanProBereichAufbauen()
    {
        var expander = new Expander
        {
            Header = Loc.T("WLAN-Details (Pro)", "Wi-Fi details (Pro)"),
            Margin = new Thickness(0, 4, 0, 0)
        };

        var inhalt = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Gespeicherte WLAN-Profile dieses Geräts (Windows-Bordmittel \"netsh wlan\") samt Passwort im " +
                "Klartext - nur zum eigenen Nachschlagen, z.B. um einem Gast das Passwort zu geben, ohne den " +
                "Router aufzusuchen. Es werden keine fremden Anmeldedaten (Browser, Mail, andere Programme) " +
                "gelesen, nur die eigenen gespeicherten WLAN-Netze.",
                "Wi-Fi profiles saved on this device (Windows built-in \"netsh wlan\") including the password " +
                "in plain text - just for your own reference, e.g. to give a guest the password without going " +
                "to the router. No other saved credentials (browser, mail, other apps) are read, only this " +
                "device's own saved Wi-Fi networks."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        _wlanStatusText = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
        inhalt.Children.Add(_wlanStatusText);

        _wlanPasswortListe = new ItemsControl();
        var zeileFactory = new FrameworkElementFactory(typeof(DockPanel));
        zeileFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));

        var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
        nameFactory.SetBinding(TextBlock.TextProperty, new Binding("Name"));
        nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        nameFactory.SetValue(DockPanel.DockProperty, Dock.Top);
        zeileFactory.AppendChild(nameFactory);

        var passwortZeileFactory = new FrameworkElementFactory(typeof(StackPanel));
        passwortZeileFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var anzeigenButtonFactory = new FrameworkElementFactory(typeof(Button));
        anzeigenButtonFactory.SetValue(ContentControl.ContentProperty, Loc.T("Anzeigen", "Reveal"));
        anzeigenButtonFactory.SetValue(Button.PaddingProperty, new Thickness(8, 3, 8, 3));
        anzeigenButtonFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        anzeigenButtonFactory.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)WlanPasswortAnzeigen_Click);
        passwortZeileFactory.AppendChild(anzeigenButtonFactory);

        var passwortTextFactory = new FrameworkElementFactory(typeof(TextBlock));
        passwortTextFactory.SetBinding(TextBlock.TextProperty, new Binding("PasswortAnzeige"));
        passwortTextFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas"));
        passwortTextFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        passwortZeileFactory.AppendChild(passwortTextFactory);

        zeileFactory.AppendChild(passwortZeileFactory);
        _wlanPasswortListe.ItemTemplate = new DataTemplate { VisualTree = zeileFactory };
        inhalt.Children.Add(_wlanPasswortListe);

        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T("Kanal-Belegung sichtbarer Netze", "Channel usage of visible networks"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 14, 0, 4)
        });
        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Zeigt, wie viele Netze in der Umgebung sich denselben Kanal teilen - viele Netze auf einem " +
                "Kanal können das eigene WLAN ausbremsen. Kein automatischer Kanal-Vorschlag, dafür fehlen zu " +
                "viele Faktoren (Wände, Nachbarn, Störquellen).",
                "Shows how many nearby networks share the same channel - many networks on one channel can slow " +
                "your own Wi-Fi down. No automatic channel recommendation, too many factors are missing for " +
                "that (walls, neighbors, interference)."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        _wlanKanalListe = new ItemsControl();
        var kanalZeileFactory = new FrameworkElementFactory(typeof(TextBlock));
        kanalZeileFactory.SetBinding(TextBlock.TextProperty, new Binding("Anzeige"));
        kanalZeileFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 4));
        kanalZeileFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        _wlanKanalListe.ItemTemplate = new DataTemplate { VisualTree = kanalZeileFactory };
        inhalt.Children.Add(_wlanKanalListe);

        var neuLadenButton = new Button
        {
            Content = Loc.T("Neu einlesen", "Reload"),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        neuLadenButton.Click += async (_, _) => await WlanProLadenAsync();
        inhalt.Children.Add(neuLadenButton);

        expander.Content = inhalt;
        ProZusatzBereich.Children.Add(expander);
    }

    private async Task WlanProLadenAsync()
    {
        _wlanStatusText.Text = Loc.T("Lese WLAN-Profile ein …", "Reading Wi-Fi profiles…");

        var gespeicherte = await _wlanDienst.GespeicherteNetzeLadenAsync();
        _wlanPasswortListe.ItemsSource = gespeicherte
            .Select(n => new WlanPasswortZeile(n.Name, n.Passwort))
            .ToList();

        var sichtbare = await _wlanDienst.SichtbareNetzeLadenAsync();
        var kanaele = WlanDetails.KanaeleAuswerten(sichtbare);
        _wlanKanalListe.ItemsSource = kanaele
            .Select(k => new WlanKanalZeile
            {
                Anzeige = Loc.T(
                    $"{k.Band ?? "?"}, Kanal {k.Kanal}: {k.AnzahlNetze} Netz(e)",
                    $"{k.Band ?? "?"}, channel {k.Kanal}: {k.AnzahlNetze} network(s)")
            })
            .ToList();

        _wlanStatusText.Text = gespeicherte.Count == 0
            ? Loc.T("Keine gespeicherten WLAN-Profile auf diesem Gerät gefunden.",
                    "No saved Wi-Fi profiles found on this device.")
            : Loc.T($"{gespeicherte.Count} gespeicherte(s) Profil(e), {sichtbare.Count} gerade sichtbare(r) Sendepunkt(e).",
                    $"{gespeicherte.Count} saved profile(s), {sichtbare.Count} access point(s) currently visible.");
    }

    private static void WlanPasswortAnzeigen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WlanPasswortZeile zeile }) zeile.UmschaltenSichtbarkeit();
    }
#endif
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

#if PRO_BUILD
public class WlanPasswortZeile : System.ComponentModel.INotifyPropertyChanged
{
    private readonly string? _passwort;
    private bool _sichtbar;

    public WlanPasswortZeile(string name, string? passwort)
    {
        Name = name;
        _passwort = passwort;
    }

    public string Name { get; }

    public string PasswortAnzeige => _passwort is null
        ? Loc.T("(offenes Netz, kein Passwort)", "(open network, no password)")
        : _sichtbar ? _passwort : "••••••••••";

    public void UmschaltenSichtbarkeit()
    {
        if (_passwort is null) return;
        _sichtbar = !_sichtbar;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(PasswortAnzeige)));
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public class WlanKanalZeile
{
    public string Anzeige { get; set; } = "";
}
#endif
