using System.Text;

namespace PCToolboxCore;

/// <summary>Eine per $FILE_NAME-Attribut gelesene Datei aus einem MFT-Datensatz - "Stufe 2" der
/// Datenrettung (s. Dateiwiederherstellung-Klassenkommentar): anders als die reine Byte-Suche
/// liefert das den ECHTEN Original-Dateinamen, solange der MFT-Datensatz selbst noch nicht durch
/// eine neuere Datei überschrieben wurde - auch wenn der Papierkorb schon geleert ist.
/// ElternRecordNummer erlaubt es (in einer weiteren Stufe), den Ordnerpfad zusammenzusetzen -
/// hier bewusst noch nicht gemacht, s. Klassenkommentar unten.</summary>
public record MftGefundeneDatei(long RecordNummer, long ElternRecordNummer, string Dateiname, long GroesseBytes, bool IstVerzeichnis, bool IstGeloescht);

/// <summary>
/// Liest einen einzelnen MFT-Datensatz (NTFS). Bewusste, dokumentierte Vereinfachung: geht von der
/// Standard-Datensatzgröße 1024 Byte aus, die praktisch jedes moderne NTFS-Volume verwendet - das
/// genaue Feld dafür im Bootsektor (Offset 0x40, vorzeichenbehaftetes Byte, negativ = 2^|wert| Byte)
/// wird nicht ausgewertet. Ebenso wird die Sequenznummer der Eltern-Referenz nicht geprüft (könnte
/// in seltenen Fällen auf einen zwischenzeitlich wiederverwendeten Datensatz zeigen). Ehrliche,
/// dokumentierte Grenzen statt stillschweigend zu raten - passend zur Haltung bei
/// Dateiwiederherstellung.CarvingScanner.
/// </summary>
public static class MftDatensatz
{
    private static readonly byte[] Signatur = "FILE"u8.ToArray();
    private const int DatensatzGroesse = 1024;

    /// <summary>
    /// Hebt die NTFS-"Fixup"-Absicherung auf: beim Schreiben ersetzt NTFS die letzten 2 Byte jedes
    /// 512-Byte-Sektors im Datensatz durch eine Prüfnummer (Update Sequence Number), die echten
    /// Original-Byte werden im Fixup-Array direkt hinter dieser Prüfnummer aufbewahrt. Ohne das
    /// rückgängig zu machen, wären die letzten 2 Byte jedes Sektors falsch - könnte mitten in einem
    /// Dateinamen oder einer Größenangabe liegen und das Ergebnis unbemerkt verfälschen.
    /// </summary>
    public static byte[]? FixupAnwenden(byte[] datensatz)
    {
        if (datensatz.Length < 8 || !datensatz.AsSpan(0, 4).SequenceEqual(Signatur)) return null;

        var usaOffset = BitConverter.ToUInt16(datensatz, 4);
        var usaLaengeWorte = BitConverter.ToUInt16(datensatz, 6); // inkl. der Pruefnummer selbst
        if (usaLaengeWorte < 1) return null;

        var sektorAnzahl = usaLaengeWorte - 1;
        if (usaOffset + usaLaengeWorte * 2 > datensatz.Length) return null;
        if (sektorAnzahl * 512 > datensatz.Length) return null;

        var ergebnis = (byte[])datensatz.Clone();
        for (var i = 0; i < sektorAnzahl; i++)
        {
            var sektorEndeOffset = (i + 1) * 512 - 2;
            var originalWertOffset = usaOffset + 2 + i * 2;
            ergebnis[sektorEndeOffset] = datensatz[originalWertOffset];
            ergebnis[sektorEndeOffset + 1] = datensatz[originalWertOffset + 1];
        }

        return ergebnis;
    }

