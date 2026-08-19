using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class DashboardView : UserControl
{
    private readonly SystemInfoService _service = new();
    private readonly HardwareInfoService _hardware = new();
    private readonly SystemmonitorService _monitorDienst = new();
    private readonly SystemmonitorVerlauf _monitorVerlauf = new(900); // 30 Min. bei 2s-Takt

    // Eigener Timer statt Dauerbetrieb wie bei GpuDiagnoseView's Live-Auslastung: hier wird
    // zusätzlich ein Verlauf für den CSV-Export aufgezeichnet, das soll bewusst nicht schon beim
    // bloßen Öffnen der Seite im Hintergrund starten - der Nutzer entscheidet per Klick.
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    public DashboardView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("System-Übersicht", "System Overview");
        QuelleHinweis.ToolTip = Loc.T(
            "Windows-Systeminformationen (WMI): Win32_OperatingSystem, Win32_Processor, Win32_BaseBoard, Win32_VideoController. Reine Abfrage, nichts wird verändert.",
            "Windows system information (WMI): Win32_OperatingSystem, Win32_Processor, Win32_BaseBoard, Win32_VideoController. Read-only query, nothing is changed.");
        StatusText.Text = Loc.T("Lade...", "Loading...");
        ArbeitsspeicherTitel.Text = Loc.T("Arbeitsspeicher", "Memory");
        LaufwerkeTitel.Text = Loc.T("Laufwerke", "Drives");

        MonitorTitel.Text = Loc.T("Live-Systemmonitor", "Live system monitor");
        MonitorEinleitung.Text = Loc.T(
            "Beobachtet CPU/RAM/Disk/Netzwerk in Echtzeit (alle 2 Sekunden) und zeichnet den Verlauf auf " +
            "- z.B. um ein sporadisches Ruckeln oder eine unerklärliche Auslastung über einige Minuten " +
            "einzufangen. Der Verlauf lässt sich als CSV speichern (Semikolon-getrennt).",
            "Watches CPU/RAM/disk/network in real time (every 2 seconds) and records the history - e.g. to " +
            "catch sporadic stuttering or unexplained load over a few minutes. The history can be saved as " +
            "CSV (semicolon-separated).");
        MonitorStartStopButton.Content = Loc.T("Aufzeichnung starten", "Start recording");
        MonitorCsvButton.Content = Loc.T("Verlauf als CSV speichern …", "Save history as CSV…");
        DiskLabel.Text = Loc.T("Disk", "Disk");
        NetzwerkLabel.Text = Loc.T("Netzwerk", "Network");

        _monitorTimer.Tick += async (_, _) => await MonitorMessenAsync();

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(DashboardView), ex.ToString());
                System.Windows.MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "DashboardView – Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };
        Unloaded += (_, _) => _monitorTimer.Stop();
    }

    private async void MonitorStartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_monitorTimer.IsEnabled)
        {
            _monitorTimer.Stop();
            MonitorStartStopButton.Content = Loc.T("Aufzeichnung starten", "Start recording");
            MonitorStatusText.Text = Loc.T(
                $"Gestoppt. {_monitorVerlauf.Werte.Count} Messpunkt(e) aufgezeichnet.",
                $"Stopped. {_monitorVerlauf.Werte.Count} sample(s) recorded.");
            return;
        }

        MonitorStartStopButton.Content = Loc.T("Aufzeichnung stoppen", "Stop recording");
        MonitorStatusText.Text = "";
        await MonitorMessenAsync();
        _monitorTimer.Start();
    }

    private async Task MonitorMessenAsync()
    {
        var messwert = await _monitorDienst.MessenAsync();
        _monitorVerlauf.Hinzufuegen(messwert);

        CpuBalken.Value = messwert.CpuProzent;
        CpuText.Text = $"{messwert.CpuProzent:0} %";
        RamBalken.Value = messwert.RamProzent;
        RamText.Text = $"{messwert.RamProzent:0} %";
        DiskBalken.Value = messwert.DiskProzent;
        DiskText.Text = $"{messwert.DiskProzent:0} %";
        NetzwerkText.Text = messwert.NetzwerkKbps >= 1024
            ? $"{messwert.NetzwerkKbps / 1024.0:0.#} MB/s"
            : $"{messwert.NetzwerkKbps:0} KB/s";

        MonitorCsvButton.IsEnabled = _monitorVerlauf.Werte.Count > 0;
    }

    private void MonitorCsvExportieren_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = $"ToolboxX-Systemmonitor-{DateTime.Now:yyyy-MM-dd-HHmm}.csv",
            Filter = Loc.T("CSV-Datei (*.csv)|*.csv", "CSV file (*.csv)|*.csv")
        };
        if (dialog.ShowDialog() != true) return;

        File.WriteAllText(dialog.FileName, SystemmonitorCsv.ZuCsv(_monitorVerlauf.Werte));
        MonitorStatusText.Text = Loc.T(
            $"✔ {_monitorVerlauf.Werte.Count} Messpunkt(e) gespeichert.",
            $"✔ {_monitorVerlauf.Werte.Count} sample(s) saved.");
    }

    private async Task LadeAsync()
    {
        var info = await _service.GetSystemInfoAsync();

        if (info.Fehler != null)
        {
            StatusText.Text = $"❌ {info.Fehler}";
            return;
        }

        StatusText.Text = $"{info.ComputerName} · Windows {info.WindowsVersion} (Build {info.WindowsBuild})";

        Inhalt.Children.Clear();
        Inhalt.Children.Add(new TextBlock { Text = Loc.T($"CPU: {info.CpuName} ({info.CpuCores} Kerne, Auslastung {info.CpuLoadPercent}%)",
                                                          $"CPU: {info.CpuName} ({info.CpuCores} cores, {info.CpuLoadPercent}% load)") });
        Inhalt.Children.Add(new TextBlock { Text = Loc.T($"Mainboard: {info.MainboardManufacturer} {info.MainboardModel}",
                                                          $"Motherboard: {info.MainboardManufacturer} {info.MainboardModel}") });
        if (info.GpuName != null)
            Inhalt.Children.Add(new TextBlock { Text = $"GPU: {info.GpuName}" });

        // Der Arbeitsspeicher steht bewusst NICHT mehr hier oben: Er bekommt weiter unten
        // seinen eigenen Abschnitt mit den einzelnen Riegeln. Zweimal dasselbe (oben "RAM",
        // unten "Arbeitsspeicher") liest sich wie ein Fehler - die Belegung gehört dorthin,
        // wo auch die Riegel stehen.
        await HardwareLadenAsync(info.RamFreeGb, info.RamTotalGb);
    }

    private async Task HardwareLadenAsync(double freiGb, double gesamtGb)
    {
        var riegel = await _hardware.SpeicherAsync();
        var laufwerke = await _hardware.LaufwerkeAsync();

        SpeicherListe.ItemsSource = riegel
            .Select(r => new HardwareZeile(r.Ueberschrift, r.Zeile2, r.Link))
            .ToList();

        // Belegung und Riegel in einer Zeile - das gehört zusammen und stand vorher
        // unnötig doppelt auf der Seite.
        var belegung = Loc.T($"Aktuell {freiGb:0.#} GB frei von {gesamtGb:0.#} GB.",
                              $"Currently {freiGb:0.#} GB free of {gesamtGb:0.#} GB.");

        SpeicherZusammenfassung.Text = riegel.Count == 0
            ? belegung + Loc.T(" Zu den einzelnen Riegeln gibt dieser PC keine Auskunft " +
                                "(kommt bei Notebooks und virtuellen Maschinen vor).",
                                " This PC doesn't report details about individual memory modules " +
                                "(common on notebooks and virtual machines).")
            : $"{belegung}  ·  " + Loc.T($"{riegel.Count} Riegel belegt, zusammen {riegel.Sum(r => r.GroesseGb):0.#} GB verbaut.",
                                        $"{riegel.Count} module(s) installed, {riegel.Sum(r => r.GroesseGb):0.#} GB total.");

        LaufwerksListe.ItemsSource = laufwerke
            .Select(l => new HardwareZeile(l.Ueberschrift, l.Zeile2, l.Link))
            .ToList();
    }

    private void Hersteller_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string adresse } || string.IsNullOrWhiteSpace(adresse)) return;

        try
        {
            Process.Start(new ProcessStartInfo(adresse) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(Loc.T($"Die Seite konnte nicht geöffnet werden:\n{adresse}", $"Couldn't open the page:\n{adresse}"),
                Loc.T("Hinweis", "Note"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

/// <summary>Eine Hardware-Zeile mit optionalem Hersteller-Link.</summary>
public class HardwareZeile
{
    public HardwareZeile(string ueberschrift, string zeile2, (string Anzeige, string Adresse)? link)
    {
        Ueberschrift = ueberschrift;
        Zeile2 = zeile2;
        LinkText = link is { } l ? $"{l.Anzeige} ↗" : "";
        LinkAdresse = link?.Adresse ?? "";
    }

    public string Ueberschrift { get; }
    public string Zeile2 { get; }
    public string LinkText { get; }
    public string LinkAdresse { get; }

    /// <summary>Kein Link bekannt = kein Knopf. Ein falscher Link wäre schlimmer als keiner.</summary>
    public Visibility LinkSichtbar => string.IsNullOrEmpty(LinkAdresse) ? Visibility.Collapsed : Visibility.Visible;
}
