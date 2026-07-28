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

public partial class UsbGeraeteView : UserControl
{
    private readonly UsbGeraeteService _dienst = new();

#if PRO_BUILD
    private readonly UsbGeraeteProService _proDienst = new();
    private readonly LaufwerksbuchstabenKonfliktService _buchstabenDienst = new();
    private Border _buchstabenBereich = null!;
    private TextBlock _buchstabenText = null!;
    private ItemsControl _buchstabenListe = null!;
    private TextBlock _geisterStatusText = null!;
    private ListView _geisterListe = null!;
#endif

    public UsbGeraeteView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("USB-Geräte", "USB Devices");
#if PRO_BUILD
        TitelText.Text += " " + Loc.T("(Pro)", "(Pro)");
#endif
        QuelleHinweis.ToolTip = Loc.T(
            "WMI Win32_PnPEntity, gefiltert auf Geräte-IDs unter dem USB-Bus - dieselbe Fehlerdeutung wie im Treiber-Check.",
            "WMI Win32_PnPEntity, filtered to device IDs under the USB bus - same error interpretation as the driver check.");
        EinleitungText.Text = Loc.T(
            "Zeigt alle gerade angeschlossenen USB-Geräte mit ihrem Status - inklusive Erklärung, wenn Windows ein Problem meldet. Reine Anzeige, nichts wird deaktiviert oder entfernt.",
            "Shows all currently connected USB devices with their status - including an explanation if Windows reports a problem. Read-only, nothing is disabled or removed.");
        EnergiesparTitel.Text = Loc.T("ℹ️ \"Gerät verschwindet manchmal von selbst\"?", "ℹ️ \"Device sometimes disappears on its own\"?");
        EnergiesparText.Text = UsbErklaerungen.EnergiesparmodusVerdacht();
        NeuLadenButton.Content = Loc.T("Neu einlesen", "Reload");

#if PRO_BUILD
        ProZusatzBereichAufbauen();
#endif

