namespace PCToolbox.Models;

public enum UninstallPhase
{
    Gesichert,
    Ausgefuehrt,
    Verifiziert,
    Fehlgeschlagen,
}

public class UninstallResult
{
    public bool Erfolg { get; set; }
    public UninstallPhase Phase { get; set; }
    public string Meldung { get; set; } = "";
    public string? SnapshotOrdner { get; set; }
    public List<LeftoverItem> Leftovers { get; set; } = new();

    public AmpelStatus Ampel => !Erfolg ? AmpelStatus.Rot
        : Leftovers.Count > 0 ? AmpelStatus.Gelb
        : AmpelStatus.Gruen;
}
