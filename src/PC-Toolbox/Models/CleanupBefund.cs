namespace PCToolbox.Models;

public enum CleanupKategorie
{
    WindowsTemp,
    Miniaturansichten,
    WindowsUpdateCache,
    WindowsOld,
}

public class CleanupBefund
{
    public CleanupKategorie Kategorie { get; set; }
    public string Titel { get; set; } = "";
    public AmpelStatus Ampel { get; set; } = AmpelStatus.Gruen;

    public string WasEsIst { get; set; } = "";
    public string Erklaerung { get; set; } = "";  // "was tun"

    public long GroesseBytes { get; set; }
    public int DateiAnzahl { get; set; }

    // Die tatsächlichen Ordner, deren UNMITTELBARE Kinder beim Bereinigen entfernt werden.
    public List<string> WurzelPfade { get; set; } = new();

    // "*" = alle Dateien/Unterordner; bei NurDateien=true wird nur nach diesem Muster gefiltert
    // UND es werden keine Unterordner angefasst (Schutz gegen zu breites Löschen, z.B. im
    // gemeinsam genutzten Explorer-Cache-Ordner, wo nur die thumbcache_*.db-Dateien gemeint sind).
    public string Dateimuster { get; set; } = "*";
    public bool NurDateien { get; set; }
}
