using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using PCToolbox.Converters;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using PCToolbox.Services.Pro;
#endif

namespace PCToolbox.Modules;

public partial class GpuDiagnoseView : UserControl
{
    private readonly GpuDetectionService _erkennung = new();
    private readonly VramTestService _vramTest = new();
    private readonly DirectXDiagnoseService _dxDiagDienst = new();
    private readonly SpeicherdiagnoseService _speicherdiagnoseDienst = new();
    private CancellationTokenSource? _laufenderTest;
    private string _letzterDxDiagPfad = "";

#if PRO_BUILD
    private readonly EigenerVramTestService _eigenerVramTest = new();
    private ComboBox _testmotorAuswahl = null!;
#endif

    // Pollt die Live-Auslastung, waehrend diese Ansicht sichtbar ist. Wird in
    // Unloaded gestoppt - sonst liefe der Timer im Hintergrund weiter, sobald
    // MainWindow zu einem anderen Modul wechselt (neue View-Instanz, alte bleibt
    // sonst als "Zombie" mit laufendem Timer im Speicher).
    private readonly DispatcherTimer _auslastungsTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    public GpuDiagnoseView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("GPU-Diagnose", "GPU Diagnostics");
        QuelleHinweis.ToolTip = Loc.T(
            "Geräteerkennung: WMI Win32_VideoController. VRAM-Test selbst: externes Tool memtest_vulkan (liest/schreibt den Grafikspeicher direkt über Vulkan).",
            "Device detection: WMI Win32_VideoController. The VRAM test itself: external tool memtest_vulkan (reads/writes graphics memory directly via Vulkan).");
        EinleitungText.Text = Loc.T(
            "Testet den Grafikkartenspeicher (VRAM) auf Fehler - unabhängig vom Hersteller (NVIDIA, AMD, Intel). Nützlich bei Bildfehlern, Abstürzen in Spielen oder nach einer Übertaktung.",
            "Tests the graphics card memory (VRAM) for errors - regardless of manufacturer (NVIDIA, AMD, Intel). Useful for graphical glitches, crashes in games, or after overclocking.");
        ErkannteGrafikkartenTitel.Text = Loc.T("Erkannte Grafikkarten", "Detected graphics cards");
        LiveAuslastungTitel.Text = Loc.T("Live-Auslastung (3D)", "Live utilization (3D)");
        VramTestTitel.Text = Loc.T("VRAM-Test", "VRAM test");
        TestdauerLabel.Text = Loc.T("Testdauer:", "Test duration:");
        Dauer1MinItem.Content = Loc.T("1 Minute (schnell)", "1 minute (quick)");
        Dauer5MinItem.Content = Loc.T("5 Minuten", "5 minutes");
        Dauer15MinItem.Content = Loc.T("15 Minuten (gründlich)", "15 minutes (thorough)");
        TestButton.Content = Loc.T("VRAM-Test starten", "Start VRAM test");
        AbbrechenButton.Content = Loc.T("Abbrechen", "Cancel");
        AmpelErklaerungTitel.Text = Loc.T("Was bedeuten die Ampel-Farben?", "What do the traffic-light colors mean?");
        AmpelGruenText.Text = Loc.T(
            "✅ Grün: Keine Fehler im getesteten Zeitraum gefunden - gutes Zeichen, aber kein 100%-Garantieschein (länger testen erhöht die Sicherheit).",
            "✅ Green: No errors found during the tested period - a good sign, but not a 100% guarantee (testing longer increases confidence).");
        AmpelGelbText.Text = Loc.T(
            "⚠️ Gelb: Test war zu kurz für eine Aussage (z.B. sehr große Karte) - Testdauer erhöhen.",
            "⚠️ Yellow: Test was too short for a conclusion (e.g. very large card) - increase the test duration.");
        AmpelRotText.Text = Loc.T(
            "❌ Rot: Speicherfehler oder Absturz gefunden - kann auf defekten VRAM, einen veralteten Treiber oder instabile Übertaktung hindeuten. Details stehen im Ergebnistext.",
            "❌ Red: Memory errors or a crash found - may indicate defective VRAM, an outdated driver, or unstable overclocking. Details are in the result text.");
        TestmotorHinweis.Text = Loc.T(
            "VRAM-Testmotor: memtest_vulkan von GpuZelenograd (zlib-Lizenz, Open Source) - github.com/GpuZelenograd/memtest_vulkan",
            "VRAM test engine: memtest_vulkan by GpuZelenograd (zlib license, open source) - github.com/GpuZelenograd/memtest_vulkan");

