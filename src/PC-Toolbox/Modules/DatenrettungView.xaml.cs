using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

/// <summary>
/// Datenrettung (Free): findet Dateien anhand von Signaturen, unabhängig vom Dateisystem - hilft
/// also auch bei gelöschten Dateien oder einer formatierten/RAW-Platte (s. DatenrettungService-
/// Kommentar für die Technik: zwei elevierte Durchgänge, Named Pipe + Carving-Engine im eigenen
/// Prozess statt in PowerShell dupliziert).
/// </summary>
public partial class DatenrettungView : UserControl
{
    private readonly DatenrettungService _dienst = new();
    private readonly PapierkorbService _papierkorbDienst = new();
    private IReadOnlyList<RettungsDiskInfo> _disks = [];
    private CancellationTokenSource? _abbruch;

    private readonly ListView _ergebnisListe;
    private readonly Button _alleMarkierenButton;
    // Live waehrend des Scans befuellt (s. Scan_Click/Fortschritt-Callback), nicht erst am Ende -
    // Nutzerwunsch: "wie bei GetDataBack sehen, was er findet, waehrend er sucht", statt einer
    // blinden Suche, bei der bis zum Schluss nichts sichtbar ist.
    private readonly ObservableCollection<FundZeile> _fundZeilen = [];

    private readonly ListView _papierkorbListe;
    private readonly Button _papierkorbWiederherstellenButton;
    private readonly ObservableCollection<PapierkorbZeile> _papierkorbZeilen = [];