    /// <summary>Liest einen bereits fixup-bereinigten Datensatz (s. FixupAnwenden). Gibt null
    /// zurück, wenn keine gültige Signatur oder kein brauchbares $FILE_NAME-Attribut gefunden wird
    /// - der häufigste Fall bei längst gelöschten, inzwischen wiederverwendeten Datensätzen.
    ///
    /// Die eigene Record-Nummer wird AUS DEM DATENSATZ SELBST gelesen (Header-Offset 0x2C, seit
    /// Windows XP vorhanden), nicht aus der Position auf der Platte hergeleitet - wichtig, weil bei
    /// einer Ganze-Platte-Suche (MftScanner) ein gefundener, verwaister Datensatz an einer ganz
    /// anderen Stelle liegen kann, als er ursprünglich in der $MFT stand. Nur über die im Datensatz
    /// gespeicherte "echte" Nummer lässt sich später ein Kind-Datensatz korrekt seinem (eventuell an
    /// anderer Stelle gefundenen) Eltern-Datensatz zuordnen.</summary>
    public static MftGefundeneDatei? Lesen(byte[] datensatz)
    {
        if (datensatz.Length < 48 || !datensatz.AsSpan(0, 4).SequenceEqual(Signatur)) return null;

        var flags = BitConverter.ToUInt16(datensatz, 22);
        var istInBenutzung = (flags & 0x1) != 0;
        var istVerzeichnis = (flags & 0x2) != 0;
        var ersteAttributOffset = BitConverter.ToUInt16(datensatz, 20);
        var eigeneRecordNummer = BitConverter.ToUInt32(datensatz, 44);

        var dateiName = FileNameAttributSuchen(datensatz, ersteAttributOffset);
        if (dateiName is null) return null;

        return new MftGefundeneDatei(
            eigeneRecordNummer, dateiName.Value.ElternRecordNummer, dateiName.Value.Name,
            dateiName.Value.GroesseBytes, istVerzeichnis, IstGeloescht: !istInBenutzung);
    }

    /// <summary>Ein Datensatz kann mehrere $FILE_NAME-Attribute haben (z.B. Langname + kurzer
    /// 8.3-DOS-Name) - der DOS-Name (Namespace 2) wird nur als letzter Ausweg genommen, wenn kein
    /// besserer gefunden wird.</summary>
    private static (string Name, long ElternRecordNummer, long GroesseBytes)? FileNameAttributSuchen(byte[] datensatz, int offset)
    {
        (string Name, long ElternRecordNummer, long GroesseBytes, byte Namespace)? bester = null;

        while (offset >= 0 && offset + 8 <= datensatz.Length)
        {
            var typ = BitConverter.ToUInt32(datensatz, offset);
            if (typ == 0xFFFFFFFF) break; // Ende-Markierung

            if (offset + 8 > datensatz.Length) break;
            var laenge = BitConverter.ToUInt32(datensatz, offset + 4);
            if (laenge == 0 || laenge > int.MaxValue || offset + laenge > datensatz.Length) break;

            if (typ == 0x30 && datensatz[offset + 8] == 0) // $FILE_NAME, resident
            {
                var inhaltOffset = offset + BitConverter.ToUInt16(datensatz, offset + 20);
                var kandidat = FileNameInhaltLesen(datensatz, inhaltOffset);
                if (kandidat is { } k && (bester is null || k.Namespace != 2))
                    bester = k;
            }

            offset += (int)laenge;
        }

        return bester is { } b ? (b.Name, b.ElternRecordNummer, b.GroesseBytes) : null;
    }

    private static (string Name, long ElternRecordNummer, long GroesseBytes, byte Namespace)? FileNameInhaltLesen(byte[] datensatz, int offset)
    {
        if (offset < 0 || offset + 0x42 > datensatz.Length) return null;

        var elternReferenz = BitConverter.ToInt64(datensatz, offset + 0x00);
        var elternRecordNummer = elternReferenz & 0x0000FFFFFFFFFFFF; // untere 48 Bit; obere 16 = Sequenznummer, s. Klassenkommentar
        var groesse = BitConverter.ToInt64(datensatz, offset + 0x30);
        var nameLaenge = datensatz[offset + 0x40];
        var nameSpace = datensatz[offset + 0x41];
        var nameOffset = offset + 0x42;
        if (nameLaenge == 0 || nameOffset + nameLaenge * 2 > datensatz.Length) return null;

        var name = Encoding.Unicode.GetString(datensatz, nameOffset, nameLaenge * 2);
        if (string.IsNullOrWhiteSpace(name)) return null;

        return (name, elternRecordNummer, groesse, nameSpace);
    }
}