        RamDirectXTitel.Text = Loc.T("RAM- & DirectX-Diagnose", "RAM & DirectX diagnostics");
        RamDirectXEinleitung.Text = Loc.T(
            "Zwei weitere Windows-Bordmittel: ein DirectX-Diagnosebericht (Version + Windows' eigene " +
            "Problem-Zusammenfassung zu Anzeige/Sound/Eingabe) und die Windows-Speicherdiagnose (testet " +
            "die Arbeitsspeicher-Riegel selbst, nicht die Grafikkarte - läuft beim nächsten Neustart).",
            "Two more Windows built-in tools: a DirectX diagnostic report (version + Windows' own problem " +
            "summary for display/sound/input) and the Windows Memory Diagnostic (tests the RAM sticks " +
            "themselves, not the graphics card - runs on the next restart).");
        DxDiagButton.Content = Loc.T("DirectX-Bericht erstellen", "Create DirectX report");
        SpeicherdiagnoseButton.Content = Loc.T("RAM-Test planen …", "Schedule RAM test…");
        DxDiagSpeichernButton.Content = Loc.T("Vollständigen Bericht speichern unter …", "Save full report as…");

#if PRO_BUILD
        BauTestmotorBereich();
#endif

        _auslastungsTimer.Tick += async (_, _) => await AuslastungAktualisierenAsync();