    public DatenrettungView()
    {
        InitializeComponent();

        _ergebnisListe = new ListView { Background = (Brush)FindResource("BgCardBrush"), BorderThickness = new Thickness(0), ItemsSource = _fundZeilen };
        var gridView = new GridView();

        var checkboxFactory = new FrameworkElementFactory(typeof(CheckBox));
        checkboxFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Ausgewaehlt") { Mode = BindingMode.TwoWay });
        checkboxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        gridView.Columns.Add(new GridViewColumn { Width = 36, CellTemplate = new DataTemplate { VisualTree = checkboxFactory } });

        gridView.Columns.Add(new GridViewColumn { Width = 100, Header = Loc.T("Art", "Type"), DisplayMemberBinding = new Binding("TypText") });
        gridView.Columns.Add(new GridViewColumn { Width = 100, Header = Loc.T("Größe", "Size"), DisplayMemberBinding = new Binding("GroesseText") });
        gridView.Columns.Add(new GridViewColumn { Width = 130, Header = Loc.T("Status", "Status"), DisplayMemberBinding = new Binding("StatusText") });

        _ergebnisListe.View = gridView;

        _alleMarkierenButton = new Button { Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Left };
        _alleMarkierenButton.Click += (_, _) =>
        {
            var alleSchonMarkiert = _fundZeilen.Count > 0 && _fundZeilen.All(z => z.Ausgewaehlt);
            foreach (var zeile in _fundZeilen) zeile.Ausgewaehlt = !alleSchonMarkiert;
        };
        DockPanel.SetDock(_alleMarkierenButton, Dock.Top);

        ErgebnisBereich.Children.Add(_alleMarkierenButton);
        ErgebnisBereich.Children.Add(_ergebnisListe);
        ErgebnisBereich.Visibility = Visibility.Collapsed;

        // Papierkorb-Ergebnisliste - eigene, kleinere Liste (kein Ampel-Konzept noetig, das ist
        // reine Byte-Suchen-Sache), aber gleiches Checkbox+Wiederherstellen-Muster.
        _papierkorbListe = new ListView { Background = (Brush)FindResource("BgPanelBrush"), BorderThickness = new Thickness(0), ItemsSource = _papierkorbZeilen };
        var papierkorbGridView = new GridView();

        var papierkorbCheckboxFactory = new FrameworkElementFactory(typeof(CheckBox));
        papierkorbCheckboxFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Ausgewaehlt") { Mode = BindingMode.TwoWay });
        papierkorbCheckboxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        papierkorbGridView.Columns.Add(new GridViewColumn { Width = 32, CellTemplate = new DataTemplate { VisualTree = papierkorbCheckboxFactory } });

        papierkorbGridView.Columns.Add(new GridViewColumn { Width = 220, Header = Loc.T("Ursprünglicher Name", "Original name"), DisplayMemberBinding = new Binding("NameText") });
        papierkorbGridView.Columns.Add(new GridViewColumn { Width = 90, Header = Loc.T("Größe", "Size"), DisplayMemberBinding = new Binding("GroesseText") });
        papierkorbGridView.Columns.Add(new GridViewColumn { Width = 140, Header = Loc.T("Gelöscht am", "Deleted on"), DisplayMemberBinding = new Binding("GeloeschtText") });
        _papierkorbListe.View = papierkorbGridView;

        _papierkorbWiederherstellenButton = new Button { Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        _papierkorbWiederherstellenButton.Content = Loc.T("Ausgewählte wiederherstellen (Original-Name)", "Restore selected (original name)");
        _papierkorbWiederherstellenButton.Click += PapierkorbWiederherstellen_Click;
        DockPanel.SetDock(_papierkorbWiederherstellenButton, Dock.Bottom);

        PapierkorbErgebnisBereich.Children.Add(_papierkorbWiederherstellenButton);
        PapierkorbErgebnisBereich.Children.Add(_papierkorbListe);

        PapierkorbTitel.Text = Loc.T("Zuerst im Papierkorb nachsehen (empfohlen)", "Check the Recycle Bin first (recommended)");
        PapierkorbText.Text = Loc.T(
            "Schnell und mit Original-Namen/-Ordner - klappt nur, wenn die Datei noch im Papierkorb liegt " +
            "(nicht geleert). Erst wenn hier nichts (mehr) zu finden ist, lohnt sich die langsamere Suche " +
            "weiter unten, die zwar auch geleerte/formatierte Laufwerke durchsucht, aber keine Original-Namen " +
            "liefern kann.",
            "Fast and with original names/folders - only works if the file is still in the Recycle Bin (not " +
            "emptied). Only if nothing is found here does the slower search below make sense - it also " +
            "searches emptied/formatted drives, but can't recover original names.");
        PapierkorbDurchsuchenButton.Content = Loc.T("Papierkorb durchsuchen", "Search Recycle Bin");

        TitelText.Text = Loc.T("Datenrettung", "Data Recovery");
        EinleitungText.Text = Loc.T(
            "Findet Dateien anhand bekannter Anfangs-/End-Kennungen, egal ob sie gelöscht wurden oder das " +
            "Dateisystem selbst beschädigt/nicht mehr lesbar (\"RAW\") ist - unabhängig vom Dateisystem-Eintrag. " +
            "Rein lesend gegenüber der Quelle: nichts auf der gewählten Platte wird verändert, Funde landen " +
            "ausschließlich im gewählten Zielordner (der zwingend auf einer ANDEREN Platte liegen muss). " +
            "Ursprüngliche Dateinamen/Ordnerstruktur sind nicht wiederherstellbar, nur der Dateiinhalt selbst.",
            "Finds files by known start/end markers, whether they were deleted or the filesystem itself is " +
            "damaged/unreadable (\"RAW\") - independent of the filesystem entry. Purely read-only against the " +
            "source: nothing on the chosen disk is changed, finds go exclusively into the chosen destination " +
            "folder (which must be on a DIFFERENT disk). Original filenames/folder structure can't be " +
            "recovered, only the file content itself.");
        PlatteLabel.Text = Loc.T("QUELLE (wird nur gelesen)", "SOURCE (read-only)");
        TypenLabel.Text = Loc.T("WOVON GESUCHT WERDEN SOLL", "WHAT TO LOOK FOR");
        ZielLabel.Text = Loc.T("ZIELORDNER (muss auf einer anderen Platte liegen)", "DESTINATION FOLDER (must be on a different disk)");
        ZielDurchsuchenButton.Content = Loc.T("Durchsuchen …", "Browse…");
        NeuLadenButton.Content = Loc.T("Platten neu einlesen", "Rescan disks");
        ScanButton.Content = Loc.T("Scan starten", "Start scan");
        RettenButton.Content = Loc.T("Gefundene Dateien retten", "Recover found files");
        AbbrechenButton.Content = Loc.T("Abbrechen", "Cancel");

        Loaded += async (_, _) => await LadenAsync();
    }

    private async void NeuLaden_Click(object sender, RoutedEventArgs e) => await LadenAsync();

    private async Task LadenAsync()
    {
        StatusText.Text = Loc.T("Lese Platten ein …", "Reading disks…");
        ScanButton.IsEnabled = false;

        _disks = await _dienst.DisksLadenAsync();
        PlatteBox.ItemsSource = _disks.Select(AnzeigeText).ToList();
        if (PlatteBox.SelectedIndex < 0 && _disks.Count > 0) PlatteBox.SelectedIndex = 0;

        StatusText.Text = _disks.Count > 0
            ? Loc.T($"{_disks.Count} Platte(n) gefunden.", $"{_disks.Count} disk(s) found.")
            : Loc.T("Keine Platten gefunden.", "No disks found.");

        await Auswahl_PruefenAsync();
    }

    private static string AnzeigeText(RettungsDiskInfo disk) =>
        $"Platte {disk.Nummer}: {(string.IsNullOrWhiteSpace(disk.Name) ? "?" : disk.Name)} - " +
        $"{disk.GroesseBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB ({disk.BusTyp}) - " +
        LaufwerksbuchstabenProPlatte.Anzeige(disk.Laufwerksbuchstaben);

    private async void Auswahl_Geaendert(object sender, SelectionChangedEventArgs e) => await Auswahl_PruefenAsync();

    private async void ZielDurchsuchen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("Zielordner wählen", "Choose destination folder") };
        if (dialog.ShowDialog() != true) return;

