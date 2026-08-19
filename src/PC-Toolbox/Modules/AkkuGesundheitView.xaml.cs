using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using PCToolbox.Services.Pro;
using PCToolboxCore.Pro;
#endif

namespace PCToolbox.Modules;

/// <summary>
/// Akku-/Energie-Diagnose (Free: Akku-Gesundheit + Aufweck-Ursachen; Pro: Standby-Blocker +
/// Energy Report). Free-Teil zeigt das Verhältnis von aktueller zu ursprünglicher
/// Vollladekapazität aus Windows' eigenem "powercfg /batteryreport" (s. AkkuService-Kommentar) -
/// dieselbe Kennzahl, die z.B. macOS direkt in den Systeminformationen zeigt. Für Desktop-PCs ohne
/// Akku gibt es einen ehrlichen Hinweis statt eines Fehlers.
/// </summary>
public partial class AkkuGesundheitView : UserControl
{
    private readonly AkkuService _dienst = new();
    private readonly EnergieService _energieDienst = new();

#if PRO_BUILD
    private readonly EnergieServicePro _energieProDienst = new();
    private TextBlock _blockerStatusText = null!;
    private StackPanel _blockerListe = null!;
    private Button _blockerAktualisierenButton = null!;
    private TextBlock _reportStatusText = null!;
    private StackPanel _reportListe = null!;
    private Button _reportStartenButton = null!;
#endif

    public AkkuGesundheitView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Akku-/Energie-Diagnose", "Battery/Energy diagnostics");
        EinleitungText.Text = Loc.T(
            "Vergleicht die aktuelle Vollladekapazität mit der ursprünglichen Werkskapazität - Akkus verlieren " +
            "mit der Zeit an Kapazität, auch ohne Defekt. Ein Akku, der nur noch 60% seiner Werkskapazität " +
            "erreicht, hält auch bei vollem Ladestand nur noch 60% der ursprünglichen Laufzeit. Quelle: Windows' " +
            "eigener Akkubericht (powercfg /batteryreport) - keine Administratorrechte nötig.",
            "Compares the current full-charge capacity with the original factory capacity - batteries lose " +
            "capacity over time even without a fault. A battery that only reaches 60% of its factory capacity " +
            "will only last 60% as long even at a full charge. Source: Windows' own battery report " +
            "(powercfg /batteryreport) - no administrator rights needed.");
        NeuLadenButton.Content = Loc.T("Aktualisieren", "Refresh");

        AufweckTitel.Text = Loc.T("Aufweck-Ursachen", "Wake sources");
        AufweckEinleitung.Text = Loc.T(
            "Wie oft wurde dieser PC seit dem letzten Zurücksetzen des Zählers aus dem Ruhezustand geweckt " +
            "(powercfg /lastwake) - keine Administratorrechte nötig.",
            "How often this PC was woken from sleep since the counter was last reset " +
            "(powercfg /lastwake) - no administrator rights needed.");

#if PRO_BUILD
        BauEnergieDiagnoseProBereich();
#endif

