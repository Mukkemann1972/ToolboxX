using PCToolboxCore.Models;

namespace PCToolboxCore;

// Baut die Kommandozeilen-Argumente fuer imdisk.exe (LTRData/ImDisk, echtes GPL,
// github.com/LTRData/ImDisk) - den Motor hinter dem RAM-Disk-Modul. Bewusst NICHT
// die Text-AUSGABE von "imdisk -l" geparst (undokumentiertes, versionsabhaengiges
// Format, dazu keine verlaessliche Quelle gefunden) - stattdessen nur Exit-Codes
// der Aktions-Kommandos auswerten + den Live-Zustand danach ganz normal ueber
// System.IO.DriveInfo abfragen (echter Laufwerksbuchstabe, von .NET selbst
// zuverlaessig gelesen, kein ImDisk-spezifisches Parsing noetig).
//
// Diese Klasse bleibt bewusst plattformneutral (kein Process-Aufruf hier drin),
// damit die reine Argument-Logik echt getestet werden kann - der tatsaechliche
// Process.Start passiert in PC-Toolbox/Services/RamDiskService.cs (Windows-only).
public static class ImDiskCommandBuilder
{
    // -a          = Add (anlegen)
    // -s <Groesse>M = Groesse in MB
    // -o awe      = AWE-Speicher (kein Auslagern auf Datentraeger noetig)
    // -m <X>:     = Laufwerksbuchstabe
    // -p "..."    = Format-Parameter fuer den anschliessenden Windows-Formatierungsaufruf
    public static string BuildErstellenArgumente(RamDiskSettings einstellungen)
    {
        var buchstabe = char.ToUpperInvariant(einstellungen.Laufwerksbuchstabe);
        var formatParameter = einstellungen.AlsNtfsFormatieren ? "/fs:ntfs /q /y" : "/fs:fat32 /q /y";

        return $"-a -s {einstellungen.GroesseMb}M -o awe -m {buchstabe}: -p \"{formatParameter}\"";
    }

    // -D = Dismount (wieder entfernen)
    public static string BuildEntfernenArgumente(char laufwerksbuchstabe) =>
        $"-D -m {char.ToUpperInvariant(laufwerksbuchstabe)}:";
}
