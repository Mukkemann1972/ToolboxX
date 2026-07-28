using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using PCToolbox.Converters;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class SuperUninstallerView : UserControl
{
    private readonly InstalledProgramsService _programme = new();
    private readonly UninstallerService _uninstaller = new();

    private List<InstalledProgram> _alleProgramme = new();
    private List<LeftoverItem> _aktuelleLeftovers = new();
    private string? _aktuellerSnapshotOrdner;

    public SuperUninstallerView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Super-Uninstaller", "Super Uninstaller");
        QuelleHinweis.ToolTip = Loc.T(
            "Registry-Uninstall-Schlüssel: HKLM, HKLM\\WOW6432Node und HKCU - dieselbe Liste wie 'Apps & Features' in Windows, nur mit Ampel-Warnung und Restsuche danach.",
            "Registry uninstall keys: HKLM, HKLM\\WOW6432Node, and HKCU - the same list as 'Apps & Features' in Windows, just with traffic-light warnings and a leftover search afterward.");
        SucheLabel.Text = Loc.T("Suche:", "Search:");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(SuperUninstallerView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "SuperUninstallerView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async Task LadeAsync()
    {
        _alleProgramme = await _programme.GetInstalledProgramsAsync();
        ListeAktualisieren();
    }

    private void SucheBox_TextChanged(object sender, TextChangedEventArgs e) => ListeAktualisieren();

    private void ListeAktualisieren()
    {
        var suchtext = SucheBox.Text?.Trim() ?? "";
        var gefiltert = string.IsNullOrEmpty(suchtext)
            ? _alleProgramme
            : _alleProgramme.Where(p =>
                p.DisplayName.Contains(suchtext, StringComparison.OrdinalIgnoreCase) ||
                p.Publisher.Contains(suchtext, StringComparison.OrdinalIgnoreCase)).ToList();

        Liste.ItemsSource = gefiltert.Select(ZuProgrammZeile).ToList();
    }

    private static ProgrammZeile ZuProgrammZeile(InstalledProgram p) => new()
    {
        Programm = p,
        Titel = $"{p.Ampel.Symbol()} {p.DisplayName}",
        InfoZeile = string.Join(" · ", new[] { p.Publisher, p.DisplayVersion, GroesseAnzeige(p.EstimatedSizeKb) }
            .Where(s => !string.IsNullOrWhiteSpace(s))),
        Erklaerung = p.Erklaerung,
        DeinstallierenBeschriftung = Loc.T("Deinstallieren", "Uninstall"),
    };

    private static string GroesseAnzeige(long? kb) => kb is > 0 ? $"{kb / 1024.0:0.#} MB" : "";

    private async void Deinstallieren_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledProgram programm }) return;

        var sicherheitsfrage = MessageBox.Show(
            Loc.T(
                $"\"{programm.DisplayName}\" deinstallieren?\n\n" +
                "Ablauf: 1) Sichern (Registry-Eintrag wird gesichert)  2) Der Deinstaller des Herstellers öffnet sich  " +
                "3) Danach wird nach Rückständen gesucht.\n\n" +
                "Folge den Anweisungen im sich öffnenden Deinstaller-Fenster, bevor du hier weitermachst.",
                $"Uninstall \"{programm.DisplayName}\"?\n\n" +
                "Process: 1) Back up (the registry entry is backed up)  2) The manufacturer's uninstaller opens  " +
                "3) Afterward, a search for leftovers is done.\n\n" +
                "Follow the instructions in the uninstaller window that opens before continuing here."),
            Loc.T("Deinstallieren – 3-Phasen-Sicherheit", "Uninstall – 3-phase safety"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (sicherheitsfrage != MessageBoxResult.OK) return;

        ErgebnisBereich.Visibility = Visibility.Collapsed;
        var fortschritt = new Progress<string>(text => StatusZeile.Text = text);

        var ergebnis = await _uninstaller.DeinstallierenAsync(programm, fortschritt);

        StatusZeile.Text = ergebnis.Erfolg ? Loc.T("Fertig.", "Done.") : Loc.T("Abgebrochen/fehlgeschlagen.", "Cancelled/failed.");

        _aktuelleLeftovers = ergebnis.Leftovers;
        _aktuellerSnapshotOrdner = ergebnis.SnapshotOrdner;

        ErgebnisTitel.Inlines.Clear();
        ErgebnisTitel.Inlines.Add(new InlineUIContainer(AmpelIconFactory.Build(ergebnis.Ampel)) { BaselineAlignment = BaselineAlignment.Center });
        ErgebnisTitel.Inlines.Add(new Run($" {programm.DisplayName}"));
        ErgebnisText.Text = ergebnis.Meldung;
        ErgebnisBereich.Visibility = Visibility.Visible;
        LeftoverListeAktualisieren();

        if (ergebnis.Erfolg)
            await LadeAsync(); // Liste neu laden - deinstalliertes Programm verschwindet daraus
    }

    private void LeftoverListeAktualisieren()
    {
        LeftoverListe.ItemsSource = _aktuelleLeftovers
            .Where(l => !l.Entfernt)
            .Select(l => new LeftoverZeile
            {
                Item = l,
                Titel = $"{l.Ampel.Symbol()} {l.Beschreibung}",
                Erklaerung = l.Erklaerung,
                EntfernenBeschriftung = Loc.T("Entfernen", "Remove"),
            })
            .ToList();
    }

    private async void LeftoverEntfernen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LeftoverItem item } button || _aktuellerSnapshotOrdner is null) return;

        button.IsEnabled = false;
        var erfolg = await _uninstaller.LeftoverEntfernenAsync(item, _aktuellerSnapshotOrdner);

        if (erfolg)
        {
            item.Entfernt = true;
            LeftoverListeAktualisieren();
        }
        else
        {
            button.IsEnabled = true;
            MessageBox.Show(
                Loc.T($"Konnte nicht entfernt werden:\n{item.Pfad}", $"Could not be removed:\n{item.Pfad}"),
                Loc.T("Fehler", "Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private class ProgrammZeile
    {
        public InstalledProgram Programm { get; set; } = null!;
        public string Titel { get; set; } = "";
        public string InfoZeile { get; set; } = "";
        public string Erklaerung { get; set; } = "";
        public string DeinstallierenBeschriftung { get; set; } = "";
    }

    private class LeftoverZeile
    {
        public LeftoverItem Item { get; set; } = null!;
        public string Titel { get; set; } = "";
        public string Erklaerung { get; set; } = "";
        public string EntfernenBeschriftung { get; set; } = "";
    }
}
