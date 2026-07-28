using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Modules;

public partial class AutostartView : UserControl
{
    private readonly AutostartService _dienst = new();

    public AutostartView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Autostart", "Startup");
        QuelleHinweis.ToolTip = Loc.T(
            "4 Quellen: Registry Run-Schlüssel (HKLM/HKCU), Autostart-Ordner, Aufgabenplanung (Task-Scheduler-XML) und der StartupApproved-Blob, der 'Ausschalten' statt Löschen ermöglicht.",
            "4 sources: Registry Run keys (HKLM/HKCU), startup folders, Task Scheduler (task scheduler XML), and the StartupApproved blob, which enables 'turn off' instead of deletion.");
        EinleitungText.Text = Loc.T(
            "Alles, was beim Einschalten deines PCs von selbst mitstartet - aus allen vier Orten, an denen Windows so etwas versteckt. Je mehr hier mitläuft, desto länger dauert der Start.",
            "Everything that starts on its own when your PC powers on - from all four places where Windows hides such things. The more that runs here, the longer startup takes.");
        NichtsKaputtTitel.Text = Loc.T("🛟 Du kannst hier nichts kaputt machen", "🛟 You can't break anything here");
        NichtsKaputtText.Text = Loc.T(
            "„Ausschalten“ löscht nichts. Der Eintrag bleibt vollständig erhalten und wird nur stillgelegt - genau so, wie es auch der Windows-Task-Manager macht. Ein Klick auf „Wieder einschalten“ macht es rückgängig. Im Zweifel gilt: ausschalten, PC neu starten, ausprobieren.",
            "\"Turn off\" deletes nothing. The entry is fully preserved and only disabled - exactly the way the Windows Task Manager does it too. One click on \"Turn back on\" reverses it. When in doubt: turn it off, restart the PC, try it out.");
        AktualisierenButton.Content = Loc.T("Neu einlesen", "Reload");
        WeitereOrteBereich.Header = Loc.T("Weitere, seltenere Orte prüfen", "Check further, rarer locations");

        Loaded += async (_, _) => await EinlesenAsync();
        Loaded += async (_, _) => await WeitereOrteLadenAsync();
    }

    private async Task WeitereOrteLadenAsync()
    {
        try
        {
            var befunde = await _dienst.WeitereOrteAsync();

            WeitereOrteErklaerung.Text = Loc.T(
                "RunOnce (läuft ein einziges Mal), die Windows-Shell (Winlogon) und Image File Execution " +
                "Options (kann Programmstarts heimlich umleiten) - seltener genutzt als Registry/Autostart-" +
                "Ordner/Aufgabenplanung oben, aber gerade IFEO und eine fremde Shell sind starke Anzeichen " +
                "für Schadsoftware, wenn sie auffällig sind.",
                "RunOnce (runs a single time), the Windows shell (Winlogon), and Image File Execution " +
                "Options (can secretly redirect program launches) - used less often than the registry/" +
                "startup folders/task scheduler above, but especially IFEO and a foreign shell are strong " +
                "signs of malware when they show up as unusual.");

            WeitereOrteListe.ItemsSource = befunde.Select(ZuWeitereOrteZeile).ToList();
        }
        catch (Exception ex)
        {
            DiagnoseLog.Schreiben(nameof(AutostartView) + ".WeitereOrte", ex.ToString());
        }
    }

    private WeitereOrteZeile ZuWeitereOrteZeile(SicherheitsBefund befund) => new()
    {
        Titel = befund.Titel,
        Text = befund.Text,
        Symbol = befund.Status.Symbol(),
        AmpelFarbe = FarbeFuer(befund.Status)
    };

    private class WeitereOrteZeile
    {
        public string Titel { get; set; } = "";
        public string Text { get; set; } = "";
        public string Symbol { get; set; } = "";
        public Brush AmpelFarbe { get; set; } = Brushes.Gray;
    }

    private async Task EinlesenAsync()
    {
        AktualisierenButton.IsEnabled = false;
        ZusammenfassungText.Text = Loc.T("Lese Autostart-Einträge ein …", "Loading startup entries…");
        HinweisText.Text = "";

        var eintraege = await _dienst.LadeAlleAsync();

        var zeilen = eintraege
            .Select(e => new AutostartZeile(e, AutostartBewerter.Bewerten(e), FarbeFuer))
            // Rot zuerst, dann Gelb, dann Grün - was Aufmerksamkeit braucht, steht oben.
            // Bereits Ausgeschaltetes rutscht ans Ende, das ist ja erledigt.
            .OrderBy(z => z.Eintrag.IstAktiv ? 0 : 1)
            .ThenBy(z => Reihenfolge(z.Bewertung.Ampel))
            .ThenBy(z => z.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Liste.ItemsSource = zeilen;

        var aktive = zeilen.Where(z => z.Eintrag.IstAktiv).ToList();
        ZusammenfassungText.Text = AutostartBewerter.Zusammenfassung(
            aktive.Select(z => z.Bewertung), aktive.Count);

        var ausgeschaltet = zeilen.Count - aktive.Count;
        HinweisText.Text = ausgeschaltet == 0
            ? Loc.T("Geprüft: Registry (beide Listen), beide Autostart-Ordner und die Aufgabenplanung.",
                    "Checked: Registry (both lists), both startup folders, and Task Scheduler.")
            : Loc.T(
                  $"{ausgeschaltet} bereits ausgeschaltete Einträge stehen unten in der Liste - " +
                  "sie sind nicht gelöscht und jederzeit wieder einschaltbar.",
                  $"{ausgeschaltet} already-disabled entries are listed at the bottom - " +
                  "they are not deleted and can be turned back on at any time.");

        AktualisierenButton.IsEnabled = true;
    }

    private static int Reihenfolge(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Rot => 0,
        AmpelStatus.Gelb => 1,
        AmpelStatus.Gruen => 2,
        _ => 3
    };

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
    {
        MeldungText.Text = "";
        await EinlesenAsync();
    }

    private async void Umschalten_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button knopf || knopf.Tag is not AutostartEintrag eintrag) return;

        knopf.IsEnabled = false;
        var aktivieren = !eintrag.IstAktiv;

        MeldungText.Text = eintrag.Quelle.BrauchtAdminrechte()
            ? Loc.T("Windows fragt gleich nach einer Bestätigung (Administratorrechte) …", "Windows will ask for confirmation (administrator rights) shortly…")
            : Loc.T("Einen Moment …", "One moment…");
        MeldungText.Foreground = (Brush)FindResource("TextMutedBrush");

        var (erfolg, meldung) = await _dienst.UmschaltenAsync(eintrag, aktivieren);

        MeldungText.Text = erfolg
            ? meldung + Loc.T(" Die Änderung gilt ab dem nächsten Start des PCs.", " The change takes effect on the next PC startup.")
            : meldung;
        MeldungText.Foreground = erfolg ? (Brush)FindResource("OkBrush") : (Brush)FindResource("DangerBrush");

        await EinlesenAsync();
    }
}

