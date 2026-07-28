namespace PCToolbox.Models;

public class DruckerInfo
{
    public string Name { get; set; } = "";
    public bool IstStandard { get; set; }
    public bool IstNetzwerkdrucker { get; set; }
    public string AnschlussName { get; set; } = "";
    public string TreiberName { get; set; } = "";
    public string BefundText { get; set; } = "";
    public AmpelStatus Ampel { get; set; }
}
