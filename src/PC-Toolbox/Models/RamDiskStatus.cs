namespace PCToolbox.Models;

public class RamDiskStatus
{
    public char Laufwerksbuchstabe { get; set; }
    public bool IstAktiv { get; set; }
    public double? GroesseGb { get; set; }
    public double? FreiGb { get; set; }
    public bool ImDiskInstalliert { get; set; }
}
