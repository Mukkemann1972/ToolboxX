namespace PCToolboxCore;

public record MinidumpModul(ulong BasisAdresse, uint Groesse, string Name)
{
    public bool Enthaelt(ulong adresse) => adresse >= BasisAdresse && adresse < BasisAdresse + Groesse;
}

/// <summary>
/// Liest die Modul-Liste (geladene Treiber/DLLs) aus einer Windows-Minidump-Datei (.dmp) -
/// reines Parsen des oeffentlich dokumentierten MINIDUMP-Dateiformats (MDMP-Signatur,
/// Stream-Verzeichnis, MINIDUMP_MODULE_LIST), KEIN Aufruf von dbghelp.dll/Win32-APIs, damit
/// die Logik hier plattformneutral bleibt und auf dem Pi echt getestet werden kann - nur das
/// Einlesen der eigentlichen .dmp-Datei (Windows-spezifischer Pfad) passiert ausserhalb, in
/// PC-Toolbox/Services/DumpReader.cs.
///
/// Bewusst konservativ: jeder einzelne Modul-Eintrag steht in seinem eigenen try/catch, ein
/// kaputter/unerwarteter Eintrag darf nicht die ganze Liste zunichtemachen. Bei irgendeinem
/// grundlegenden Problem (falsche Signatur, kein Stream vom Typ ModuleListStream) liefert
/// Parse() einfach eine leere Liste - der Aufrufer faellt dann auf den bisherigen, ehrlichen
/// "wissen wir nicht"-Text zurueck statt eine falsche Vermutung zu zeigen.
/// </summary>
public static class MinidumpModuleListParser
{
    private const uint SignaturMdmp = 0x504D444D; // "MDMP" als little-endian uint
    private const uint StreamTypModulListe = 4;    // MINIDUMP_STREAM_TYPE.ModuleListStream
    private const int GroesseModulEintrag = 108;   // sizeof(MINIDUMP_MODULE), Microsoft-dokumentiert, fixe Groesse

    public static IReadOnlyList<MinidumpModul> Parse(byte[] dateiBytes)
    {
        var ergebnis = new List<MinidumpModul>();

        try
        {
            if (dateiBytes.Length < 32) return ergebnis;
            if (LiesUInt32(dateiBytes, 0) != SignaturMdmp) return ergebnis;

            var anzahlStreams = LiesUInt32(dateiBytes, 8);
            var streamVerzeichnisRva = LiesUInt32(dateiBytes, 12);

            uint modulListeRva = 0;
            var gefunden = false;
            for (uint i = 0; i < anzahlStreams; i++)
            {
                var eintragOffset = checked((long)streamVerzeichnisRva + i * 12);
                if (eintragOffset + 12 > dateiBytes.Length) break;

                var streamTyp = LiesUInt32(dateiBytes, eintragOffset);
                if (streamTyp == StreamTypModulListe)
                {
                    modulListeRva = LiesUInt32(dateiBytes, eintragOffset + 8);
                    gefunden = true;
                    break;
                }
            }

            if (!gefunden) return ergebnis;
            if (modulListeRva + 4 > dateiBytes.Length) return ergebnis;

            var anzahlModule = LiesUInt32(dateiBytes, modulListeRva);
            var basisOffset = (long)modulListeRva + 4;

            for (uint i = 0; i < anzahlModule; i++)
            {
                try
                {
                    var eintragOffset = basisOffset + i * GroesseModulEintrag;
                    if (eintragOffset + GroesseModulEintrag > dateiBytes.Length) break;

                    var basisAdresse = LiesUInt64(dateiBytes, eintragOffset);
                    var groesse = LiesUInt32(dateiBytes, eintragOffset + 8);
                    var nameRva = LiesUInt32(dateiBytes, eintragOffset + 20);

                    var name = LiesMinidumpString(dateiBytes, nameRva);
                    if (name is not null)
                        ergebnis.Add(new MinidumpModul(basisAdresse, groesse, LetzterPfadteil(name)));
                }
                catch
                {
                    // ein einzelner kaputter Eintrag soll nicht die restliche Liste kosten
                }
            }
        }
        catch
        {
            return new List<MinidumpModul>();
        }

        return ergebnis;
    }

    /// <summary>
    /// Sucht unter den übergebenen Adress-Kandidaten (z.B. die vier Bugcheck-Parameter) die
    /// erste, die in den Adressbereich eines geladenen Moduls fällt - das ist derselbe
    /// Best-Effort-Ansatz, den auch andere Minidump-Auswerter nutzen (keine Symbol-Aufloesung,
    /// nur Adressbereich-Zuordnung). Kann danebenliegen, deshalb im UI immer als
    /// "wahrscheinlich" kennzeichnen, nie als sichere Diagnose.
    /// </summary>
    public static string? FindeWahrscheinlichenVerursacher(IReadOnlyList<MinidumpModul> module, IEnumerable<ulong> adressKandidaten)
    {
        foreach (var adresse in adressKandidaten)
        {
            if (adresse == 0) continue;
            var treffer = module.FirstOrDefault(m => m.Enthaelt(adresse));
            if (treffer is not null) return treffer.Name;
        }
        return null;
    }

    private static string LetzterPfadteil(string pfad)
    {
        var trennstelle = pfad.LastIndexOfAny(['\\', '/']);
        return trennstelle >= 0 ? pfad[(trennstelle + 1)..] : pfad;
    }

    private static string? LiesMinidumpString(byte[] daten, uint rva)
    {
        if (rva + 4 > daten.Length) return null;
        var laengeBytes = LiesUInt32(daten, rva);
        var start = (long)rva + 4;
        if (start + laengeBytes > daten.Length || laengeBytes > int.MaxValue) return null;

        return System.Text.Encoding.Unicode.GetString(daten, (int)start, (int)laengeBytes);
    }

    private static uint LiesUInt32(byte[] daten, long offset) =>
        BitConverter.ToUInt32(daten, checked((int)offset));

    private static ulong LiesUInt64(byte[] daten, long offset) =>
        BitConverter.ToUInt64(daten, checked((int)offset));
}
