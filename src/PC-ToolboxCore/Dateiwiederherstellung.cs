namespace PCToolboxCore;

public enum DateiTyp { Jpeg, Png, Pdf, Zip, Gif }

/// <summary>Kopf-/Fuss-Bytemuster für eine Dateiart, wie sie File-Carving klassischerweise nutzt
/// (dieselbe Grundtechnik, mit der toolstar®recoveryPRO und vergleichbare Tools arbeiten).
/// MaxGroesseBytes ist eine Notbremse: findet sich innerhalb dieser Grenze kein Fuss, wird der
/// Fund als unsicher (s. GefundeneDatei.Sicher) statt verworfen ausgegeben.</summary>
public record DateiSignatur(DateiTyp Typ, string Endung, byte[] Kopf, byte[] Fuss, long MaxGroesseBytes);

/// <summary>Ein gefundener Kandidat: Position und Länge auf dem durchsuchten Datenträger,
/// bezogen auf den Anfang des Scans (nicht zwingend Anfang der physischen Platte - der Service
/// entscheidet, ab welchem Offset gescannt wird). Sicher=false heisst: der Kopf wurde gefunden,
/// aber kein passender Fuss innerhalb der Größengrenze - entweder eine überschriebene/beschädigte
/// Datei, oder das Ende wurde einfach noch nicht erreicht. Wird trotzdem zurückgegeben statt
/// verworfen (s. CarvingScanner), damit der Nutzer selbst entscheiden kann, statt dass ein
/// möglicher Treffer ihm gar nicht erst gezeigt wird.</summary>
public record GefundeneDatei(DateiTyp Typ, string Endung, long StartOffset, long Laenge, bool Sicher = true);

/// <summary>
/// Reine Signatur-Suche (File-Carving) für die Datenrettung: findet Dateien anhand bekannter
/// Kopf-/Fuss-Bytemuster, unabhängig vom Dateisystem - funktioniert deshalb auch, wenn der
/// Dateisystem-Eintrag weg ist (gelöschte Datei) oder das Dateisystem selbst beschädigt/RAW ist.
///
/// Bewusste Grenzen, ehrlich benannt statt verschwiegen (wie beim Minidump-Reader):
/// - Geht von EINER zusammenhängenden Datei aus (keine Fragment-Erkennung) - bei stark
///   fragmentierten Laufwerken (viele kleine Dateien über Jahre, wenig freier Platz) können
///   Treffer unvollständig oder falsch zusammengesetzt sein.
/// - Pro Dateiart ist immer nur EIN Kandidat gleichzeitig offen - taucht ein zweiter Kopf
///   derselben Art auf, während der erste noch auf seinen Fuss wartet (z.B. ein JPEG-Thumbnail
///   in den EXIF-Daten eines anderen JPEGs), wird das nicht als verschachtelter Fund erkannt.
/// Für den typischen Fall ("ein paar Fotos/Dokumente aus dem Papierkorb oder von einer
/// formatierten Karte") ist das die richtige Abwägung zwischen Einfachheit und Treffgenauigkeit.
/// </summary>
public static class Dateiwiederherstellung
{
    public static readonly IReadOnlyList<DateiSignatur> BekannteSignaturen =
    [
        new(DateiTyp.Jpeg, "jpg", [0xFF, 0xD8, 0xFF], [0xFF, 0xD9], 20L * 1024 * 1024),
        new(DateiTyp.Png, "png",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82], 50L * 1024 * 1024),
        new(DateiTyp.Pdf, "pdf",
            [0x25, 0x50, 0x44, 0x46],
            [0x25, 0x25, 0x45, 0x4F, 0x46], 100L * 1024 * 1024),
        // ZIP - deckt technisch auch DOCX/XLSX/PPTX ab (das sind ZIP-Container), kommt aber als
        // .zip zurueck, nicht mit der urspruenglichen Office-Endung erkannt (dafuer muesste man in
        // den Inhalt reinschauen, nicht nur Kopf/Fuss - bewusst nicht gebaut, siehe Klassenkommentar
        // zu den Grenzen dieser Technik). "End of Central Directory"-Signatur als Fuss.
        new(DateiTyp.Zip, "zip",
            [0x50, 0x4B, 0x03, 0x04],
            [0x50, 0x4B, 0x05, 0x06], 200L * 1024 * 1024),
        // GIF hat nur einen einzigen Trailer-Byte (0x3B) als Ende-Markierung - schwaecher als die
        // anderen Fuss-Muster (kommt als einzelnes Byte staendig zufaellig vor), aber das ist eine
        // Eigenheit des GIF-Formats selbst, kein Bug hier - andere Carving-Tools haben dasselbe
        // Problem. MaxGroesseBytes faengt die schlimmsten Fehltreffer ab.
        new(DateiTyp.Gif, "gif",
            [0x47, 0x49, 0x46, 0x38, 0x39, 0x61],
            [0x3B], 20L * 1024 * 1024),
    ];
}

