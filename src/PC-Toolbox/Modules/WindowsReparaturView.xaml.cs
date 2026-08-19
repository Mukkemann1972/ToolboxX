using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PCToolbox.Converters;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using PCToolbox.Services.Pro;
using PCToolboxCore.Pro;
#endif

namespace PCToolbox.Modules;

public partial class WindowsReparaturView : UserControl
{
    private readonly WindowsReparaturService _service = new();
    private readonly WindowsUpdateVerlaufService _updateVerlaufService = new();
    private CancellationTokenSource? _abbruch;
    private string? _geladenerSchluessel;

#if PRO_BUILD
    private readonly KomponentenspeicherService _komponentenspeicherService = new();
    private Button _analysierenButton = null!;
    private Button _bereinigenButton = null!;
    private Button _bereinigenResetBaseButton = null!;
    private TextBlock _komponentenspeicherStatusText = null!;
    private TextBlock _komponentenspeicherMeldung = null!;

    private readonly UpdateReparaturService _updateReparaturService = new();
    private Button _updateResetButton = null!;
    private Button _updateResetSequenzButton = null!;
    private TextBlock _updateResetMeldung = null!;
#endif

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

        SchluesselBereich.Header = Loc.T("Produktschlüssel sichern", "Back up product key");
        SchluesselErklaerung.Text = Loc.T(
            "Liest den werksseitig hinterlegten OEM-Produktschlüssel aus - praktisch, um ihn VOR " +
            "einer Neuinstallation zu sichern. Rein lesend, ändert nichts. Wird nichts gefunden, " +
            "ist das bei vielen Windows-10/11-Installationen normal: eine digitale Lizenz hängt " +
            "am Microsoft-Konto bzw. der Hardware selbst, nicht an einem 25-stelligen Schlüssel.",
            "Reads the factory-embedded OEM product key - handy to back up BEFORE a reinstall. " +
            "Read-only, changes nothing. Finding nothing is normal on many Windows 10/11 " +
            "installations: a digital license is tied to the Microsoft account or the hardware " +
            "itself, not to a 25-character key.");
        SchluesselLadenButton.Content = Loc.T("Schlüssel anzeigen", "Show key");
        SchluesselSpeichernButton.Content = Loc.T("Als Datei speichern …", "Save as file …");

#if PRO_BUILD
        BauKomponentenspeicherBereich();
        BauUpdateReparaturBereich();
#endif

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

    private async void SchluesselLaden_Click(object sender, RoutedEventArgs e)
    {
        SchluesselLadenButton.IsEnabled = false;
        SchluesselStatusText.Text = Loc.T("Lese …", "Reading…");
        SchluesselStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        _geladenerSchluessel = await ProduktSchluesselService.OemSchluesselLadenAsync();
        SchluesselLadenButton.IsEnabled = true;

        if (_geladenerSchluessel is null || !ProduktSchluesselFormat.SiehtGueltigAus(_geladenerSchluessel))
        {
            SchluesselFeld.Visibility = Visibility.Collapsed;
            SchluesselSpeichernButton.Visibility = Visibility.Collapsed;
            SchluesselStatusText.Text = Loc.T(
                "Kein Firmware-Schlüssel gefunden - üblich bei digitaler Lizenz (s. Erklärung oben), kein Fehler.",
                "No firmware key found - common with a digital license (see explanation above), not an error.");
            SchluesselStatusText.Foreground = (Brush)FindResource("TextMutedBrush");
            return;
        }

        SchluesselFeld.Text = _geladenerSchluessel;
        SchluesselFeld.Visibility = Visibility.Visible;
        SchluesselSpeichernButton.Visibility = Visibility.Visible;
        SchluesselStatusText.Text = Loc.T("✔ Schlüssel gefunden.", "✔ Key found.");
        SchluesselStatusText.Foreground = (Brush)FindResource("OkBrush");
    }

