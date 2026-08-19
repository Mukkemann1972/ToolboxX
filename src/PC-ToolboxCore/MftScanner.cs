namespace PCToolboxCore;

/// <summary>
/// Durchsucht eine ganze rohe Platte nach MFT-Datensätzen (Signatur "FILE"), unabhängig davon, ob
/// das aktuelle Dateisystem sie noch kennt - genau das macht den Unterschied zur "einfach die
/// aktuelle $MFT auslesen"-Idee: nach einer Schnellformatierung legt Windows eine komplett neue,
/// leere $MFT an, die alten Datensätze bleiben aber oft noch (verwaist) irgendwo auf der Platte
/// liegen, bis sie durch neue Daten überschrieben werden. Diese Ganze-Platte-Suche findet auch
/// die, genau wie GetDataBack & Co. es tun - im Gegensatz zur reinen Byte-Signatur-Suche
/// (CarvingScanner) liefert das echte Original-Dateinamen (s. MftDatensatz).
///
/// Verarbeitet blockweise wie CarvingScanner, mit Überlapp zwischen zwei Blöcken, damit ein
/// Datensatz, der genau auf einer Blockgrenze beginnt, nicht übersehen wird.
/// </summary>
public sealed class MftScanner
{
    private static readonly byte[] Signatur = "FILE"u8.ToArray();
    private const int DatensatzGroesse = 1024;

    private byte[] _ueberlauf = [];
    private long _gesamtGelesen;

    /// <summary>Füttert den nächsten Block (in Lesereihenfolge, lückenlos anschließend an den
    /// vorherigen). Gibt alle in diesem Aufruf vollständig auswertbaren Funde zurück - liegt ein
    /// Datensatz nur teilweise im aktuellen Puffer, wird er beim nächsten Aufruf (mit dem
    /// Überlapp) erneut versucht.</summary>
    public IReadOnlyList<MftGefundeneDatei> NaechsterBlock(byte[] block)
    {
        var gefunden = new List<MftGefundeneDatei>();
        var puffer = _ueberlauf.Length == 0 ? block : Kombiniere(_ueberlauf, block);

        var suchAb = 0;
        while (true)
        {
            var idx = IndexOf(puffer, Signatur, suchAb);
            if (idx < 0) break;

            if (idx + DatensatzGroesse > puffer.Length)
            {
                // Datensatz reicht ueber das Pufferende hinaus - beim naechsten Block (mit
                // Ueberlapp, der genau dafuer sorgt) nochmal von hier aus versuchen.
                break;
            }

            var kandidat = puffer[idx..(idx + DatensatzGroesse)];
            var bereinigt = MftDatensatz.FixupAnwenden(kandidat);
            var eintrag = bereinigt is not null ? MftDatensatz.Lesen(bereinigt) : null;
            if (eintrag is not null) gefunden.Add(eintrag);

            // Direkt hinter dieser Signatur weitersuchen, nicht um eine volle Datensatzgroesse
            // springen - ein ungueltiger/teilweise ueberschriebener Treffer soll keine echten
            // Funde direkt danach verdecken.
            suchAb = idx + Signatur.Length;
        }

        _gesamtGelesen += block.Length;
        var ueberlappLaenge = DatensatzGroesse + Signatur.Length - 1;
        _ueberlauf = puffer.Length > ueberlappLaenge ? puffer[^ueberlappLaenge..] : puffer;

        return gefunden;
    }

    private static byte[] Kombiniere(byte[] a, byte[] b)
    {
        var ergebnis = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, ergebnis, 0, a.Length);
        Buffer.BlockCopy(b, 0, ergebnis, a.Length, b.Length);
        return ergebnis;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0 || start < 0) return -1;

        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var treffer = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { treffer = false; break; }
            }
            if (treffer) return i;
        }
        return -1;
    }
}

/// <summary>
/// Baut aus den (in beliebiger Reihenfolge gefundenen) MFT-Datensätzen so viel Ordnerpfad wie
/// möglich zusammen - läuft von jeder gefundenen Datei aus über ElternRecordNummer nach oben,
/// solange der jeweilige Eltern-Datensatz auch gefunden wurde. Bricht ab (und liefert nur den
/// bis dahin zusammengesetzten Teilpfad), wenn ein Eltern-Datensatz fehlt (z.B. selbst schon
/// überschrieben) oder eine Ringreferenz erkannt wird - ehrlich ein Teilergebnis statt zu raten.
/// </summary>
public static class MftPfadAufloeser
{
    private const int MaxTiefe = 64;

    public static string PfadAufloesen(MftGefundeneDatei datei, IReadOnlyDictionary<long, MftGefundeneDatei> alleNachRecordNummer)
    {
        var teile = new List<string>();
        var besucht = new HashSet<long>();
        var aktuelleElternNummer = datei.ElternRecordNummer;

        for (var tiefe = 0; tiefe < MaxTiefe; tiefe++)
        {
            if (!besucht.Add(aktuelleElternNummer)) break; // Ringreferenz - abbrechen statt endlos zu laufen
            if (!alleNachRecordNummer.TryGetValue(aktuelleElternNummer, out var eltern)) break;
            if (eltern.RecordNummer == eltern.ElternRecordNummer) break; // Wurzelverzeichnis zeigt auf sich selbst

            teile.Insert(0, eltern.Dateiname);
            aktuelleElternNummer = eltern.ElternRecordNummer;
        }

        teile.Add(datei.Dateiname);
        return string.Join('\\', teile);
    }
}
