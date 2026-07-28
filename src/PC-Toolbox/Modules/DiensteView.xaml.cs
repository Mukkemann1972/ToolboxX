using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class DiensteView : UserControl
{
    private readonly DiensteService _dienst = new();
    private List<DienstZeile> _alle = [];

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