/// <summary>
/// Eine Zeile der Liste - fasst Eintrag und Bewertung zu fertigen Anzeigetexten zusammen.
/// Nach jeder Aenderung wird die Liste komplett neu eingelesen, deshalb reichen hier
/// einfache, unveraenderliche Eigenschaften ohne Aenderungsbenachrichtigung.
/// </summary>
public class AutostartZeile
{
    public AutostartZeile(AutostartEintrag eintrag, AutostartBewertung bewertung, Func<AmpelStatus, Brush> farbe)
    {
        Eintrag = eintrag;
        Bewertung = bewertung;
        AmpelFarbe = farbe(bewertung.Ampel);
    }

    public AutostartEintrag Eintrag { get; }
    public AutostartBewertung Bewertung { get; }
    public Brush AmpelFarbe { get; }

    public string Name => Eintrag.Name;
    public string Symbol => Eintrag.IstAktiv ? Bewertung.Ampel.Symbol() : "⏸️";
    public string WasEsIst => Bewertung.WasEsIst;
    public string Empfehlung => Bewertung.Empfehlung;

    public string Kurzurteil => Eintrag.IstAktiv
        ? Bewertung.Kurzurteil
        : Loc.T("Ausgeschaltet", "Turned off") +
          (Eintrag.DeaktiviertAm is { } zeit ? Loc.T($" seit {zeit:dd.MM.yyyy}", $" since {zeit:yyyy-MM-dd}") : "") +
          Loc.T(" - startet nicht mehr mit", " - no longer starts");

    public string SchalterBeschriftung => Eintrag.IstAktiv ? Loc.T("Ausschalten", "Turn off") : Loc.T("Wieder einschalten", "Turn back on");

    public string AdminHinweis => Eintrag.Quelle.BrauchtAdminrechte()
        ? Loc.T("Windows fragt nach Bestätigung", "Windows will ask for confirmation")
        : "";

    /// <summary>Woher der Eintrag stammt und was genau gestartet wird - zum Nachvollziehen.</summary>
    public string Herkunft
    {
        get
        {
            var pfad = string.IsNullOrWhiteSpace(Eintrag.ProgrammPfad) ? Eintrag.Befehl : Eintrag.ProgrammPfad;
            var text = Loc.T($"Startet: {pfad}   ·   Ort: {Eintrag.Quelle.Klartext()}",
                              $"Starts: {pfad}   ·   Location: {Eintrag.Quelle.Klartext()}");

            if (Eintrag.DateiFehlt)
                text += Loc.T("   ·   Datei nicht gefunden", "   ·   File not found");

            return text;
        }
    }
}
