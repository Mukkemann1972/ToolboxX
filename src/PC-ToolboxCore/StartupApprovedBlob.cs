namespace PCToolboxCore;

/// <summary>
/// Liest und schreibt die kleinen Binaerdaten, mit denen Windows sich merkt, ob ein
/// Autostart-Eintrag ein- oder ausgeschaltet ist (Registry-Schluessel "StartupApproved").
///
/// Warum genau dieser Weg: Es ist derselbe Mechanismus, den der Windows-Task-Manager
/// beim "Deaktivieren" benutzt. Der eigentliche Eintrag bleibt dabei unangetastet
/// stehen - ausgeschaltet heisst also wirklich nur ausgeschaltet, nicht geloescht.
/// Das passt zum 3-Phasen-Sicherheitsprinzip von Toolbox X: nichts ist endgueltig.
///
/// Aufbau der 12 Bytes:
///   Byte 0   : Zustand (gerades Byte = eingeschaltet, ungerades = ausgeschaltet;
///              Windows schreibt ueblicherweise 0x02 bzw. 0x03)
///   Byte 1-3 : ungenutzt (0)
///   Byte 4-11: Zeitpunkt des Ausschaltens als Windows-Dateizeit, sonst 0
/// </summary>
public static class StartupApprovedBlob
{
    public const int Laenge = 12;

    private const byte Eingeschaltet = 0x02;
    private const byte Ausgeschaltet = 0x03;

    /// <summary>
    /// Fehlender oder unbrauchbarer Eintrag bedeutet "eingeschaltet" - Windows legt den
    /// Wert erst an, wenn zum ersten Mal etwas deaktiviert wird.
    /// </summary>
    public static bool IstAktiviert(byte[]? daten)
    {
        if (daten is null || daten.Length == 0) return true;

        return (daten[0] & 0x01) == 0;
    }

    /// <summary>
    /// Wann wurde der Eintrag ausgeschaltet? Null, wenn er aktiv ist oder Windows
    /// keinen brauchbaren Zeitpunkt hinterlegt hat.
    /// </summary>
    public static DateTime? DeaktiviertAm(byte[]? daten)
    {
        if (daten is null || daten.Length < Laenge) return null;
        if (IstAktiviert(daten)) return null;

        var dateiZeit = BitConverter.ToInt64(daten, 4);
        if (dateiZeit <= 0) return null;

        try
        {
            return DateTime.FromFileTime(dateiZeit);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Unsinniger Zeitstempel - dann lieber gar keine Zeit anzeigen als eine falsche.
            return null;
        }
    }

    public static byte[] Erzeugen(bool aktiviert, DateTime zeitpunkt)
    {
        var daten = new byte[Laenge];
        daten[0] = aktiviert ? Eingeschaltet : Ausgeschaltet;

        if (!aktiviert)
            BitConverter.GetBytes(zeitpunkt.ToFileTime()).CopyTo(daten, 4);

        return daten;
    }

    /// <summary>Hex-Schreibweise fuer die Uebergabe an reg.exe (/t REG_BINARY /d ...).</summary>
    public static string AlsHex(byte[] daten) => Convert.ToHexString(daten).ToLowerInvariant();
}
