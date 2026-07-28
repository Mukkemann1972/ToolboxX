using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Services;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Modules;

public partial class DatentraegerView : UserControl
{
    private readonly DatentraegerService _dienst = new();
    private readonly LaufwerksbuchstabenKonfliktService _konfliktDienst = new();
    private List<DatentraegerVolume> _volumes = [];

    public DatentraegerView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Datenträger", "Drives");
        QuelleHinweis.ToolTip = Loc.T(
            "Partitions-/Volume-Verwaltung über die Windows-Datenträgerverwaltung (WMI/DiskPart-Befehle) - dieselbe Ebene wie diskmgmt.msc, nur mit Erklärung statt Fachbegriffen.",
            "Partition/volume management via Windows disk management (WMI/DiskPart commands) - the same level as diskmgmt.msc, just with explanations instead of jargon.");
        EinleitungText.Text = Loc.T(
            "Formatieren in allen gängigen Formaten, Bezeichnung und Laufwerksbuchstabe ändern - und die Größe verlustfrei anpassen, ohne die Daten anzufassen.",
            "Format in all common formats, change the label and drive letter - and resize losslessly without touching the data.");
        NichtsPassierenTitel.Text = Loc.T("🛟 Was hier nicht passieren kann", "🛟 What can't happen here");
        NichtsPassierenText.Text = Loc.T(
            "Das Laufwerk, auf dem Windows läuft, steht hier gar nicht erst zur Auswahl - keine Warnung zum Wegklicken, es geht schlicht nicht. Größe ändern ist verlustfrei: die Daten bleiben. Formatieren dagegen löscht alles und lässt sich durch nichts rückgängig machen - deshalb musst du dafür die Laufwerksbezeichnung abtippen.",
            "The drive Windows runs on isn't even available for selection here - no warning to click past, it simply isn't possible. Resizing is lossless: the data stays. Formatting, on the other hand, deletes everything and cannot be undone by any means - which is why you have to type the drive's label to confirm it.");
        LaufwerkLabel.Text = Loc.T("LAUFWERK", "DRIVE");
        BezeichnungBuchstabeTitel.Text = Loc.T("Bezeichnung und Laufwerksbuchstabe", "Label and drive letter");
        BezeichnungUebernehmenButton.Content = Loc.T("Bezeichnung übernehmen", "Apply label");
        BuchstabeAendernButton.Content = Loc.T("Buchstabe ändern", "Change letter");
        GroesseAendernTitel.Text = Loc.T("Größe ändern (verlustfrei)", "Resize (lossless)");
        GroesseButton.Content = Loc.T("Größe übernehmen", "Apply size");
        FormatierenTitel.Text = Loc.T("Formatieren", "Format");
        SchnellBox.Content = Loc.T("Schnellformatierung", "Quick format");
        FormatierenButton.Content = Loc.T("Jetzt formatieren", "Format now");
        NeuLadenButton.Content = Loc.T("Neu einlesen", "Reload");

        foreach (var dateisystem in DateisystemRegeln.Alle)
            DateisystemBox.Items.Add(dateisystem);

        GeisterBereich.Header = Loc.T("Reservierte Laufwerksbuchstaben prüfen", "Check reserved drive letters");

