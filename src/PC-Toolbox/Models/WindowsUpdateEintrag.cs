namespace PCToolbox.Models;

public class WindowsUpdateEintrag
{
    public DateTime Datum { get; set; }
    public string Titel { get; set; } = "";
    public string? KbNummer { get; set; }
    public string Operation { get; set; } = "";
    public string Ergebnis { get; set; } = "";
    public bool IstFehlgeschlagen { get; set; }
}
