namespace PCToolboxCore;

/// <summary>
/// Übersetzt die Zahlencodes, die Windows für Hardware-Eigenschaften liefert, in
/// Klartext. Diese Codes stammen aus dem SMBIOS-Standard - dieselbe Quelle, aus der
/// auch das BIOS seine Anzeige speist. Deshalb steht in unserer Liste dasselbe wie
/// im BIOS, nur ohne dass man den PC dafür neu starten muss.
/// </summary>
public static class HardwareKlartext
{
    /// <summary>Speicherart aus dem SMBIOS-Code (Win32_PhysicalMemory.SMBIOSMemoryType).</summary>
    public static string SpeicherTyp(int code) => code switch
    {
        2 => "DRAM",
        17 => "SDRAM",
        18 => "EDO",
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        0 or 1 => Loc.T("unbekannt", "unknown"),
        _ => Loc.T($"Typ-Code {code}", $"Type code {code}")
    };

    /// <summary>Bauform des Riegels (Win32_PhysicalMemory.FormFactor).</summary>
    public static string Bauform(int code) => code switch
    {
        8 => Loc.T("DIMM (Desktop)", "DIMM (desktop)"),
        12 => Loc.T("SO-DIMM (Notebook)", "SO-DIMM (laptop)"),
        13 => "SRIMM",
        _ => ""
    };

    /// <summary>
    /// Art des Laufwerks (Win32_DiskDrive.MediaType/InterfaceType sind oft nichtssagend -
    /// "Fixed hard disk media" steht auch auf jeder SSD). Deshalb wird zusätzlich der
    /// Modellname herangezogen, der in der Praxis viel mehr verrät.
    /// </summary>
    public static string LaufwerksArt(string? mediaType, string? schnittstelle, string? modell)
    {
        var m = (modell ?? "").ToUpperInvariant();
        var s = (schnittstelle ?? "").ToUpperInvariant();

        if (m.Contains("NVME") || s.Contains("NVME")) return Loc.T("NVMe-SSD (gesteckt, sehr schnell)", "NVMe SSD (plugged in, very fast)");
        if (m.Contains("SSD") || m.Contains("SOLID STATE")) return "SSD";
        if (s.Contains("USB")) return Loc.T("Externes Laufwerk (USB)", "External drive (USB)");

        var mt = (mediaType ?? "").ToUpperInvariant();
        if (mt.Contains("REMOVABLE")) return Loc.T("Wechseldatenträger", "Removable media");
        if (mt.Contains("FIXED")) return Loc.T("Festplatte oder SSD", "Hard drive or SSD");

        return string.IsNullOrWhiteSpace(mediaType) ? Loc.T("unbekannt", "unknown") : mediaType!;
    }
}
