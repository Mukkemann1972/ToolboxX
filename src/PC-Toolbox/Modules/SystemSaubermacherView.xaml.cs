using System.Windows;
using System.Windows.Controls;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class SystemSaubermacherView : UserControl
{
    private readonly CleanupScanner _scanner = new();
    private readonly CleanupService _cleanup = new();

    public SystemSaubermacherView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("System-Saubermacher", "System cleanup");
        EinleitungText.Text = Loc.T("Alles wandert in den Windows-Papierkorb - nichts wird sofort endgültig gelöscht.",
                                    "Everything goes to the Windows recycle bin - nothing is immediately permanently deleted.");
        QuelleHinweis.ToolTip = Loc.T(
            "Dateisystem-Scan bekannter Cache-/Temp-Pfade: %TEMP%, Miniaturansichten-Cache, Windows-Update-Downloadcache, Windows.old - geschützte Systempfade sind tabu.",
            "File system scan of known cache/temp paths: %TEMP%, thumbnail cache, Windows Update download cache, Windows.old - protected system paths are off-limits.");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
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
}
