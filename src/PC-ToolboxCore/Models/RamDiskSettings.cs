namespace PCToolboxCore.Models;

// Reines Einstellungsobjekt fuer eine RAM-Disk. Bewusst simpel gehalten fuer
// V0.1 (Free): eine Disk, ein Laufwerksbuchstabe, eine Groesse - keine
// automatische Ordner-Umleitung (Temp/Browser-Cache) in dieser Version, das
// ist Backlog (s. IDEEN_BACKLOG.md), nicht V1-Scope.
public class RamDiskSettings
{
    public char Laufwerksbuchstabe { get; set; } = 'R';
    public int GroesseMb { get; set; } = 512;
    public bool AlsNtfsFormatieren { get; set; } = true;
}
