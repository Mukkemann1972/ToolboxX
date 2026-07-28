using System.Windows.Controls;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class DiskCheckView : UserControl
{
    private readonly SmartService _service = new();

    public DiskCheckView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Laufwerke", "Drives");
        QuelleHinweis.ToolTip = Loc.T(
            "Speicherplatz: WMI Win32_LogicalDisk. SMART-Werte: root\\WMI MSStorageDriver_FailurePredictStatus/-Data (braucht Administrator-Rechte, sonst nur die Größenanzeige).",
            "Disk space: WMI Win32_LogicalDisk. SMART values: root\\WMI MSStorageDriver_FailurePredictStatus/-Data (needs Administrator rights, otherwise only the size is shown).");

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeAsync();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(DiskCheckView), ex.ToString());
                System.Windows.MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "DiskCheckView – Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };
    }

    private async Task LadeAsync()
    {
        var disks = await _service.GetDisksAsync();
        Liste.ItemsSource = disks.Select(ZuAnzeigeZeile).ToList();
    }

    private static DiskAnzeige ZuAnzeigeZeile(DiskInfo d) => new()
    {
        Titel = $"{d.SpeicherplatzAmpel.Symbol()} {d.DeviceId}  {d.Model}".Trim(),
        SpeicherZeile = Loc.T($"{d.FreeGb:0.#} GB frei von {d.SizeGb:0.#} GB ({d.FreePercent:0}%)",
                              $"{d.FreeGb:0.#} GB free of {d.SizeGb:0.#} GB ({d.FreePercent:0}%)"),
        SmartZeile = d.SmartVerfuegbar
            ? $"{d.SmartAmpel.Symbol()} " + Loc.T(
                  $"SMART: Reallocated={d.ReallocatedSectors ?? 0}, Pending={d.CurrentPendingSectors ?? 0}, " +
                  $"Temp={(d.TemperaturCelsius.HasValue ? d.TemperaturCelsius + "°C" : "n/a")}, Betriebsstunden={d.PowerOnHours ?? 0}. {d.Erklaerung}",
                  $"SMART: Reallocated={d.ReallocatedSectors ?? 0}, Pending={d.CurrentPendingSectors ?? 0}, " +
                  $"Temp={(d.TemperaturCelsius.HasValue ? d.TemperaturCelsius + "°C" : "n/a")}, Power-on hours={d.PowerOnHours ?? 0}. {d.Erklaerung}")
            : $"❔ {d.Erklaerung}"
    };

    private class DiskAnzeige
    {
        public string Titel { get; set; } = "";
        public string SpeicherZeile { get; set; } = "";
        public string SmartZeile { get; set; } = "";
    }
}
