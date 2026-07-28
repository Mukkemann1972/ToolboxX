using System.Management;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest Basis-Systeminformationen über WMI (Win32_*-Klassen).
/// Läuft ohne Admin-Rechte.
/// </summary>
public class SystemInfoService
{
    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfo();
            try
            {
                using var os = new ManagementObjectSearcher("SELECT Caption, BuildNumber, FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (var o in os.Get())
                {
                    info.WindowsVersion = o["Caption"]?.ToString()?.Trim() ?? "";
                    info.WindowsBuild = o["BuildNumber"]?.ToString() ?? "";
                    var totalKb = Convert.ToDouble(o["TotalVisibleMemorySize"] ?? 0);
                    var freeKb = Convert.ToDouble(o["FreePhysicalMemory"] ?? 0);
                    info.RamTotalGb = Math.Round(totalKb / 1024 / 1024, 1);
                    info.RamFreeGb = Math.Round(freeKb / 1024 / 1024, 1);
                }

                using var cs = new ManagementObjectSearcher("SELECT Name FROM Win32_ComputerSystem");
                foreach (var o in cs.Get())
                    info.ComputerName = o["Name"]?.ToString() ?? "";

                using var cpu = new ManagementObjectSearcher("SELECT Name, NumberOfCores, LoadPercentage FROM Win32_Processor");
                foreach (var o in cpu.Get())
                {
                    info.CpuName = o["Name"]?.ToString()?.Trim() ?? "";
                    info.CpuCores = Convert.ToInt32(o["NumberOfCores"] ?? 0);
                    info.CpuLoadPercent = Convert.ToDouble(o["LoadPercentage"] ?? 0);
                }

                using var board = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (var o in board.Get())
                {
                    info.MainboardManufacturer = o["Manufacturer"]?.ToString()?.Trim() ?? "";
                    info.MainboardModel = o["Product"]?.ToString()?.Trim() ?? "";
                }

                using var gpu = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (var o in gpu.Get())
                {
                    info.GpuName = o["Name"]?.ToString()?.Trim();
                    break; // nur die erste (primäre) GPU
                }
            }
            catch (Exception ex)
            {
                info.Fehler = Loc.T(
                    $"WMI nicht verfügbar: {ex.Message} → Als Administrator starten oder 'Windows Management Instrumentation'-Dienst prüfen (services.msc).",
                    $"WMI not available: {ex.Message} → Start as Administrator or check the 'Windows Management Instrumentation' service (services.msc).");
            }

            return info;
        });
    }
}
