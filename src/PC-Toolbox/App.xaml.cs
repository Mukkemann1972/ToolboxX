using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml;
using PCToolboxCore;
using PCToolboxCore.Models;
using PCToolboxCore.Services;

namespace PCToolbox;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ohne globalen Handler stürzt WPF bei jeder unbehandelten Exception (auch aus
        // "async void"-Event-Handlern wie Loaded) sofort und ohne Meldung ab. Damit sieht
        // man wenigstens, WAS schiefging, statt dass die App wortlos verschwindet.
        var settings = SettingsService.Load();
        Loc.Load(settings.Sprache ?? Loc.ErkenneAusSystemsprache());

        DispatcherUnhandledException += (_, args) =>
        {
            DiagnoseLog.Schreiben("App (UI-Thread)", args.Exception.ToString());

            MessageBox.Show(
                Loc.T($"Unerwarteter Fehler:\n\n{args.Exception}", $"Unexpected error:\n\n{args.Exception}"),
                "Toolbox X – Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Handled=true verhindert nur den WPF-Absturz, lässt die App aber als
            // "Zombie" ohne Fenster im Hintergrund weiterlaufen. Deshalb nach der
            // Meldung sauber beenden statt weiterzulaufen als wäre nichts gewesen.
            args.Handled = true;
            Shutdown();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            DiagnoseLog.Schreiben("App (Hintergrund)", args.ExceptionObject?.ToString() ?? "");

            MessageBox.Show(
                Loc.T($"Unerwarteter Fehler (nicht behebbar):\n\n{args.ExceptionObject}",
                      $"Unexpected error (unrecoverable):\n\n{args.ExceptionObject}"),
                "Toolbox X – Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        ApplyTheme(settings.Theme, settings.AccentColor);

        // Kein StartupUri (s. App.xaml) - erst NACH dem Theme-Laden erzeugen,
        // sonst wuerden Styles mit den alten/leeren Ressourcen aufgeloest.
        new MainWindow().Show();
    }

    // Laedt die passende Theme-Datei als Text (bewusst NICHT als kompilierte
    // WPF-Page, s. .csproj), ersetzt die Akzentfarben-Platzhalter durch die
    // gewaehlte Farbe (+ automatisch abgedunkelte Variante) und haengt das
    // Ergebnis als einzige Resource-Dictionary der App ein. So sind alle
    // StaticResource-Verweise in Styles/Views schon beim ersten Fenster mit
    // den richtigen, fertig aufgeloesten Farben da - kein Nachjustieren zur
    // Laufzeit noetig (Theme-Wechsel passiert stattdessen ueber einen
    // Neustart, s. SettingsView.Apply_Click).
    private void ApplyTheme(AppTheme theme, string accentHex)
    {
        var datei = theme == AppTheme.Hell ? "ToolboxXThemeLight.xaml" : "ToolboxXTheme.xaml";
        var pfad = Path.Combine(AppContext.BaseDirectory, "Themes", datei);

        try
        {
            var xaml = File.ReadAllText(pfad);
            var dim = SettingsService.Darken(accentHex);
            var quelleHinweis = Loc.T("ℹ Woher weiß die Toolbox das?", "ℹ How does the toolbox know this?");
            xaml = xaml.Replace("ACCENT_TOKEN", accentHex).Replace("ACCENT_DIM_TOKEN", dim)
                       .Replace("QUELLE_HINWEIS_TOKEN", quelleHinweis);

            using var stringReader = new StringReader(xaml);
            using var xmlReader = XmlReader.Create(stringReader);
            if (XamlReader.Load(xmlReader) is ResourceDictionary dict)
            {
                Resources.MergedDictionaries.Clear();
                Resources.MergedDictionaries.Add(dict);
            }
        }
        catch (Exception ex)
        {
            DiagnoseLog.Schreiben("App (Theme laden)", ex.ToString());

            // Theme kaputt/Datei fehlt soll den Start nicht verhindern - dann
            // eben ohne Farb-Theme starten (Standard-WPF-Optik) statt
            // abzustürzen, bevor überhaupt ein Fenster zu sehen ist.
            MessageBox.Show(
                Loc.T($"Theme konnte nicht geladen werden, Toolbox X startet mit Standard-Optik:\n\n{ex.Message}",
                      $"Theme could not be loaded, Toolbox X starts with the default look:\n\n{ex.Message}"),
                "Toolbox X – Theme Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
