namespace PCToolboxCore;

/// <summary>
/// Ordnet SMART-Datensaetze dem richtigen physischen Laufwerk zu.
///
/// Hintergrund: SMART-Werte kommen aus <c>root\WMI</c> (MSStorageDriver_FailurePredict*)
/// und sind dort nur ueber einen <c>InstanceName</c> gekennzeichnet, z.B.
///   <c>SCSI\Disk&amp;Ven_ADATA&amp;Prod_SU650\5&amp;10db0b5b&amp;0&amp;020000_0</c>
/// Die physischen Laufwerke kommen aus <c>Win32_DiskDrive</c> und haben eine
/// <c>PNPDeviceID</c>, z.B.
///   <c>SCSI\DISK&amp;VEN_ADATA&amp;PROD_SU650\5&amp;10DB0B5B&amp;0&amp;020000</c>
///
/// Beides ist dieselbe Kennung - nur in anderer Gross-/Kleinschreibung und mit einem
/// angehaengten Zaehler (<c>_0</c>) beim InstanceName. Genau das leistet diese Klasse.
///
/// Warum das wichtig ist: bis 27.07.2026 wurden SMART-Werte schlicht ueber die
/// REIHENFOLGE zugeordnet (erster SMART-Datensatz -> erstes Laufwerk in der Liste).
/// Auf einem PC mit mehreren Laufwerken landet die Ausfallwarnung dadurch am
/// falschen Laufwerk - im Test bekam die System-SSD die Warnung einer ganz anderen
/// Platte angezeigt. Bei "Backup machen, Laufwerk ersetzen" ist das keine
/// Kleinigkeit, sondern eine Falschaussage.
/// </summary>
public static class SmartInstanzZuordnung
{
    /// <summary>
    /// Bringt Instanzname bzw. PNPDeviceID auf eine vergleichbare Form:
    /// Grossbuchstaben, ohne umgebende Leerzeichen, ohne angehaengten
    /// Instanz-Zaehler (<c>_0</c>, <c>_1</c>, ...).
    /// </summary>
    public static string Normalisieren(string? kennung)
    {
        if (string.IsNullOrWhiteSpace(kennung)) return "";

        var wert = kennung.Trim().ToUpperInvariant();

        // Angehaengter Instanz-Zaehler: nur abschneiden, wenn hinter dem letzten
        // Unterstrich ausschliesslich Ziffern stehen. Sonst wuerde z.B. ein
        // Produktname mit Unterstrich beschnitten.
        var strich = wert.LastIndexOf('_');
        if (strich > 0 && strich < wert.Length - 1)
        {
            var rest = wert[(strich + 1)..];
            if (rest.All(char.IsDigit)) wert = wert[..strich];
        }

        return wert;
    }

    /// <summary>
    /// Sucht zu einem SMART-Instanznamen die passende PNPDeviceID heraus.
    /// Gibt <c>null</c> zurueck, wenn es keine eindeutige Entsprechung gibt -
    /// dann bleibt das Laufwerk bewusst ohne SMART-Werte, statt sie zu erraten.
    /// </summary>
    public static string? FindePnpId(string? instanzName, IEnumerable<string> pnpIds)
    {
        var gesucht = Normalisieren(instanzName);
        if (gesucht.Length == 0) return null;

        // 1) Exakte Entsprechung nach Normalisierung - der Normalfall.
        foreach (var pnp in pnpIds)
        {
            if (Normalisieren(pnp) == gesucht) return pnp;
        }

        // 2) Manche Treiber kuerzen den Produktnamen im Instanznamen. Deshalb
        //    zusaetzlich der Vergleich ueber die Instanz-Kennung hinter dem
        //    letzten Backslash - die ist geraetespezifisch (z.B. "5&10DB0B5B&0&020000").
        var gesuchteInstanz = HinterLetztemBackslash(gesucht);
        if (gesuchteInstanz.Length == 0) return null;

        string? treffer = null;
        foreach (var pnp in pnpIds)
        {
            if (HinterLetztemBackslash(Normalisieren(pnp)) != gesuchteInstanz) continue;
            if (treffer != null) return null;   // mehrdeutig -> lieber nichts zuordnen
            treffer = pnp;
        }
        return treffer;
    }

    private static string HinterLetztemBackslash(string wert)
    {
        var pos = wert.LastIndexOf('\\');
        return pos >= 0 && pos < wert.Length - 1 ? wert[(pos + 1)..] : "";
    }
}
