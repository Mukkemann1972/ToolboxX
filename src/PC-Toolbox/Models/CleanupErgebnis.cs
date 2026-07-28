namespace PCToolbox.Models;

public class CleanupErgebnis
{
    public long BytesFreigegeben { get; set; }
    public int DateienEntfernt { get; set; }
    public int DateienUebersprungen { get; set; }  // z.B. gerade in Benutzung
    public string Meldung { get; set; } = "";
}
