using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using PCToolbox.Services.Pro;
#endif

namespace PCToolbox.Modules;

public partial class DiensteView : UserControl
{
    private readonly DiensteService _dienst = new();
    private List<DienstZeile> _alle = [];

#if PRO_BUILD
    private TextBlock _baselineStatusText = null!;
    private StackPanel _baselineErgebnisListe = null!;
#endif

    public DiensteView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Dienste", "Services");
        QuelleHinweis.ToolTip = Loc.T(
            "Windows-Dienstverwaltung (Service Control Manager) über die ServiceController-API - dieselbe Quelle wie die Dienste-Verwaltung von Windows selbst.",
            "Windows service management (Service Control Manager) via the ServiceController API - the same source as Windows' own Services management console.");
        EinleitungText.Text = Loc.T(
            "Dienste sind Programme, die ohne Fenster im Hintergrund laufen - vom Druckdienst bis zur Datensammlung. Hier siehst du, welche davon dauerhaft mitlaufen, ohne dass sie es müssten.",
            "Services are programs that run in the background without a window - from the print spooler to data collection. Here you see which of them run permanently without needing to.");
        WenigKlicksTitel.Text = Loc.T("🛟 Warum hier so wenig zum Anklicken ist", "🛟 Why there's so little to click here");
        WenigKlicksText.Text = Loc.T(
            "Bei „Dienste optimieren“-Anleitungen aus dem Netz geht Wochen später der Drucker oder das WLAN nicht mehr, und niemand sieht den Zusammenhang. Deshalb: Systemwichtige Dienste bieten wir gar nicht erst zum Ändern an, und empfohlen wird immer „nur bei Bedarf“ statt „abschalten“ - dann startet der Dienst weiterhin, falls ihn doch jemand braucht.",
            "With \"optimize services\" guides from the internet, the printer or Wi-Fi stops working weeks later, and nobody sees the connection. That's why: we don't even offer system-critical services for changing, and we always recommend \"only on demand\" instead of \"disable\" - that way the service still starts if someone does need it.");
        NeuladenButton.Content = Loc.T("Neu einlesen", "Reload");
        AlleAnzeigenBox.Content = Loc.T("Alle Dienste anzeigen (auch die geschützten)", "Show all services (including protected ones)");

#if PRO_BUILD
        BauBaselineBereich();
#endif

