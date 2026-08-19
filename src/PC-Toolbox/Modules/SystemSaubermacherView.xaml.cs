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

public partial class SystemSaubermacherView : UserControl
{
    private readonly CleanupScanner _scanner = new();
    private readonly CleanupService _cleanup = new();

#if PRO_BUILD
    private readonly WiederherstellungspunkteService _wiederherstellungspunkteService = new();
    private TextBlock _wpStatusText = null!;
    private StackPanel _wpListe = null!;
    private Button _wpNeuestenBehaltenButton = null!;
    private Button _wpAlleLoeschenButton = null!;
#endif

    public SystemSaubermacherView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("System-Saubermacher", "System cleanup");
        EinleitungText.Text = Loc.T("Alles wandert in den Windows-Papierkorb - nichts wird sofort endgültig gelöscht.",
                                    "Everything goes to the Windows recycle bin - nothing is immediately permanently deleted.");
        QuelleHinweis.ToolTip = Loc.T(
            "Dateisystem-Scan bekannter Cache-/Temp-Pfade: %TEMP%, Miniaturansichten-Cache, Windows-Update-Downloadcache, Windows.old - geschützte Systempfade sind tabu.",
            "File system scan of known cache/temp paths: %TEMP%, thumbnail cache, Windows Update download cache, Windows.old - protected system paths are off-limits.");

#if PRO_BUILD
        BauWiederherstellungspunkteBereich();
#endif

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
#if PRO_BUILD
                await WiederherstellungspunkteLadenAsync();
#endif
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(SystemSaubermacherView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "SystemSaubermacherView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async Task LadeAsync()
    {
        StatusZeile.Text = Loc.T("Suche nach überflüssigen Dateien...", "Searching for unnecessary files...");
        var befunde = await _scanner.ScanAsync();
        Liste.ItemsSource = befunde.Select(ZuZeile).ToList();
        StatusZeile.Text = befunde.Count == 0 ? Loc.T("Nichts gefunden.", "Nothing found.") : "";
    }

    private static BefundZeile ZuZeile(CleanupBefund b) => new()
    {
        Befund = b,
        Titel = $"{b.Ampel.Symbol()} {b.Titel}",
        GroesseZeile = Loc.T($"{GroesseAnzeige(b.GroesseBytes)} · {b.DateiAnzahl} Element(e)",
                             $"{GroesseAnzeige(b.GroesseBytes)} · {b.DateiAnzahl} item(s)"),
        WasEsIst = b.WasEsIst,
        Erklaerung = b.Erklaerung,
        BereinigenBeschriftung = Loc.T("Bereinigen", "Clean up"),
    };

    private static string GroesseAnzeige(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:0.#} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => Loc.T($"{bytes} Byte", $"{bytes} bytes"),
    };

    private async void Bereinigen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CleanupBefund befund } button) return;

        var frage = MessageBox.Show(
            Loc.T(
                $"\"{befund.Titel}\" bereinigen ({GroesseAnzeige(befund.GroesseBytes)})?\n\n" +
                "Alles wandert in den Windows-Papierkorb, nichts wird sofort endgültig gelöscht - " +
                "du kannst es dort bei Bedarf wiederherstellen.",
                $"Clean up \"{befund.Titel}\" ({GroesseAnzeige(befund.GroesseBytes)})?\n\n" +
                "Everything goes to the Windows recycle bin, nothing is immediately permanently deleted - " +
                "you can restore it from there if needed."),
            Loc.T("Bereinigen bestätigen", "Confirm cleanup"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (frage != MessageBoxResult.OK) return;

        button.IsEnabled = false;
        var fortschritt = new Progress<string>(text => StatusZeile.Text = text);

        var ergebnis = await _cleanup.BereinigenAsync(befund, fortschritt);

        StatusZeile.Text = Loc.T($"{GroesseAnzeige(ergebnis.BytesFreigegeben)} freigegeben. {ergebnis.Meldung}",
                                 $"{GroesseAnzeige(ergebnis.BytesFreigegeben)} freed. {ergebnis.Meldung}");
        await LadeAsync(); // neu scannen, Kategorie zeigt danach den echten Reststand
    }

    private class BefundZeile
    {
        public CleanupBefund Befund { get; set; } = null!;
        public string Titel { get; set; } = "";
        public string GroesseZeile { get; set; } = "";
        public string WasEsIst { get; set; } = "";
        public string Erklaerung { get; set; } = "";
        public string BereinigenBeschriftung { get; set; } = "";
    }

#if PRO_BUILD
    /// <summary>
    /// Wiederherstellungspunkte (Pro) - anderer Fall als die Free-Kategorien oben (Temp/
    /// Miniaturansichten/Update-Cache/Windows.old, allesamt gefahrlos): hier ist Löschen
    /// endgültig, kein Papierkorb-Weg möglich, deshalb bewusst Pro statt Free (Free/Pro-
    /// Risikoteilung). Auch das reine LESEN braucht hier schon Adminrechte (live geprüft,
    /// s. WiederherstellungspunkteService-Klassendoku) - anders als bei jedem anderen Modul.
    /// </summary>
    private void BauWiederherstellungspunkteBereich()
    {
        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T("Wiederherstellungspunkte (Pro)", "Restore points (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (!WindowsReparaturService.HatAdminRechte())
        {
            ProZusatzBereich.Children.Add(new TextBlock
            {
                Text = Loc.T(
                    "Windows lässt selbst das Auflesen der Wiederherstellungspunkte nur mit " +
                    "Administratorrechten zu (anders als jedes andere Modul hier) - Toolbox X per " +
                    "Rechtsklick „Als Administrator ausführen\" neu starten, um diesen Bereich zu nutzen.",
                    "Windows only allows even reading restore points with administrator rights (unlike " +
                    "every other module here) - restart Toolbox X via right-click \"Run as administrator\" " +
                    "to use this section."),
                Foreground = (Brush)FindResource("WarnBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Windows legt bei größeren Änderungen (Treiber-/Programminstallation, Updates) automatisch " +
                "Rückkehrpunkte an - über „Systemwiederherstellung\" reversibel, solange sie existieren. Sie " +
                "belegen aber dauerhaft Speicherplatz. Löschen ist ENDGÜLTIG - danach ist eine Rückkehr zu " +
                "diesem Zeitpunkt nicht mehr möglich.",
                "Windows automatically creates restore points for bigger changes (driver/program installs, " +
                "updates) - reversible via \"System Restore\" as long as they exist. But they permanently use " +
                "disk space. Deleting is PERMANENT - afterward you can no longer go back to that point in time."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        _wpStatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _wpListe = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        _wpNeuestenBehaltenButton = new Button
        {
            Content = Loc.T("Alle bis auf den neuesten löschen", "Delete all but the newest"),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _wpNeuestenBehaltenButton.Click += (_, _) => WiederherstellungspunkteBereinigenAsync(alle: false);

        _wpAlleLoeschenButton = new Button
        {
            Content = Loc.T("Wirklich alle löschen", "Really delete all"),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _wpAlleLoeschenButton.Click += (_, _) => WiederherstellungspunkteBereinigenAsync(alle: true);

        ProZusatzBereich.Children.Add(_wpStatusText);
        ProZusatzBereich.Children.Add(_wpListe);
        ProZusatzBereich.Children.Add(_wpNeuestenBehaltenButton);
        ProZusatzBereich.Children.Add(_wpAlleLoeschenButton);
    }

    private async Task WiederherstellungspunkteLadenAsync()
    {
        if (!WindowsReparaturService.HatAdminRechte()) return;

        _wpStatusText.Text = Loc.T("Lese Wiederherstellungspunkte …", "Reading restore points…");
        _wpListe.Children.Clear();

        var punkte = await _wiederherstellungspunkteService.PunkteLadenAsync();
        var speicher = await _wiederherstellungspunkteService.SpeichernutzungLadenAsync();

        var speicherText = speicher.HasValue
            ? Loc.T(
                $"{speicher.Value.UsedBytes / 1024.0 / 1024 / 1024:0.0} GB von max. {speicher.Value.MaxBytes / 1024.0 / 1024 / 1024:0.0} GB reserviertem Speicher belegt.",
                $"{speicher.Value.UsedBytes / 1024.0 / 1024 / 1024:0.0} GB used of {speicher.Value.MaxBytes / 1024.0 / 1024 / 1024:0.0} GB reserved space.")
            : "";

        _wpStatusText.Text = (punkte.Count == 0
            ? Loc.T("Keine Wiederherstellungspunkte gefunden. ", "No restore points found. ")
            : Loc.T($"{punkte.Count} Wiederherstellungspunkt(e). ", $"{punkte.Count} restore point(s). ")) + speicherText;
        _wpStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var istNeuester = true;
        foreach (var punkt in punkte)
        {
            _wpListe.Children.Add(new TextBlock
            {
                Text = Loc.T(
                    $"{(istNeuester ? "🕐 (neuester) " : "")}{punkt.ErstelltUtc.ToLocalTime():dd.MM.yyyy HH:mm} · {punkt.Beschreibung}",
                    $"{(istNeuester ? "🕐 (newest) " : "")}{punkt.ErstelltUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {punkt.Beschreibung}"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 3),
                TextWrapping = TextWrapping.Wrap
            });
            istNeuester = false;
        }

        _wpNeuestenBehaltenButton.IsEnabled = punkte.Count > 1;
        _wpAlleLoeschenButton.IsEnabled = punkte.Count > 0;
    }

    private async void WiederherstellungspunkteBereinigenAsync(bool alle)
    {
        var text = alle
            ? Loc.T(
                "WIRKLICH ALLE Wiederherstellungspunkte löschen?\n\nDas ist endgültig - danach gibt es keinen " +
                "einzigen Rückkehrpunkt mehr. Falls andere Software (z.B. ein Backup-Programm) ebenfalls " +
                "Schattenkopien auf C: nutzt, gehen die auch verloren.\n\nWirklich fortfahren?",
                "REALLY delete ALL restore points?\n\nThis is permanent - afterward there won't be a single " +
                "point to go back to. If other software (e.g. a backup program) also uses shadow copies on " +
                "C:, those are lost too.\n\nReally continue?")
            : Loc.T(
                "Alle Wiederherstellungspunkte bis auf den neuesten löschen?\n\nDas ist endgültig - die " +
                "gelöschten Punkte sind unwiderruflich weg, der neueste bleibt erhalten.\n\nFortfahren?",
                "Delete all restore points except the newest?\n\nThis is permanent - the deleted points are " +
                "gone for good, the newest one is kept.\n\nContinue?");

        var antwort = MessageBox.Show(text, Loc.T("Wiederherstellungspunkte löschen", "Delete restore points"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (antwort != MessageBoxResult.Yes) return;

        _wpNeuestenBehaltenButton.IsEnabled = false;
        _wpAlleLoeschenButton.IsEnabled = false;
        _wpStatusText.Text = Loc.T("Lösche …", "Deleting…");

        var (erfolg, meldung) = alle
            ? await _wiederherstellungspunkteService.AlleLoeschenAsync()
            : await _wiederherstellungspunkteService.AlleBisAufNeuestenLoeschenAsync();

        _wpStatusText.Text = (erfolg ? "✔ " : "✘ ") + meldung;
        _wpStatusText.Foreground = (Brush)FindResource(erfolg ? "OkBrush" : "DangerBrush");

        await WiederherstellungspunkteLadenAsync();
    }
#endif
}
