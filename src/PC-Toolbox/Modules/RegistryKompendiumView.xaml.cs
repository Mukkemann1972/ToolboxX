using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
using PCToolboxCore.Models;
#if PRO_BUILD
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using PCToolbox.Services.Pro;
#endif

namespace PCToolbox.Modules;

public partial class RegistryKompendiumView : UserControl
{
    private readonly RegistryKompendiumService _dienst = new();
    private List<RegistryZeile> _zeilen = [];

#if PRO_BUILD
    private readonly RegistryZuruecksetzenService _proDienst = new();
    private Border _proBereich = null!;
    private ItemsControl _proListe = null!;
#endif

    public RegistryKompendiumView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Registry-Intelligenz", "Registry Intelligence");
        QuelleHinweis.ToolTip = Loc.T(
            "Direkter Registry-Zugriff (HKEY_LOCAL_MACHINE/HKEY_CURRENT_USER) auf eine handverlesene Auswahl von Stellen - kein vollständiger Registry-Browser, sondern eine erklärte Auswahl. Rein lesend.",
            "Direct registry access (HKEY_LOCAL_MACHINE/HKEY_CURRENT_USER) to a handpicked selection of locations - not a full registry browser, but an explained selection. Read-only.");
        EinleitungText.Text = Loc.T(
            "Die Windows-Registry ist riesig und für sich genommen unlesbar - ein vollständiger Browser wie regedit hilft nur, wenn man schon weiß, wonach man sucht. Hier steht stattdessen eine handverlesene Auswahl von Stellen, die wirklich oft für Verwirrung sorgen oder sicherheitsrelevant sind, mit Erklärung und deinem aktuellen Wert. Nichts wird verändert.",
            "The Windows registry is huge and unreadable on its own - a full browser like regedit only helps once you already know what you're looking for. Instead, here's a handpicked selection of locations that genuinely cause confusion often or are security-relevant, with an explanation and your current value. Nothing is changed.");

#if PRO_BUILD
        ProZusatzBereichAufbauen();
#endif

        Loaded += async (_, _) =>
        {
            try
            {
                await LadenAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(RegistryKompendiumView), ex.ToString());
                System.Windows.MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "RegistryKompendiumView – Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };
    }

    private async Task LadenAsync()
    {
        _zeilen = await _dienst.LadeAsync();
        Liste.ItemsSource = _zeilen.Select(ZuAnzeigeZeile).ToList();

#if PRO_BUILD
        ProAktualisieren();
#endif
    }

    private RegistryAnzeigeZeile ZuAnzeigeZeile(RegistryZeile zeile) => new()
    {
        Titel = zeile.Eintrag.Titel,
        Pfad = $"{zeile.Eintrag.Hive}\\{zeile.Eintrag.Pfad}\\{zeile.Eintrag.Wertname}",
        Erklaerung = zeile.Eintrag.Erklaerung,
        BefundText = zeile.Befund.Text,
        Symbol = zeile.Befund.Status.Symbol(),
        AmpelFarbe = FarbeFuer(zeile.Befund.Status),
        AktuellerWertZeile = Loc.T($"Aktueller Wert: {zeile.AktuellerWertText}", $"Current value: {zeile.AktuellerWertText}")
    };

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private class RegistryAnzeigeZeile
    {
        public string Titel { get; set; } = "";
        public string Pfad { get; set; } = "";
        public string Erklaerung { get; set; } = "";
        public string BefundText { get; set; } = "";
        public string Symbol { get; set; } = "";
        public Brush AmpelFarbe { get; set; } = Brushes.Gray;
        public string AktuellerWertZeile { get; set; } = "";
    }

#if PRO_BUILD
    /// <summary>
    /// Baut zur Laufzeit eine eigene Karte in ProZusatzBereich (leerer Platzhalter in der
    /// gemeinsamen XAML, s. UsbGeraeteView-Pattern) statt die Free-Kartenliste selbst zu
    /// verändern - so bleibt "Registry-Intelligenz" für Free/Pro dieselbe Liste, nur mit einer
    /// zusätzlichen, klar abgegrenzten Aktionsbox obendrüber für Pro.
    /// </summary>
    private void ProZusatzBereichAufbauen()
    {
        var border = new Border
        {
            Background = (Brush)FindResource("BgCardBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Visibility = Visibility.Collapsed
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = Loc.T("Sicherheitsempfehlungen anwenden (Pro)", "Apply security recommendations (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 6)
        });
        stack.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Für die unten aufgeführten Stellen setzt dieser Knopf GENAU diesen einen Wert auf die empfohlene " +
                "Einstellung zurück - nichts anderes wird verändert. Stellen, die Windows selbst gehören (nicht " +
                "deinem Benutzerkonto), brauchen dafür eine kurze Administratorbestätigung (UAC).",
                "For the entries listed below, this button resets EXACTLY that one value to the recommended " +
                "setting - nothing else is changed. Entries belonging to Windows itself (not your user account) " +
                "need a brief administrator confirmation (UAC) for that."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        _proListe = new ItemsControl();
        var zeileFactory = new FrameworkElementFactory(typeof(DockPanel));
        zeileFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 8));