        ZielPfadFeld.Text = dialog.FolderName;
        await Auswahl_PruefenAsync();
    }

    private async Task Auswahl_PruefenAsync()
    {
        var platte = PlatteBox.SelectedIndex >= 0 ? _disks[PlatteBox.SelectedIndex] : null;

        // Papierkorb-Bereich zeigen, sobald eine Platte mit mindestens einem Laufwerksbuchstaben
        // gewaehlt ist - unabhaengig vom Zielordner, weil das reine Durchsuchen (im Gegensatz zum
        // Wiederherstellen) noch kein Ziel braucht.
        PapierkorbBereich.Visibility = platte?.Laufwerksbuchstaben is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        if (platte is null)
        {
            _papierkorbZeilen.Clear();
            PapierkorbErgebnisBereich.Visibility = Visibility.Collapsed;
            _papierkorbWiederherstellenButton.Visibility = Visibility.Collapsed;
            PapierkorbStatusText.Text = "";
        }

        if (platte is null || string.IsNullOrWhiteSpace(ZielPfadFeld.Text))
        {
            ZielInfoText.Text = "";
            ScanButton.IsEnabled = false;
            return;
        }

        var (ok, begruendung) = await _dienst.ZielPruefenAsync(ZielPfadFeld.Text, platte.Nummer);
        ZielInfoText.Text = begruendung;
        ZielInfoText.Foreground = (Brush)FindResource(ok ? "WarnBrush" : "DangerBrush");
        ScanButton.IsEnabled = ok;
    }

    private async void PapierkorbDurchsuchen_Click(object sender, RoutedEventArgs e)
    {
        var platte = PlatteBox.SelectedIndex >= 0 ? _disks[PlatteBox.SelectedIndex] : null;
        if (platte?.Laufwerksbuchstaben is not { Count: > 0 } buchstaben) return;

        PapierkorbDurchsuchenButton.IsEnabled = false;
        PapierkorbStatusText.Text = Loc.T("Durchsuche Papierkorb …", "Searching Recycle Bin…");
        PapierkorbStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var funde = await _papierkorbDienst.DurchsuchenAsync(buchstaben);

        _papierkorbZeilen.Clear();
        foreach (var fund in funde)
        {
            _papierkorbZeilen.Add(new PapierkorbZeile
            {
                Fund = fund,
                Ausgewaehlt = true,
                NameText = fund.OriginalName,
                GroesseText = GroesseAnzeige(fund.GroesseBytes),
                GeloeschtText = fund.GeloeschtAm?.ToString("dd.MM.yyyy HH:mm") ?? Loc.T("unbekannt", "unknown")
            });
        }

        PapierkorbStatusText.Text = funde.Count == 0
            ? Loc.T("Nichts im Papierkorb gefunden - weiter unten mit der Tiefensuche versuchen.",
                    "Nothing found in the Recycle Bin - try the deep search below.")
            : Loc.T($"✔ {funde.Count} Datei(en) im Papierkorb gefunden.", $"✔ {funde.Count} file(s) found in the Recycle Bin.");
        PapierkorbStatusText.Foreground = (Brush)FindResource(funde.Count == 0 ? "TextMutedBrush" : "OkBrush");

        PapierkorbErgebnisBereich.Visibility = funde.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _papierkorbWiederherstellenButton.Visibility = funde.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PapierkorbDurchsuchenButton.IsEnabled = true;
    }

    private async void PapierkorbWiederherstellen_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ZielPfadFeld.Text))
        {
            PapierkorbStatusText.Text = "✘ " + Loc.T("Bitte zuerst einen Zielordner wählen (unten).", "Please choose a destination folder first (below).");
            PapierkorbStatusText.Foreground = (Brush)FindResource("DangerBrush");
            return;
        }

        var ausgewaehlt = _papierkorbZeilen.Where(z => z.Ausgewaehlt).Select(z => z.Fund).ToList();
        if (ausgewaehlt.Count == 0)
        {
            PapierkorbStatusText.Text = "✘ " + Loc.T("Bitte mindestens eine Datei auswählen.", "Please select at least one file.");
            PapierkorbStatusText.Foreground = (Brush)FindResource("DangerBrush");
            return;
        }

        _papierkorbWiederherstellenButton.IsEnabled = false;
        PapierkorbStatusText.Text = Loc.T("Stelle wieder her …", "Restoring…");
        PapierkorbStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var (ok, fehlgeschlagen) = await _papierkorbDienst.WiederherstellenAsync(ausgewaehlt, ZielPfadFeld.Text);

        PapierkorbStatusText.Text = (fehlgeschlagen == 0 ? "✔ " : "✘ ") + Loc.T(
            $"{ok} Datei(en) wiederhergestellt nach \"{ZielPfadFeld.Text}\"" + (fehlgeschlagen > 0 ? $", {fehlgeschlagen} fehlgeschlagen." : "."),
            $"{ok} file(s) restored to \"{ZielPfadFeld.Text}\"" + (fehlgeschlagen > 0 ? $", {fehlgeschlagen} failed." : "."));
        PapierkorbStatusText.Foreground = (Brush)FindResource(fehlgeschlagen == 0 ? "OkBrush" : "DangerBrush");
        _papierkorbWiederherstellenButton.IsEnabled = true;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var platte = _disks[PlatteBox.SelectedIndex];
        var typen = AusgewaehlteTypen();
        if (typen.Count == 0)
        {
            StatusText.Text = Loc.T("Bitte mindestens eine Dateiart auswählen.", "Please select at least one file type.");
            return;
        }

        RettenButton.Visibility = Visibility.Collapsed;
        ScanButton.Visibility = Visibility.Collapsed;
        AbbrechenButton.Visibility = Visibility.Visible;
        FortschrittBalken.Visibility = Visibility.Visible;
        FortschrittBalken.Value = 0;
        NeuLadenButton.IsEnabled = false;
        PlatteBox.IsEnabled = false;

        // Liste sofort leeren und einblenden, nicht erst wenn Ergebnisse da sind - Funde poppen
        // waehrend des Scans live einzeln rein (s. Fortschritt-Callback), statt alles erst am Ende
        // auf einmal zu zeigen.
        _fundZeilen.Clear();
        _alleMarkierenButton.Content = Loc.T("Alle / keine auswählen", "Select all / none");
        ErgebnisBereich.Visibility = Visibility.Visible;
        RettenButton.Visibility = Visibility.Collapsed;

        _abbruch = new CancellationTokenSource();
        var fortschritt = new Progress<RettungsScanFortschritt>(f =>
        {
            FortschrittBalken.Value = f.Prozent;
            StatusText.Text = Loc.T(
                $"Scanne: {f.GelesenBytes / (double)DateisystemRegeln.Gigabyte:0.#} von {f.GesamtBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB " +
                $"({f.Prozent:0.#} %) - {f.GefundenBisher} gefunden",
                $"Scanning: {f.GelesenBytes / (double)DateisystemRegeln.Gigabyte:0.#} of {f.GesamtBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB " +
                $"({f.Prozent:0.#}%) - {f.GefundenBisher} found");

            if (f.NeueFunde is not { Count: > 0 } neueFunde) return;
            foreach (var fund in neueFunde) _fundZeilen.Add(ZuFundZeile(fund));
        });

        var ergebnis = await _dienst.ScanAsync(platte, typen, fortschritt, _abbruch.Token);
        var abgebrochen = _abbruch.IsCancellationRequested;

        FortschrittBalken.Visibility = Visibility.Collapsed;
        AbbrechenButton.Visibility = Visibility.Collapsed;
        ScanButton.Visibility = Visibility.Visible;
        NeuLadenButton.IsEnabled = true;
        PlatteBox.IsEnabled = true;

        if (!ergebnis.Erfolgreich)
        {
            StatusText.Text = "✘ " + (ergebnis.Fehlermeldung ?? Loc.T("Unbekannter Fehler.", "Unknown error."));
            StatusText.Foreground = (Brush)FindResource("DangerBrush");
            ErgebnisBereich.Visibility = _fundZeilen.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RettenButton.Visibility = _fundZeilen.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        var anzahlProTyp = ergebnis.Funde.GroupBy(f => f.Typ).Select(g => $"{g.Count()}x {g.Key}");
        StatusText.Text = ergebnis.Funde.Count == 0
            ? Loc.T("Scan fertig - nichts gefunden.", "Scan done - nothing found.")
            : abgebrochen
                ? Loc.T($"⏸ Abgebrochen: {ergebnis.Funde.Count} Datei(en) bis dahin gefunden ({string.Join(", ", anzahlProTyp)}).",
                        $"⏸ Cancelled: {ergebnis.Funde.Count} file(s) found so far ({string.Join(", ", anzahlProTyp)}).")
                : Loc.T($"✔ Scan fertig: {ergebnis.Funde.Count} Datei(en) gefunden ({string.Join(", ", anzahlProTyp)}).",
                        $"✔ Scan done: {ergebnis.Funde.Count} file(s) found ({string.Join(", ", anzahlProTyp)}).");
        StatusText.Foreground = (Brush)FindResource(ergebnis.Funde.Count == 0 ? "TextMutedBrush" : abgebrochen ? "WarnBrush" : "OkBrush");

        ErgebnisBereich.Visibility = ergebnis.Funde.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RettenButton.Visibility = ergebnis.Funde.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static FundZeile ZuFundZeile(GefundeneDatei f) => new()
    {
        Fund = f,
        Ausgewaehlt = true,
        TypText = f.Typ.ToString(),
        GroesseText = GroesseAnzeige(f.Laenge),
        StatusText = f.Sicher
            ? Loc.T("🟢 sauber", "🟢 clean")
            : Loc.T("🟡 unsicher (kein Ende gefunden)", "🟡 uncertain (no end found)")
    };

    private static string GroesseAnzeige(long bytes) => bytes >= DateisystemRegeln.Gigabyte
        ? $"{bytes / (double)DateisystemRegeln.Gigabyte:0.#} GB"
        : $"{bytes / (1024.0 * 1024.0):0.#} MB";

    private async void Retten_Click(object sender, RoutedEventArgs e)
    {
        var platte = _disks[PlatteBox.SelectedIndex];
        var zielOrdner = ZielPfadFeld.Text;

        var ausgewaehlteZeilen = _fundZeilen.Where(z => z.Ausgewaehlt).ToList();
        var ausgewaehlteFunde = ausgewaehlteZeilen.Select(z => z.Fund).ToList();

        if (ausgewaehlteFunde.Count == 0)
        {
            StatusText.Text = "✘ " + Loc.T("Bitte mindestens eine gefundene Datei auswählen.", "Please select at least one found file.");
            StatusText.Foreground = (Brush)FindResource("DangerBrush");
            return;
        }

        RettenButton.IsEnabled = false;
        AbbrechenButton.Visibility = Visibility.Visible;
        FortschrittBalken.Visibility = Visibility.Visible;
        FortschrittBalken.Value = 0;

        _abbruch = new CancellationTokenSource();
        var fortschritt = new Progress<RettenFortschritt>(f =>
        {
            FortschrittBalken.Value = f.Prozent;
            StatusText.Text = Loc.T($"Rette Datei {f.Fertig} von {f.Gesamt} …", $"Recovering file {f.Fertig} of {f.Gesamt}…");
        });

        var ergebnis = await _dienst.RettenAsync(platte, ausgewaehlteFunde, zielOrdner, fortschritt, _abbruch.Token);

        FortschrittBalken.Visibility = Visibility.Collapsed;
        AbbrechenButton.Visibility = Visibility.Collapsed;
        RettenButton.IsEnabled = true;

        StatusText.Text = ergebnis.Erfolgreich
            ? Loc.T($"✔ Fertig: {ergebnis.Gerettet} Datei(en) gerettet nach \"{zielOrdner}\"" +
                    (ergebnis.Fehlgeschlagen > 0 ? $", {ergebnis.Fehlgeschlagen} fehlgeschlagen." : "."),
                    $"✔ Done: {ergebnis.Gerettet} file(s) recovered to \"{zielOrdner}\"" +
                    (ergebnis.Fehlgeschlagen > 0 ? $", {ergebnis.Fehlgeschlagen} failed." : "."))
            : "✘ " + (ergebnis.Fehlermeldung ?? Loc.T("Unbekannter Fehler.", "Unknown error."));
        StatusText.Foreground = (Brush)FindResource(ergebnis.Erfolgreich ? "OkBrush" : "DangerBrush");

        // Ergebnis pro Zeile eintragen (🔴 fehlgeschlagen / ✔ gerettet) statt die Liste danach
        // einfach auszublenden - sonst sieht man nie, WELCHE Dateien es waren, die nicht geklappt
        // haben, nur eine Gesamtzahl. Nur bei einem grundsaetzlich erfolgreichen Durchlauf (der
        // Vorgang selbst ist durchgelaufen, auch wenn einzelne Dateien fehlschlugen) - schlug schon
        // der ganze Vorgang fehl (z.B. UAC abgelehnt), waere "gerettet" fuer alle Zeilen falsch.
        if (ergebnis.Erfolgreich)
        {
            var fehlgeschlageneIndizes = (ergebnis.FehlgeschlageneIndizes ?? []).ToHashSet();
            for (var i = 0; i < ausgewaehlteZeilen.Count; i++)
            {
                ausgewaehlteZeilen[i].StatusText = fehlgeschlageneIndizes.Contains(i)
                    ? Loc.T("🔴 fehlgeschlagen", "🔴 failed")
                    : Loc.T("✔ gerettet", "✔ recovered");
            }
        }

        if (ergebnis.Erfolgreich && ergebnis.Fehlgeschlagen == 0)
            RettenButton.Visibility = Visibility.Collapsed;
    }

    private void Abbrechen_Click(object sender, RoutedEventArgs e) => _abbruch?.Cancel();

    private List<DateiTyp> AusgewaehlteTypen()
    {
        var typen = new List<DateiTyp>();
        if (JpegBox.IsChecked == true) typen.Add(DateiTyp.Jpeg);
        if (PngBox.IsChecked == true) typen.Add(DateiTyp.Png);
        if (GifBox.IsChecked == true) typen.Add(DateiTyp.Gif);
        if (PdfBox.IsChecked == true) typen.Add(DateiTyp.Pdf);
        if (ZipBox.IsChecked == true) typen.Add(DateiTyp.Zip);
        return typen;
    }
}