        Loaded += async (_, _) =>
        {
            await LadenAsync();
            await AufweckLadenAsync();
#if PRO_BUILD
            await StandbyBlockerLadenAsync();
#endif
        };
    }

    private async void NeuLaden_Click(object sender, RoutedEventArgs e)
    {
        await LadenAsync();
        await AufweckLadenAsync();
    }

    private async Task AufweckLadenAsync()
    {
        AufweckText.Text = Loc.T("Lese Aufweck-Verlauf …", "Reading wake history…");

        var ergebnis = await _energieDienst.AufweckVerlaufAsync();

        AufweckText.Text = ergebnis.Anzahl switch
        {
            null => Loc.T("Konnte nicht gelesen werden.", "Could not be read."),
            0 => Loc.T(
                "0 Aufweck-Ereignisse seit dem letzten Zurücksetzen des Zählers protokolliert.",
                "0 wake events logged since the counter was last reset."),
            var n => Loc.T(
                $"{n} Aufweck-Ereignis(se) protokolliert.\n\n{ergebnis.RohDetails}",
                $"{n} wake event(s) logged.\n\n{ergebnis.RohDetails}")
        };
    }

    private async Task LadenAsync()
    {
        NeuLadenButton.IsEnabled = false;
        ErgebnisKarte.Visibility = Visibility.Collapsed;
        StatusText.Text = Loc.T("Erzeuge Akkubericht …", "Generating battery report…");

        var ergebnis = await _dienst.GesundheitAsync();

        NeuLadenButton.IsEnabled = true;

        if (!ergebnis.AkkuVorhanden || ergebnis.Kapazitaet is not { } kapazitaet)
        {
            StatusText.Text = ergebnis.Fehlermeldung ?? Loc.T("Kein Akku gefunden.", "No battery found.");
            return;
        }

        StatusText.Text = "";
        var ampel = AkkuGesundheit.Bewerten(kapazitaet);
        var prozent = AkkuGesundheit.GesundheitsProzent(kapazitaet);

        ErgebnisTitel.Text = $"{ampel.Symbol()} {Loc.T("Akkugesundheit:", "Battery health:")} {prozent:0.#}%";
        ErgebnisText.Text = AkkuGesundheit.Erklaerung(kapazitaet) + Loc.T(
            $"\n\nWerkskapazität: {kapazitaet.DesignKapazitaetMwh / 1000.0:0.#} Wh · Aktuelle Vollladekapazität: {kapazitaet.VollladeKapazitaetMwh / 1000.0:0.#} Wh",
            $"\n\nFactory capacity: {kapazitaet.DesignKapazitaetMwh / 1000.0:0.#} Wh · Current full-charge capacity: {kapazitaet.VollladeKapazitaetMwh / 1000.0:0.#} Wh");
        GesundheitsBalken.Value = Math.Clamp(prozent, 0, 100);
        ErgebnisKarte.Visibility = Visibility.Visible;
    }

