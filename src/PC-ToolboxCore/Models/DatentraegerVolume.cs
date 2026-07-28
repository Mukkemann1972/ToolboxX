using PCToolboxCore;

namespace PCToolboxCore.Models;

/// <summary>Ein Laufwerk, so wie es das Datenträger-Werkzeug sieht.</summary>
public class DatentraegerVolume
{
    public char Buchstabe { get; set; }
    public string Bezeichnung { get; set; } = "";
    public string Dateisystem { get; set; } = "";
    public long GroesseBytes { get; set; }
    public long FreiBytes { get; set; }

    /// <summary>Wechseldatenträger: USB-Stick, Speicherkarte, externe Platte, Diskette.</summary>
    public bool IstWechseldatentraeger { get; set; }

    /// <summary>Hier läuft Windows selbst - unantastbar.</summary>
    public bool IstSystemlaufwerk { get; set; }

    public long BelegtBytes => Math.Max(0, GroesseBytes - FreiBytes);
}

/// <summary>Ergebnis der Schutzprüfung, bevor irgendetwas verändert wird.</summary>
public record SchutzUrteil(bool Erlaubt, string Begruendung);

/// <summary>
/// Die Leitplanken des Datenträger-Werkzeugs.
///
/// Grundsatz: Was schiefgehen kann, wird gar nicht erst angeboten. Das Systemlaufwerk
/// steht deshalb nicht "ausgegraut mit Warnung" da, sondern lässt sich schlicht nicht
/// auswählen - eine Warnung, die man wegklicken kann, ist keine Sicherung.
/// </summary>
public static class DatentraegerSchutz
{
    public static SchutzUrteil DarfFormatieren(DatentraegerVolume volume)
    {
        if (volume.IstSystemlaufwerk)
            return new SchutzUrteil(false, Loc.T(
                "Auf diesem Laufwerk läuft Windows selbst. Es zu formatieren würde den PC " +
                "unbrauchbar machen - deshalb bietet die Toolbox das gar nicht erst an.",
                "Windows itself runs on this drive. Formatting it would make the PC " +
                "unusable - which is why the toolbox doesn't offer this at all."));

        if (volume.BelegtBytes > 0)
            return new SchutzUrteil(true, Loc.T(
                $"Achtung: Auf diesem Laufwerk liegen bereits Daten " +
                $"({volume.BelegtBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB belegt). " +
                "Beim Formatieren sind sie unwiderruflich weg - das lässt sich durch nichts " +
                "rückgängig machen. Sichere vorher, was du behalten willst.",
                $"Warning: This drive already has data on it " +
                $"({volume.BelegtBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB used). " +
                "Formatting will remove it irrevocably - this cannot be undone by any means. " +
                "Back up anything you want to keep first."));

        return new SchutzUrteil(true, Loc.T(
            "Dieses Laufwerk ist leer. Formatieren ist hier gefahrlos.",
            "This drive is empty. Formatting is safe here."));
    }

    public static SchutzUrteil DarfGroesseAendern(DatentraegerVolume volume)
    {
        if (volume.IstSystemlaufwerk)
            return new SchutzUrteil(false, Loc.T(
                "Die Größe des Windows-Laufwerks ändert die Toolbox nicht - das gehört zu den " +
                "Eingriffen, bei denen ein Fehlschlag den PC nicht mehr starten lässt.",
                "The toolbox does not change the size of the Windows drive - this is one of the " +
                "operations where a failure could leave the PC unable to start."));

        if (!volume.Dateisystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            return new SchutzUrteil(false, Loc.T(
                $"Die Größe lässt sich nur bei NTFS-Laufwerken verlustfrei ändern, dieses ist " +
                $"mit {(string.IsNullOrWhiteSpace(volume.Dateisystem) ? "keinem bekannten Dateisystem" : volume.Dateisystem)} " +
                "formatiert. Bei allen anderen Formaten ginge das nur über Formatieren - und " +
                "damit über den Verlust der Daten.",
                $"The size can only be changed losslessly on NTFS drives, this one is " +
                $"formatted with {(string.IsNullOrWhiteSpace(volume.Dateisystem) ? "no known file system" : volume.Dateisystem)}. " +
                "For all other formats, that would only be possible via formatting - " +
                "meaning data loss."));

        return new SchutzUrteil(true, Loc.T(
            "Die Daten bleiben dabei erhalten - Windows schiebt sie im laufenden Betrieb " +
            "zusammen. Trotzdem gilt: Bei einem Stromausfall mitten im Vorgang kann etwas " +
            "kaputtgehen. Ein Backup vorher ist nie verkehrt.",
            "The data is preserved during this - Windows shifts it around while the system " +
            "keeps running. That said: a power outage in the middle of the process could " +
            "damage something. A backup beforehand is never a bad idea."));
    }

    /// <summary>
    /// Prüft eine gewünschte neue Größe gegen die Grenzen, die Windows meldet.
    /// </summary>
    public static SchutzUrteil GroessePruefen(long gewuenschtBytes, long minBytes, long maxBytes)
    {
        if (gewuenschtBytes < minBytes)
            return new SchutzUrteil(false, Loc.T(
                $"So klein geht es nicht: Es liegen zu viele Daten darauf. Kleinstmögliche " +
                $"Größe ist {minBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB.",
                $"It can't be that small: there's too much data on it. The smallest possible " +
                $"size is {minBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB."));

        if (gewuenschtBytes > maxBytes)
            return new SchutzUrteil(false, Loc.T(
                $"So groß geht es nicht: Dahinter ist kein freier Platz mehr. Größtmögliche " +
                $"Größe ist {maxBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB.",
                $"It can't be that large: there's no free space beyond it. The largest possible " +
                $"size is {maxBytes / (double)DateisystemRegeln.Gigabyte:0.#} GB."));

        return new SchutzUrteil(true, Loc.T("Diese Größe ist möglich.", "This size is possible."));
    }
}