        Loaded += async (_, _) =>
        {
            try
            {
                await LadenAsync();
#if PRO_BUILD
                await BuchstabenLadenAsync();
                await GeisterLadenAsync();
#endif
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(UsbGeraeteView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "UsbGeraeteView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async void NeuLaden_Click(object sender, RoutedEventArgs e)
    {
        await LadenAsync();
#if PRO_BUILD
        await BuchstabenLadenAsync();
        await GeisterLadenAsync();
#endif
    }

    private async Task LadenAsync()
    {
        NeuLadenButton.IsEnabled = false;
        ZusammenfassungText.Text = Loc.T("Lese USB-Geräte ein …", "Reading USB devices…");

        var geraete = await _dienst.LadeAsync();
        var heute = DateTime.Today;

        var zeilen = geraete
            .Select(g => new UsbZeile(g, TreiberBewerter.Bewerten(g, heute), FarbeFuer))
            .OrderBy(z => Reihenfolge(z.Ampel))
            .ThenBy(z => z.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Liste.ItemsSource = zeilen;

        var mitProblem = zeilen.Count(z => z.Ampel == AmpelStatus.Rot);
        var hinweise = zeilen.Count(z => z.Ampel == AmpelStatus.Gelb);

        ZusammenfassungText.Text = TreiberBewerter.Zusammenfassung(zeilen.Count, mitProblem, hinweise);

        NeuLadenButton.IsEnabled = true;
    }

    private static int Reihenfolge(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Rot => 0,
        AmpelStatus.Gelb => 1,
        _ => 2
    };

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

#if PRO_BUILD
    /// <summary>
    /// Baut die Pro-Zusatzteile (Geister-Geräte, Laufwerksbuchstaben-Konflikte) zur Laufzeit in
    /// ProZusatzBereich ein, statt sie fest in UsbGeraeteView.xaml zu verdrahten - so bleibt die
    /// XAML-Datei fuer Free/Pro identisch (leerer Platzhalter-Container), genau wie bei den
    /// Pro-Sidebar-/Kachel-Eintraegen in MainWindow.xaml.cs/OverviewView.xaml.cs.
    /// </summary>
    private void ProZusatzBereichAufbauen()
    {
        _buchstabenBereich = new Border
        {
            Background = (Brush)FindResource("BgCardBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 14),
            Visibility = Visibility.Collapsed
        };
        var buchstabenStack = new StackPanel();
        buchstabenStack.Children.Add(new TextBlock
        {
            Text = Loc.T("Reservierte Laufwerksbuchstaben", "Reserved drive letters"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });
        _buchstabenText = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };
        buchstabenStack.Children.Add(_buchstabenText);

        _buchstabenListe = new ItemsControl();
        var buchstabenZeileFactory = new FrameworkElementFactory(typeof(DockPanel));
        buchstabenZeileFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));

        var buchstabenButtonFactory = new FrameworkElementFactory(typeof(Button));
        buchstabenButtonFactory.SetValue(DockPanel.DockProperty, Dock.Right);
        buchstabenButtonFactory.SetBinding(ContentControl.ContentProperty, new Binding("AufloesenText"));
        buchstabenButtonFactory.SetValue(Button.PaddingProperty, new Thickness(10, 4, 10, 4));
        buchstabenButtonFactory.SetBinding(FrameworkElement.TagProperty, new Binding("Buchstabe"));
        buchstabenButtonFactory.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)BuchstabeAufloesen_Click);
        buchstabenZeileFactory.AppendChild(buchstabenButtonFactory);

        var buchstabenTextFactory = new FrameworkElementFactory(typeof(TextBlock));
        buchstabenTextFactory.SetBinding(TextBlock.TextProperty, new Binding("Anzeige"));
        buchstabenTextFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        buchstabenZeileFactory.AppendChild(buchstabenTextFactory);

        _buchstabenListe.ItemTemplate = new DataTemplate { VisualTree = buchstabenZeileFactory };
        buchstabenStack.Children.Add(_buchstabenListe);
        _buchstabenBereich.Child = buchstabenStack;
        ProZusatzBereich.Children.Add(_buchstabenBereich);

        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T("Geräte, die Windows sich noch merkt", "Devices Windows still remembers"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });
        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Geräte, die früher mal angeschlossen waren, aber gerade nicht da sind. Harmlos, solange sie nicht " +
                "stören - \"Entfernen\" löscht nur den Windows-internen Eintrag, das physische Gerät ist davon nicht " +
                "betroffen und meldet sich beim nächsten Anschließen ganz normal neu an.",
                "Devices that were connected before but aren't currently present. Harmless unless they cause trouble - " +
                "\"Remove\" only deletes the internal Windows entry, the physical device itself is unaffected and " +
                "will register normally again the next time it's plugged in."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        _geisterStatusText = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };
        ProZusatzBereich.Children.Add(_geisterStatusText);