        Loaded += async (_, _) =>
        {
            try
            {
                await GpusLadenAsync();
                await AuslastungAktualisierenAsync();
                _auslastungsTimer.Start();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(GpuDiagnoseView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "GpuDiagnoseView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
        Unloaded += (_, _) => _auslastungsTimer.Stop();
    }

    private async Task AuslastungAktualisierenAsync()
    {
        var prozent = await _erkennung.GetLiveAuslastungProzentAsync();
        if (prozent.HasValue)
        {
            AuslastungBalken.Value = prozent.Value;
            AuslastungText.Text = $"{prozent.Value:0} %";
        }
        else
        {
            AuslastungBalken.Value = 0;
            AuslastungText.Text = Loc.T("nicht verfügbar", "not available");
        }
    }

    private async Task GpusLadenAsync()
    {
        var gpus = await _erkennung.GetGpusAsync();
        GpuListe.ItemsSource = gpus.Select(g => new GpuZeile
        {
            Titel = g.Name,
            Details = $"{(g.VramGb.HasValue ? g.VramGb + " GB VRAM" : Loc.T("VRAM unbekannt", "VRAM unknown"))} · {g.Hersteller}" +
                      (string.IsNullOrEmpty(g.TreiberVersion) ? "" : Loc.T($" · Treiber {g.TreiberVersion}", $" · Driver {g.TreiberVersion}"))
        }).ToList();

        if (!_vramTest.ToolVorhanden)
        {
            StatusZeile.Text = Loc.T(
                "⚠️ memtest_vulkan wurde nicht gefunden - \"VRAM-Test starten\" zeigt dir, " +
                "wo du die (kostenlose) Datei herbekommst.",
                "⚠️ memtest_vulkan was not found - \"Start VRAM test\" will show you " +
                "where to get the (free) file.");
        }
    }

    private async void TestStarten_Click(object sender, RoutedEventArgs e)
    {
        var dauer = DauerAuswahl.SelectedItem is ComboBoxItem { Tag: string tagWert } &&
                    int.TryParse(tagWert, out var minuten)
            ? TimeSpan.FromMinutes(minuten)
            : TimeSpan.FromMinutes(1);

        TestButton.IsEnabled = false;
        AbbrechenButton.IsEnabled = true;
        ErgebnisPanel.Visibility = Visibility.Collapsed;
        StatusZeile.Text = Loc.T($"Test läuft ({dauer.TotalMinutes:0} Min.)...", $"Test running ({dauer.TotalMinutes:0} min.)...");

        _laufenderTest = new CancellationTokenSource();
        var fortschritt = new Progress<string>(text => StatusZeile.Text = text);

#if PRO_BUILD
        var ergebnis = _testmotorAuswahl.SelectedIndex == 1
            ? await _eigenerVramTest.TestenAsync(dauer, fortschritt, _laufenderTest.Token)
            : await _vramTest.TestenAsync(dauer, fortschritt, _laufenderTest.Token);
#else
        var ergebnis = await _vramTest.TestenAsync(dauer, fortschritt, _laufenderTest.Token);
#endif

        ErgebnisTitel.Inlines.Clear();
        ErgebnisTitel.Inlines.Add(new InlineUIContainer(AmpelIconFactory.Build(ergebnis.Ampel)) { BaselineAlignment = BaselineAlignment.Center });
        ErgebnisTitel.Inlines.Add(new Run(Loc.T(" Ergebnis", " Result")));
        ErgebnisText.Text = ergebnis.Hinweis;
        ErgebnisPanel.Visibility = Visibility.Visible;
        StatusZeile.Text = "";

        TestButton.IsEnabled = true;
        AbbrechenButton.IsEnabled = false;
        _laufenderTest = null;
    }

    private void Abbrechen_Click(object sender, RoutedEventArgs e) => _laufenderTest?.Cancel();

    private async void DxDiagErstellen_Click(object sender, RoutedEventArgs e)
    {
        DxDiagButton.IsEnabled = false;
        DxDiagSpeichernButton.Visibility = Visibility.Collapsed;
        DxDiagNotizenListe.Children.Clear();
        DxDiagStatusText.Text = Loc.T("Erstelle DirectX-Bericht …", "Creating DirectX report…");

        var (auswertung, pfad) = await _dxDiagDienst.BerichtErstellenAsync();
        _letzterDxDiagPfad = pfad;

        DxDiagButton.IsEnabled = true;

        if (auswertung.Version is null)
        {
            DxDiagStatusText.Text = Loc.T("Bericht konnte nicht ausgewertet werden.", "Report could not be evaluated.");
            return;
        }

        var alleUnauffaellig = auswertung.Notizen.All(n => n.IstUnauffaellig);
        DxDiagStatusText.Text = Loc.T(
            $"{(alleUnauffaellig ? "✔" : "⚠️")} {auswertung.Version} · " +
            $"{(alleUnauffaellig ? "keine Probleme gefunden" : "Windows meldet mögliche Probleme, siehe unten")}",
            $"{(alleUnauffaellig ? "✔" : "⚠️")} {auswertung.Version} · " +
            $"{(alleUnauffaellig ? "no problems found" : "Windows reports possible problems, see below")}");
        DxDiagStatusText.FontWeight = FontWeights.SemiBold;
        DxDiagStatusText.Foreground = (Brush)FindResource(alleUnauffaellig ? "OkBrush" : "WarnBrush");

        foreach (var notiz in auswertung.Notizen.Where(n => !n.IstUnauffaellig))
        {
            DxDiagNotizenListe.Children.Add(new TextBlock
            {
                Text = $"⚠️ {notiz.Bereich}: {notiz.Befund}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("WarnBrush"),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        if (!string.IsNullOrEmpty(pfad))
            DxDiagSpeichernButton.Visibility = Visibility.Visible;
    }

    private void DxDiagSpeichern_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_letzterDxDiagPfad) || !File.Exists(_letzterDxDiagPfad)) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "DirectX-Bericht.txt",
            Filter = Loc.T("Textdatei (*.txt)|*.txt", "Text file (*.txt)|*.txt")
        };
        if (dialog.ShowDialog() == true)
            File.Copy(_letzterDxDiagPfad, dialog.FileName, overwrite: true);
    }

    private void Speicherdiagnose_Click(object sender, RoutedEventArgs e)
    {
        var frage = MessageBox.Show(
            Loc.T(
                "Öffnet Windows' eigene Speicherdiagnose. Dort wählst du \"Jetzt neu starten und nach " +
                "Problemen suchen\" oder \"Beim nächsten Systemstart nach Problemen suchen\" - der Test " +
                "läuft dann vor dem Windows-Start, dauert je nach RAM-Größe mehrere Minuten und der PC " +
                "startet danach automatisch neu in Windows.",
                "Opens Windows' own Memory Diagnostic. There you choose \"Restart now and check for " +
                "problems\" or \"Check for problems the next time I start my computer\" - the test then " +
                "runs before Windows starts, takes several minutes depending on RAM size, and the PC " +
                "automatically restarts into Windows afterward."),
            Loc.T("Windows-Speicherdiagnose öffnen", "Open Windows Memory Diagnostic"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (frage == MessageBoxResult.OK)
            _speicherdiagnoseDienst.Starten();
    }

#if PRO_BUILD
    /// <summary>
    /// Motor-Auswahl zwischen memtest_vulkan (extern, dieselbe Engine wie Free) und dem
    /// selbst geschriebenen Vulkan-Compute-Test (s. EigenerVramTestService,
    /// GPU_DIAGNOSE_VISION.md "Alleinstellungsmerkmal"). Standardmaessig ist der eigene Test
    /// vorausgewaehlt - genau das Feature, das Pro von Free unterscheidet, soll nicht
    /// versteckt in einem zweiten Klick liegen.
    /// </summary>
    private void BauTestmotorBereich()
    {
        var label = new TextBlock
        {
            Text = Loc.T("Testmotor (Pro):", "Test engine (Pro):"),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        _testmotorAuswahl = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        _testmotorAuswahl.Items.Add(new ComboBoxItem { Content = Loc.T("memtest_vulkan (extern, wie Free)", "memtest_vulkan (external, same as Free)") });
        _testmotorAuswahl.Items.Add(new ComboBoxItem { Content = Loc.T("Eigener Test (selbst geschrieben)", "Built-in test (written from scratch)") });
        _testmotorAuswahl.SelectedIndex = 1;

        var hinweis = new TextBlock
        {
            Text = Loc.T(
                "Der eigene Test läuft komplett innerhalb der Toolbox (keine externe .exe), testet echten " +
                "Grafikspeicher direkt über eigene Compute-Shader - kein fremder Code, aber noch jünger als " +
                "memtest_vulkan (weniger Praxiserfahrung). Bei Zweifeln an einem Ergebnis lohnt sich ein " +
                "Gegentest mit der jeweils anderen Engine.",
                "The built-in test runs entirely inside the Toolbox (no external .exe), tests real graphics " +
                "memory directly via its own compute shaders - no third-party code, but younger than " +
                "memtest_vulkan (less real-world track record). If you doubt a result, cross-checking with " +
                "the other engine is worthwhile."),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        };

        ProZusatzBereich.Children.Add(label);
        ProZusatzBereich.Children.Add(_testmotorAuswahl);
        ProZusatzBereich.Children.Add(hinweis);
    }
#endif

    private class GpuZeile
    {
        public string Titel { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
