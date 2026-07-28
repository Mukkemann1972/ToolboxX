using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolboxCore;
using PCToolboxCore.Models;
using PCToolboxCore.Services;

namespace PCToolbox.Modules;

public partial class SettingsView : UserControl
{
    // Kleine, bewusst begrenzte Auswahl statt freiem Farbwaehler - schnell zu
    // ueberblicken, jede Farbe vorher auf beiden Themes gegengeprueft.
    private static (string Name, string Hex)[] AccentPresets => new[]
    {
        (Loc.T("Cyan (Standard)", "Cyan (default)"), "#22D3EE"),
        ("Amber",                                     "#F59E0B"),
        (Loc.T("Violett", "Violet"),                  "#A78BFA"),
        (Loc.T("Grün", "Green"),                       "#4ADE80"),
        ("Pink",                                       "#F472B6"),
        (Loc.T("Rot", "Red"),                          "#F87171"),
    };

    private static readonly Brush NeutralBorder = new SolidColorBrush(Color.FromRgb(0x50, 0x5A, 0x6E));

    private readonly List<Button> _accentButtons = new();
    private AppTheme _pendingTheme;
    private string _pendingAccent;
    private Sprache _pendingSprache;

    public SettingsView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Einstellungen", "Settings");
        EinleitungText.Text = Loc.T(
            "Auswahl wird beim Klick auf 'Übernehmen' gespeichert - Toolbox X startet dafür kurz neu, damit die neue Optik ueberall sauber greift.",
            "The selection is saved when you click 'Apply' - Toolbox X briefly restarts for this, so the new look takes effect cleanly everywhere.");
        DesignLabel.Text = Loc.T("DESIGN", "DESIGN");
        ThemeDunkelTitel.Text = Loc.T("Dunkel (Neon)", "Dark (Neon)");
        ThemeDunkelUntertitel.Text = Loc.T("Bisheriges Theme", "Previous theme");
        ThemeHellTitel.Text = Loc.T("Hell", "Light");
        ThemeHellUntertitel.Text = Loc.T("Für helle Räume/Bildschirme", "For bright rooms/screens");
        AkzentfarbeLabel.Text = Loc.T("AKZENTFARBE", "ACCENT COLOR");
        ApplyButton.Content = Loc.T("Übernehmen (Neustart)", "Apply (restart)");

        DiagnoseLabel.Text = Loc.T("DIAGNOSE", "DIAGNOSTICS");
        DiagnoseErklaerung.Text = Loc.T(
            "Falls mal etwas nicht funktioniert: Toolbox X schreibt Fehler in eine kleine Log-Datei. Die kannst du bei Problemen einfach weitergeben, dann lässt sich das leichter nachvollziehen.",
            "If something doesn't work: Toolbox X writes errors to a small log file. If you run into trouble, you can just pass it along - that makes it much easier to track down.");
        DiagnoseOeffnenButton.Content = Loc.T("Diagnose-Log öffnen", "Open diagnostic log");

        var current = SettingsService.Load();
        _pendingTheme = current.Theme;
        _pendingAccent = current.AccentColor;
        _pendingSprache = current.Sprache ?? Loc.ErkenneAusSystemsprache();

        foreach (var (name, hex) in AccentPresets)
        {
            var swatch = new Button
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 0, 10, 10),
                Background = new SolidColorBrush((Color) ColorConverter.ConvertFromString(hex)),
                BorderThickness = new Thickness(3),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = name,
                Tag = hex,
            };
            swatch.Click += AccentSwatch_Click;
            _accentButtons.Add(swatch);
            AccentPanel.Children.Add(swatch);
        }

        RefreshAuswahl();
    }

    private void ThemeDunkel_Click(object sender, RoutedEventArgs e)
    {
        _pendingTheme = AppTheme.DunkelNeon;
        RefreshAuswahl();
    }

    private void ThemeHell_Click(object sender, RoutedEventArgs e)
    {
        _pendingTheme = AppTheme.Hell;
        RefreshAuswahl();
    }

    private void AccentSwatch_Click(object sender, RoutedEventArgs e)
    {
        _pendingAccent = (string) ((Button) sender).Tag;
        RefreshAuswahl();
    }

    private void SpracheDeutsch_Click(object sender, RoutedEventArgs e)
    {
        _pendingSprache = Sprache.Deutsch;
        RefreshAuswahl();
    }

    private void SpracheEnglisch_Click(object sender, RoutedEventArgs e)
    {
        _pendingSprache = Sprache.Englisch;
        RefreshAuswahl();
    }

    // Markiert die aktuell gewaehlte Theme-Karte + Akzentfarbe + Sprache mit
    // einem hellen/farbigen Rahmen, alle anderen bleiben neutral.
    private void RefreshAuswahl()
    {
        ThemeDunkelButton.BorderBrush = _pendingTheme == AppTheme.DunkelNeon ? Brushes.White : NeutralBorder;
        ThemeHellButton.BorderBrush = _pendingTheme == AppTheme.Hell ? Brushes.White : NeutralBorder;
        SpracheDeutschButton.BorderBrush = _pendingSprache == Sprache.Deutsch ? Brushes.White : NeutralBorder;
        SpracheEnglischButton.BorderBrush = _pendingSprache == Sprache.Englisch ? Brushes.White : NeutralBorder;

        foreach (var btn in _accentButtons)
        {
            var hex = (string) btn.Tag;
            btn.BorderBrush = hex == _pendingAccent ? Brushes.White : Brushes.Transparent;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Save(new AppSettings { Theme = _pendingTheme, AccentColor = _pendingAccent, Sprache = _pendingSprache });

        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });

        System.Windows.Application.Current.Shutdown();
    }

    // Oeffnet den Explorer mit der Log-Datei markiert, statt sie selbst anzuzeigen -
    // so kann man sie direkt per Rechtsklick kopieren/weiterschicken. Gibt es noch
    // keine Log-Datei (bisher fehlerfrei gelaufen), zeigt ein Hinweis das ehrlich an
    // statt Explorer auf einen nicht vorhandenen Ordner zu oeffnen.
    private void DiagnoseOeffnen_Click(object sender, RoutedEventArgs e)
    {
        var pfad = DiagnoseLog.DefaultPfad;

        if (!System.IO.File.Exists(pfad))
        {
            System.Windows.MessageBox.Show(
                Loc.T("Bisher wurden keine Fehler aufgezeichnet - es gibt noch keine Log-Datei.",
                      "No errors have been recorded so far - there's no log file yet."),
                Loc.T("Diagnose-Log", "Diagnostic log"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{pfad}\"") { UseShellExecute = true });
    }
}