        var geisterWerkzeugleiste = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var alleMarkierenButton = new Button { Content = Loc.T("Alle markieren", "Select all"), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0) };
        alleMarkierenButton.Click += (_, _) =>
        {
            if (_geisterListe.ItemsSource is not IEnumerable<GeisterZeile> zeilen) return;
            var liste = zeilen.ToList();
            var alleSchonMarkiert = liste.Count > 0 && liste.All(z => z.Ausgewaehlt);
            foreach (var zeile in liste) zeile.Ausgewaehlt = !alleSchonMarkiert;
        };
        geisterWerkzeugleiste.Children.Add(alleMarkierenButton);

        var ausgewaehlteEntfernenButton = new Button { Content = Loc.T("Ausgewählte entfernen", "Remove selected"), Padding = new Thickness(10, 4, 10, 4) };
        ausgewaehlteEntfernenButton.Click += AusgewaehlteEntfernen_Click;
        geisterWerkzeugleiste.Children.Add(ausgewaehlteEntfernenButton);
        ProZusatzBereich.Children.Add(geisterWerkzeugleiste);

        _geisterListe = new ListView { Background = (Brush)FindResource("BgCardBrush"), BorderThickness = new Thickness(0) };
        var gridView = new GridView();

        var checkboxFactory = new FrameworkElementFactory(typeof(CheckBox));
        checkboxFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Ausgewaehlt") { Mode = BindingMode.TwoWay });
        checkboxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        gridView.Columns.Add(new GridViewColumn { Width = 32, CellTemplate = new DataTemplate { VisualTree = checkboxFactory } });

        gridView.Columns.Add(new GridViewColumn { Width = 280, DisplayMemberBinding = new Binding("Name") });
        gridView.Columns.Add(new GridViewColumn { Width = 180, DisplayMemberBinding = new Binding("LetzteAktivitaetAnzeige") });

        var entfernenButtonFactory = new FrameworkElementFactory(typeof(Button));
        entfernenButtonFactory.SetBinding(ContentControl.ContentProperty, new Binding("EntfernenText"));
        entfernenButtonFactory.SetValue(Button.PaddingProperty, new Thickness(10, 4, 10, 4));
        entfernenButtonFactory.SetBinding(FrameworkElement.TagProperty, new Binding("PnpDeviceId"));
        entfernenButtonFactory.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)GeraetEntfernen_Click);
        gridView.Columns.Add(new GridViewColumn { Width = 140, CellTemplate = new DataTemplate { VisualTree = entfernenButtonFactory } });

        _geisterListe.View = gridView;
        _geisterListe.PreviewMouseWheel += (_, e) =>
        {
            HauptScroll.ScrollToVerticalOffset(HauptScroll.VerticalOffset - e.Delta);
            e.Handled = true;
        };
        ProZusatzBereich.Children.Add(_geisterListe);
    }

    private async Task BuchstabenLadenAsync()
    {
        var geister = await _buchstabenDienst.GeisterBuchstabenAsync();
        if (geister.Count == 0)
        {
            _buchstabenBereich.Visibility = Visibility.Collapsed;
            return;
        }

        _buchstabenBereich.Visibility = Visibility.Visible;
        _buchstabenText.Text = Loc.T(
            "Diese Laufwerksbuchstaben sind noch reserviert, obwohl gerade kein Laufwerk damit verbunden ist. " +
            "Auflösen entfernt nur die Reservierung - falls das Gerät nochmal angeschlossen wird, bekommt es einfach " +
            "einen neuen Buchstaben zugewiesen.",
            "These drive letters are still reserved even though no drive is currently connected with them. " +
            "Resolving only removes the reservation - if the device is connected again, it simply gets a new letter assigned.");

        _buchstabenListe.ItemsSource = geister.Select(b => new BuchstabenZeile
        {
            Buchstabe = b,
            Anzeige = $"{b}:",
            AufloesenText = Loc.T("Auflösen", "Resolve")
        }).ToList();
    }

    private async Task GeisterLadenAsync()
    {
        NeuLadenButton.IsEnabled = false;
        _geisterStatusText.Text = Loc.T("Lese Geräteverlauf ein …", "Reading device history…");

        var geister = await _proDienst.LadeGeisterGeraeteAsync();

        _geisterListe.ItemsSource = geister.Select(g => new GeisterZeile
        {
            PnpDeviceId = g.PnpDeviceId,
            Name = g.Name,
            LetzteAktivitaetAnzeige = g.LetzteAktivitaet is { } zeit ? zeit.ToString("dd.MM.yyyy HH:mm") : Loc.T("unbekannt", "unknown"),
            EntfernenText = Loc.T("Entfernen", "Remove")
        }).ToList();

        _geisterStatusText.Text = geister.Count == 0
            ? Loc.T("Keine früheren Geräte gefunden - entweder alles noch angeschlossen oder nichts gespeichert.",
                    "No earlier devices found - either everything is still connected or nothing is stored.")
            : Loc.T($"{geister.Count} frühere Geräte gefunden.", $"{geister.Count} earlier devices found.");

        NeuLadenButton.IsEnabled = true;
    }

    private async void GeraetEntfernen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string pnpDeviceId } button) return;

        var name = (_geisterListe.ItemsSource as IEnumerable<GeisterZeile>)?.FirstOrDefault(z => z.PnpDeviceId == pnpDeviceId)?.Name ?? pnpDeviceId;

        var antwort = MessageBox.Show(
            Loc.T(
                $"„{name}\" wirklich entfernen?\n\nDas löscht nur den Windows-internen Eintrag für dieses Gerät. " +
                "Wird es nochmal angeschlossen, meldet es sich ganz normal neu an - nichts geht dabei verloren.\n\n" +
                "Windows fragt im nächsten Schritt nach einer Administratorbestätigung.",
                $"Really remove \"{name}\"?\n\nThis only deletes the internal Windows entry for this device. " +
                "If it's connected again, it will register normally - nothing is lost.\n\n" +
                "Windows will ask for an administrator confirmation in the next step."),
            Loc.T("Gerät entfernen", "Remove device"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        button.IsEnabled = false;
        var erfolg = await Task.Run(() => _proDienst.GeraetEntfernen(pnpDeviceId));

        _geisterStatusText.Text = erfolg
            ? Loc.T($"„{name}\" wurde entfernt.", $"\"{name}\" was removed.")
            : Loc.T($"„{name}\" konnte nicht entfernt werden - entweder wurde die Bestätigung abgelehnt, oder Windows kennt diesen Eintrag so nicht (mehr).",
                    $"\"{name}\" could not be removed - either the confirmation was declined, or Windows no longer recognizes this entry.");

        await GeisterLadenAsync();
    }

    private async void AusgewaehlteEntfernen_Click(object sender, RoutedEventArgs e)
    {
        if (_geisterListe.ItemsSource is not IEnumerable<GeisterZeile> zeilen) return;

        var ausgewaehlt = zeilen.Where(z => z.Ausgewaehlt).ToList();
        if (ausgewaehlt.Count == 0)
        {
            MessageBox.Show(
                Loc.T("Keine Geräte markiert.", "No devices selected."),
                Loc.T("Auswahl leer", "Selection empty"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var namenListe = string.Join("\n", ausgewaehlt.Select(z => $"• {z.Name}"));
        var antwort = MessageBox.Show(
            Loc.T(
                $"{ausgewaehlt.Count} Geräte wirklich entfernen?\n\n{namenListe}\n\n" +
                "Das löscht nur die Windows-internen Einträge, keine physischen Geräte gehen dabei verloren.\n\n" +
                "Windows fragt im nächsten Schritt nach EINER Administratorbestätigung für alle zusammen.",
                $"Really remove {ausgewaehlt.Count} devices?\n\n{namenListe}\n\n" +
                "This only deletes the internal Windows entries, no physical devices are affected.\n\n" +
                "Windows will ask for ONE administrator confirmation covering all of them in the next step."),
            Loc.T("Mehrere Geräte entfernen", "Remove multiple devices"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        var vorherAnzahl = zeilen.Count();
        var ids = ausgewaehlt.Select(z => z.PnpDeviceId).ToList();

        NeuLadenButton.IsEnabled = false;
        _geisterStatusText.Text = Loc.T("Entferne ausgewählte Geräte …", "Removing selected devices…");

        var erfolg = await Task.Run(() => _proDienst.GeraeteEntfernen(ids));

        await GeisterLadenAsync();
        var nachherAnzahl = (_geisterListe.ItemsSource as IEnumerable<GeisterZeile>)?.Count() ?? 0;
        var entferntAnzahl = vorherAnzahl - nachherAnzahl;

        _geisterStatusText.Text = erfolg
            ? Loc.T($"{entferntAnzahl} von {ausgewaehlt.Count} ausgewählten Geräten wurden entfernt.",
                    $"{entferntAnzahl} of {ausgewaehlt.Count} selected devices were removed.")
            : Loc.T("Entfernen wurde abgebrochen oder abgelehnt - es wurde nichts geändert.",
                    "Removal was cancelled or declined - nothing was changed.");
    }

    private async void BuchstabeAufloesen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: char buchstabe } button) return;

        var antwort = MessageBox.Show(
            Loc.T(
                $"Reservierung für Laufwerksbuchstabe „{buchstabe}:\" auflösen?\n\n" +
                "Es wird nur die veraltete Zuordnung entfernt, kein Laufwerk und keine Daten. " +
                "Windows fragt im nächsten Schritt nach einer Administratorbestätigung.",
                $"Resolve the reservation for drive letter \"{buchstabe}:\"?\n\n" +
                "Only the outdated mapping is removed, no drive and no data. " +
                "Windows will ask for an administrator confirmation in the next step."),
            Loc.T("Laufwerksbuchstabe freigeben", "Free up drive letter"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        button.IsEnabled = false;
        var erfolg = await Task.Run(() => _proDienst.LaufwerksbuchstabenKonfliktAufloesen(buchstabe));

        _buchstabenText.Text = erfolg
            ? Loc.T($"Buchstabe „{buchstabe}:\" wurde freigegeben.", $"Letter \"{buchstabe}:\" was freed up.")
            : Loc.T($"Buchstabe „{buchstabe}:\" konnte nicht freigegeben werden - vermutlich wurde die Bestätigung abgelehnt.",
                    $"Letter \"{buchstabe}:\" could not be freed up - the confirmation was probably declined.");

        await BuchstabenLadenAsync();
    }
#endif
}

#if PRO_BUILD
public class GeisterZeile : System.ComponentModel.INotifyPropertyChanged
{
    private bool _ausgewaehlt;

    public string PnpDeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string LetzteAktivitaetAnzeige { get; set; } = "";
    public string EntfernenText { get; set; } = "";

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

public class BuchstabenZeile
{
    public char Buchstabe { get; set; }
    public string Anzeige { get; set; } = "";
    public string AufloesenText { get; set; } = "";
}
#endif

public class UsbZeile
{
    private readonly GeraetBefund _geraet;

    public UsbZeile(GeraetBefund geraet, TreiberBewertung bewertung, Func<AmpelStatus, Brush> farbe)
    {
        _geraet = geraet;
        Ampel = bewertung.Ampel;
        Kurzform = bewertung.Kurzform;
        WasEsIst = bewertung.WasEsIst;
        Empfehlung = bewertung.Empfehlung;
        Symbol = bewertung.Ampel.Symbol();
        AmpelFarbe = farbe(bewertung.Ampel);
    }

    public AmpelStatus Ampel { get; }
    public string Name => _geraet.Name;
    public string Kurzform { get; }
    public string WasEsIst { get; }
    public string Empfehlung { get; }
    public string Symbol { get; }
    public Brush AmpelFarbe { get; }

    public string Herkunft
    {
        get
        {
            var teile = new List<string>();
            if (!string.IsNullOrWhiteSpace(_geraet.Hersteller)) teile.Add(_geraet.Hersteller!);
            if (!string.IsNullOrWhiteSpace(_geraet.Klasse)) teile.Add(Loc.T($"Kategorie: {_geraet.Klasse}", $"Category: {_geraet.Klasse}"));
            if (_geraet.FehlerCode != 0) teile.Add(Loc.T($"Windows-Fehlernummer {_geraet.FehlerCode}", $"Windows error number {_geraet.FehlerCode}"));
            return string.Join("   ·   ", teile);
        }
    }
}