/// <summary>Eine Zeile in der Ergebnisliste des Datenrettung-Scans - erlaubt, einzelne Funde vor
/// dem Retten abzuwählen (z.B. offensichtliche Fehltreffer), statt wie zuvor alles blind zu
/// retten. Gleiches Auswahl-Muster wie GeisterZeile in UsbGeraeteView.</summary>
public class FundZeile : System.ComponentModel.INotifyPropertyChanged
{
    private bool _ausgewaehlt;

    private string _statusText = "";

    public required GefundeneDatei Fund { get; init; }
    public string TypText { get; init; } = "";
    public string GroesseText { get; init; } = "";

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    public bool Ausgewaehlt
    {
        get => _ausgewaehlt;
        set
        {
            _ausgewaehlt = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Ausgewaehlt)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Eine Zeile in der Papierkorb-Ergebnisliste - eigener, einfacherer Typ als FundZeile
/// (kein Ampel-Konzept noetig, ein Papierkorb-Fund hat einen echten Original-Namen und ist
/// entweder da oder nicht, keine Qualitaetsabstufung wie beim Carving).</summary>
public class PapierkorbZeile : System.ComponentModel.INotifyPropertyChanged
{
    private bool _ausgewaehlt;

    public required PapierkorbFund Fund { get; init; }
    public string NameText { get; init; } = "";
    public string GroesseText { get; init; } = "";
    public string GeloeschtText { get; init; } = "";

    public bool Ausgewaehlt
    {
        get => _ausgewaehlt;
        set
        {
            _ausgewaehlt = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Ausgewaehlt)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
