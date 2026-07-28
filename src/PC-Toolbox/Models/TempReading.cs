namespace PCToolbox.Models;

public class TempReading
{
    public string Bauteil { get; set; } = "";     // "CPU", "GPU", "Laufwerk: C:"
    public double? Celsius { get; set; }           // null = nicht verfügbar auf diesem System
    public AmpelStatus Ampel { get; set; } = AmpelStatus.Unbekannt;
    public string Hinweis { get; set; } = "";
}
