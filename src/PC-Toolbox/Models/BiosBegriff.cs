using PCToolboxCore;

namespace PCToolbox.Models;

public enum BiosKategorie
{
    Boot,
    Sicherheit,
    Leistung,
    Speicher,
    Sonstiges,
}

public class BiosBegriff
{
    public string Name { get; set; } = "";
    public BiosKategorie Kategorie { get; set; }
    public AmpelStatus Risiko { get; set; } = AmpelStatus.Gruen;

    public string WasEsIst { get; set; } = "";
    public string WasPassiertBeiAenderung { get; set; } = "";
    public string WannAendern { get; set; } = "";
    public string FallsProblem { get; set; } = "";  // "was tun", wenn's nach der Änderung nicht mehr geht

    public static string KategorieName(BiosKategorie k) => k switch
    {
        BiosKategorie.Boot => Loc.T("Start/Boot", "Startup/Boot"),
        BiosKategorie.Sicherheit => Loc.T("Sicherheit", "Security"),
        BiosKategorie.Leistung => Loc.T("Leistung", "Performance"),
        BiosKategorie.Speicher => Loc.T("Speicher/Laufwerke", "Storage/Drives"),
        _ => Loc.T("Sonstiges", "Other"),
    };
}
