using System.Windows.Controls;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class TemperatureView : UserControl
{
    private readonly SmartService _smartService = new();
    private readonly TemperatureService _tempService = new();
    private readonly HardwareMonitorService _hardwareMonitor = new();
    private readonly TemperaturUeberwachungService _ueberwachungDienst = new();
    private readonly List<TemperaturMessung> _messungen = [];
    private List<DiskInfo> _disks = [];

    public TemperatureView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Temperaturen", "Temperatures");
        QuelleHinweis.ToolTip = Loc.T(
            "Ohne PawnIO: ACPI-Thermalzone (MSAcpi_ThermalZoneTemperature) - liefert auf vielen Mainboards keine Werte. Mit PawnIO: echte Sensorwerte über LibreHardwareMonitorLib.",
            "Without PawnIO: ACPI thermal zone (MSAcpi_ThermalZoneTemperature) - returns no values on many mainboards. With PawnIO: real sensor values via LibreHardwareMonitorLib.");
        InstallierenErklaerung.Text = Loc.T(
            "Für echte CPU/GPU-Werte statt Näherungswerten: der offizielle PawnIO-Treiber liegt bereits bei - startet einen ganz normalen Installationsvorgang mit eigener Bestätigung, nichts läuft im Hintergrund.",
            "For real CPU/GPU values instead of estimates: the official PawnIO driver is already included - it starts a completely normal installation with its own confirmation, nothing runs in the background.");
        PawnIoInstallierenButton.Content = Loc.T("PawnIO installieren", "Install PawnIO");

        UeberwachungBereich.Header = Loc.T("Verlauf & Warnung", "History & alerting");
        UeberwachungErklaerung.Text = Loc.T(
            "Die Liste oben ist eine Momentaufnahme - ob z.B. während eines Belastungstests die Kühlung " +
            "mithält, sieht man erst über Zeit. Hier wird alle 5 Sekunden gemessen und protokolliert, solange " +
            "dieses Modul offen bleibt, und gemeldet, wenn ein Bauteil über den Grenzwert steigt.",
            "The list above is a snapshot - whether cooling keeps up during e.g. a stress test can only be " +
            "seen over time. Here it measures and logs every 5 seconds for as long as this module stays open, " +
            "and reports whenever a component rises above the threshold.");
        GrenzwertLabel.Text = Loc.T("Grenzwert (°C):", "Threshold (°C):");
        UeberwachungButton.Content = Loc.T("Überwachung starten", "Start monitoring");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(TemperatureView), ex.ToString());
                System.Windows.MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "TemperatureView – Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };
        Unloaded += (_, _) => _ueberwachungDienst.Stoppen();
    }

    private async Task LadeAsync()
    {
        InstallierenPanel.Visibility = !_hardwareMonitor.IstPawnIoInstalliert() && _hardwareMonitor.InstallerVorhanden()
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        _disks = await _smartService.GetDisksAsync();
        var readings = await _tempService.GetTemperaturenAsync(_disks);

        Liste.ItemsSource = readings.Select(r => new TempAnzeige
        {
            Titel = $"{r.Ampel.Symbol()} {r.Bauteil}: " +
                    (r.Celsius.HasValue ? r.Celsius + "°C" : Loc.T("nicht verfügbar", "not available")),
            Hinweis = r.Hinweis
        }).ToList();
    }

    private void PawnIoInstallieren_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        PawnIoInstallierenButton.IsEnabled = false;
        var gestartet = _hardwareMonitor.InstallerStarten();
        InstallierenMeldung.Text = gestartet
            ? Loc.T("Installer gestartet - folge dem Installationsdialog, danach dieses Modul neu öffnen.",
                    "Installer started - follow the installation dialog, then reopen this module.")
            : Loc.T("Installer konnte nicht gestartet werden (z.B. UAC abgelehnt).",
                    "Installer could not be started (e.g. UAC declined).");
        PawnIoInstallierenButton.IsEnabled = true;
    }

    private void Ueberwachung_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_ueberwachungDienst.LaeuftGerade)
        {
            _ueberwachungDienst.Stoppen();
            UeberwachungButton.Content = Loc.T("Überwachung starten", "Start monitoring");
            return;
        }

        if (!double.TryParse(GrenzwertFeld.Text, out var grenzwert))
        {
            UeberwachungStatus.Text = Loc.T("Bitte einen gültigen Grenzwert eingeben (z.B. 80).",
                                             "Please enter a valid threshold (e.g. 80).");
            return;
        }

        _messungen.Clear();
        UeberschreitungsListe.ItemsSource = null;
        UeberwachungButton.Content = Loc.T("Überwachung stoppen", "Stop monitoring");
        UeberwachungStatus.Text = Loc.T("Erste Messung folgt in Kürze …", "First measurement coming shortly…");

        _ueberwachungDienst.Starten(TimeSpan.FromSeconds(5), _disks, messungen =>
            Dispatcher.Invoke(() => VerarbeiteMessungen(messungen, grenzwert)));
    }

    private void VerarbeiteMessungen(IReadOnlyList<TemperaturMessung> messungen, double grenzwertCelsius)
    {
        _messungen.AddRange(messungen);
        var auswertung = TemperaturUeberwachung.Auswerten(_messungen, grenzwertCelsius);

        var hoechstwerte = string.Join(", ", auswertung.HoechstwertJeBauteil
            .Select(kv => $"{kv.Key} {kv.Value}°C"));

        UeberwachungStatus.Text = Loc.T(
            $"{auswertung.AnzahlMessungen} Messung(en) seit dem Start. Höchstwerte bisher: {hoechstwerte}.",
            $"{auswertung.AnzahlMessungen} measurement(s) since starting. Peak values so far: {hoechstwerte}.");

        UeberschreitungsListe.ItemsSource = auswertung.UeberschreitungsPerioden
            .Select(p => p.Dauer is { } dauer
                ? Loc.T($"{p.Bauteil}: über {grenzwertCelsius}°C von {p.Start:HH:mm:ss} bis {p.Ende:HH:mm:ss} " +
                        $"({dauer.TotalSeconds:0} Sekunden, Spitze {p.SpitzenwertCelsius}°C)",
                        $"{p.Bauteil}: above {grenzwertCelsius}°C from {p.Start:HH:mm:ss} to {p.Ende:HH:mm:ss} " +
                        $"({dauer.TotalSeconds:0} seconds, peak {p.SpitzenwertCelsius}°C)")
                : Loc.T($"{p.Bauteil}: über {grenzwertCelsius}°C seit {p.Start:HH:mm:ss} - dauert noch an (Spitze {p.SpitzenwertCelsius}°C)",
                        $"{p.Bauteil}: above {grenzwertCelsius}°C since {p.Start:HH:mm:ss} - still ongoing (peak {p.SpitzenwertCelsius}°C)"))
            .Reverse()
            .ToList();
    }

    private class TempAnzeige
    {
        public string Titel { get; set; } = "";
        public string Hinweis { get; set; } = "";
    }
}
