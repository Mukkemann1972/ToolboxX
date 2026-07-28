using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class DruckerView : UserControl
{
    private readonly DruckerService _dienst = new();

    public DruckerView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Drucker", "Printers");
        QuelleHinweis.ToolTip = Loc.T(
            "WMI-Abfrage (Win32_Printer) - dieselbe Quelle, die auch die Windows-Einstellungen-App unter der Haube nutzt. Rein lesend.",
            "WMI query (Win32_Printer) - the same source the Windows Settings app uses under the hood. Read-only.");
        EinleitungText.Text = Loc.T(
            "Zeigt alle installierten Drucker mit ihrem aktuellen Status - bereit, druckt, oder ein konkreter Fehler wie \"kein Papier\" oder \"Tonerstand niedrig\", statt nur einem kryptischen Symbol im Infobereich. Reine Anzeige, es wird nichts gedruckt oder verändert.",
            "Shows all installed printers with their current status - ready, printing, or a concrete error like \"out of paper\" or \"toner low\", instead of just a cryptic icon in the system tray. Read-only, nothing is printed or changed.");
        NeuLadenButton.Content = Loc.T("Neu einlesen", "Reload");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadenAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(DruckerView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "DruckerView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async void NeuLaden_Click(object sender, RoutedEventArgs e) => await LadenAsync();

    private async Task LadenAsync()
    {
        NeuLadenButton.IsEnabled = false;

        var drucker = await _dienst.GetDruckerAsync();

        Liste.ItemsSource = drucker.Count == 0
            ? new[] { new DruckerAnzeigeZeile
              {
                  Name = Loc.T("Keine Drucker gefunden", "No printers found"),
                  BefundText = Loc.T("Entweder ist kein Drucker installiert, oder der Druckerspooler-Dienst läuft nicht.",
                                     "Either no printer is installed, or the print spooler service isn't running."),
                  Symbol = AmpelStatus.Unbekannt.Symbol(),
                  AmpelFarbe = FarbeFuer(AmpelStatus.Unbekannt),
                  DetailZeile = "",
                  StandardSichtbar = Visibility.Collapsed
              } }
            : drucker.Select(ZuAnzeigeZeile).ToList();

        NeuLadenButton.IsEnabled = true;
    }

    private DruckerAnzeigeZeile ZuAnzeigeZeile(DruckerInfo d) => new()
    {
        Name = d.Name,
        BefundText = d.BefundText,
        Symbol = d.Ampel.Symbol(),
        AmpelFarbe = FarbeFuer(d.Ampel),
        DetailZeile = Loc.T(
            $"{(d.IstNetzwerkdrucker ? "Netzwerkdrucker" : "Lokal angeschlossen")} · Anschluss: {d.AnschlussName} · Treiber: {d.TreiberName}",
            $"{(d.IstNetzwerkdrucker ? "Network printer" : "Locally connected")} · Port: {d.AnschlussName} · Driver: {d.TreiberName}"),
        StandardSichtbar = d.IstStandard ? Visibility.Visible : Visibility.Collapsed
    };

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private class DruckerAnzeigeZeile
    {
        public string Name { get; set; } = "";
        public string BefundText { get; set; } = "";
        public string Symbol { get; set; } = "";
        public Brush AmpelFarbe { get; set; } = Brushes.Gray;
        public string DetailZeile { get; set; } = "";
        public Visibility StandardSichtbar { get; set; }
    }
}
