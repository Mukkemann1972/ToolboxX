namespace PCToolbox.Models;

public class BluescreenEntry
{
    public DateTime Zeitpunkt { get; set; }
    public string BugCheckCode { get; set; } = "";          // z.B. "0x0000001A"
    public string BugCheckName { get; set; } = "";           // z.B. "MEMORY_MANAGEMENT"
    public string? VerursacherTreiber { get; set; }
    public string Loesungsvorschlag { get; set; } = "";
    public string Quelle { get; set; } = "";                 // "Event-Log" oder "Minidump-Datei (nur Zeitstempel)"
}
