using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Services;
using PCToolboxCore;
using PCToolboxCore.Models;
#if PRO_BUILD
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using PCToolbox.Services.Pro;
#endif

namespace PCToolbox.Modules;

public partial class DatentraegerView : UserControl
{
    private readonly DatentraegerService _dienst = new();
    private readonly LaufwerksbuchstabenKonfliktService _konfliktDienst = new();
    private List<DatentraegerVolume> _volumes = [];

#if PRO_BUILD
    private readonly DatentraegerStufe2Service _stufe2Dienst = new();
    private IReadOnlyList<DiskStufe2Info> _disks = [];
    private IReadOnlyList<DiskPartitionInfo> _partitionen = [];
    private DiskStufe2Info? _gewaehlteDisk;
    private HashSet<char> _geisterBuchstabenStufe2 = [];

    private ComboBox _diskAuswahl = null!;
    private TextBlock _diskInfoText = null!;
    private TextBox _neueGroesseFeld = null!;
    private CheckBox _maximaleGroesseBox = null!;
    private ComboBox _neuerBuchstabeBox = null!;
    private ComboBox _neuesDateisystemBox = null!;
    private TextBox _neueBezeichnungFeld = null!;
    private TextBlock _stufe2StatusText = null!;
    private ItemsControl _partitionenListe = null!;
#endif