/// <summary>
/// Verarbeitet einen Datenträger blockweise (s. <see cref="NaechsterBlock"/>), ohne je den ganzen
/// Inhalt im Speicher zu halten - nur ein kleiner Überlapp zwischen zwei Blöcken wird
/// mitgeschleppt, damit ein Kopf/Fuss, der genau auf der Blockgrenze liegt, nicht übersehen wird.
/// Reine Logik, kein Datenträgerzugriff - der bewirkt sich im Service (echtes Lesen von
/// \\.\PhysicalDriveN), diese Klasse bekommt die Bytes nur gereicht.
/// </summary>
public sealed class CarvingScanner
{
    private sealed class Zustand
    {
        public required DateiSignatur Signatur;
        public long? OffenerStart;
        public long KopfSucheAb;
    }

    private readonly List<Zustand> _zustaende;
    private readonly int _ueberlappLaenge;
    private byte[] _ueberlauf = [];
    private long _gesamtGelesen;

    public CarvingScanner(IReadOnlyList<DateiSignatur> signaturen)
    {
        if (signaturen.Count == 0) throw new ArgumentException("Mindestens eine Signatur nötig.", nameof(signaturen));

        _zustaende = signaturen.Select(s => new Zustand { Signatur = s }).ToList();
        _ueberlappLaenge = signaturen.Max(s => Math.Max(s.Kopf.Length, s.Fuss.Length)) - 1;
    }

    /// <summary>Füttert den nächsten Block (in Lesereihenfolge, lückenlos anschließend an den
    /// vorherigen). Gibt alle Dateien zurück, deren Fuss in diesem Aufruf gefunden wurde.</summary>
    public IReadOnlyList<GefundeneDatei> NaechsterBlock(byte[] block)
    {
        var gefunden = new List<GefundeneDatei>();
        var puffer = _ueberlauf.Length == 0 ? block : Kombiniere(_ueberlauf, block);
        var pufferStart = _gesamtGelesen - _ueberlauf.Length;

        foreach (var z in _zustaende)
        {
            // Kein einmaliges "erst Fuss, dann Kopf" - ein neu geöffneter Kandidat kann seinen
            // Fuss (oder gleich noch die nächste Datei ihren Kopf) im selben Puffer haben, vor
            // allem wenn mehrere kleine Dateien direkt hintereinander in einem Block liegen.
            // Solange sich noch etwas tut, weitersuchen.
            bool fortschritt;
            do
            {
                fortschritt = false;

                if (z.OffenerStart is { } start)
                {
                    var fussSucheAbLokal = (int)Math.Max(0, Math.Min(puffer.Length, start - pufferStart + z.Signatur.Kopf.Length));
                    var fussIdx = IndexOf(puffer, z.Signatur.Fuss, fussSucheAbLokal);
                    if (fussIdx >= 0)
                    {
                        var ende = pufferStart + fussIdx + z.Signatur.Fuss.Length;
                        gefunden.Add(new GefundeneDatei(z.Signatur.Typ, z.Signatur.Endung, start, ende - start, Sicher: true));
                        z.OffenerStart = null;
                        z.KopfSucheAb = ende;
                        fortschritt = true;
                    }
                    else if (pufferStart + puffer.Length - start > z.Signatur.MaxGroesseBytes)
                    {
                        // Kein Fuss innerhalb der Groessengrenze - Kopf trotzdem als unsicheren Fund
                        // ausgeben (Sicher: false) statt stillschweigend zu verwerfen: koennte eine
                        // teilweise ueberschriebene/beschaedigte Datei sein, aber auch ein echter
                        // Fund, dessen Fuss durch Fragmentierung woanders liegt. Der Nutzer soll das
                        // selbst sehen und entscheiden koennen, statt dass ein moeglicher Treffer ihm
                        // gar nicht erst gezeigt wird.
                        gefunden.Add(new GefundeneDatei(z.Signatur.Typ, z.Signatur.Endung, start, z.Signatur.MaxGroesseBytes, Sicher: false));
                        z.OffenerStart = null;
                        z.KopfSucheAb = start + z.Signatur.MaxGroesseBytes;
                        fortschritt = true;
                    }
                }

                if (z.OffenerStart is null)
                {
                    var kopfSucheAbLokal = (int)Math.Max(0, Math.Min(puffer.Length, z.KopfSucheAb - pufferStart));
                    var kopfIdx = IndexOf(puffer, z.Signatur.Kopf, kopfSucheAbLokal);
                    if (kopfIdx >= 0)
                    {
                        z.OffenerStart = pufferStart + kopfIdx;
                        fortschritt = true;
                    }
                }
            } while (fortschritt);
        }

        _gesamtGelesen += block.Length;
        _ueberlauf = puffer.Length > _ueberlappLaenge ? puffer[^_ueberlappLaenge..] : puffer;

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