        Loaded += async (_, _) => await LadenAsync();
        Loaded += async (_, _) => await GeisterBuchstabenLadenAsync();
    }

    private async Task GeisterBuchstabenLadenAsync()
    {
        try
        {
            var geister = await _konfliktDienst.GeisterBuchstabenAsync();
            if (geister.Count == 0) return;

            GeisterBereich.Visibility = Visibility.Visible;
            var liste = string.Join(", ", geister.Select(b => $"{b}:"));
            GeisterText.Text = Loc.T(
                $"{liste} {(geister.Count == 1 ? "ist" : "sind")} noch in Windows reserviert, obwohl gerade kein " +
                "Laufwerk mit diesem Buchstaben angeschlossen ist - meist ein früher angeschlossenes Gerät, " +
                "das Windows sich dauerhaft gemerkt hat. Das ist die häufigste, unsichtbare Ursache dafür, " +
                "dass ein neu angeschlossener USB-Stick nicht den erwarteten Buchstaben bekommt oder " +
                "gar nicht erst angezeigt wird - er bekommt dann automatisch einen anderen, freien Buchstaben. " +
                "Das ist harmlos und muss nicht behoben werden; falls doch gewünscht, hilft in der " +
                "Datenträgerverwaltung (diskmgmt.msc) oder per \"mountvol /r\" als Administrator das " +
                "Aufräumen alter Zuordnungen.",
                $"{liste} {(geister.Count == 1 ? "is" : "are")} still reserved in Windows even though no drive " +
                "with that letter is currently connected - usually a device that was connected before and " +
                "that Windows remembered permanently. This is the most common, invisible reason a newly " +
                "connected USB stick doesn't get the letter you expect, or doesn't show up at first - it then " +
                "automatically gets a different, free letter. This is harmless and doesn't need fixing; if you " +
                "want to clean it up anyway, Disk Management (diskmgmt.msc) or \"mountvol /r\" as " +
                "administrator removes old assignments.");
        }
        catch (Exception ex)
        {
            DiagnoseLog.Schreiben(nameof(DatentraegerView) + ".Geisterbuchstaben", ex.ToString());
        }
    }

    private DatentraegerVolume? Gewaehlt =>
        LaufwerkBox.SelectedIndex >= 0 && LaufwerkBox.SelectedIndex < _volumes.Count
            ? _volumes[LaufwerkBox.SelectedIndex]
            : null;

    private async Task LadenAsync()
    {
        var alle = await _dienst.LadeAsync();

        // Das Windows-Laufwerk taucht bewusst gar nicht erst in der Auswahl auf.
        _volumes = alle.Where(v => !v.IstSystemlaufwerk).ToList();

        LaufwerkBox.Items.Clear();
        foreach (var v in _volumes)
            LaufwerkBox.Items.Add($"{v.Buchstabe}:  {(string.IsNullOrWhiteSpace(v.Bezeichnung) ? Loc.T("(ohne Bezeichnung)", "(no label)") : v.Bezeichnung)}" +
                                   $"  ·  {v.Dateisystem}  ·  {v.GroesseBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB");

        var ausgeblendet = alle.Count - _volumes.Count;

        if (_volumes.Count == 0)
        {
            LaufwerkInfoText.Text = Loc.T(
                "Es gibt hier nichts zu bearbeiten: Ausser dem Windows-Laufwerk " +
                "(das die Toolbox bewusst nicht anfasst) ist kein Datenträger angeschlossen. " +
                "Steck einen USB-Stick oder eine externe Platte an und lies neu ein.",
                "There's nothing to edit here: apart from the Windows drive " +
                "(which the toolbox deliberately doesn't touch), no drive is connected. " +
                "Plug in a USB stick or external drive and reload.");
            AllesAbschalten();
            return;
        }

        LaufwerkBox.SelectedIndex = 0;

        if (ausgeblendet > 0)
            MeldungText.Text = Loc.T(
                $"{ausgeblendet} Laufwerk(e) mit Windows darauf sind ausgeblendet - " +
                "die kann und soll man hier nicht bearbeiten.",
                $"{ausgeblendet} drive(s) with Windows on them are hidden - " +
                "those can't and shouldn't be edited here.");
    }

    private void AllesAbschalten()
    {
        foreach (var element in new UIElement[]
                 { BezeichnungFeld, BuchstabeBox, GroesseFeld, GroesseButton, DateisystemBox, BestaetigungFeld, FormatierenButton })
            element.IsEnabled = false;
    }

    private async void Laufwerk_Gewechselt(object sender, SelectionChangedEventArgs e)
    {
        var volume = Gewaehlt;
        if (volume is null) return;

        BezeichnungFeld.Text = volume.Bezeichnung;
        BezeichnungFeld.IsEnabled = true;
        BestaetigungFeld.Text = "";
        BestaetigungFeld.IsEnabled = true;
        FormatierenButton.IsEnabled = true;
        DateisystemBox.IsEnabled = true;

        LaufwerkInfoText.Text = Loc.T(
            $"{(volume.IstWechseldatentraeger ? "Wechseldatenträger" : "Fest eingebaut")} · " +
            $"{volume.BelegtBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB belegt, " +
            $"{volume.FreiBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB frei",
            $"{(volume.IstWechseldatentraeger ? "Removable drive" : "Permanently installed")} · " +
            $"{volume.BelegtBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB used, " +
            $"{volume.FreiBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB free");

        BuchstabeBox.Items.Clear();
        var belegte = _volumes.Select(v => v.Buchstabe).ToHashSet();
        foreach (var b in "DEFGHIJKLMNOPQRSTUVWXYZ".Where(b => !belegte.Contains(b)))
            BuchstabeBox.Items.Add($"{b}:");
        BuchstabeBox.IsEnabled = BuchstabeBox.Items.Count > 0;
        if (BuchstabeBox.Items.Count > 0) BuchstabeBox.SelectedIndex = 0;

        // Format-Vorschlag passend zum Datenträger
        var empfehlung = DateisystemRegeln.Empfehlung(volume.GroesseBytes, volume.IstWechseldatentraeger);
        DateisystemBox.SelectedItem = empfehlung;
        DateisystemHinweisAktualisieren();

        var schutz = DatentraegerSchutz.DarfFormatieren(volume);
        FormatWarnung.Text = schutz.Begruendung;
        FormatWarnung.Foreground = volume.BelegtBytes > 0
            ? (Brush)FindResource("DangerBrush")
            : (Brush)FindResource("TextMutedBrush");

        BestaetigungBeschriftung.Text = volume.BelegtBytes > 0
            ? Loc.T(
                  $"Zum Bestätigen bitte die Bezeichnung des Laufwerks abtippen: " +
                  $"„{(string.IsNullOrWhiteSpace(volume.Bezeichnung) ? volume.Buchstabe + ":" : volume.Bezeichnung)}\"",
                  $"To confirm, please type the drive's label: " +
                  $"\"{(string.IsNullOrWhiteSpace(volume.Bezeichnung) ? volume.Buchstabe + ":" : volume.Bezeichnung)}\"")
            : Loc.T("Das Laufwerk ist leer - hier genügt der Knopf.", "The drive is empty - the button alone is enough here.");

        await GroessenGrenzenLadenAsync(volume);
    }

    private async Task GroessenGrenzenLadenAsync(DatentraegerVolume volume)
    {
        var schutz = DatentraegerSchutz.DarfGroesseAendern(volume);

        GroesseFeld.IsEnabled = schutz.Erlaubt;
        GroesseButton.IsEnabled = schutz.Erlaubt;
        GroesseFeld.Text = (volume.GroesseBytes / (double)DateisystemRegeln.Gigabyte).ToString("0.#", CultureInfo.CurrentCulture);

        if (!schutz.Erlaubt)
        {
            GroesseHinweis.Text = schutz.Begruendung;
            return;
        }

        GroesseHinweis.Text = Loc.T("Ermittle die möglichen Grenzen …", "Determining the possible limits…");

        var grenzen = await _dienst.GrenzenAsync(volume.Buchstabe);

        GroesseHinweis.Text = grenzen is { } g
            ? Loc.T(
                  $"Möglich zwischen {g.Min / (double)DateisystemRegeln.Gigabyte:0.#} GB und " +
                  $"{g.Max / (double)DateisystemRegeln.Gigabyte:0.#} GB. {schutz.Begruendung}",
                  $"Possible between {g.Min / (double)DateisystemRegeln.Gigabyte:0.#} GB and " +
                  $"{g.Max / (double)DateisystemRegeln.Gigabyte:0.#} GB. {schutz.Begruendung}")
            : Loc.T(
                  "Die Grenzen konnten nicht ermittelt werden (dafür fragt Windows unter Umständen nach " +
                  "Administratorrechten). Du kannst trotzdem eine Größe eingeben - passt sie nicht, " +
                  "lehnt Windows sie ab, ohne etwas zu verändern.",
                  "The limits could not be determined (Windows may ask for administrator rights " +
                  "for this). You can still enter a size - if it doesn't fit, " +
                  "Windows will reject it without changing anything.");
    }

    private void Dateisystem_Gewechselt(object sender, SelectionChangedEventArgs e) => DateisystemHinweisAktualisieren();

    private void DateisystemHinweisAktualisieren()
    {
        var volume = Gewaehlt;
        if (volume is null || DateisystemBox.SelectedItem is not string dateisystem) return;

        var pruefung = DateisystemRegeln.Pruefen(dateisystem, volume.GroesseBytes);

        DateisystemHinweis.Text = pruefung.Hinweis;
        DateisystemHinweis.Foreground = pruefung.Moeglich
            ? (Brush)FindResource("TextMutedBrush")
            : (Brush)FindResource("DangerBrush");

        FormatierenButton.IsEnabled = pruefung.Moeglich;
    }

    private void Melden(string text, bool erfolg)
    {
        MeldungText.Text = text;
        MeldungText.Foreground = erfolg ? (Brush)FindResource("OkBrush") : (Brush)FindResource("DangerBrush");
    }

    // ================= Aktionen =================

    private async void Bezeichnung_Click(object sender, RoutedEventArgs e)
    {
        if (Gewaehlt is not { } volume) return;

        var (erfolg, meldung) = await _dienst.BezeichnungAendernAsync(volume, BezeichnungFeld.Text.Trim());
        Melden(meldung, erfolg);

        if (erfolg) await LadenAsync();
    }

    private async void Buchstabe_Click(object sender, RoutedEventArgs e)
    {
        if (Gewaehlt is not { } volume || BuchstabeBox.SelectedItem is not string wahl) return;

        var (erfolg, meldung) = await _dienst.BuchstabeAendernAsync(volume, wahl[0]);
        Melden(meldung, erfolg);

        if (erfolg) await LadenAsync();
    }

    private async void Groesse_Click(object sender, RoutedEventArgs e)
    {
        if (Gewaehlt is not { } volume) return;

        if (!double.TryParse(GroesseFeld.Text.Trim().Replace('.', ','), NumberStyles.Any,
                CultureInfo.CurrentCulture, out var gb) || gb <= 0)
        {
            Melden(Loc.T("Bitte eine Größe in GB eingeben, zum Beispiel 250.", "Please enter a size in GB, for example 250."), false);
            return;
        }

        Melden(Loc.T("Ändere die Größe (Windows fragt nach Bestätigung) …", "Changing the size (Windows will ask for confirmation)…"), true);

        var (erfolg, meldung) = await _dienst.GroesseAendernAsync(volume, (long)(gb * DateisystemRegeln.Gigabyte));
        Melden(meldung, erfolg);

        if (erfolg) await LadenAsync();
    }

    private async void Formatieren_Click(object sender, RoutedEventArgs e)
    {
        if (Gewaehlt is not { } volume || DateisystemBox.SelectedItem is not string dateisystem) return;

        // Bei belegten Laufwerken muss die Bezeichnung abgetippt werden. Ein Klick auf
        // "Ja" ist zu schnell passiert - Tippen erzwingt einen Moment Nachdenken.
        if (volume.BelegtBytes > 0)
        {
            var erwartet = string.IsNullOrWhiteSpace(volume.Bezeichnung)
                ? volume.Buchstabe + ":"
                : volume.Bezeichnung;

            if (!string.Equals(BestaetigungFeld.Text.Trim(), erwartet, StringComparison.OrdinalIgnoreCase))
            {
                Melden(Loc.T($"Zum Bestätigen bitte genau „{erwartet}\" eintippen. Es wurde nichts verändert.",
                             $"To confirm, please type exactly \"{erwartet}\". Nothing was changed."), false);
                return;
            }
        }

        var antwort = MessageBox.Show(
            Loc.T(
                $"Laufwerk {volume.Buchstabe}: wird jetzt mit {dateisystem} formatiert.\n\n" +
                "ALLE Daten darauf sind danach unwiderruflich weg - das lässt sich durch nichts " +
                "rückgängig machen.\n\nWirklich fortfahren?",
                $"Drive {volume.Buchstabe}: will now be formatted with {dateisystem}.\n\n" +
                "ALL data on it will be irrevocably gone afterward - this cannot be " +
                "undone by any means.\n\nReally continue?"),
            Loc.T("Letzte Nachfrage", "Final confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes)
        {
            Melden(Loc.T("Abgebrochen. Es wurde nichts verändert.", "Cancelled. Nothing was changed."), true);
            return;
        }

        Melden(Loc.T("Formatiere (Windows fragt nach Bestätigung) …", "Formatting (Windows will ask for confirmation)…"), true);

        var (erfolg, meldung) = await _dienst.FormatierenAsync(
            volume, dateisystem, BezeichnungFeld.Text.Trim(), SchnellBox.IsChecked == true);

        Melden(meldung, erfolg);

        if (erfolg) await LadenAsync();
    }

    private async void Neuladen_Click(object sender, RoutedEventArgs e)
    {
        MeldungText.Text = "";
        await LadenAsync();
    }
}
