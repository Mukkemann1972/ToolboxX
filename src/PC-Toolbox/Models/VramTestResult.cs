using PCToolboxCore.Models;

namespace PCToolbox.Models;

public class VramTestResult
{
    public AmpelStatus Ampel { get; set; } = AmpelStatus.Unbekannt;
    public string Hinweis { get; set; } = "";
    public bool ToolGefunden { get; set; }
    public VramTestParseResult? Details { get; set; }

    /// <summary>Höchste während des Tests gemessene GPU-Temperatur (null = keine Temperaturdaten
    /// verfügbar, z.B. PawnIO nicht installiert) - s. VramThermalAuswertung.</summary>
    public double? SpitzentemperaturCelsius { get; set; }
}
