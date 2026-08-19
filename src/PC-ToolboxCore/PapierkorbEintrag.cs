using System.Text;

namespace PCToolboxCore;

/// <summary>Ein Eintrag aus dem Windows-Papierkorb ($I-Datei) - mit Original-Pfad/-Namen, anders
/// als die reine Byte-Signatur-Suche (Dateiwiederherstellung.cs), die nur Inhalte findet, aber
/// nie Namen. Der Papierkorb ist deshalb der schnellste, genaueste erste Versuch bei einer
/// kürzlich gelöschten Datei - solange er nicht geleert wurde.</summary>
public record PapierkorbEintrag(string OriginalPfad, long GroesseBytes, DateTime? GeloeschtAm)
{
    public string OriginalName => Path.GetFileName(OriginalPfad);
}

/// <summary>
/// Liest die $I-Metadatendatei, die Windows für jede in den Papierkorb verschobene Datei anlegt
/// (Gegenstück: die $R-Datei mit dem eigentlichen Inhalt, unverändert - die muss hier nicht
/// geparst werden, nur kopiert). Zwei Formate, je nach Windows-Version:
/// - Version 1 (Vista bis Windows 8.1): fester 520-Byte-Namensblock (260 WCHAR, wie MAX_PATH).
/// - Version 2 (Windows 10 1809 und neuer): 4-Byte-Namenslänge, danach der Name variabler Länge.
/// Unbekannte/zukünftige Versionen werden bewusst ignoriert statt geraten - lieber ehrlich nichts
/// liefern als einen falschen Namen erfinden.
/// </summary>
public static class PapierkorbIDatei
{
    public static PapierkorbEintrag? Lesen(byte[] bytes)
    {
        if (bytes.Length < 24) return null;

        var version = BitConverter.ToInt64(bytes, 0);
        var groesse = BitConverter.ToInt64(bytes, 8);
        var geloeschtFileTime = BitConverter.ToInt64(bytes, 16);

        DateTime? geloeschtAm;
        try
        {
            geloeschtAm = geloeschtFileTime > 0 ? DateTime.FromFileTime(geloeschtFileTime) : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            geloeschtAm = null;
        }

        string? pfad = version switch
        {
            1 when bytes.Length >= 24 + 520 => Utf16NullTerminiert(bytes, 24, 520),
            2 when bytes.Length >= 28 => NameAusVersion2(bytes),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(pfad)) return null;
        return new PapierkorbEintrag(pfad, groesse, geloeschtAm);
    }

    private static string? NameAusVersion2(byte[] bytes)
    {
        var laengeZeichen = BitConverter.ToInt32(bytes, 24);
        var laengeBytes = laengeZeichen * 2;
        if (laengeBytes <= 0 || 28 + laengeBytes > bytes.Length) return null;

        return Utf16NullTerminiert(bytes, 28, laengeBytes);
    }

    private static string Utf16NullTerminiert(byte[] bytes, int start, int laenge)
    {
        var text = Encoding.Unicode.GetString(bytes, start, laenge);
        var nullIdx = text.IndexOf('\0');
        return nullIdx >= 0 ? text[..nullIdx] : text;
    }
}
