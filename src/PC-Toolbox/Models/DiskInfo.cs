namespace PCToolbox.Models;

public class DiskInfo
{
    public string DeviceId { get; set; } = "";          // z.B. "C:" oder "\\.\PHYSICALDRIVE0"
    public string Model { get; set; } = "";
    public string MediaType { get; set; } = "";          // SSD / HDD / NVMe / USB

    // Kennung des physischen Laufwerks, auf dem dieser Laufwerksbuchstabe liegt.
    // Erst damit lassen sich Modellname und SMART-Werte dem RICHTIGEN Laufwerk
    // zuordnen statt der Reihenfolge nach (s. SmartService / SmartInstanzZuordnung).
    // Leer, wenn es kein physisches Laufwerk dahinter gibt - z.B. bei virtuellen
    // oder Netzlaufwerken wie einem eingebundenen Cloud-Speicher.
    public string PnpDeviceId { get; set; } = "";
    public string PhysischesLaufwerk { get; set; } = "";  // z.B. "\\.\PHYSICALDRIVE2"
    public double SizeGb { get; set; }
    public double FreeGb { get; set; }
    public double FreePercent => SizeGb > 0 ? FreeGb / SizeGb * 100 : 0;

    public AmpelStatus SpeicherplatzAmpel { get; set; } = AmpelStatus.Unbekannt;

    // SMART
    public bool SmartVerfuegbar { get; set; }
    public bool SmartVorausfallStatus { get; set; }       // true = Hersteller meldet drohenden Ausfall
    public long? ReallocatedSectors { get; set; }
    public long? CurrentPendingSectors { get; set; }
    public double? TemperaturCelsius { get; set; }
    public long? PowerOnHours { get; set; }

    public AmpelStatus SmartAmpel { get; set; } = AmpelStatus.Unbekannt;
    public string Erklaerung { get; set; } = "";           // Lösungsvorschlag falls Gelb/Rot
}
