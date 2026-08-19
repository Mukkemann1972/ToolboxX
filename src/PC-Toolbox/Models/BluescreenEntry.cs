namespace PCToolbox.Models;

public class BluescreenEntry
{
    public DateTime Zeitpunkt { get; set; }
    public string BugCheckCode { get; set; } = "";          // z.B. "0x0000001A"
    public string BugCheckName { get; set; } = "";           // z.B. "MEMORY_MANAGEMENT"
    public string? VerursacherTreiber { get; set; }
    public string Loesungsvorschlag { get; set; } = "";
    public string Quelle { get; set; } = "";                 // "Event-Log" oder "Minidump-Datei (nur Zeitstempel)"

    // True nur bei genau einem synthetischen "Zugriff verweigert"-Eintrag (s. DumpReader): die
    // App selbst bleibt dabei unelevated, es wird nur EINE eng begrenzte, erklaerte Aktion
    // (Minidump-Ordner auflisten) bei Bedarf mit Rechte-Anhebung nachgeholt - kein "ganze App als
    // Administrator neu starten", das waere für Free-Nutzer ein viel zu grosser, unerklaerter Hebel.
    public bool BrauchtErhoehteRechte { get; set; }
}
