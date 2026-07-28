using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using PCToolbox.Converters;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class GpuDiagnoseView : UserControl
{
    private readonly GpuDetectionService _erkennung = new();
    private readonly VramTestService _vramTest = new();
    private CancellationTokenSource? _laufenderTest;

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

        var ergebnis = await _vramTest.TestenAsync(dauer, fortschritt, _laufenderTest.Token);

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

    private class GpuZeile
    {
        public string Titel { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
