using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class MausDiagnoseView : UserControl
{
    private readonly MausGeraeteService _dienst = new();
    private readonly MausWerkBrueckeService _bruecke = new();

    public MausDiagnoseView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Maus-Diagnose", "Mouse diagnostics");
        QuelleHinweis.ToolTip = Loc.T(
            "WMI Win32_PnPEntity, gefiltert auf PNPClass \"Mouse\" - dieselbe Fehlerdeutung wie im Treiber-Check.",
            "WMI Win32_PnPEntity, filtered to PNPClass \"Mouse\" - same error interpretation as the driver check.");
        EinleitungText.Text = Loc.T(
            "Zeigt alle gerade angeschlossenen Mäuse mit ihrem Status - egal von welchem Hersteller. Reine Anzeige, es wird nichts konfiguriert oder verändert.",
            "Shows all currently connected mice with their status - regardless of manufacturer. Read-only, nothing is configured or changed.");
        DpiHinweisTitel.Text = Loc.T("ℹ️ Was hier nicht drinsteht: echte DPI", "ℹ️ What's not shown here: real DPI");
        DpiHinweisText.Text = Loc.T(
            "Die tatsächliche Sensor-DPI einer fremden Maus ist ohne das SDK des jeweiligen Herstellers technisch nicht auslesbar - das gilt für jedes Diagnose-Tool, nicht nur für Toolbox X. Windows kennt nur die \"Mauszeiger-Geschwindigkeit\"-Einstellung, das ist keine echte DPI-Angabe. Ehrlich weggelassen statt geraten.",
            "The actual sensor DPI of a third-party mouse can't technically be read without that manufacturer's own SDK - true for any diagnostic tool, not just Toolbox X. Windows only knows the \"pointer speed\" setting, which is not a real DPI value. Honestly left out instead of guessed.");
        NeuLadenButton.Content = Loc.T("Neu einlesen", "Reload");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadenAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(MausDiagnoseView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "MausDiagnoseView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async void NeuLaden_Click(object sender, RoutedEventArgs e) => await LadenAsync();

    private async Task LadenAsync()
    {
        NeuLadenButton.IsEnabled = false;
        ZusammenfassungText.Text = Loc.T("Lese Mäuse ein …", "Reading mice…");

        var geraete = await _dienst.LadeAsync();
        var heute = DateTime.Today;

        var mausWerkInstalliert = _bruecke.IstMausWerkInstalliert();

        var zeilen = geraete
            .Select(g => new MausZeile(g, TreiberBewerter.Bewerten(g, heute), FarbeFuer, _bruecke, mausWerkInstalliert))
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

    private async void MausWerkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string aktion } button) return;

        button.IsEnabled = false;

        var erfolg = aktion switch
        {
            "Oeffnen" => _bruecke.MausWerkOeffnen(),
            "Installieren" => _bruecke.InstallerStarten(),
            _ => false
        };

        if (!erfolg)
        {
            MessageBox.Show(
                Loc.T("Konnte nicht gestartet werden.", "Could not be started."),
                Loc.T("MausWerk", "MausWerk"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        button.IsEnabled = true;
        await LadenAsync();
    }
}

public class MausZeile
{
    private readonly PCToolboxCore.GeraetBefund _geraet;

    public MausZeile(PCToolboxCore.GeraetBefund geraet, TreiberBewertung bewertung, Func<AmpelStatus, Brush> farbe,
        MausWerkBrueckeService bruecke, bool mausWerkInstalliert)
    {
        _geraet = geraet;
        Ampel = bewertung.Ampel;
        Kurzform = bewertung.Kurzform;
        WasEsIst = bewertung.WasEsIst;
        Empfehlung = bewertung.Empfehlung;
        Symbol = bewertung.Ampel.Symbol();
        AmpelFarbe = farbe(bewertung.Ampel);

        var kompatibel = bruecke.IstMausWerkKompatibel(geraet.GeraeteId);
        MausWerkBereichSichtbar = kompatibel ? Visibility.Visible : Visibility.Collapsed;

        if (kompatibel && mausWerkInstalliert)
        {
            MausWerkHinweis = Loc.T(
                "Diese Maus ist mit MausWerk kompatibel und MausWerk ist bereits installiert.",
                "This mouse is compatible with MausWerk, and MausWerk is already installed.");
            MausWerkButtonText = Loc.T("MausWerk öffnen", "Open MausWerk");
            MausWerkAktion = "Oeffnen";
        }
        else if (kompatibel && bruecke.InstallerVorhanden())
        {
            MausWerkHinweis = Loc.T(
                "Diese Maus ist mit MausWerk kompatibel - dem separaten Treiber für erweiterte Funktionen (DPI-Stufen, Tasten-Remapping, Signal-Überwachung). MausWerk ist noch nicht installiert.",
                "This mouse is compatible with MausWerk - the separate driver for advanced features (DPI levels, key remapping, signal monitoring). MausWerk is not installed yet.");
            MausWerkButtonText = Loc.T("MausWerk installieren", "Install MausWerk");
            MausWerkAktion = "Installieren";
        }
        else if (kompatibel)
        {
            MausWerkHinweis = Loc.T(
                "Diese Maus ist mit MausWerk kompatibel, aber es wurde kein MausWerk-Installer gefunden.",
                "This mouse is compatible with MausWerk, but no MausWerk installer was found.");
            MausWerkButtonText = "";
            MausWerkAktion = "";
        }
    }

    public AmpelStatus Ampel { get; }
    public string Name => _geraet.Name;
    public string Kurzform { get; }
    public string WasEsIst { get; }
    public string Empfehlung { get; }
    public string Symbol { get; }
    public Brush AmpelFarbe { get; }

    public Visibility MausWerkBereichSichtbar { get; }
    public string MausWerkHinweis { get; } = "";
    public string MausWerkButtonText { get; } = "";
    public string MausWerkAktion { get; } = "";
    public Visibility MausWerkButtonSichtbar => string.IsNullOrEmpty(MausWerkAktion) ? Visibility.Collapsed : Visibility.Visible;

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
