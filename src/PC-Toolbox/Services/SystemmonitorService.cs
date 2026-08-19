using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Live-Systemmonitor (Free, kein Admin nötig): CPU/RAM/Disk/Netzwerk über WMI-Perf-Formatted-
/// Klassen. Live gegen diesen Rechner geprüft, zwei echte Fallstricke dabei gefunden:
/// (1) Win32_PerfFormattedData_PerfDisk_PhysicalDisk hat pro Laufwerksbuchstaben EINE Instanz plus
///     eine "_Total"-Instanz - ohne den WHERE-Filter auf "_Total" bekäme man alle Laufwerke
///     gemischt zurück, PercentDiskTime kann laut Microsoft-Dokumentation bei mehreren
///     gleichzeitigen Warteschlangen auch über 100 liegen, deshalb hier für die Anzeige gedeckelt.
/// (2) Win32_PerfFormattedData_Tcpip_NetworkInterface hat, ANDERS als die Disk-Klasse, KEINE
///     "_Total"-Instanz - live bestätigt (zwei echte Adapter, kein Summeneintrag) - deshalb wird
///     hier über alle Adapter-Instanzen selbst aufsummiert.
/// </summary>
public class SystemmonitorService
{
    public Task<SystemmonitorMesswert> MessenAsync() => Task.Run(() => new SystemmonitorMesswert(
        DateTime.Now, CpuProzent(), RamProzent(), DiskProzent(), NetzwerkKbps()));

    private static double CpuProzent()
    {
        using var suche = new ManagementObjectSearcher(
            "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
        foreach (ManagementObject mo in suche.Get())
            return Convert.ToDouble(mo["PercentProcessorTime"]);
        return 0;
    }

    private static double RamProzent()
    {
        using var suche = new ManagementObjectSearcher(
            "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
        foreach (ManagementObject mo in suche.Get())
        {
            var frei = Convert.ToDouble(mo["FreePhysicalMemory"]);
            var gesamt = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
            return gesamt <= 0 ? 0 : Math.Clamp((gesamt - frei) * 100.0 / gesamt, 0, 100);
        }
        return 0;
    }

    private static double DiskProzent()
    {
        using var suche = new ManagementObjectSearcher(
            "SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'");
        foreach (ManagementObject mo in suche.Get())
            return Math.Clamp(Convert.ToDouble(mo["PercentDiskTime"]), 0, 100);
        return 0;
    }

    private static double NetzwerkKbps()
    {
        using var suche = new ManagementObjectSearcher(
            "SELECT BytesTotalPersec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface");
        double summeBytes = 0;
        foreach (ManagementObject mo in suche.Get())
            summeBytes += Convert.ToDouble(mo["BytesTotalPersec"]);
        return summeBytes / 1024.0;
    }
}
