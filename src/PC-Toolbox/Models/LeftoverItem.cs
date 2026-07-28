namespace PCToolbox.Models;

public enum LeftoverArt
{
    RegistrySchluessel,
    Ordner,
}

public class LeftoverItem
{
    public LeftoverArt Art { get; set; }
    public string Pfad { get; set; } = "";                 // Registry-Pfad oder Dateisystem-Pfad
    public string Beschreibung { get; set; } = "";          // was ist das
    public string Erklaerung { get; set; } = "";            // "was tun"-Text
    public AmpelStatus Ampel { get; set; } = AmpelStatus.Gelb;

    public bool Entfernt { get; set; }
}
