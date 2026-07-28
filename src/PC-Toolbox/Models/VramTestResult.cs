using PCToolboxCore.Models;

namespace PCToolbox.Models;

public class VramTestResult
{
    public AmpelStatus Ampel { get; set; } = AmpelStatus.Unbekannt;
    public string Hinweis { get; set; } = "";
    public bool ToolGefunden { get; set; }
    public VramTestParseResult? Details { get; set; }
}
