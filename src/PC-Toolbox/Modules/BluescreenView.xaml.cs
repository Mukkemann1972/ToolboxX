using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Converters;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class BluescreenView : UserControl
{
    private readonly DumpReader _service = new();
    private readonly WindowsFehleranalyseService _fehleranalyseDienst = new();
    private readonly ZuverlaessigkeitsVerlaufService _verlaufDienst = new();

    public BluescreenView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Letzte Abstürze (Bluescreens)", "Recent crashes (blue screens)");
        QuelleHinweis.ToolTip = Loc.T(
            "Windows-Ereignisprotokoll: Provider Microsoft-Windows-WER-SystemErrorReporting, Event-ID 1001. Fallback, falls das Log leer ist: Zeitstempel der .dmp-Dateien.",
            "Windows event log: provider Microsoft-Windows-WER-SystemErrorReporting, event ID 1001. Fallback if the log is empty: timestamps of the .dmp files.");

        FehleranalyseTitel.Text = Loc.T("Windows-Fehleranalyse", "Windows error analysis");
        FehleranalyseEinleitung.Text = Loc.T(
            "Breiter als oben (nicht nur Abstürze): alle Kritisch/Fehler/Warnung-Einträge aus dem System- " +
            "und Anwendungsprotokoll im gewählten Zeitraum, nach Häufigkeit verdichtet - derselbe Fehler " +
            "taucht oft alle paar Minuten erneut auf, eine unverdichtete Liste wäre unlesbar.",
            "Broader than above (not just crashes): all Critical/Error/Warning entries from the System and " +
            "Application log in the selected time range, condensed by frequency - the same error often " +
            "recurs every few minutes, an uncondensed list would be unreadable.");
        ZeitraumLabel.Text = Loc.T("Zeitraum:", "Time range:");
        Zeitraum24hItem.Content = Loc.T("Letzte 24 Stunden", "Last 24 hours");
        Zeitraum7TageItem.Content = Loc.T("Letzte 7 Tage", "Last 7 days");
        Zeitraum30TageItem.Content = Loc.T("Letzte 30 Tage", "Last 30 days");
        FehleranalyseButton.Content = Loc.T("Analysieren", "Analyze");

        VerlaufTitel.Text = Loc.T("Zuverlässigkeitsverlauf", "Reliability history");
        VerlaufEinleitung.Text = Loc.T(
            "Zeitachse: an welchen Tagen Abstürze, fehlgeschlagene Updates/Installationen oder " +
            "Treiberfehler auftraten - dieselbe Datenquelle wie Windows' eigener Zuverlässigkeitsverlauf.",
            "Timeline: on which days crashes, failed updates/installs, or driver errors occurred - " +
            "the same data source as Windows' own Reliability Monitor.");
        VerlaufZeitraumLabel.Text = Loc.T("Zeitraum:", "Time range:");
        VerlaufZeitraum7TageItem.Content = Loc.T("Letzte 7 Tage", "Last 7 days");
        VerlaufZeitraum30TageItem.Content = Loc.T("Letzte 30 Tage", "Last 30 days");
        VerlaufZeitraum90TageItem.Content = Loc.T("Letzte 90 Tage", "Last 90 days");
        VerlaufButton.Content = Loc.T("Verlauf laden", "Load history");

        // Team-Regel: keine Auffälligkeit ohne Klartext-Erklärung, was Symbol/Farbe bedeutet -
        // gilt für Symbole genauso wie für Textmeldungen, deshalb hier eine sichtbare Legende
        // statt anzunehmen, dass Rot/Gelb selbsterklärend sind.
        LegendeFuellen(FehleranalyseLegende, (AmpelStatus.Rot, Loc.T("Kritisch/Fehler", "Critical/Error")),
            (AmpelStatus.Gelb, Loc.T("Warnung", "Warning")));
        LegendeFuellen(VerlaufLegende, (AmpelStatus.Rot, Loc.T("Absturz", "Crash")),
            (AmpelStatus.Gelb, Loc.T("Fehlgeschlagene Installation/Update", "Failed install/update")),
            (null, Loc.T("Zahl ohne Symbol = erfolgreiche Installation/Update", "Number without icon = successful install/update")));

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
        QuelleText = Loc.T($"Quelle: {e.Quelle}", $"Source: {e.Quelle}"),
        BrauchtErhoehteRechte = e.BrauchtErhoehteRechte,
        ErhoehteRechteBeschriftung = Loc.T("🔒 Mit Administratorrechten prüfen", "🔒 Check with administrator rights"),
    };

    // Erst NACH ausdruecklicher Zustimmung aufgerufen. Elevated wird hier NUR das Auflisten des
    // Minidump-Ordners, nicht die App selbst - kein Grund, einem Free-Nutzer "starte alles als
    // Administrator neu" zuzumuten, nur um ein paar Dateizeitstempel zu sehen.
    private async void MinidumpsMitErhoehtenRechten_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var weiter = System.Windows.MessageBox.Show(
            Loc.T(
                "Der Minidump-Ordner (C:\\Windows\\Minidump) gehört Windows selbst - nur Administratoren dürfen " +
                "hineinschauen, deshalb die Sperre. Jetzt mit Administratorrechten nachsehen? Windows zeigt dafür " +
                "eine Sicherheitsabfrage (UAC). Es wird dabei NUR dieser eine Ordner gelesen, sonst nichts.",
                "The minidump folder (C:\\Windows\\Minidump) belongs to Windows itself - only administrators may " +
                "look into it, hence the block. Look now with administrator rights? Windows will show a security " +
                "prompt (UAC) for this. Only this one folder is read, nothing else."),
            Loc.T("Administratorrechte nötig", "Administrator rights needed"),
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

        if (weiter != System.Windows.MessageBoxResult.Yes) return;

        button.IsEnabled = false;
        var eintraege = await _service.MinidumpsMitErhoehtenRechtenListenAsync();
        Liste.ItemsSource = eintraege.Select(ZuAnzeigeZeile).ToList();
    }

    private async void FehleranalyseStarten_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var stunden = ZeitraumAuswahl.SelectedItem is ComboBoxItem { Tag: string tagWert } &&
                      int.TryParse(tagWert, out var h)
            ? h
            : 24;

        FehleranalyseButton.IsEnabled = false;
        FehlerGruppenListe.Children.Clear();
        FehleranalyseStatusText.Text = Loc.T("Lese Ereignisprotokolle …", "Reading event logs…");

        var gruppen = await _fehleranalyseDienst.AnalysierenAsync(TimeSpan.FromHours(stunden));

        FehleranalyseButton.IsEnabled = true;

        if (gruppen.Count == 0)
        {
            FehleranalyseStatusText.Text = Loc.T(
                "✅ Keine Kritisch-/Fehler-/Warnung-Einträge in diesem Zeitraum.",
                "✅ No Critical/Error/Warning entries in this time range.");
            return;
        }

        FehleranalyseStatusText.Text = Loc.T(
            $"{gruppen.Count} unterschiedliche Fehlerarten gefunden ({gruppen.Sum(g => g.Anzahl)} Einträge insgesamt).",
            $"{gruppen.Count} distinct error types found ({gruppen.Sum(g => g.Anzahl)} entries total).");

        foreach (var gruppe in gruppen.Take(30))
        {
            var (status, ressource) = gruppe.Schweregrad switch
            {
                FehlerSchweregrad.Kritisch => (AmpelStatus.Rot, "DangerBrush"),
                FehlerSchweregrad.Fehler => (AmpelStatus.Rot, "DangerBrush"),
                _ => (AmpelStatus.Gelb, "WarnBrush")
            };

            var karte = new StackPanel { Margin = new System.Windows.Thickness(0, 0, 0, 8) };
            var kopfzeile = new StackPanel { Orientation = Orientation.Horizontal };
            kopfzeile.Children.Add(AmpelIconFactory.Build(status, 14));
            kopfzeile.Children.Add(new TextBlock
            {
                Text = $" {gruppe.Anbieter} (ID {gruppe.EreignisId}) · {gruppe.Anzahl}× · zuletzt {gruppe.LetztesVorkommen:dd.MM.yyyy HH:mm}",
                FontWeight = System.Windows.FontWeights.SemiBold,
                FontSize = 12,
                Foreground = (Brush)FindResource(ressource),
                TextWrapping = System.Windows.TextWrapping.Wrap
            });
            karte.Children.Add(kopfzeile);
            if (!string.IsNullOrWhiteSpace(gruppe.BeispielText))
            {
                karte.Children.Add(new TextBlock
                {
                    Text = gruppe.BeispielText,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 2, 0, 0)
                });
            }
            FehlerGruppenListe.Children.Add(karte);
        }
    }

    private async void VerlaufStarten_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var stunden = VerlaufZeitraumAuswahl.SelectedItem is ComboBoxItem { Tag: string tagWert } &&
                      int.TryParse(tagWert, out var h)
            ? h
            : 720;

        VerlaufButton.IsEnabled = false;
        VerlaufTageListe.Children.Clear();
        VerlaufStatusText.Text = Loc.T("Lese Zuverlässigkeitsverlauf …", "Reading reliability history…");

        var eintraege = await _verlaufDienst.LesenAsync(TimeSpan.FromHours(stunden));
        var tage = ZuverlaessigkeitsVerlauf.NachTagGruppieren(eintraege);

        VerlaufButton.IsEnabled = true;

        if (tage.Count == 0)
        {
            VerlaufStatusText.Text = Loc.T(
                "Keine Einträge in diesem Zeitraum gefunden.",
                "No entries found in this time range.");
            return;
        }

        var abstuerze = tage.Sum(t => t.AnzahlAbsturz);
        var fehler = tage.Sum(t => t.AnzahlFehler);
        VerlaufStatusText.Text = abstuerze == 0 && fehler == 0
            ? Loc.T(
                $"✅ Keine Abstürze oder fehlgeschlagenen Updates in diesem Zeitraum ({tage.Count} Tage mit Aktivität).",
                $"✅ No crashes or failed updates in this time range ({tage.Count} days with activity).")
            : Loc.T(
                $"{abstuerze} Absturz/Abstürze, {fehler} fehlgeschlagene Installation(en)/Update(s) an {tage.Count} Tagen.",
                $"{abstuerze} crash(es), {fehler} failed install(s)/update(s) across {tage.Count} days.");

        foreach (var tag in tage.Take(90))
        {
            var karte = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 0, 0, 6) };
            karte.Children.Add(new TextBlock
            {
                Text = tag.Tag.ToString("dd.MM.yyyy"),
                Width = 90,
                FontSize = 12,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });

            if (tag.AnzahlAbsturz > 0)
                karte.Children.Add(TagBadge(AmpelStatus.Rot, tag.AnzahlAbsturz));
            if (tag.AnzahlFehler > 0)
                karte.Children.Add(TagBadge(AmpelStatus.Gelb, tag.AnzahlFehler));
            if (tag.AnzahlInfo > 0)
            {
                karte.Children.Add(new TextBlock
                {
                    Text = tag.AnzahlInfo.ToString(),
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    Margin = new System.Windows.Thickness(0, 0, 14, 0),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                });
            }

            VerlaufTageListe.Children.Add(karte);
        }
    }

    private void LegendeFuellen(StackPanel ziel, params (AmpelStatus? Status, string Beschriftung)[] eintraege)
    {
        ziel.Children.Clear();
        foreach (var (status, beschriftung) in eintraege)
        {
            var zeile = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 16, 0) };
            if (status is { } s)
                zeile.Children.Add(AmpelIconFactory.Build(s, 13));
            zeile.Children.Add(new TextBlock
            {
                Text = beschriftung,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Margin = new Thickness(status is null ? 0 : 4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            ziel.Children.Add(zeile);
        }
    }

    // Vektor-Ampelsymbol statt rohem Emoji (🔴/🟠) - manche Windows-Installationen haben keine
    // vollstaendige Emoji-Schriftart, dann erscheinen unbekannte Emoji als leere Kaestchen statt
    // als Symbol (live an Alex' Rechner bestaetigt). AmpelIconFactory zeichnet stattdessen selbst.
    private static UIElement TagBadge(AmpelStatus status, int anzahl)
    {
        var zeile = new StackPanel { Orientation = Orientation.Horizontal, Margin = new System.Windows.Thickness(0, 0, 14, 0) };
        zeile.Children.Add(AmpelIconFactory.Build(status, 15));
        zeile.Children.Add(new TextBlock
        {
            Text = anzahl.ToString(),
            FontSize = 12,
            Margin = new System.Windows.Thickness(3, 0, 0, 0),
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        });
        return zeile;
    }

    private class BsodAnzeige
    {
        public string Titel { get; set; } = "";
        public string Loesung { get; set; } = "";
        public string QuelleText { get; set; } = "";
        public bool BrauchtErhoehteRechte { get; set; }
        public string ErhoehteRechteBeschriftung { get; set; } = "";
    }
}
