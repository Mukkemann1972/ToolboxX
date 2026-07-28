namespace PCToolbox.Models;

public class InstalledProgram
{
    public string DisplayName { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string DisplayVersion { get; set; } = "";
    public string InstallDateRoh { get; set; } = "";       // Registry-Rohformat "yyyyMMdd"
    public long? EstimatedSizeKb { get; set; }
    public string UninstallString { get; set; } = "";
    public string QuietUninstallString { get; set; } = "";
    public string InstallLocation { get; set; } = "";
    public string RegistryKeyPfad { get; set; } = "";       // voller Pfad inkl. Hive, fuer Snapshot/Export
    public bool SystemKomponente { get; set; }

    public AmpelStatus Ampel { get; set; } = AmpelStatus.Gruen;
    public string Erklaerung { get; set; } = "";            // "was tun"-Text, RetroTrax-HelpPanel-Prinzip

    public bool KannDeinstallieren => !string.IsNullOrWhiteSpace(UninstallString);
}
