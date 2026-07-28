namespace PCToolbox.Models;

public class SystemInfo
{
    public string ComputerName { get; set; } = "";
    public string WindowsVersion { get; set; } = "";
    public string WindowsBuild { get; set; } = "";
    public string CpuName { get; set; } = "";
    public int CpuCores { get; set; }
    public double CpuLoadPercent { get; set; }
    public double RamTotalGb { get; set; }
    public double RamFreeGb { get; set; }
    public string MainboardManufacturer { get; set; } = "";
    public string MainboardModel { get; set; } = "";
    public string? GpuName { get; set; }

    public string? Fehler { get; set; }
}
