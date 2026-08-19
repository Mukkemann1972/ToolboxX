using System.IO;
using System.IO.Compression;
using PCToolbox.Models;
using PCToolbox.Safety;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Ein einzelner fehlgeschlagener Eintrag: Klartext-Meldung fürs Anzeigen + das rohe
/// Exception-HResult (0 wenn nicht durch eine echte Exception verursacht, z.B. "Ziel existiert
/// schon"). Free ignoriert HResult komplett - Pro nutzt es (s. CommanderView), um bei einer
/// Sharing-Violation die "Datei entsperren"-Hilfe anzubieten, ohne dass diese Klasse selbst
/// irgendetwas Pro-Spezifisches wissen muss.
/// </summary>
public record DateiAktionFehler(string Pfad, string Meldung, int HResult)
{
    public override string ToString() => Meldung;
}

/// <summary>Ergebnis einer Datei-Aktion, in Klartext für die Statuszeile.</summary>
public record DateiAktionErgebnis(int Erledigt, List<DateiAktionFehler> Fehler)
{
    public bool Erfolgreich => Fehler.Count == 0;
}

/// <summary>
/// Die Dateiarbeit hinter dem Commander-Modul.
///
/// Zwei Festlegungen, die das Verhalten vom üblichen Windows-Explorer unterscheiden:
/// 1. <b>Gelöscht wird ausschliesslich in den Papierkorb</b> (dieselbe Sicherung wie im
///    System-Saubermacher). Kein "endgültig löschen"-Weg - wer das wirklich will, kann
///    den Papierkorb selbst leeren.
/// 2. <b>Nie überschreiben.</b> Liegt am Ziel schon etwas gleichnamiges, wird
///    "Name (2).endung" daraus. Eine überschriebene Datei ist unwiderruflich weg, und
///    genau das soll in dieser Toolbox nirgends aus Versehen passieren können.
/// </summary>
public class DateiManagerService
{
    public Task<List<DateiEintrag>> AuflistenAsync(string ordner) => Task.Run(() => Auflisten(ordner));