    private void SchluesselSpeichern_Click(object sender, RoutedEventArgs e)
    {
        if (_geladenerSchluessel is null) return;

        var dialog = new SaveFileDialog
        {
            Title = Loc.T("Produktschlüssel speichern", "Save product key"),
            Filter = "Textdatei (*.txt)|*.txt",
            FileName = $"windows-produktschluessel-{Environment.MachineName}-{DateTime.Now:yyyy-MM-dd}.txt"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var inhalt = Loc.T(
                $"Windows-Produktschlüssel für {Environment.MachineName}\nGesichert am {DateTime.Now:dd.MM.yyyy HH:mm}\n\n{_geladenerSchluessel}\n",
                $"Windows product key for {Environment.MachineName}\nSaved on {DateTime.Now:yyyy-MM-dd HH:mm}\n\n{_geladenerSchluessel}\n");
            File.WriteAllText(dialog.FileName, inhalt);

            SchluesselStatusText.Text = Loc.T($"✔ Gespeichert: {dialog.FileName}", $"✔ Saved: {dialog.FileName}");
            SchluesselStatusText.Foreground = (Brush)FindResource("OkBrush");
        }
        catch (IOException ex)
        {
            SchluesselStatusText.Text = Loc.T($"✘ Konnte nicht gespeichert werden: {ex.Message}", $"✘ Could not save: {ex.Message}");
            SchluesselStatusText.Foreground = (Brush)FindResource("DangerBrush");
        }
    }

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

#if PRO_BUILD
    /// <summary>
    /// Komponentenspeicher-Aufräumen (Pro) - bewusst getrennt vom Prüfen/Reparieren-Fluss oben:
    /// hier geht es nicht um Beschädigung, sondern um Speicherplatz, den alte/überflüssige
    /// Update-Versionen im WinSxS-Ordner belegen. Genau die Aktion, vor der die Free-Erklärung
    /// oben warnt ("ein Tuning-Programm hat in WinSxS aufgeräumt") - hier aber über den
    /// offiziellen DISM-Weg, mit ehrlicher Erklärung, was man dafür aufgibt (s. Bestätigungstexte
    /// unten): die Möglichkeit, betroffene Updates später zu deinstallieren.
    /// </summary>
    private void BauKomponentenspeicherBereich()
    {
        var titel = new TextBlock
        {
            Text = Loc.T("Speicherplatz zurückgewinnen (Pro)", "Reclaim disk space (Pro)"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var erklaerung = new TextBlock
        {
            Text = Loc.T(
                "Der Komponentenspeicher (WinSxS) sammelt mit jedem Windows-Update alte Dateiversionen an - " +
                "die dienen als Rückfallebene, falls ein Update deinstalliert werden muss. DISM kann diese " +
                "alten Versionen offiziell aufräumen (nicht von Hand löschen - genau das richtet echten Schaden an). " +
                "Der Tradeoff: danach lassen sich die betroffenen Updates nicht mehr deinstallieren.",
                "The component store (WinSxS) accumulates old file versions with every Windows update - " +
                "they serve as a fallback if an update needs to be uninstalled. DISM can officially clean up these " +
                "old versions (not by deleting by hand - that's exactly what causes real damage). " +
                "The tradeoff: afterward, the affected updates can no longer be uninstalled."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        };

        _analysierenButton = new Button
        {
            Content = Loc.T("Speicherplatz analysieren", "Analyze disk space"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _analysierenButton.Click += KomponentenspeicherAnalysieren_Click;

        _komponentenspeicherStatusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 0)
        };

        _bereinigenButton = new Button
        {
            Content = Loc.T("Jetzt bereinigen", "Clean up now"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _bereinigenButton.Click += (_, _) => KomponentenspeicherBereinigenAsync(resetBase: false);

        _bereinigenResetBaseButton = new Button
        {
            Content = Loc.T("Auch aktuelle Updates einschließen (ResetBase)", "Also include current updates (ResetBase)"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _bereinigenResetBaseButton.Click += (_, _) => KomponentenspeicherBereinigenAsync(resetBase: true);

        _komponentenspeicherMeldung = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 0)
        };

        ProZusatzBereich.Children.Add(titel);
        ProZusatzBereich.Children.Add(erklaerung);
        ProZusatzBereich.Children.Add(_analysierenButton);
        ProZusatzBereich.Children.Add(_komponentenspeicherStatusText);
        ProZusatzBereich.Children.Add(_bereinigenButton);
        ProZusatzBereich.Children.Add(_bereinigenResetBaseButton);
        ProZusatzBereich.Children.Add(_komponentenspeicherMeldung);
    }

    private async void KomponentenspeicherAnalysieren_Click(object sender, RoutedEventArgs e) =>
        await KomponentenspeicherAnalysierenAsync();

    private async Task KomponentenspeicherAnalysierenAsync()
    {
        _analysierenButton.IsEnabled = false;
        _bereinigenButton.Visibility = Visibility.Collapsed;
        _bereinigenResetBaseButton.Visibility = Visibility.Collapsed;
        _komponentenspeicherStatusText.Text = Loc.T("Analysiere Komponentenspeicher …", "Analyzing component store…");
        _komponentenspeicherStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var (info, roh) = await _komponentenspeicherService.AnalysierenAsync();

        if (info is null)
        {
            _komponentenspeicherStatusText.Text = Loc.T($"Analyse fehlgeschlagen: {roh}", $"Analysis failed: {roh}");
            _komponentenspeicherStatusText.Foreground = (Brush)FindResource("DangerBrush");
            _analysierenButton.IsEnabled = true;
            return;
        }

        var groesseText = info.GroesseGb.HasValue
            ? Loc.T($"Aktuelle Größe: {info.GroesseGb.Value:0.0} GB.", $"Current size: {info.GroesseGb.Value:0.0} GB.")
            : "";
        var letzteText = info.LetzteBereinigung.HasValue
            ? Loc.T($" Letzte Bereinigung: {info.LetzteBereinigung.Value:dd.MM.yyyy}.", $" Last cleanup: {info.LetzteBereinigung.Value:yyyy-MM-dd}.")
            : Loc.T(" Noch nie bereinigt.", " Never cleaned up.");
        var empfehlungText = info.BereinigungEmpfohlen switch
        {
            true => Loc.T($" Windows empfiehlt eine Bereinigung ({info.WiederverwendbarePakete ?? 0} wiederverwendbare Pakete).",
                          $" Windows recommends a cleanup ({info.WiederverwendbarePakete ?? 0} reclaimable packages)."),
            false => Loc.T(" Windows sieht aktuell keinen Bereinigungsbedarf.", " Windows currently sees no need for cleanup."),
            null => ""
        };

        _komponentenspeicherStatusText.Text = groesseText + letzteText + empfehlungText;
        _komponentenspeicherStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var lohntSich = info.BereinigungEmpfohlen == true || (info.WiederverwendbarePakete ?? 0) > 0;
        _bereinigenButton.Visibility = lohntSich ? Visibility.Visible : Visibility.Collapsed;
        _bereinigenResetBaseButton.Visibility = lohntSich ? Visibility.Visible : Visibility.Collapsed;

        _analysierenButton.IsEnabled = true;
    }

    private async void KomponentenspeicherBereinigenAsync(bool resetBase)
    {
        var antwort = resetBase
            ? MessageBox.Show(
                Loc.T(
                    "ResetBase geht weiter als eine normale Bereinigung: danach lässt sich KEIN aktuell " +
                    "installiertes Update mehr deinstallieren, nicht nur alte überflüssige Versionen. " +
                    "Das gibt den meisten Platz frei, ist aber der am wenigsten rückgängig machbare Weg.\n\n" +
                    "Nur fortfahren, wenn das Update-Deinstallieren wirklich egal ist.\n\nJetzt bereinigen?",
                    "ResetBase goes further than a normal cleanup: afterward NO currently installed update " +
                    "can be uninstalled anymore, not just old superseded versions. " +
                    "This frees the most space, but is the least reversible option.\n\n" +
                    "Only continue if being unable to uninstall updates truly doesn't matter to you.\n\nClean up now?"),
                Loc.T("Komponentenspeicher bereinigen (ResetBase)", "Clean up component store (ResetBase)"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(
                Loc.T(
                    "Alte, durch Updates überflüssig gewordene Dateiversionen werden entfernt. " +
                    "Danach lassen sich genau diese Updates nicht mehr deinstallieren - der aktuelle " +
                    "Windows-Zustand selbst ist davon nicht betroffen.\n\nJetzt bereinigen?",
                    "Old file versions made superseded by updates are removed. " +
                    "Afterward, exactly those updates can no longer be uninstalled - the current " +
                    "Windows state itself is not affected.\n\nClean up now?"),
                Loc.T("Komponentenspeicher bereinigen", "Clean up component store"),
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        _analysierenButton.IsEnabled = false;
        _bereinigenButton.IsEnabled = false;
        _bereinigenResetBaseButton.IsEnabled = false;
        _komponentenspeicherMeldung.Text = Loc.T("Bereinige … das kann einige Minuten dauern.", "Cleaning up… this can take a few minutes.");
        _komponentenspeicherMeldung.Foreground = (Brush)FindResource("TextMutedBrush");

        var (erfolg, roh) = await _komponentenspeicherService.BereinigenAsync(resetBase,
            zeile => Dispatcher.Invoke(() => _komponentenspeicherMeldung.Text = zeile));

        _komponentenspeicherMeldung.Text = erfolg
            ? Loc.T("✔ Bereinigung abgeschlossen.", "✔ Cleanup completed.")
            : Loc.T($"✘ Bereinigung fehlgeschlagen: {roh}", $"✘ Cleanup failed: {roh}");
        _komponentenspeicherMeldung.Foreground = erfolg ? (Brush)FindResource("OkBrush") : (Brush)FindResource("DangerBrush");

        _bereinigenButton.IsEnabled = true;
        _bereinigenResetBaseButton.IsEnabled = true;

        // Direkt neu analysieren, damit die neue (kleinere) Größe als konkreter Beleg zu sehen
        // ist, statt nur der Meldung "erfolgreich" zu vertrauen.
        if (erfolg) await KomponentenspeicherAnalysierenAsync();
    }

    /// <summary>
    /// Update-Komponenten zurücksetzen (Pro) - ein anderes Problem als das Aufräumen oben
    /// (Speicherplatz vs. ein haengender/festgefahrener Update-Cache), s.
    /// <see cref="UpdateReparaturService"/>. Bewusst als eigener Abschnitt statt in denselben
    /// Bereich gemischt, damit klar bleibt, welche Aktion welches Problem loest.
    /// </summary>
    private void BauUpdateReparaturBereich()
    {
        var titel = new TextBlock
        {
            Text = Loc.T("Update-Komponenten zurücksetzen (Pro)", "Reset update components (Pro)"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 24, 0, 6)
        };
        var erklaerung = new TextBlock
        {
            Text = Loc.T(
                "Ein anderes Problem als das Aufräumen oben: Windows lädt Updates in den Ordner " +
                "SoftwareDistribution und merkt sich Zertifikatsdaten in catroot2 - wird dieser " +
                "Cache inkonsistent (abgebrochener Download, fehlerhafte Signaturprüfung), bleibt " +
                "Windows Update oft dauerhaft hängen, ohne verständliche Fehlermeldung. Die " +
                "Update-Dienste werden kurz gestoppt, der Cache umbenannt (nicht gelöscht - als " +
                "Backup-Ordner erhalten) und die Dienste neu gestartet. Windows baut beide Ordner " +
                "danach automatisch neu auf.",
                "A different problem than the cleanup above: Windows downloads updates into the " +
                "SoftwareDistribution folder and keeps certificate data in catroot2 - if this " +
                "cache becomes inconsistent (interrupted download, failed signature check), " +
                "Windows Update often gets permanently stuck with no understandable error. The " +
                "update services get stopped briefly, the cache gets renamed (not deleted - kept " +
                "as a backup folder) and the services restart. Windows automatically rebuilds " +
                "both folders afterward."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        };

        _updateResetButton = new Button
        {
            Content = Loc.T("Update-Komponenten zurücksetzen", "Reset update components"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _updateResetButton.Click += (_, _) => UpdateResetAsync(auchDismSfc: false);

        _updateResetSequenzButton = new Button
        {
            Content = Loc.T("Komplette Reparatursequenz (Reset + DISM + SFC)", "Full repair sequence (reset + DISM + SFC)"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _updateResetSequenzButton.Click += (_, _) => UpdateResetAsync(auchDismSfc: true);

        _updateResetMeldung = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 0)
        };

        ProZusatzBereich.Children.Add(titel);
        ProZusatzBereich.Children.Add(erklaerung);
        ProZusatzBereich.Children.Add(_updateResetButton);
        ProZusatzBereich.Children.Add(_updateResetSequenzButton);
        ProZusatzBereich.Children.Add(_updateResetMeldung);
    }

    private async void UpdateResetAsync(bool auchDismSfc)
    {
        var basisText = Loc.T(
            "Die Windows-Update-Dienste werden kurz gestoppt, der Update-Cache umbenannt " +
            "(SoftwareDistribution + catroot2 - als Backup-Ordner erhalten, nicht gelöscht) und " +
            "die Dienste neu gestartet. Während dieser wenigen Sekunden lassen sich keine " +
            "Updates suchen oder installieren.",
            "The Windows Update services get stopped briefly, the update cache gets renamed " +
            "(SoftwareDistribution + catroot2 - kept as backup folders, not deleted) and the " +
            "services restart. During these few seconds, updates can't be searched for or " +
            "installed.");
        var zusatzText = auchDismSfc
            ? Loc.T(
                "\n\nAnschließend läuft direkt die komplette Reparatursequenz weiter: " +
                "Komponentenspeicher wiederherstellen (DISM) und Systemdateien ersetzen (sfc) - " +
                "das dauert danach zusätzlich 10 bis 40 Minuten und braucht eine " +
                "Internetverbindung.",
                "\n\nAfterward, the full repair sequence continues directly: restore the " +
                "component store (DISM) and replace system files (sfc) - this then takes an " +
                "additional 10 to 40 minutes and needs an internet connection.")
            : "";

        var antwort = MessageBox.Show(
            basisText + zusatzText + Loc.T("\n\nJetzt starten?", "\n\nStart now?"),
            Loc.T("Update-Komponenten zurücksetzen", "Reset update components"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        _updateResetButton.IsEnabled = false;
        _updateResetSequenzButton.IsEnabled = false;
        _updateResetMeldung.Text = Loc.T("Wird zurückgesetzt …", "Resetting…");
        _updateResetMeldung.Foreground = (Brush)FindResource("TextMutedBrush");

        var (erfolg, meldung) = await _updateReparaturService.ZuruecksetzenAsync(
            zeile => Dispatcher.Invoke(() => _updateResetMeldung.Text = zeile));

        _updateResetMeldung.Text = (erfolg ? "✔ " : "✘ ") + meldung;
        _updateResetMeldung.Foreground = erfolg ? (Brush)FindResource("OkBrush") : (Brush)FindResource("DangerBrush");

        _updateResetButton.IsEnabled = true;
        _updateResetSequenzButton.IsEnabled = true;

        // Nur weitermachen, wenn das Zuruecksetzen selbst geklappt hat - sonst wuerden DISM/SFC
        // gegen einen Update-Dienst laufen, der eventuell noch nicht sauber neu gestartet ist.
        if (erfolg && auchDismSfc)
        {
            await AusfuehrenAsync(WindowsReparaturService.DismReparieren, WindowsReparaturService.SfcReparieren, reparaturModus: true);
        }
    }
#endif
}
