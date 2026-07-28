namespace PCToolbox.Models;

public class BiosInfo
{
    public string Hersteller { get; set; } = "";
    public string Version { get; set; } = "";
    public string ReleaseDatum { get; set; } = "";
    public string? Fehler { get; set; }
}