#if PRO_BUILD
    /// <summary>
    /// Standby-Blocker ("powercfg /requests") + Energy Report ("powercfg /energy") - beide
    /// brauchen, anders als Aufweck-Ursachen oben (Free), Administratorrechte schon zum LESEN
    /// (live geprüft: unelevated liefert Windows nur "Dieser Befehl erfordert
    /// Administratorrechte"). Gleiches Muster wie Wiederherstellungspunkte/Boot-Auswirkung: die
    /// ganze App muss bereits erhöht laufen, kein "runas" auf eine einzelne Aktion möglich.
    /// </summary>
    private void BauEnergieDiagnoseProBereich()
    {
        var titel = new TextBlock
        {
            Text = Loc.T("Standby-Blocker & Energy Report (Pro)", "Standby blockers & energy report (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        };
        ProZusatzBereich.Children.Add(titel);

        if (!WindowsReparaturService.HatAdminRechte())
        {
            ProZusatzBereich.Children.Add(new TextBlock
            {
                Text = Loc.T(
                    "Windows lässt selbst das Auflesen von Standby-Blockern und Energy-Report nur mit " +
                    "Administratorrechten zu (anders als die Aufweck-Ursachen oben) - Toolbox X per " +
                    "Rechtsklick „Als Administrator ausführen\" neu starten, um diesen Bereich zu nutzen.",
                    "Windows only allows even reading standby blockers and the energy report with " +
                    "administrator rights (unlike the wake sources above) - restart Toolbox X via right-click " +
                    "\"Run as administrator\" to use this section."),
                Foreground = (Brush)FindResource("WarnBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        // Standby-Blocker
        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Zeigt, welche Programme/Treiber/Dienste GERADE JETZT verhindern, dass der PC in den " +
                "Ruhezustand geht oder den Bildschirm abschaltet (powercfg /requests) - z.B. ein " +
                "Download, ein Video oder eine App mit einer stillen Anforderung im Hintergrund.",
                "Shows which programs/drivers/services are CURRENTLY preventing the PC from sleeping or " +
                "turning off the display (powercfg /requests) - e.g. a download, a video, or an app with " +
                "a silent background request."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        _blockerStatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _blockerListe = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        _blockerAktualisierenButton = new Button
        {
            Content = Loc.T("Aktualisieren", "Refresh"),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 16)
        };
        _blockerAktualisierenButton.Click += async (_, _) => await StandbyBlockerLadenAsync();

        ProZusatzBereich.Children.Add(_blockerStatusText);
        ProZusatzBereich.Children.Add(_blockerListe);
        ProZusatzBereich.Children.Add(_blockerAktualisierenButton);

        // Energy Report
        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T("Energy Report", "Energy report"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });
        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Windows misst 60 Sekunden lang Energiesparverhalten und listet konkrete Probleme " +
                "(powercfg /energy) - z.B. Energiesparpläne, die Energiesparen verhindern, oder " +
                "USB-Geräte ohne selektives Energiesparen. Läuft im Hintergrund, der PC bleibt bedienbar.",
                "Windows measures energy-saving behavior for 60 seconds and lists concrete problems " +
                "(powercfg /energy) - e.g. power plans that prevent energy saving, or USB devices without " +
                "selective suspend. Runs in the background, the PC stays usable."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        _reportStatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _reportListe = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        _reportStartenButton = new Button
        {
            Content = Loc.T("60-Sekunden-Test starten", "Start 60-second test"),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _reportStartenButton.Click += async (_, _) => await EnergyReportStartenAsync();

        ProZusatzBereich.Children.Add(_reportStartenButton);
        ProZusatzBereich.Children.Add(_reportStatusText);
        ProZusatzBereich.Children.Add(_reportListe);
    }

    private async Task StandbyBlockerLadenAsync()
    {
        if (!WindowsReparaturService.HatAdminRechte()) return;

        _blockerStatusText.Text = Loc.T("Lese Standby-Blocker …", "Reading standby blockers…");
        _blockerListe.Children.Clear();

        var bloecke = await _energieProDienst.StandbyBlockerAsync();
        var aktive = bloecke.Where(b => b.HatBlocker).ToList();

        _blockerStatusText.Text = aktive.Count == 0
            ? Loc.T("Aktuell blockiert nichts den Ruhezustand.", "Nothing is currently blocking sleep.")
            : "";

        foreach (var block in aktive)
        {
            foreach (var eintrag in block.Eintraege)
            {
                var zusatz = eintrag.Zusatz is { Length: > 0 } z ? $" ({z})" : "";
                _blockerListe.Children.Add(new TextBlock
                {
                    Text = $"⚠️ {block.Kategorie}: [{eintrag.Typ}] {eintrag.Quelle}{zusatz}",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)FindResource("WarnBrush"),
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
        }
    }

    private async Task EnergyReportStartenAsync()
    {
        if (!WindowsReparaturService.HatAdminRechte()) return;

        _reportStartenButton.IsEnabled = false;
        _reportStatusText.Text = Loc.T("Messe 60 Sekunden lang …", "Measuring for 60 seconds…");
        _reportListe.Children.Clear();

        var zusammenfassung = await _energieProDienst.EnergyReportAsync();

        _reportStartenButton.IsEnabled = true;
        _reportStatusText.Text = Loc.T(
            $"✔ {zusammenfassung.AnzahlFehler} Fehler, {zusammenfassung.AnzahlWarnungen} Warnungen, {zusammenfassung.AnzahlInfo} Informationen gefunden.",
            $"✔ {zusammenfassung.AnzahlFehler} errors, {zusammenfassung.AnzahlWarnungen} warnings, {zusammenfassung.AnzahlInfo} info items found.");
        _reportStatusText.FontWeight = FontWeights.SemiBold;
        _reportStatusText.Foreground = (Brush)FindResource("OkBrush");

        foreach (var eintrag in zusammenfassung.Eintraege.Where(e => e.Schweregrad != EnergieSchweregrad.Info))
        {
            var symbol = eintrag.Schweregrad == EnergieSchweregrad.Fehler ? "🔴" : "🟡";
            var farbe = eintrag.Schweregrad == EnergieSchweregrad.Fehler
                ? (Brush)FindResource("DangerBrush")
                : (Brush)FindResource("WarnBrush");

            var karte = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            karte.Children.Add(new TextBlock
            {
                Text = $"{symbol} {eintrag.Kategorie}: {eintrag.Titel}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = farbe,
                TextWrapping = TextWrapping.Wrap
            });
            karte.Children.Add(new TextBlock
            {
                Text = eintrag.Beschreibung,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            _reportListe.Children.Add(karte);
        }
    }
#endif
}
