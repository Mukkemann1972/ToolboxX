using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        AktualisierenButton.ToolTip = Loc.T(
            "Liste neu einlesen (z.B. wenn ein Deinstaller im Hintergrund noch fertig wird)",
            "Reload the list (e.g. if an uninstaller is still finishing in the background)");
        GruppierenBox.Content = Loc.T("Nach Hersteller gruppieren", "Group by publisher");
        GruppierenBox.ToolTip = Loc.T(
            "Praktisch, um alles von einem Hersteller auf einen Blick zu sehen (z.B. mehrere HP-Programme " +
            "und -Treiber, die als Einzeleinträge sonst nicht erkennbar zusammengehören).",
            "Useful for seeing everything from one publisher at a glance (e.g. several HP programs and " +
            "drivers that, as separate entries, wouldn't otherwise look related).");

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

    private void GruppierenBox_Geaendert(object sender, RoutedEventArgs e) => ListeAktualisieren();

    // Manueller Kurzschluss fuer "raus und wieder rein" - vor allem wichtig direkt nach einer
    // Deinstallation: manche Deinstaller starten sich selbst in einem Temp-Ordner neu und laufen
    // dann noch, waehrend die App schon "fertig" meldet (s. Kommentar bei NativenDeinstallerStartenAsync)
    // - die Liste kann direkt danach also noch den alten Stand zeigen.
    private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
    {
        AktualisierenButton.IsEnabled = false;
        await LadeAsync();
        AktualisierenButton.IsEnabled = true;
    }

    private void ListeAktualisieren()
    {
        var suchtext = SucheBox.Text?.Trim() ?? "";
        var gefiltert = string.IsNullOrEmpty(suchtext)
            ? _alleProgramme
            : _alleProgramme.Where(p =>
                p.DisplayName.Contains(suchtext, StringComparison.OrdinalIgnoreCase) ||
                p.Publisher.Contains(suchtext, StringComparison.OrdinalIgnoreCase)).ToList();

        if (GruppierenBox.IsChecked != true)
        {
            Liste.ItemsSource = gefiltert.Select(ZuProgrammZeile).ToList();
            return;
        }

        // Gruppiert nach Hersteller statt alphabetisch nach Programmname - macht auf einen Blick
        // sichtbar, was zusammengehört (z.B. mehrere HP-Programme/-Treiber), ohne dass man das
        // selbst aus lauter Einzeleinträgen zusammenpuzzeln muss. Die Publisher-Angabe ist schon
        // vorhanden (InstalledProgramsService liest sie für jedes Programm), hier nur neu sortiert
        // und mit einer Gruppenüberschrift versehen - keine zusätzliche Suche nötig.
        var sortiert = gefiltert
            .OrderBy(p => HerstellerAnzeige(p.Publisher), StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(ZuProgrammZeile)
            .ToList();

        var ansicht = new ListCollectionView(sortiert);
        ansicht.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProgrammZeile.HerstellerGruppe)));
        Liste.ItemsSource = ansicht;
    }

    private static string HerstellerAnzeige(string publisher) =>
        string.IsNullOrWhiteSpace(publisher) ? Loc.T("Unbekannter Hersteller", "Unknown publisher") : publisher.Trim();

    private static ProgrammZeile ZuProgrammZeile(InstalledProgram p) => new()
    {
        Programm = p,
        Titel = $"{p.Ampel.Symbol()} {p.DisplayName}",
        InfoZeile = string.Join(" · ", new[] { p.Publisher, p.DisplayVersion, GroesseAnzeige(p.EstimatedSizeKb) }
            .Where(s => !string.IsNullOrWhiteSpace(s))),
        Erklaerung = p.Erklaerung,
        DeinstallierenBeschriftung = Loc.T("Deinstallieren", "Uninstall"),
        HerstellerGruppe = HerstellerAnzeige(p.Publisher),
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

        if (!erfolg)
            erfolg = await ErhoehteRechteAnbietenUndVersuchenAsync(item, _aktuellerSnapshotOrdner);

        if (erfolg)
        {
            item.Entfernt = true;
            LeftoverListeAktualisieren();
        }
        else
        {
            button.IsEnabled = true;
        }
    }

    // Wird erst NACH einem fehlgeschlagenen normalen Versuch aufgerufen. Erklaert dem Nutzer, WARUM
    // Windows hier blockiert (Haftungsgrund: niemand soll ein UAC-Fenster bestaetigen, ohne zu
    // wissen wofuer) und fragt ausdruecklich nach, bevor mit Administratorrechten nochmal
    // versucht wird.
    private async Task<bool> ErhoehteRechteAnbietenUndVersuchenAsync(LeftoverItem item, string snapshotOrdner)
    {
        var weiter = MessageBox.Show(
            Loc.T(
                $"Konnte nicht entfernt werden:\n{item.Pfad}\n\n" +
                "Vermutlich ist dieser Ordner/Eintrag von Windows geschützt - nur Administratoren dürfen ihn entfernen. " +
                "Jetzt mit Administratorrechten nochmal versuchen? Windows zeigt dafür eine Sicherheitsabfrage (UAC), " +
                "die du bestätigen musst.",
                $"Could not be removed:\n{item.Pfad}\n\n" +
                "This folder/entry is likely protected by Windows - only administrators may remove it. " +
                "Try again with administrator rights now? Windows will show a security prompt (UAC) that you need to approve."),
            Loc.T("Administratorrechte nötig", "Administrator rights needed"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (weiter != MessageBoxResult.Yes) return false;

        var erfolg = await _uninstaller.LeftoverMitErhoehtenRechtenEntfernenAsync(item, snapshotOrdner);
        if (!erfolg)
        {
            MessageBox.Show(
                Loc.T(
                    $"Auch mit Administratorrechten nicht möglich:\n{item.Pfad}\n\n" +
                    "Vermutlich wird die Datei/der Ordner gerade von einem laufenden Programm verwendet, oder die " +
                    "Sicherheitsabfrage wurde abgelehnt.",
                    $"Not possible even with administrator rights:\n{item.Pfad}\n\n" +
                    "The file/folder is likely still in use by a running program, or the security prompt was declined."),
                Loc.T("Fehler", "Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return erfolg;
    }

    private class ProgrammZeile
    {
        public InstalledProgram Programm { get; set; } = null!;
        public string Titel { get; set; } = "";
        public string InfoZeile { get; set; } = "";
        public string Erklaerung { get; set; } = "";
        public string DeinstallierenBeschriftung { get; set; } = "";
        public string HerstellerGruppe { get; set; } = "";
    }

    private class LeftoverZeile
    {
        public LeftoverItem Item { get; set; } = null!;
        public string Titel { get; set; } = "";
        public string Erklaerung { get; set; } = "";
        public string EntfernenBeschriftung { get; set; } = "";
    }
}