    private static List<DateiEintrag> Auflisten(string ordner)
    {
        var eintraege = new List<DateiEintrag>();

        if (DateiHelfer.EineEbeneHoeher(ordner) is { } hoeher)
            eintraege.Add(new DateiEintrag
            {
                Name = "..",
                VollerPfad = hoeher,
                IstOrdner = true,
                IstNachOben = true
            });

        var verzeichnis = new DirectoryInfo(ordner);

        var ordnerListe = new List<DateiEintrag>();
        var dateiListe = new List<DateiEintrag>();

        try
        {
            foreach (var unterOrdner in verzeichnis.GetDirectories())
            {
                if (unterOrdner.Attributes.HasFlag(FileAttributes.System)) continue;

                ordnerListe.Add(new DateiEintrag
                {
                    Name = unterOrdner.Name,
                    VollerPfad = unterOrdner.FullName,
                    IstOrdner = true,
                    IstJunction = NtfsBesonderheiten.IstReparsePoint(unterOrdner.Attributes),
                    Geaendert = SicheresDatum(() => unterOrdner.LastWriteTime)
                });
            }

            foreach (var datei in verzeichnis.GetFiles())
                dateiListe.Add(new DateiEintrag
                {
                    Name = datei.Name,
                    VollerPfad = datei.FullName,
                    IstOrdner = false,
                    GroesseBytes = SichereGroesse(datei),
                    Geaendert = SicheresDatum(() => datei.LastWriteTime),
                    HatHerkunftsmarkierung = File.Exists(NtfsBesonderheiten.ZoneIdentifierPfad(datei.FullName))
                });
        }
        catch (Exception e) when (e is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            // Kein Zugriff: Es bleibt bei dem, was bis hierhin eingesammelt wurde
            // (mindestens der ".."-Eintrag, damit man wieder herauskommt).
        }

        // Ordner immer zuerst - so ist es in jedem Dateimanager, und man findet sich sofort zurecht.
        eintraege.AddRange(ordnerListe.OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase));
        eintraege.AddRange(dateiListe.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase));

        return eintraege;
    }

    private static long? SichereGroesse(FileInfo datei)
    {
        try
        {
            return datei.Length;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTime? SicheresDatum(Func<DateTime> lesen)
    {
        try
        {
            return lesen();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static List<string> Laufwerke()
    {
        try
        {
            return DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }

    // ================= Kopieren / Verschieben =================

    public Task<DateiAktionErgebnis> KopierenAsync(IEnumerable<string> quellen, string zielOrdner) =>
        Task.Run(() => Uebertragen(quellen, zielOrdner, verschieben: false));

    public Task<DateiAktionErgebnis> VerschiebenAsync(IEnumerable<string> quellen, string zielOrdner) =>
        Task.Run(() => Uebertragen(quellen, zielOrdner, verschieben: true));

    private static DateiAktionErgebnis Uebertragen(IEnumerable<string> quellen, string zielOrdner, bool verschieben)
    {
        var erledigt = 0;
        var fehler = new List<DateiAktionFehler>();

        foreach (var quelle in quellen)
        {
            var name = Path.GetFileName(quelle.TrimEnd('\\'));

            try
            {
                var istOrdner = Directory.Exists(quelle);

                if (istOrdner && DateiHelfer.ZielLiegtInQuelle(quelle, zielOrdner))
                {
                    fehler.Add(new DateiAktionFehler(quelle, Loc.T($"{name}: Ein Ordner lässt sich nicht in sich selbst kopieren.",
                                     $"{name}: A folder cannot be copied into itself."), 0));
                    continue;
                }

                var ziel = DateiHelfer.FreierZielName(Path.Combine(zielOrdner, name),
                    p => File.Exists(p) || Directory.Exists(p));

                if (istOrdner)
                {
                    OrdnerKopieren(quelle, ziel);
                    if (verschieben) Directory.Delete(quelle, recursive: true);
                }
                else
                {
                    File.Copy(quelle, ziel);
                    if (verschieben) File.Delete(quelle);
                }

                erledigt++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                fehler.Add(new DateiAktionFehler(quelle, $"{name}: {e.Message}", e.HResult));
            }
        }

        return new DateiAktionErgebnis(erledigt, fehler);
    }

    /// <summary>
    /// Ordner werden bewusst selbst durchlaufen und kopiert (statt über einen fertigen
    /// Aufruf), damit ein einzelner gesperrter Unterordner nicht die ganze Aktion abbricht.
    ///
    /// Reparse Points (Junctions/symbolische Links) werden bewusst NICHT verfolgt: ein
    /// Junction kann auf einen eigenen übergeordneten Ordner zeigen, ein naives Hinterherfolgen
    /// würde dann in eine Endlosschleife laufen (s. NtfsBesonderheiten).
    /// </summary>
    private static void OrdnerKopieren(string quelle, string ziel)
    {
        Directory.CreateDirectory(ziel);

        foreach (var datei in Directory.GetFiles(quelle))
            File.Copy(datei, Path.Combine(ziel, Path.GetFileName(datei)), overwrite: false);

        foreach (var unterOrdner in Directory.GetDirectories(quelle))
        {
            if (NtfsBesonderheiten.IstReparsePoint(new DirectoryInfo(unterOrdner).Attributes)) continue;
            OrdnerKopieren(unterOrdner, Path.Combine(ziel, Path.GetFileName(unterOrdner)));
        }
    }

    // ================= Löschen / Umbenennen / Neuer Ordner =================

    public Task<DateiAktionErgebnis> InDenPapierkorbAsync(IEnumerable<string> pfade) => Task.Run(() =>
    {
        var erledigt = 0;
        var fehler = new List<DateiAktionFehler>();

        foreach (var pfad in pfade)
        {
            if (RecycleBinDeleter.InDenPapierkorbVerschieben(pfad)) erledigt++;
            else fehler.Add(new DateiAktionFehler(pfad,
                Loc.T($"{Path.GetFileName(pfad.TrimEnd('\\'))}: konnte nicht in den Papierkorb verschoben werden.",
                      $"{Path.GetFileName(pfad.TrimEnd('\\'))}: could not be moved to the recycle bin."), 0));
        }

        return new DateiAktionErgebnis(erledigt, fehler);
    });

    public (bool Erfolg, string Meldung) Umbenennen(string pfad, string neuerName)
    {
        if (string.IsNullOrWhiteSpace(neuerName))
            return (false, Loc.T("Es wurde kein neuer Name eingegeben.", "No new name was entered."));

        if (neuerName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return (false, Loc.T("Der Name enthält Zeichen, die Windows in Dateinamen nicht erlaubt.",
                                 "The name contains characters that Windows doesn't allow in file names."));

        try
        {
            var ordner = Path.GetDirectoryName(pfad) ?? "";
            var ziel = Path.Combine(ordner, neuerName);

            if (File.Exists(ziel) || Directory.Exists(ziel))
                return (false, Loc.T("Unter diesem Namen gibt es hier schon etwas.", "Something already exists here under this name."));

            if (Directory.Exists(pfad)) Directory.Move(pfad, ziel);
            else File.Move(pfad, ziel);

            return (true, Loc.T($"Umbenannt in „{neuerName}\".", $"Renamed to \"{neuerName}\"."));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return (false, Loc.T($"Umbenennen nicht möglich: {e.Message}", $"Renaming not possible: {e.Message}"));
        }
    }

    public (bool Erfolg, string Meldung) NeuerOrdner(string elternOrdner, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, Loc.T("Es wurde kein Name eingegeben.", "No name was entered."));

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return (false, Loc.T("Der Name enthält Zeichen, die Windows in Ordnernamen nicht erlaubt.",
                                 "The name contains characters that Windows doesn't allow in folder names."));

        try
        {
            var ziel = Path.Combine(elternOrdner, name);

            if (Directory.Exists(ziel)) return (false, Loc.T("Diesen Ordner gibt es hier schon.", "This folder already exists here."));

            Directory.CreateDirectory(ziel);
            return (true, Loc.T($"Ordner „{name}\" angelegt.", $"Folder \"{name}\" created."));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return (false, Loc.T($"Ordner konnte nicht angelegt werden: {e.Message}", $"Folder could not be created: {e.Message}"));
        }
    }

    // ================= ZIP-Archive =================

    /// <summary>Liest nur die Struktur (Pfade + Größen) - die eigentliche Deutung übernimmt <see cref="ZipVerzeichnisListe"/> im Kern.</summary>
    public Task<List<ZipVerzeichnisListe.ZipQuellEintrag>> ZipEintraegeAsync(string zipPfad) => Task.Run(() =>
    {
        using var archiv = ZipFile.OpenRead(zipPfad);
        return archiv.Entries.Select(e => new ZipVerzeichnisListe.ZipQuellEintrag(e.FullName, e.Length)).ToList();
    });

    public Task<(bool Erfolg, string Meldung)> AlsZipErstellenAsync(IReadOnlyList<string> quellen, string zielOrdner, string wunschName) =>
        Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(wunschName))
                return (false, Loc.T("Es wurde kein Name eingegeben.", "No name was entered."));

            var name = wunschName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? wunschName : wunschName + ".zip";

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return (false, Loc.T("Der Name enthält Zeichen, die Windows in Dateinamen nicht erlaubt.",
                                     "The name contains characters that Windows doesn't allow in file names."));

            try
            {
                var zielPfad = DateiHelfer.FreierZielName(Path.Combine(zielOrdner, name),
                    p => File.Exists(p) || Directory.Exists(p));

                using (var archiv = ZipFile.Open(zielPfad, ZipArchiveMode.Create))
                {
                    foreach (var quelle in quellen)
                    {
                        if (File.Exists(quelle))
                            archiv.CreateEntryFromFile(quelle, Path.GetFileName(quelle));
                        else if (Directory.Exists(quelle))
                            ZipOrdnerHinzufuegen(archiv, quelle, Path.GetFileName(quelle));
                    }
                }

                return (true, Loc.T($"„{Path.GetFileName(zielPfad)}\" mit {quellen.Count} Einträgen erstellt.",
                                    $"\"{Path.GetFileName(zielPfad)}\" created with {quellen.Count} entries."));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return (false, Loc.T($"ZIP konnte nicht erstellt werden: {e.Message}", $"ZIP could not be created: {e.Message}"));
            }
        });

    /// <summary>Reparse Points werden übersprungen - dieselbe Schleifengefahr wie bei <see cref="OrdnerKopieren"/>.</summary>
    private static void ZipOrdnerHinzufuegen(ZipArchive archiv, string ordnerPfad, string zipPfadPraefix)
    {
        foreach (var datei in Directory.GetFiles(ordnerPfad))
            archiv.CreateEntryFromFile(datei, $"{zipPfadPraefix}/{Path.GetFileName(datei)}");

        foreach (var unterOrdner in Directory.GetDirectories(ordnerPfad))
        {
            if (NtfsBesonderheiten.IstReparsePoint(new DirectoryInfo(unterOrdner).Attributes)) continue;
            ZipOrdnerHinzufuegen(archiv, unterOrdner, $"{zipPfadPraefix}/{Path.GetFileName(unterOrdner)}");
        }
    }
}
