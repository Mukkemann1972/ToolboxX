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
    }

    private async Task LadeAsync()
    {
        InstallierenPanel.Visibility = !_hardwareMonitor.IstPawnIoInstalliert() && _hardwareMonitor.InstallerVorhanden()
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        var disks = await _smartService.GetDisksAsync();
        var readings = await _tempService.GetTemperaturenAsync(disks);

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

    private class TempAnzeige
    {
        public string Titel { get; set; } = "";
        public string Hinweis { get; set; } = "";
    }
}