    public DatentraegerView()
    {
        InitializeComponent();

#if PRO_BUILD
        Stufe2BereichAufbauen();
#endif

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
#if PRO_BUILD
        Loaded += async (_, _) => await Stufe2LadenAsync();
#endif
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

#if PRO_BUILD
    /// <summary>
    /// Baut den Stufe-2-Bereich (Partitionen erstellen/löschen) zur Laufzeit in
    /// ProZusatzBereich ein, statt ihn fest in DatentraegerView.xaml zu verdrahten - so
    /// bleibt die XAML-Datei fuer Free/Pro identisch, genau wie bei USB-Geräte/Netzwerk.
    /// </summary>
    private void Stufe2BereichAufbauen()
    {
        var expander = new Expander
        {
            Header = Loc.T("Partitionen verwalten (Pro)", "Manage partitions (Pro)")
        };

        var inhalt = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Partitionen erstellen oder löschen - riskanter als oben, weil hier die Partitionstabelle " +
                "einer ganzen Platte verändert wird, nicht nur eine schon bestehende Partition angepasst " +
                "wird. Das Systemlaufwerk und alle seine Partitionen (auch versteckte wie EFI/" +
                "Wiederherstellung) sind komplett gesperrt.",
                "Create or delete partitions - riskier than above, because this changes an entire disk's " +
                "partition table instead of just adjusting an existing partition. The system disk and all " +
                "its partitions (including hidden ones like EFI/recovery) are completely locked."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        });

        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T("PLATTE", "DISK"), FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextMutedBrush"), Margin = new Thickness(0, 0, 0, 6)
        });

        _diskAuswahl = new ComboBox { Width = 460, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        _diskAuswahl.SelectionChanged += DiskAuswahl_Gewechselt;
        inhalt.Children.Add(_diskAuswahl);

        _diskInfoText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 18),
            Foreground = (Brush)FindResource("TextMutedBrush")
        };
        inhalt.Children.Add(_diskInfoText);

        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T("Neue Partition im unallokierten Platz", "New partition in unallocated space"),
            FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8)
        });

        var neueZeile1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _neueGroesseFeld = new TextBox { Width = 90, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 8, 0) };
        neueZeile1.Children.Add(_neueGroesseFeld);
        neueZeile1.Children.Add(new TextBlock { Text = "GB", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) });
        _neuerBuchstabeBox = new ComboBox { Width = 70, Margin = new Thickness(0, 0, 16, 0) };
        neueZeile1.Children.Add(_neuerBuchstabeBox);
        _neuesDateisystemBox = new ComboBox { Width = 100 };
        foreach (var fs in DateisystemRegeln.Alle) _neuesDateisystemBox.Items.Add(fs);
        if (_neuesDateisystemBox.Items.Count > 0) _neuesDateisystemBox.SelectedIndex = 0;
        neueZeile1.Children.Add(_neuesDateisystemBox);
        inhalt.Children.Add(neueZeile1);

        _maximaleGroesseBox = new CheckBox
        {
            Content = Loc.T("Gesamten unallokierten Platz verwenden (empfohlen statt Größe eintippen)",
                             "Use all unallocated space (recommended instead of typing a size)"),
            IsChecked = true,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _maximaleGroesseBox.Checked += (_, _) => _neueGroesseFeld.IsEnabled = false;
        _maximaleGroesseBox.Unchecked += (_, _) => _neueGroesseFeld.IsEnabled = true;
        _neueGroesseFeld.IsEnabled = false;
        inhalt.Children.Add(_maximaleGroesseBox);

        var neueZeile2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _neueBezeichnungFeld = new TextBox
        {
            Width = 220, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 10, 0),
            Text = Loc.T("Neue Partition", "New partition")
        };
        neueZeile2.Children.Add(_neueBezeichnungFeld);
        var erstellenButton = new Button { Content = Loc.T("Partition erstellen", "Create partition"), Padding = new Thickness(12, 6, 12, 6) };
        erstellenButton.Click += PartitionErstellen_Click;
        neueZeile2.Children.Add(erstellenButton);
        inhalt.Children.Add(neueZeile2);

        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T("Partitionen auf dieser Platte", "Partitions on this disk"),
            FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 8)
        });

        _partitionenListe = new ItemsControl();
        var zeileFactory = new FrameworkElementFactory(typeof(Border));
        zeileFactory.SetValue(Border.BackgroundProperty, (Brush)FindResource("BgCardBrush"));
        zeileFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        zeileFactory.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));
        zeileFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));

        var innenFactory = new FrameworkElementFactory(typeof(DockPanel));

        var loeschenButtonFactory = new FrameworkElementFactory(typeof(Button));
        loeschenButtonFactory.SetValue(DockPanel.DockProperty, Dock.Right);
        loeschenButtonFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        loeschenButtonFactory.SetBinding(ContentControl.ContentProperty, new Binding("LoeschenText"));
        loeschenButtonFactory.SetBinding(UIElement.IsEnabledProperty, new Binding("KannLoeschen"));
        loeschenButtonFactory.SetValue(Button.PaddingProperty, new Thickness(10, 4, 10, 4));
        loeschenButtonFactory.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)PartitionLoeschen_Click);
        innenFactory.AppendChild(loeschenButtonFactory);

        var textStackFactory = new FrameworkElementFactory(typeof(StackPanel));
        var titelFactory = new FrameworkElementFactory(typeof(TextBlock));
        titelFactory.SetBinding(TextBlock.TextProperty, new Binding("Anzeige"));
        titelFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textStackFactory.AppendChild(titelFactory);

        var begruendungFactory = new FrameworkElementFactory(typeof(TextBlock));
        begruendungFactory.SetBinding(TextBlock.TextProperty, new Binding("Begruendung"));
        begruendungFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        begruendungFactory.SetValue(TextBlock.ForegroundProperty, (Brush)FindResource("TextMutedBrush"));
        begruendungFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        begruendungFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
        textStackFactory.AppendChild(begruendungFactory);

        innenFactory.AppendChild(textStackFactory);
        zeileFactory.AppendChild(innenFactory);
        _partitionenListe.ItemTemplate = new DataTemplate { VisualTree = zeileFactory };
        inhalt.Children.Add(_partitionenListe);

        _stufe2StatusText = new TextBlock { FontSize = 12, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
        inhalt.Children.Add(_stufe2StatusText);

        var neuLadenStufe2Button = new Button
        {
            Content = Loc.T("Neu einlesen", "Reload"), Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left
        };
        neuLadenStufe2Button.Click += async (_, _) => await Stufe2LadenAsync();
        inhalt.Children.Add(neuLadenStufe2Button);

        expander.Content = inhalt;
        ProZusatzBereich.Children.Add(expander);
    }

    private async Task Stufe2LadenAsync()
    {
        _stufe2StatusText.Text = Loc.T("Lese Platten und Partitionen ein …", "Reading disks and partitions…");

        var (disks, partitionen) = await _stufe2Dienst.LadenAsync();
        _disks = disks;
        _partitionen = partitionen;

        // Geister-Buchstaben (reserviert, aber kein aktives Laufwerk - z.B. ein getrennt
        // gemountetes Cloud-Laufwerk wie Google Drive) muessen bei der Buchstaben-Auswahl
        // fuer eine neue Partition ausgeschlossen werden, sonst schlaegt New-Partition mit
        // "Pfad wird bereits verwendet" fehl (genau so beim ersten echten Test passiert).
        _geisterBuchstabenStufe2 = (await _konfliktDienst.GeisterBuchstabenAsync()).ToHashSet();

        var vorherIndex = _diskAuswahl.SelectedIndex;
        _diskAuswahl.Items.Clear();
        foreach (var d in _disks)
            _diskAuswahl.Items.Add(Loc.T(
                $"Platte {d.Nummer}: {d.Name}  ·  {d.GroesseBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB gesamt, " +
                $"{d.UnallokiertBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB unallokiert  ·  " +
                LaufwerksbuchstabenProPlatte.Anzeige(d.Laufwerksbuchstaben),
                $"Disk {d.Nummer}: {d.Name}  ·  {d.GroesseBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB total, " +
                $"{d.UnallokiertBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB unallocated  ·  " +
                LaufwerksbuchstabenProPlatte.Anzeige(d.Laufwerksbuchstaben)));

        if (_disks.Count > 0)
            _diskAuswahl.SelectedIndex = vorherIndex >= 0 && vorherIndex < _disks.Count ? vorherIndex : 0;

        _stufe2StatusText.Text = Loc.T(
            $"{_disks.Count} Platte(n), {_partitionen.Count} Partition(en) gefunden.",
            $"{_disks.Count} disk(s), {_partitionen.Count} partition(s) found.");
    }

    private void DiskAuswahl_Gewechselt(object sender, SelectionChangedEventArgs e)
    {
        if (_diskAuswahl.SelectedIndex < 0 || _diskAuswahl.SelectedIndex >= _disks.Count)
        {
            _gewaehlteDisk = null;
            _partitionenListe.ItemsSource = null;
            return;
        }

        var disk = _disks[_diskAuswahl.SelectedIndex];
        _gewaehlteDisk = disk;

        var systemHinweis = disk.IstBoot || disk.IstSystem
            ? Loc.T(" Systemplatte - Partitionen hier lassen sich nicht löschen.",
                    " System disk - partitions here cannot be deleted.")
            : "";
        _diskInfoText.Text = Loc.T(
            $"{disk.UnallokiertBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB unallokiert ({disk.Partitionsstil}).{systemHinweis}",
            $"{disk.UnallokiertBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB unallocated ({disk.Partitionsstil}).{systemHinweis}");

        // DriveInfo.GetDrives() statt nur der eigenen Listen: liefert JEDEN zur Zeit
        // zugewiesenen Buchstaben, auch kaputte/leere Volume-Reste ohne echtes Dateisystem
        // dahinter (IsReady=false) - genau die Art "Geister im laufenden Betrieb", die weder
        // in den eigenen Partitionslisten noch in der Registry-basierten Geister-Erkennung
        // auftaucht, aber New-Partition trotzdem mit "Pfad wird bereits verwendet" blockiert.
        var systemweitVergeben = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();

        var belegteBuchstaben = _partitionen
            .Select(p => p.Buchstabe)
            .Concat(_volumes.Select(v => (char?)v.Buchstabe))
            .Where(b => b.HasValue)
            .Select(b => b!.Value)
            .Concat(_geisterBuchstabenStufe2)
            .Concat(systemweitVergeben)
            .ToHashSet();

        _neuerBuchstabeBox.Items.Clear();
        foreach (var b in "DEFGHIJKLMNOPQRSTUVWXYZ".Where(b => !belegteBuchstaben.Contains(b)))
            _neuerBuchstabeBox.Items.Add($"{b}:");
        if (_neuerBuchstabeBox.Items.Count > 0) _neuerBuchstabeBox.SelectedIndex = 0;

        PartitionenListeAktualisieren();
    }

    private void PartitionenListeAktualisieren()
    {
        if (_gewaehlteDisk is not { } disk)
        {
            _partitionenListe.ItemsSource = null;
            return;
        }

        _partitionenListe.ItemsSource = _partitionen
            .Where(p => p.DiskNummer == disk.Nummer)
            .Select(p =>
            {
                var schutz = DatentraegerStufe2Schutz.DarfPartitionLoeschen(p, disk);
                return new PartitionZeile
                {
                    Partition = p,
                    Anzeige = Loc.T(
                        $"Partition {p.PartitionNummer}{(p.Buchstabe is { } b ? $"  ({b}:)" : "")}  ·  " +
                        $"{p.GroesseBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB  ·  {p.Typ}",
                        $"Partition {p.PartitionNummer}{(p.Buchstabe is { } b2 ? $"  ({b2}:)" : "")}  ·  " +
                        $"{p.GroesseBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB  ·  {p.Typ}"),
                    Begruendung = schutz.Begruendung,
                    KannLoeschen = schutz.Erlaubt,
                    LoeschenText = Loc.T("Löschen", "Delete")
                };
            })
            .ToList();
    }

    private async void PartitionErstellen_Click(object sender, RoutedEventArgs e)
    {
        if (_gewaehlteDisk is not { } disk) return;
        if (_neuerBuchstabeBox.SelectedItem is not string buchstabeText || _neuesDateisystemBox.SelectedItem is not string dateisystem)
        {
            _stufe2StatusText.Text = Loc.T("Bitte Laufwerksbuchstabe und Dateisystem wählen.", "Please choose a drive letter and file system.");
            return;
        }

        long? groesseBytes;
        string groesseAnzeige;

        if (_maximaleGroesseBox.IsChecked == true)
        {
            groesseBytes = null;
            groesseAnzeige = Loc.T($"gesamten unallokierten Platz ({disk.UnallokiertBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB)",
                                   $"all unallocated space ({disk.UnallokiertBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB)");
        }
        else
        {
            if (!double.TryParse(_neueGroesseFeld.Text.Trim().Replace('.', ','), NumberStyles.Any,
                    CultureInfo.CurrentCulture, out var gb) || gb <= 0)
            {
                _stufe2StatusText.Text = Loc.T("Bitte eine Größe in GB eingeben, zum Beispiel 50.", "Please enter a size in GB, for example 50.");
                return;
            }

            groesseBytes = (long)(gb * DateisystemRegeln.Gigabyte);
            groesseAnzeige = $"{gb:0.#} GB";
        }

        var buchstabe = buchstabeText[0];

        var antwort = MessageBox.Show(
            Loc.T(
                $"Neue Partition ({groesseAnzeige}) auf {disk.Name} erstellen, formatiert mit {dateisystem}, erreichbar unter {buchstabe}:?",
                $"Create a new partition ({groesseAnzeige}) on {disk.Name}, formatted with {dateisystem}, accessible at {buchstabe}:?"),
            Loc.T("Partition erstellen", "Create partition"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (antwort != MessageBoxResult.Yes) return;

        _stufe2StatusText.Text = Loc.T("Erstelle Partition (Windows fragt nach Bestätigung) …", "Creating partition (Windows will ask for confirmation)…");

        var (erfolg, meldung) = await _stufe2Dienst.PartitionErstellenAsync(
            disk, groesseBytes, buchstabe, dateisystem, _neueBezeichnungFeld.Text.Trim());
        _stufe2StatusText.Text = meldung;

        // Eine neue Partition aendert die Laufwerksbuchstaben-Liste oben ("Laufwerke zum
        // Formatieren") - die muss mit aktualisiert werden, sonst zeigt sie den alten Stand,
        // bis man dort manuell auf "Neu einlesen" klickt (genau so live entdeckt).
        if (erfolg)
        {
            await Stufe2LadenAsync();
            await LadenAsync();
        }
    }

    private async void PartitionLoeschen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PartitionZeile zeile } || _gewaehlteDisk is not { } disk) return;

        var partition = zeile.Partition;
        var bezeichnung = partition.Buchstabe is { } b
            ? $"{b}:"
            : Loc.T($"Partition {partition.PartitionNummer}", $"partition {partition.PartitionNummer}");

        var antwort = MessageBox.Show(
            Loc.T(
                $"{bezeichnung} auf {disk.Name} wirklich löschen?\n\nALLE Daten darauf sind danach unwiderruflich weg. " +
                "Der Platz wird unallokiert, nicht automatisch einem Nachbarlaufwerk zugeschlagen.",
                $"Really delete {bezeichnung} on {disk.Name}?\n\nALL data on it will be irrevocably gone afterward. " +
                "The space becomes unallocated, not automatically added to a neighboring drive."),
            Loc.T("Partition löschen", "Delete partition"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (antwort != MessageBoxResult.Yes) return;

        if (!BestaetigungswortAbfragen(
                Loc.T("Zum endgültigen Bestätigen bitte LÖSCHEN eintippen (Groß-/Kleinschreibung egal):",
                      "To finally confirm, please type DELETE (case doesn't matter):"),
                Loc.T("LÖSCHEN", "DELETE")))
        {
            _stufe2StatusText.Text = Loc.T("Abgebrochen. Es wurde nichts verändert.", "Cancelled. Nothing was changed.");
            return;
        }

        _stufe2StatusText.Text = Loc.T("Lösche Partition (Windows fragt nach Bestätigung) …", "Deleting partition (Windows will ask for confirmation)…");

        var (erfolg, meldung) = await _stufe2Dienst.PartitionLoeschenAsync(partition, disk);
        _stufe2StatusText.Text = meldung;

        if (erfolg)
        {
            await Stufe2LadenAsync();
            await LadenAsync();
        }
    }

    /// <summary>
    /// Kleiner eigener Bestätigungsdialog mit Abtipp-Zwang - wie bei Formatieren in Stufe 1
    /// (dort die Laufwerksbezeichnung), hier zusätzlich zur normalen Ja/Nein-Nachfrage, weil
    /// eine Partition löschen genauso endgültig ist wie Formatieren.
    /// </summary>
    private bool BestaetigungswortAbfragen(string erklaerung, string erwartetesWort)
    {
        var fenster = new Window
        {
            Title = Loc.T("Bestätigung erforderlich", "Confirmation required"),
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("BgPanelBrush")
        };

        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = erklaerung, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });

        var eingabe = new TextBox { Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 16) };
        stack.Children.Add(eingabe);

        var bestaetigt = false;
        var knopfZeile = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var abbrechenButton = new Button { Content = Loc.T("Abbrechen", "Cancel"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        abbrechenButton.Click += (_, _) => fenster.Close();
        var okButton = new Button { Content = Loc.T("Bestätigen", "Confirm"), Padding = new Thickness(12, 6, 12, 6), IsEnabled = false };
        okButton.Click += (_, _) => { bestaetigt = true; fenster.Close(); };
        eingabe.TextChanged += (_, _) => okButton.IsEnabled = string.Equals(eingabe.Text.Trim(), erwartetesWort, StringComparison.OrdinalIgnoreCase);
        knopfZeile.Children.Add(abbrechenButton);
        knopfZeile.Children.Add(okButton);
        stack.Children.Add(knopfZeile);

        fenster.Content = stack;
        fenster.ShowDialog();

        return bestaetigt;
    }
#endif
}

#if PRO_BUILD
public class PartitionZeile
{
    public DiskPartitionInfo Partition { get; set; } = null!;
    public string Anzeige { get; set; } = "";
    public string Begruendung { get; set; } = "";
    public bool KannLoeschen { get; set; }
    public string LoeschenText { get; set; } = "";
}
#endif