        var buttonFactory = new FrameworkElementFactory(typeof(Button));
        buttonFactory.SetValue(DockPanel.DockProperty, Dock.Right);
        buttonFactory.SetBinding(ContentControl.ContentProperty, new Binding("ButtonText"));
        buttonFactory.SetBinding(FrameworkElement.TagProperty, new Binding("EintragId"));
        buttonFactory.SetValue(Button.PaddingProperty, new Thickness(10, 4, 10, 4));
        buttonFactory.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)EmpfehlungAnwenden_Click);
        zeileFactory.AppendChild(buttonFactory);

        var textStackFactory = new FrameworkElementFactory(typeof(StackPanel));

        var titelFactory = new FrameworkElementFactory(typeof(TextBlock));
        titelFactory.SetBinding(TextBlock.TextProperty, new Binding("Titel"));
        titelFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textStackFactory.AppendChild(titelFactory);

        var anzeigeFactory = new FrameworkElementFactory(typeof(TextBlock));
        anzeigeFactory.SetBinding(TextBlock.TextProperty, new Binding("Anzeige"));
        anzeigeFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        anzeigeFactory.SetValue(TextBlock.ForegroundProperty, (Brush)FindResource("TextMutedBrush"));
        textStackFactory.AppendChild(anzeigeFactory);

        zeileFactory.AppendChild(textStackFactory);
        _proListe.ItemTemplate = new DataTemplate { VisualTree = zeileFactory };
        stack.Children.Add(_proListe);

        border.Child = stack;
        _proBereich = border;
        ProZusatzBereich.Children.Add(border);
    }

    private void ProAktualisieren()
    {
        var kandidaten = _zeilen
            .Where(z => z.Befund.Status != AmpelStatus.Gruen)
            .Select(z => (Zeile: z, Empfehlung: RegistryKompendium.EmpfehlungFuer(z.Eintrag.Id)))
            .Where(x => x.Empfehlung is not null)
            .ToList();

        if (kandidaten.Count == 0)
        {
            _proBereich.Visibility = Visibility.Collapsed;
            return;
        }

        _proBereich.Visibility = Visibility.Visible;
        _proListe.ItemsSource = kandidaten.Select(k => new RegistryEmpfehlungZeile
        {
            EintragId = k.Zeile.Eintrag.Id,
            Titel = k.Zeile.Eintrag.Titel,
            Anzeige = Loc.T($"Empfohlen: {k.Empfehlung!.Wert}", $"Recommended: {k.Empfehlung!.Wert}"),
            ButtonText = Loc.T("Zurücksetzen", "Reset")
        }).ToList();
    }

    private async void EmpfehlungAnwenden_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string eintragId } button) return;

        var zeile = _zeilen.FirstOrDefault(z => z.Eintrag.Id == eintragId);
        var empfehlung = zeile is null ? null : RegistryKompendium.EmpfehlungFuer(eintragId);
        if (zeile is null || empfehlung is null) return;

        var brauchtElevation = _proDienst.BrauchtElevation(zeile.Eintrag);

        var frage = brauchtElevation
            ? Loc.T(
                $"„{zeile.Eintrag.Titel}\" auf den empfohlenen Wert zurücksetzen?\n\n{zeile.Eintrag.Erklaerung}\n\n" +
                "Diese Stelle gehört Windows selbst (HKEY_LOCAL_MACHINE) - Windows fragt im nächsten Schritt nach " +
                "einer Administratorbestätigung (UAC). Es wird NUR dieser eine Wert geschrieben, sonst nichts.",
                $"Reset \"{zeile.Eintrag.Titel}\" to the recommended value?\n\n{zeile.Eintrag.Erklaerung}\n\n" +
                "This location belongs to Windows itself (HKEY_LOCAL_MACHINE) - Windows will ask for an " +
                "administrator confirmation (UAC) in the next step. ONLY this one value is written, nothing else.")
            : Loc.T(
                $"„{zeile.Eintrag.Titel}\" auf den empfohlenen Wert zurücksetzen?\n\n{zeile.Eintrag.Erklaerung}\n\n" +
                "Diese Stelle gehört deinem Benutzerkonto - keine Administratorrechte nötig.",
                $"Reset \"{zeile.Eintrag.Titel}\" to the recommended value?\n\n{zeile.Eintrag.Erklaerung}\n\n" +
                "This location belongs to your user account - no administrator rights needed.");

        var antwort = System.Windows.MessageBox.Show(frage, Loc.T("Auf Empfehlung zurücksetzen", "Reset to recommendation"),
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No);
        if (antwort != System.Windows.MessageBoxResult.Yes) return;

        button.IsEnabled = false;
        var erfolg = brauchtElevation
            ? await _proDienst.ZuruecksetzenMitErhoehtenRechtenAsync(zeile.Eintrag, empfehlung)
            : await _proDienst.ZuruecksetzenAsync(zeile.Eintrag, empfehlung);

        if (erfolg)
        {
            System.Windows.MessageBox.Show(
                Loc.T($"„{zeile.Eintrag.Titel}\" wurde zurückgesetzt.", $"\"{zeile.Eintrag.Titel}\" was reset."),
                Loc.T("Erledigt", "Done"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            await LadenAsync();
        }
        else
        {
            button.IsEnabled = true;
            System.Windows.MessageBox.Show(
                Loc.T(
                    "Zurücksetzen fehlgeschlagen - entweder wurde die Sicherheitsabfrage abgelehnt, oder der Wert " +
                    "konnte nicht geschrieben werden.",
                    "Reset failed - either the security prompt was declined, or the value could not be written."),
                Loc.T("Fehlgeschlagen", "Failed"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }
#endif
}

#if PRO_BUILD
public class RegistryEmpfehlungZeile
{
    public string EintragId { get; set; } = "";
    public string Titel { get; set; } = "";
    public string Anzeige { get; set; } = "";
    public string ButtonText { get; set; } = "";
}
#endif
