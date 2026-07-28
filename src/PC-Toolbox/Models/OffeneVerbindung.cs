namespace PCToolbox.Models;

public class OffeneVerbindung
{
    public string Programm { get; set; } = "";
    public int Pid { get; set; }
    public string Protokoll { get; set; } = "";
    public string RemoteAdresse { get; set; } = "";
    public int RemotePort { get; set; }
}
