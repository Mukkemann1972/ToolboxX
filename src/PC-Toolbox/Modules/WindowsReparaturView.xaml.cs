using System.Windows;
using System.Windows.Controls;
using PCToolbox.Converters;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class WindowsReparaturView : UserControl
{
    private readonly WindowsReparaturService _service = new();
    private readonly WindowsUpdateVerlaufService _updateVerlaufService = new();
    private CancellationTokenSource? _abbruch;

    public WindowsReparaturView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Windows reparieren", "Repair Windows");
        QuelleHinweis.ToolTip = Loc.T(
            "Windows-eigene Werkzeuge: DISM (/Online /Cleanup-Image) für den Komponentenspeicher und sfc für die Systemdateien. Toolbox X ruft sie in der richtigen Reihenfolge auf und übersetzt die Ausgabe - es wird nichts Eigenes an Systemdateien vorbeigeschrieben.",
            "Windows' own tools: DISM (/Online /Cleanup-Image) for the component store and sfc for the system files. Toolbox X calls them in the right order and translates the output - nothing of our own is written to system files.");
        EinleitungText.Text = Loc.T(
            "Prüft die Windows-Systemdateien auf Beschädigungen und repariert sie - mit Windows' eigenen Werkzeugen, aber in der richtigen Reihenfolge und mit einer Erklärung, die man auch versteht.",
            "Checks the Windows system files for damage and repairs them - with Windows' own tools, but in the right order and with an explanation you can actually understand.");
        WarumScheiternTitel.Text = Loc.T("ⓘ  Warum Reparaturversuche so oft scheitern", "ⓘ  Why repair attempts so often fail");
        WarumScheiternText.Text = Loc.T(
            "Windows hat zwei Reparaturen, und die Reihenfolge entscheidet. sfc ersetzt beschädigte Systemdateien - die Ersatzdateien holt es sich aus dem Komponentenspeicher. Ist dieser Speicher selbst beschädigt, kann sfc nichts reparieren, und man bekommt endlos „konnte einige nicht reparieren“. Deshalb wird hier immer zuerst der Speicher geprüft (DISM), dann die Dateien (sfc). Das steht in keiner Windows-Meldung.",
            "Windows has two repair tools, and the order decides everything. sfc replaces damaged system files - it gets the replacement files from the component store. If that store itself is damaged, sfc can't repair anything, and you get an endless \"could not fix some of them\". That's why the store is always checked first here (DISM), then the files (sfc). No Windows message tells you that.");
        AdminFehlenTitel.Text = Loc.T("Administrator-Rechte fehlen", "Administrator rights missing");
        AdminFehlenText.Text = Loc.T(
            "Windows lässt die Systemprüfung ohne Administrator-Rechte gar nicht erst zu. Toolbox X schließen und per Rechtsklick „Als Administrator ausführen“ neu starten. Das ist eine Schutzmaßnahme von Windows, kein Fehler.",
            "Windows doesn't allow the system check at all without administrator rights. Close Toolbox X and restart it via right-click \"Run as administrator\". This is a Windows protection measure, not a bug.");
        PruefenButton.Content = Loc.T("Windows überprüfen (ändert nichts)", "Check Windows (changes nothing)");
        ReparierenButton.Content = Loc.T("Jetzt reparieren", "Repair now");
        AbbrechenButton.Content = Loc.T("Abbrechen", "Cancel");
        LaufZeile.Text = Loc.T("Die Prüfung dauert je nach PC einige Minuten. Sie liest nur - es wird dabei nichts verändert.",
                               "Depending on the PC, the check takes a few minutes. It only reads - nothing is changed.");
        DismTitelText.Text = Loc.T("1. Komponentenspeicher (das Lager mit den Ersatzdateien)", "1. Component store (the storage with the replacement files)");
        SfcTitelText.Text = Loc.T("2. Systemdateien selbst", "2. System files themselves");
        DismUrsacheBereich.Header = Loc.T("Wie kommt so ein Schaden zustande?", "How does damage like this happen?");
        SfcUrsacheBereich.Header = Loc.T("Wie kommt so ein Schaden zustande?", "How does damage like this happen?");
        DismRohBereich.Header = Loc.T("Originalausgabe von Windows anzeigen", "Show original Windows output");
        SfcRohBereich.Header = Loc.T("Originalausgabe von Windows anzeigen", "Show original Windows output");

        UpdateVerlaufBereich.Header = Loc.T("Windows-Update-Verlauf (letzte 15)", "Windows update history (last 15)");

        Loaded += (_, _) =>
        {
            if (!WindowsReparaturService.HatAdminRechte())
            {
                AdminHinweis.Visibility = Visibility.Visible;
                PruefenButton.IsEnabled = false;
            }
        };

        // Unabhaengig von Pruefen/Reparieren: der Update-Verlauf braucht keine Admin-Rechte
        // und keinen Systemscan, laedt deshalb sofort beim Oeffnen des Moduls.
        Loaded += async (_, _) =>
        {
            try
            {
                await LadeUpdateVerlaufAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(WindowsReparaturView) + ".UpdateVerlauf", ex.ToString());
            }
        };
    }

    private async Task LadeUpdateVerlaufAsync()
    {
        var verlauf = await _updateVerlaufService.GetVerlaufAsync();

        if (verlauf.Count == 0)
        {
            UpdateVerlaufHinweis.Text = Loc.T(
                "Kein Update-Verlauf gefunden (z.B. wenn der Windows-Update-Dienst deaktiviert ist).",
                "No update history found (e.g. if the Windows Update service is disabled).");
            UpdateVerlaufListe.ItemsSource = null;
            return;
        }

        var fehlgeschlageneAnzahl = verlauf.Count(e => e.IstFehlgeschlagen);
        UpdateVerlaufHinweis.Text = fehlgeschlageneAnzahl > 0
            ? Loc.T(
                $"{fehlgeschlageneAnzahl} von {verlauf.Count} Einträgen sind nicht sauber durchgelaufen - passt das zeitlich zu einem gefundenen Schaden oben, ist das ein starker Kandidat für die Ursache.",
                $"{fehlgeschlageneAnzahl} of {verlauf.Count} entries didn't complete cleanly - if the timing matches damage found above, that's a strong candidate for the cause.")
            : Loc.T(
                "Alle Einträge im Verlauf sind sauber durchgelaufen.",
                "All entries in the history completed cleanly.");

        UpdateVerlaufListe.ItemsSource = verlauf.Select(ZuAnzeigeZeile).ToList();
    }

    private static string ZuAnzeigeZeile(WindowsUpdateEintrag e)
    {
        var praefix = e.IstFehlgeschlagen ? "⚠️" : "✅";
        var kb = e.KbNummer is not null ? $" ({e.KbNummer})" : "";
        return $"{praefix} {e.Datum:dd.MM.yyyy HH:mm}  {e.Operation}: {e.Titel}{kb} — {e.Ergebnis}";
    }

    private async void Pruefen_Click(object sender, RoutedEventArgs e)
    {
        // Reihenfolge ist Absicht und der eigentliche Mehrwert des Moduls:
        // zuerst der Komponentenspeicher (DISM), dann die Systemdateien (SFC).
        await AusfuehrenAsync(
            WindowsReparaturService.DismPruefen,
            WindowsReparaturService.SfcPruefen,
            reparaturModus: false);
    }

    private async void Reparieren_Click(object sender, RoutedEventArgs e)
    {
        var antwort = MessageBox.Show(
            Loc.T(
                "Windows wird jetzt reparieren:\n\n" +
                "1. Komponentenspeicher wiederherstellen (DISM)\n" +
                "2. Beschädigte Systemdateien ersetzen (sfc)\n\n" +
                "Das dauert je nach PC 10 bis 40 Minuten und braucht eine Internetverbindung, " +
                "weil Windows sich die Ersatzdateien holt. Der PC bleibt dabei benutzbar, " +
                "sollte aber nicht ausgeschaltet werden.\n\n" +
                "Es werden ausschließlich Windows-eigene Systemdateien durch die Originale ersetzt. " +
                "Deine Dateien, Programme und Einstellungen bleiben unberührt.\n\n" +
                "Jetzt starten?",
                "Windows will now repair:\n\n" +
                "1. Restore component store (DISM)\n" +
                "2. Replace damaged system files (sfc)\n\n" +
                "Depending on the PC, this takes 10 to 40 minutes and needs an internet connection, " +
                "since Windows fetches the replacement files. The PC remains usable during this, " +
                "but should not be turned off.\n\n" +
                "Only Windows' own system files are replaced with the originals. " +
                "Your files, programs, and settings remain untouched.\n\n" +
                "Start now?"),
            Loc.T("Windows reparieren", "Repair Windows"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (antwort != MessageBoxResult.OK) return;

        await AusfuehrenAsync(
            WindowsReparaturService.DismReparieren,
            WindowsReparaturService.SfcReparieren,
            reparaturModus: true);
    }

    private void Abbrechen_Click(object sender, RoutedEventArgs e) => _abbruch?.Cancel();

    private async Task AusfuehrenAsync(string dismArgumente, string sfcArgumente, bool reparaturModus)
    {
        _abbruch = new CancellationTokenSource();
        PruefenButton.IsEnabled = false;
        ReparierenButton.Visibility = Visibility.Collapsed;
        AbbrechenButton.IsEnabled = true;
        FazitZeile.Visibility = Visibility.Collapsed;
        DismKarte.Visibility = Visibility.Collapsed;
        SfcKarte.Visibility = Visibility.Collapsed;

        try
        {
            LaufZeile.Text = reparaturModus
                ? Loc.T("Schritt 1 von 2: Komponentenspeicher wird wiederhergestellt - das ist der lange Teil, bitte Geduld.",
                        "Step 1 of 2: Restoring the component store - this is the long part, please be patient.")
                : Loc.T("Schritt 1 von 2: Komponentenspeicher wird geprüft...", "Step 1 of 2: Checking the component store...");

            var (dismErgebnis, dismRoh) = await _service.DismAusfuehrenAsync(
                dismArgumente, ZeigeFortschritt, _abbruch.Token);
            ZeigeKarte(DismKarte, DismBefund, DismSchritt, DismRohBereich, DismRoh,
                       DismUrsacheBereich, DismUrsache, dismErgebnis, dismRoh);

            LaufZeile.Text = reparaturModus
                ? Loc.T("Schritt 2 von 2: Beschädigte Systemdateien werden ersetzt...", "Step 2 of 2: Replacing damaged system files...")
                : Loc.T("Schritt 2 von 2: Systemdateien werden geprüft...", "Step 2 of 2: Checking system files...");

            var (sfcErgebnis, sfcRoh) = await _service.SfcAusfuehrenAsync(
                sfcArgumente, ZeigeFortschritt, _abbruch.Token);
            ZeigeKarte(SfcKarte, SfcBefund, SfcSchritt, SfcRohBereich, SfcRoh,
                       SfcUrsacheBereich, SfcUrsache, sfcErgebnis, sfcRoh);

            ZeigeFazit(dismErgebnis, sfcErgebnis, reparaturModus);
        }
        finally
        {
            PruefenButton.IsEnabled = true;
            AbbrechenButton.IsEnabled = false;
            _abbruch?.Dispose();
            _abbruch = null;
        }
    }

    private void ZeigeFortschritt(string zeile)
    {
        // DISM schreibt seinen Fortschritt als sich wiederholende Prozentzeilen -
        // nur die letzte Zeile zeigen, sonst rauscht die Anzeige.
        Dispatcher.Invoke(() => LaufZeile.Text = zeile);
    }

    private static void ZeigeKarte(Border karte, ContentControl befundFeld, TextBlock schrittFeld,
                                   Expander rohBereich, TextBox rohFeld,
                                   Expander ursacheBereich, TextBlock ursacheFeld,
                                   PruefErgebnis ergebnis, string rohausgabe)
    {
        // Das "Warum" nur zeigen, wenn es wirklich etwas zu erklären gibt - bei
        // einem sauberen Ergebnis wäre eine Ursachenliste nur Verunsicherung.
        if (ergebnis.Ursache.Length > 0)
        {
            ursacheFeld.Text = ergebnis.Ursache;
            ursacheBereich.Visibility = Visibility.Visible;
        }
        else
        {
            ursacheBereich.Visibility = Visibility.Collapsed;
        }

        befundFeld.Content = AmpelIconFactory.Build(ZuAmpel(ergebnis.Befund)) is { } icon
            ? BaueBefundZeile(icon, ergebnis.Klartext)
            : new TextBlock { Text = ergebnis.Klartext, TextWrapping = TextWrapping.Wrap };
        schrittFeld.Text = ergebnis.NaechsterSchritt;

        // Originalausgabe nur anbieten, wenn es etwas zu zeigen gibt. Bei "Unklar"
        // ist sie das Wichtigste, deshalb dann gleich aufgeklappt.
        if (rohausgabe.Trim().Length > 0)
        {
            rohFeld.Text = rohausgabe.Trim();
            rohBereich.Visibility = Visibility.Visible;
            rohBereich.IsExpanded = ergebnis.Befund == PruefBefund.Unklar;
        }
        else
        {
            rohBereich.Visibility = Visibility.Collapsed;
        }

        karte.Visibility = Visibility.Visible;
    }

    private static UIElement BaueBefundZeile(UIElement icon, string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        block.Inlines.Add(new System.Windows.Documents.InlineUIContainer(icon)
        {
            BaselineAlignment = BaselineAlignment.Center
        });
        block.Inlines.Add(new System.Windows.Documents.Run(" " + text));
        return block;
    }

    private static AmpelStatus ZuAmpel(PruefBefund befund) => befund switch
    {
        PruefBefund.Sauber => AmpelStatus.Gruen,
        PruefBefund.BeschaedigtBehoben => AmpelStatus.Gruen,
        PruefBefund.BeschaedigtReparierbar => AmpelStatus.Gelb,
        PruefBefund.BeschaedigtNichtBehebbar => AmpelStatus.Rot,
        _ => AmpelStatus.Unbekannt
    };

    private void ZeigeFazit(PruefErgebnis dism, PruefErgebnis sfc, bool reparaturModus)
    {
        var reparaturNoetig =
            dism.Befund is PruefBefund.BeschaedigtReparierbar or PruefBefund.BeschaedigtNichtBehebbar ||
            sfc.Befund is PruefBefund.BeschaedigtReparierbar or PruefBefund.BeschaedigtNichtBehebbar;

        if (!reparaturModus && reparaturNoetig)
        {
            // Reparaturknopf erscheint erst, wenn es wirklich etwas zu reparieren gibt -
            // nicht als Dauerangebot "einmal alles durchlaufen lassen".
            ReparierenButton.Visibility = Visibility.Visible;
            LaufZeile.Text = Loc.T(
                "Es wurde etwas gefunden. Der Knopf \"Jetzt reparieren\" führt beide Reparaturen " +
                "in der richtigen Reihenfolge aus.",
                "Something was found. The \"Repair now\" button runs both repairs " +
                "in the right order.");
            return;
        }

        LaufZeile.Text = reparaturModus
            ? Loc.T("Reparatur abgeschlossen.", "Repair completed.")
            : Loc.T("Prüfung abgeschlossen - es wurde nichts verändert.", "Check completed - nothing was changed.");

        FazitZeile.Visibility = Visibility.Visible;
        FazitZeile.Text = (dism.Befund, sfc.Befund) switch
        {
            (PruefBefund.Sauber, PruefBefund.Sauber) => Loc.T(
                "Windows selbst ist in Ordnung - weder der Komponentenspeicher noch die Systemdateien sind " +
                "beschädigt. Wenn dein Problem bleibt, liegt es nicht an Windows-Systemdateien. Die nächsten " +
                "Kandidaten sind dann Treiber (siehe Treiber-Check), Autostart oder die Hardware selbst.",
                "Windows itself is fine - neither the component store nor the system files are " +
                "damaged. If your problem remains, it's not due to Windows system files. The next " +
                "candidates are then drivers (see the driver check), startup programs, or the hardware itself."),

            (_, PruefBefund.BeschaedigtNichtBehebbar) => Loc.T(
                "Es sind Beschädigungen übrig, die diese Werkzeuge nicht beheben konnten. Der nächste sinnvolle " +
                "Schritt ist eine Reparatur-Installation über das offizielle Windows-Installationsmedium - die " +
                "behält Programme und Daten. Vorher trotzdem ein Backup machen.",
                "There is remaining damage that these tools could not fix. The next sensible " +
                "step is a repair installation via the official Windows installation media - it " +
                "keeps programs and data. Make a backup beforehand anyway."),

            _ when reparaturModus => Loc.T(
                "Ein Neustart ist jetzt sinnvoll, damit die ersetzten Dateien überall greifen. Danach diese " +
                "Prüfung noch einmal laufen lassen: erst dann ist bestätigt, dass wirklich alles behoben ist.",
                "A restart is a good idea now, so the replaced files take effect everywhere. Then run this " +
                "check again: only then is it confirmed that everything is really fixed."),

            _ => Loc.T("Prüfung durchgelaufen. Die Einzelbefunde stehen oben.", "Check completed. The individual findings are above.")
        };
    }
}
