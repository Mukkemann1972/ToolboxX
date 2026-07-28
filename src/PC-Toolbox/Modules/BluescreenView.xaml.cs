using System.Windows.Controls;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class BluescreenView : UserControl
{
    private readonly DumpReader _service = new();

    public BluescreenView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Letzte Abstürze (Bluescreens)", "Recent crashes (blue screens)");
        QuelleHinweis.ToolTip = Loc.T(
            "Windows-Ereignisprotokoll: Provider Microsoft-Windows-WER-SystemErrorReporting, Event-ID 1001. Fallback, falls das Log leer ist: Zeitstempel der .dmp-Dateien.",
            "Windows event log: provider Microsoft-Windows-WER-SystemErrorReporting, event ID 1001. Fallback if the log is empty: timestamps of the .dmp files.");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(BluescreenView), ex.ToString());
                System.Windows.MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "BluescreenView – Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };
    }

    private async Task LadeAsync()
    {
        var eintraege = await _service.GetLetzteBluescreensAsync();

        if (eintraege.Count == 0)
        {
            Liste.ItemsSource = new[] { new BsodAnzeige { Titel = Loc.T("✅ Keine Abstürze in den letzten Ereignissen gefunden.", "✅ No crashes found in recent events."), Loesung = "", QuelleText = "" } };
            return;
        }

        Liste.ItemsSource = eintraege.Select(ZuAnzeigeZeile).ToList();
    }

    private static BsodAnzeige ZuAnzeigeZeile(BluescreenEntry e) => new()
    {
        Titel = $"{e.Zeitpunkt:dd.MM.yyyy HH:mm}  {e.BugCheckCode}  {e.BugCheckName}",
        Loesung = e.Loesungsvorschlag,
        QuelleText = Loc.T($"Quelle: {e.Quelle}", $"Source: {e.Quelle}")
    };

    private class BsodAnzeige
    {
        public string Titel { get; set; } = "";
        public string Loesung { get; set; } = "";
        public string QuelleText { get; set; } = "";
    }
}
