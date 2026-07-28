using PCToolboxCore;

namespace PCToolbox.Models;

public class GpuInfo
{
    public string Name { get; set; } = "";
    public double? VramGb { get; set; }
    public string? TreiberVersion { get; set; }
    public string? PnpDeviceId { get; set; }

    // Grobe Hersteller-Einordnung nur aus Name/PnpDeviceId - reicht fuer die
    // Anzeige (Icon/Hinweis), keine Verhaltensaenderung des VRAM-Tests danach,
    // da memtest_vulkan selbst herstellerunabhaengig ueber Vulkan laeuft.
    public string Hersteller => Name switch
    {
        var n when n.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) => "NVIDIA",
        var n when n.Contains("AMD", StringComparison.OrdinalIgnoreCase) || n.Contains("Radeon", StringComparison.OrdinalIgnoreCase) => "AMD",
        var n when n.Contains("Intel", StringComparison.OrdinalIgnoreCase) => "Intel",
        _ => Loc.T("Unbekannt", "Unknown")
    };
}
