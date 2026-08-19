using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolboxCore;

namespace PCToolbox.Modules;

/// <summary>
/// Legacy-Windows-Werkzeuge (Free): Direktzugriffe auf klassische Windows-Systemwerkzeuge, die
/// Microsoft in der modernen Einstellungen-App zunehmend versteckt (s. LegacyWindowsWerkzeuge
/// in PCToolboxCore für die kuratierte Liste + Begründung). Startet nur, ändert selbst nichts -
/// alles Weitere passiert in Windows' eigenen, altbekannten Dialogen.
/// </summary>
public partial class LegacyWerkzeugeView : UserControl
{
    public LegacyWerkzeugeView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Windows-Bordmittel", "Windows Built-in Tools");
        EinleitungText.Text = Loc.T(
            "Klassische Windows-Systemwerkzeuge, die in der modernen Einstellungen-App kaum noch " +
            "auffindbar sind - mit Erklärung, wofür jedes einzelne gut ist. Ein Klick startet das " +
            "jeweilige Windows-Bordmittel, Toolbox X ändert dabei selbst nichts.",
            "Classic Windows system tools that are barely findable anymore in the modern Settings app - " +
            "with an explanation of what each one is good for. One click starts the respective Windows " +
            "built-in tool, Toolbox X itself doesn't change anything.");
        SucheLabel.Text = Loc.T("Suche:", "Search:");

        ListeAufbauen(LegacyWindowsWerkzeuge.Werkzeuge);
    }

    private void Suche_Changed(object sender, TextChangedEventArgs e) =>
        ListeAufbauen(LegacyWindowsWerkzeuge.Suchen(SucheBox.Text));

    private void ListeAufbauen(IReadOnlyList<LegacyWerkzeug> werkzeuge)
    {
        Liste.Children.Clear();

        if (werkzeuge.Count == 0)
        {
            Liste.Children.Add(new TextBlock
            {
                Text = Loc.T("Keine Treffer.", "No matches."),
                Foreground = (Brush)FindResource("TextMutedBrush"),
                FontSize = 12,
            });
            return;
        }

        foreach (var werkzeug in werkzeuge)
            Liste.Children.Add(WerkzeugKarte(werkzeug));
    }

    private Border WerkzeugKarte(LegacyWerkzeug werkzeug)
    {
        var innen = new DockPanel();

        var startenButton = new Button
        {
            Content = Loc.T("Öffnen", "Open"),
            Padding = new Thickness(12, 5, 12, 5),
            VerticalAlignment = VerticalAlignment.Top,
        };
        startenButton.Click += (_, _) => Werkzeug_Starten(werkzeug);
        DockPanel.SetDock(startenButton, Dock.Right);
        innen.Children.Add(startenButton);

        var textStack = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        textStack.Children.Add(new TextBlock { Text = werkzeug.Name, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 0, 0, 4) });
        textStack.Children.Add(new TextBlock { Text = werkzeug.Erklaerung, TextWrapping = TextWrapping.Wrap });
        innen.Children.Add(textStack);

        return new Border
        {
            Background = (Brush)FindResource("BgCardBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = innen,
        };
    }

    private void Werkzeug_Starten(LegacyWerkzeug werkzeug)
    {
        try
        {
            Process.Start(new ProcessStartInfo(werkzeug.Befehl, werkzeug.Argumente) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(
                Loc.T($"„{werkzeug.Name}\" konnte nicht gestartet werden:\n{ex.Message}",
                      $"\"{werkzeug.Name}\" could not be started:\n{ex.Message}"),
                Loc.T("Hinweis", "Note"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
