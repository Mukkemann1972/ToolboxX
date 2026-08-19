using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PCToolboxCore;
using PCToolboxCore.Models;
using PCToolboxCore.Services;
#if PRO_BUILD
using PCToolboxCore.Pro;
#endif

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

#if PRO_BUILD
    private TextBlock _profilStatusText = null!;
#endif

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

#if PRO_BUILD
        BauToolboxProfilBereich();
#endif
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

#if PRO_BUILD
    /// <summary>
    /// "Toolbox-Profil" (Pro, Hermes-Vorschlag 17.08.2026): Einstellungen einmal
    /// konfigurieren, als JSON exportieren, auf einem anderen PC importieren. Import
    /// zeigt vorher eine "Diese Werte werden geaendert"-Vorschau statt still zu
    /// ueberschreiben (s. Team-Konvention, Ampel-Prinzip).
    /// </summary>
    private void BauToolboxProfilBereich()
    {
        ProProfilBereich.Children.Add(new TextBlock
        {
            Text = Loc.T("TOOLBOX-PROFIL (PRO)", "TOOLBOX PROFILE (PRO)"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        ProProfilBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Speichert Theme, Akzentfarbe und Sprache in einer Datei - praktisch, um dieselbe " +
                "Optik/Konfiguration auf mehreren PCs zu uebernehmen (z.B. in der Werkstatt), statt " +
                "jedes Mal von Hand einzustellen.",
                "Saves theme, accent color, and language into a file - handy for carrying the same " +
                "look/configuration over to other PCs (e.g. in a workshop), instead of setting it up " +
                "by hand every time."),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var knopfZeile = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        var exportButton = new Button
        {
            Content = Loc.T("Exportieren …", "Export …"),
            Padding = new Thickness(18, 10, 18, 10),
            Margin = new Thickness(0, 0, 14, 0)
        };
        exportButton.Click += ProfilExportieren_Click;

        var importButton = new Button
        {
            Content = Loc.T("Importieren …", "Import …"),
            Padding = new Thickness(18, 10, 18, 10)
        };
        importButton.Click += ProfilImportieren_Click;

        knopfZeile.Children.Add(exportButton);
        knopfZeile.Children.Add(importButton);
        ProProfilBereich.Children.Add(knopfZeile);

        _profilStatusText = new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap };
        ProProfilBereich.Children.Add(_profilStatusText);
    }

    private void ProfilExportieren_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.T("Toolbox-Profil exportieren", "Export Toolbox profile"),
            Filter = "Toolbox-X-Profil (*.json)|*.json",
            FileName = $"toolboxx-profil-{DateTime.Now:yyyy-MM-dd}.json"
        };
        if (dialog.ShowDialog() != true) return;

        var profil = ToolboxProfilDienst.Erstellen(SettingsService.Load());
        File.WriteAllText(dialog.FileName, ToolboxProfilDienst.AlsJson(profil));

        ZeigeProfilStatus(
            Loc.T($"✔ Profil gespeichert: {Path.GetFileName(dialog.FileName)}",
                  $"✔ Profile saved: {Path.GetFileName(dialog.FileName)}"),
            erfolg: true);
    }

    private void ProfilImportieren_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Toolbox-Profil importieren", "Import Toolbox profile"),
            Filter = "Toolbox-X-Profil (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true) return;

        string json;
        try
        {
            json = File.ReadAllText(dialog.FileName);
        }
        catch (IOException)
        {
            ZeigeProfilImportFehler();
            return;
        }

        var profil = ToolboxProfilDienst.AusJson(json);
        if (profil is null)
        {
            ZeigeProfilImportFehler();
            return;
        }

        var aktuell = SettingsService.Load();
        var aenderungen = ToolboxProfilDienst.Vergleichen(profil, aktuell);

        if (aenderungen.Count == 0)
        {
            ZeigeProfilStatus(
                Loc.T("Keine Änderungen - das Profil entspricht bereits den aktuellen Einstellungen.",
                      "No changes - the profile already matches the current settings."),
                erfolg: true);
            return;
        }

        var aenderungsText = string.Join("\n", aenderungen.Select(a => $"• {a.Feld}: {a.AlterWert} → {a.NeuerWert}"));
        var antwort = MessageBox.Show(
            Loc.T($"Folgende Werte werden geändert:\n\n{aenderungsText}\n\nÜbernehmen? Toolbox X startet danach kurz neu.",
                  $"The following values will change:\n\n{aenderungsText}\n\nApply? Toolbox X will briefly restart afterward."),
            Loc.T("Toolbox-Profil anwenden", "Apply Toolbox profile"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (antwort != MessageBoxResult.Yes) return;

        SettingsService.Save(ToolboxProfilDienst.ZuAppSettings(profil));

        var exePath = Environment.ProcessPath;
        if (exePath != null)
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });

        System.Windows.Application.Current.Shutdown();
    }

    private void ZeigeProfilImportFehler() => ZeigeProfilStatus(
        Loc.T("✘ Diese Datei ist kein gültiges Toolbox-Profil (kaputt, falsches Format oder aus einer neueren Toolbox-X-Version).",
              "✘ This file is not a valid Toolbox profile (corrupt, wrong format, or from a newer Toolbox X version)."),
        erfolg: false);

    private void ZeigeProfilStatus(string text, bool erfolg)
    {
        _profilStatusText.Text = text;
        _profilStatusText.FontWeight = FontWeights.SemiBold;
        _profilStatusText.Foreground = (Brush)FindResource(erfolg ? "OkBrush" : "DangerBrush");
    }
#endif
}
