namespace PCToolboxCore;

/// <summary>Ergebnis einer Prüfung: geht das, und was muss man dazu wissen?</summary>
public record DateisystemPruefung(bool Moeglich, string Hinweis);

/// <summary>
/// Das Wissen darüber, welches Dateisystem wofür taugt - und wo die Grenzen liegen,
/// über die man sonst erst stolpert, wenn es zu spät ist.
///
/// Der Klassiker: Ein USB-Stick wird mit FAT32 formatiert, und Monate später lässt sich
/// eine 5-GB-Videodatei nicht darauf kopieren. Windows sagt dann nur "Die Datei ist zu
/// groß für das Zieldateisystem" - warum, steht nirgends. Solche Grenzen gehören VOR
/// das Formatieren, nicht danach.
/// </summary>
public static class DateisystemRegeln
{
    public const long Gigabyte = 1024L * 1024 * 1024;

    /// <summary>Windows' eigenes Formatierwerkzeug macht FAT32 nur bis 32 GB.</summary>
    private const long Fat32Grenze = 32 * Gigabyte;

    /// <summary>FAT16 kommt über 4 GB nicht hinaus.</summary>
    private const long FatGrenze = 4 * Gigabyte;

    public static readonly string[] Alle = ["NTFS", "exFAT", "FAT32", "FAT"];

    public static DateisystemPruefung Pruefen(string dateisystem, long groesseBytes)
    {
        switch (dateisystem.ToUpperInvariant())
        {
            case "NTFS":
                return new DateisystemPruefung(true, Loc.T(
                    "Der Standard für Windows-Festplatten: keine praktischen Größengrenzen, " +
                    "Rechteverwaltung, robust. Andere Geräte (Fernseher, Autoradio, ältere " +
                    "Kameras) können damit oft nichts anfangen.",
                    "The standard for Windows hard drives: no practical size limits, " +
                    "permission management, robust. Other devices (TVs, car radios, older " +
                    "cameras) often can't read it."));

            case "EXFAT":
                return new DateisystemPruefung(true, Loc.T(
                    "Die beste Wahl für USB-Sticks, externe Platten und Speicherkarten: keine " +
                    "4-GB-Grenze für einzelne Dateien, und praktisch jedes moderne Gerät " +
                    "(auch Mac) kann es lesen.",
                    "The best choice for USB sticks, external drives, and memory cards: no " +
                    "4 GB limit for individual files, and practically every modern device " +
                    "(including Mac) can read it."));

            case "FAT32":
                return groesseBytes > Fat32Grenze
                    ? new DateisystemPruefung(false, Loc.T(
                        $"Windows formatiert FAT32 nur bis 32 GB - dieser Datenträger hat " +
                        $"{groesseBytes / (double)Gigabyte:0.#} GB. Nimm exFAT, wenn es überall " +
                        "lesbar sein soll, sonst NTFS.",
                        $"Windows only formats FAT32 up to 32 GB - this drive has " +
                        $"{groesseBytes / (double)Gigabyte:0.#} GB. Use exFAT if it needs to be " +
                        "readable everywhere, otherwise NTFS."))
                    : new DateisystemPruefung(true, Loc.T(
                        "Wird wirklich von allem gelesen, auch von sehr alten Geräten. " +
                        "Wichtige Grenze: Eine einzelne Datei darf höchstens 4 GB groß sein - " +
                        "größere Videos passen also nicht drauf, egal wie viel Platz frei ist.",
                        "Truly readable by everything, even very old devices. " +
                        "Important limit: a single file may be at most 4 GB - " +
                        "so larger videos won't fit, no matter how much free space there is."));

            case "FAT":
                return groesseBytes > FatGrenze
                    ? new DateisystemPruefung(false, Loc.T(
                        $"FAT reicht nur bis 4 GB - dieser Datenträger hat " +
                        $"{groesseBytes / (double)Gigabyte:0.#} GB. Für alte Geräte ist FAT32 " +
                        "die richtige Wahl.",
                        $"FAT only goes up to 4 GB - this drive has " +
                        $"{groesseBytes / (double)Gigabyte:0.#} GB. FAT32 is " +
                        "the right choice for old devices."))
                    : new DateisystemPruefung(true, Loc.T(
                        "Das ganz alte Format (FAT12/FAT16). Sinnvoll nur noch für Disketten " +
                        "und sehr alte Geräte, die sonst nichts lesen können.",
                        "The very old format (FAT12/FAT16). Only useful anymore for floppy disks " +
                        "and very old devices that can't read anything else."));

            default:
                return new DateisystemPruefung(false, Loc.T($"Das Format „{dateisystem}\" kennen wir nicht.",
                                                              $"We don't recognize the format \"{dateisystem}\"."));
        }
    }

    /// <summary>
    /// Was wir vorschlagen würden. Bewusst ein Vorschlag und keine Bevormundung -
    /// die Auswahl bleibt offen, hier steht nur, was in den allermeisten Fällen passt.
    /// </summary>
    public static string Empfehlung(long groesseBytes, bool istWechseldatentraeger)
    {
        if (!istWechseldatentraeger) return "NTFS";

        return groesseBytes > Fat32Grenze ? "exFAT" : "FAT32";
    }

    public static string EmpfehlungBegruenden(long groesseBytes, bool istWechseldatentraeger)
    {
        if (!istWechseldatentraeger)
            return Loc.T("Ein fest eingebauter Datenträger ist mit NTFS am besten aufgehoben.",
                         "A permanently installed drive is best off with NTFS.");

        return groesseBytes > Fat32Grenze
            ? Loc.T("Bei einem Wechseldatenträger dieser Größe passt exFAT am besten: überall lesbar " +
                    "und ohne die 4-GB-Grenze für einzelne Dateien.",
                    "For a removable drive of this size, exFAT fits best: readable everywhere " +
                    "and without the 4 GB limit for individual files.")
            : Loc.T("Bei einem kleinen Wechseldatenträger ist FAT32 die verträglichste Wahl - das liest " +
                    "wirklich jedes Gerät.",
                    "For a small removable drive, FAT32 is the most compatible choice - " +
                    "truly every device can read it.");
    }
}
