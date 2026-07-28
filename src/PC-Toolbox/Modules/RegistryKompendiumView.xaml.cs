using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class RegistryKompendiumView : UserControl
{
    private readonly RegistryKompendiumService _dienst = new();

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
        var zeilen = await _dienst.LadeAsync();
        Liste.ItemsSource = zeilen.Select(ZuAnzeigeZeile).ToList();
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
}