        Loaded += async (_, _) => await LadenAsync();
    }

    private async Task LadenAsync()
    {
        NeuladenButton.IsEnabled = false;
        ZusammenfassungText.Text = Loc.T("Lese Dienste ein …", "Loading services…");

        var dienste = await _dienst.LadeAsync();

        _alle = dienste
            .Select(d => new DienstZeile(d, DiensteBewerter.Bewerten(d), FarbeFuer))
            .OrderBy(z => z.Bewertung.Ampel == AmpelStatus.Gelb ? 0 : 1)
            .ThenBy(z => z.Anzeigename, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var vorschlaege = _alle.Count(z => z.Bewertung.Ampel == AmpelStatus.Gelb);
        ZusammenfassungText.Text = DiensteBewerter.Zusammenfassung(_alle.Count, vorschlaege);

        AnzeigeAktualisieren();
        NeuladenButton.IsEnabled = true;

#if PRO_BUILD
        BaselineAnzeigeAktualisieren();
#endif
    }

    /// <summary>
    /// Standardmäßig nur das, wozu wir wirklich etwas zu sagen haben. Eine Liste mit 250
    /// Diensten, von denen 240 "kennen wir nicht" heissen, hilft niemandem.
    /// </summary>
    private void AnzeigeAktualisieren() =>
        Liste.ItemsSource = AlleAnzeigenBox.IsChecked == true
            ? _alle
            : _alle.Where(z => DiensteBewerter.IstVorschlag(z.Dienst.Name)).ToList();

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private async void Neuladen_Click(object sender, RoutedEventArgs e)
    {
        MeldungText.Text = "";
        await LadenAsync();
    }

    private void AlleAnzeigen_Click(object sender, RoutedEventArgs e) => AnzeigeAktualisieren();

    private async void AufBedarf_Click(object sender, RoutedEventArgs e) => await UmstellenAsync(sender, "Manual");

    private async void AufAuto_Click(object sender, RoutedEventArgs e) => await UmstellenAsync(sender, "Auto");

    private async Task UmstellenAsync(object sender, string starttyp)
    {
        if (sender is not Button { Tag: DienstInfo dienst }) return;

        MeldungText.Text = Loc.T("Windows fragt gleich nach einer Bestätigung (Administratorrechte) …", "Windows will ask for confirmation (administrator rights) shortly…");
        MeldungText.Foreground = (Brush)FindResource("TextMutedBrush");

        var (erfolg, meldung) = await _dienst.StarttypAendernAsync(dienst, starttyp);

        MeldungText.Text = meldung;
        MeldungText.Foreground = erfolg ? (Brush)FindResource("OkBrush") : (Brush)FindResource("DangerBrush");

        await LadenAsync();
    }

#if PRO_BUILD
    /// <summary>
    /// Baseline-Vergleich (Pro): ein neuer Dienst seit einem gespeicherten Referenzpunkt ist ein
    /// starkes Indiz für kürzlich installierte Software - gewollt oder nicht. Anderer Fall als
    /// die Free-Funktion oben (Starttyp umstellen): hier geht es nicht um "was läuft gerade
    /// unnötig mit", sondern um "was ist NEU dazugekommen" - klassisches Vorher-Nachher-Prinzip
    /// wie beim Install-Tracker, nur über Wochen/Monate statt Sekunden.
    /// </summary>
    private void BauBaselineBereich()
    {
        var titel = new TextBlock
        {
            Text = Loc.T("Baseline-Vergleich (Pro)", "Baseline comparison (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        };
        var erklaerung = new TextBlock
        {
            Text = Loc.T(
                "Speichert den aktuellen Dienste-Stand als Referenzpunkt. Bei jedem späteren Öffnen zeigt " +
                "dieser Bereich, welche Dienste seitdem NEU dazugekommen sind - ein starkes Indiz für kürzlich " +
                "installierte Software, gewollt oder nicht. Sinnvoller Zeitpunkt zum Speichern: gleich nach " +
                "einer sauberen Neueinrichtung oder zu Beginn einer Reparatur.",
                "Saves the current service state as a reference point. Every time this opens later, this " +
                "section shows which services have been newly added since then - a strong indicator of " +
                "recently installed software, intended or not. A good time to save: right after a clean " +
                "setup, or at the start of a repair."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };

        var speichernButton = new Button
        {
            Content = Loc.T("Aktuellen Stand als Baseline speichern", "Save current state as baseline"),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        speichernButton.Click += (_, _) =>
        {
            DiensteBaselineService.SnapshotSpeichern(_alle.Select(z => z.Dienst));
            BaselineAnzeigeAktualisieren();
        };

        _baselineStatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _baselineErgebnisListe = new StackPanel();

        ProZusatzBereich.Children.Add(titel);
        ProZusatzBereich.Children.Add(erklaerung);
        ProZusatzBereich.Children.Add(speichernButton);
        ProZusatzBereich.Children.Add(_baselineStatusText);
        ProZusatzBereich.Children.Add(_baselineErgebnisListe);
    }

    private void BaselineAnzeigeAktualisieren()
    {
        _baselineErgebnisListe.Children.Clear();

        var vergleich = DiensteBaselineService.Vergleichen(_alle.Select(z => z.Dienst));
        if (vergleich is null)
        {
            _baselineStatusText.Text = Loc.T(
                "Noch keine Baseline gespeichert - mit dem Knopf oben einen Referenzpunkt anlegen.",
                "No baseline saved yet - use the button above to set a reference point.");
            _baselineStatusText.Foreground = (Brush)FindResource("TextMutedBrush");
            return;
        }

        var (ergebnis, erstelltUtc) = vergleich.Value;
        var zeitpunkt = erstelltUtc.ToLocalTime();

        if (ergebnis.Neu.Count == 0 && ergebnis.Entfernt.Count == 0)
        {
            _baselineStatusText.Text = Loc.T(
                $"Baseline vom {zeitpunkt:dd.MM.yyyy HH:mm} - keine Änderungen seitdem.",
                $"Baseline from {zeitpunkt:yyyy-MM-dd HH:mm} - no changes since then.");
            _baselineStatusText.Foreground = (Brush)FindResource("OkBrush");
            return;
        }

        _baselineStatusText.Text = Loc.T(
            $"Baseline vom {zeitpunkt:dd.MM.yyyy HH:mm}: {ergebnis.Neu.Count} neue(r), {ergebnis.Entfernt.Count} entfernte(r) Dienst(e).",
            $"Baseline from {zeitpunkt:yyyy-MM-dd HH:mm}: {ergebnis.Neu.Count} new, {ergebnis.Entfernt.Count} removed service(s).");
        _baselineStatusText.Foreground = (Brush)FindResource(ergebnis.Neu.Count > 0 ? "WarnBrush" : "TextMutedBrush");

        foreach (var eintrag in ergebnis.Neu)
            _baselineErgebnisListe.Children.Add(new TextBlock
            {
                Text = Loc.T($"➕ NEU: {eintrag.Anzeigename} ({eintrag.Name}) · {eintrag.Starttyp}",
                             $"➕ NEW: {eintrag.Anzeigename} ({eintrag.Name}) · {eintrag.Starttyp}"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3),
                TextWrapping = TextWrapping.Wrap
            });

        foreach (var eintrag in ergebnis.Entfernt)
            _baselineErgebnisListe.Children.Add(new TextBlock
            {
                Text = Loc.T($"➖ entfernt: {eintrag.Anzeigename} ({eintrag.Name})",
                             $"➖ removed: {eintrag.Anzeigename} ({eintrag.Name})"),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Margin = new Thickness(0, 0, 0, 3),
                TextWrapping = TextWrapping.Wrap
            });
    }
#endif
}

public class DienstZeile
{
    public DienstZeile(DienstInfo dienst, DienstBewertung bewertung, Func<AmpelStatus, Brush> farbe)
    {
        Dienst = dienst;
        Bewertung = bewertung;
        AmpelFarbe = farbe(bewertung.Ampel);
    }

    public DienstInfo Dienst { get; }
    public DienstBewertung Bewertung { get; }
    public Brush AmpelFarbe { get; }

    public string Anzeigename => Dienst.Anzeigename;
    public string Kurzurteil => Bewertung.Kurzurteil;
    public string WasEsIst => Bewertung.WasEsIst;
    public string Empfehlung => Bewertung.Empfehlung;

    public string Symbol => Bewertung.Geschuetzt ? "🔒" : Bewertung.Ampel.Symbol();

    private bool LaeuftAutomatisch => Dienst.Starttyp.Equals("Auto", StringComparison.OrdinalIgnoreCase);

    public Visibility UmstellenSichtbar =>
        !Bewertung.Geschuetzt && LaeuftAutomatisch ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ZurueckSichtbar =>
        !Bewertung.Geschuetzt && !LaeuftAutomatisch ? Visibility.Visible : Visibility.Collapsed;

    public string AufBedarfBeschriftung => Loc.T("Auf Bedarf stellen", "Set to on-demand");
    public string WiederAutoBeschriftung => Loc.T("Wieder automatisch", "Automatic again");

    public string SchutzHinweis => Bewertung.Geschuetzt
        ? Loc.T("geschützt", "protected")
        : Loc.T("Windows fragt nach Bestätigung", "Windows will ask for confirmation");

    public string Herkunft =>
        Loc.T($"{Dienst.StarttypKlartext}   ·   {(Dienst.Laeuft ? "läuft gerade" : "läuft gerade nicht")}   ·   {Dienst.Name}",
              $"{Dienst.StarttypKlartext}   ·   {(Dienst.Laeuft ? "currently running" : "not currently running")}   ·   {Dienst.Name}");
}
