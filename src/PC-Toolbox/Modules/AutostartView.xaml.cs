using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
using PCToolboxCore.Models;
#if PRO_BUILD
using PCToolbox.Services.Pro;
using PCToolboxCore.Pro;
#endif

namespace PCToolbox.Modules;

public partial class AutostartView : UserControl
{
    private readonly AutostartService _dienst = new();

#if PRO_BUILD
    private readonly BootAuswirkungService _bootAuswirkungService = new();
    private TextBlock _bootStatusText = null!;
    private StackPanel _bootListe = null!;
#endif

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

#if PRO_BUILD
        BauBootAuswirkungBereich();
#endif

        Loaded += async (_, _) => await EinlesenAsync();
        Loaded += async (_, _) => await WeitereOrteLadenAsync();
#if PRO_BUILD
        Loaded += async (_, _) => await BootAuswirkungLadenAsync();
#endif
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

#if PRO_BUILD
    /// <summary>
    /// Boot-Auswirkung (Pro) - anderer Charakter als die Free-Liste oben (die zeigt ALLES, was
    /// potenziell mitstartet, rein aus Registry/Ordner-Vorhandensein). Hier stehen nur Einträge,
    /// die WINDOWS SELBST als tatsächliche Boot-Verzögerung gemessen hat (ETW, s.
    /// BootAuswirkungService) - echte Millisekunden statt Vermutung. Genau die im Backlog
    /// (IDEEN_BACKLOG.md) schon als offene Idee vermerkte "Startzeit-Messung".
    /// </summary>
    private void BauBootAuswirkungBereich()
    {
        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T("Boot-Auswirkung, gemessen (Pro)", "Boot impact, measured (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });

        if (!WindowsReparaturService.HatAdminRechte())
        {
            ProZusatzBereich.Children.Add(new TextBlock
            {
                Text = Loc.T(
                    "Windows lässt das Auslesen der eigenen Boot-Messwerte nur mit Administratorrechten zu - " +
                    "Toolbox X per Rechtsklick „Als Administrator ausführen\" neu starten, um diesen Bereich zu nutzen.",
                    "Windows only allows reading its own boot measurements with administrator rights - " +
                    "restart Toolbox X via right-click \"Run as administrator\" to use this section."),
                Foreground = (Brush)FindResource("WarnBrush"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        ProZusatzBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Windows protokolliert bei jedem Start selbst, welche Programme den Systemstart wirklich " +
                "verzögert haben - in echten Millisekunden, nicht geschätzt. Gemittelt über alle bisher " +
                "protokollierten Starts.",
                "Windows itself logs at every startup which programs actually delayed the system start - " +
                "in real milliseconds, not estimated. Averaged over all previously logged startups."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        _bootStatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _bootListe = new StackPanel();

        ProZusatzBereich.Children.Add(_bootStatusText);
        ProZusatzBereich.Children.Add(_bootListe);
    }

    private async Task BootAuswirkungLadenAsync()
    {
        if (!WindowsReparaturService.HatAdminRechte()) return;

        _bootStatusText.Text = Loc.T("Werte Boot-Protokoll aus …", "Analyzing boot log…");
        _bootListe.Children.Clear();

        var zusammenfassungen = await _bootAuswirkungService.LadenAsync();

        if (zusammenfassungen.Count == 0)
        {
            _bootStatusText.Text = Loc.T(
                "Keine Boot-Verzögerungen protokolliert - entweder läuft alles schnell genug, oder Windows hat " +
                "noch keine auffälligen Starts aufgezeichnet.",
                "No boot delays logged - either everything runs fast enough, or Windows hasn't recorded any " +
                "noteworthy startups yet.");
            return;
        }

        _bootStatusText.Text = Loc.T($"{zusammenfassungen.Count} Programm(e) mit protokollierter Boot-Verzögerung:",
                                     $"{zusammenfassungen.Count} program(s) with logged boot delay:");

        foreach (var z in zusammenfassungen.Take(15))
        {
            var zeile = new Border
            {
                Background = (Brush)FindResource("BgCardBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var inhalt = new StackPanel();
            inhalt.Children.Add(new TextBlock
            {
                Text = $"{z.Ampel.Symbol()} {(string.IsNullOrWhiteSpace(z.Anzeigename) ? z.Dateiname : z.Anzeigename)}",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            inhalt.Children.Add(new TextBlock
            {
                Text = Loc.T(
                    $"⌀ {z.DurchschnittDegradationMs} ms Verzögerung · {z.AnzahlMessungen}x gemessen · zuletzt {z.LetzteMessungUtc.ToLocalTime():dd.MM.yyyy} · {z.Dateiname}",
                    $"⌀ {z.DurchschnittDegradationMs} ms delay · measured {z.AnzahlMessungen}x · last {z.LetzteMessungUtc.ToLocalTime():yyyy-MM-dd} · {z.Dateiname}"),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            zeile.Child = inhalt;
            _bootListe.Children.Add(zeile);
        }
    }
#endif
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
