using System.Diagnostics;
using System.IO;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Services;

/// <summary>
/// Führt die Datenträger-Aktionen aus - über die Bordmittel von Windows
/// (PowerShell-Speicherbefehle), nicht über eigene Zugriffe auf die Partitionstabelle.
///
/// Warum so: Ein selbstgeschriebener Partitionierer wäre die einzige Stelle in der
/// ganzen Toolbox, an der ein Programmierfehler von uns direkt Daten vernichten könnte.
/// Die Windows-Befehle sind seit Jahren erprobt und sichern selbst ab; unsere Aufgabe ist
/// die verständliche Oberfläche darüber und die Leitplanken davor (DatentraegerSchutz).
/// </summary>
public class DatentraegerService
{
    public Task<List<DatentraegerVolume>> LadeAsync() => Task.Run(Lade);

    private static List<DatentraegerVolume> Lade()
    {
        var volumes = new List<DatentraegerVolume>();

        var systemWurzel = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        foreach (var laufwerk in DriveInfo.GetDrives())
        {
            // Netzlaufwerke und CD/DVD lassen sich hiermit nicht sinnvoll behandeln.
            if (laufwerk.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            if (!laufwerk.IsReady) continue;

            try
            {
                volumes.Add(new DatentraegerVolume
                {
                    Buchstabe = laufwerk.Name[0],
                    Bezeichnung = laufwerk.VolumeLabel,
                    Dateisystem = laufwerk.DriveFormat,
                    GroesseBytes = laufwerk.TotalSize,
                    FreiBytes = laufwerk.AvailableFreeSpace,
                    IstWechseldatentraeger = laufwerk.DriveType == DriveType.Removable,
                    IstSystemlaufwerk = systemWurzel is not null &&
                                        laufwerk.Name.StartsWith(systemWurzel, StringComparison.OrdinalIgnoreCase)
                });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Laufwerk meldet gerade keine Daten - überspringen statt abstürzen.
            }
        }

        return volumes.OrderBy(v => v.Buchstabe).ToList();
    }

    /// <summary>
    /// Fragt bei Windows nach, wie klein/groß die Partition werden darf. Läuft ohne
    /// Rechteanhebung: Klappt es nicht, wird das ehrlich gemeldet, statt eine Grenze
    /// zu erfinden.
    /// </summary>
    public async Task<(long Min, long Max)?> GrenzenAsync(char buchstabe)
    {
        var argumente = DatentraegerBefehle.AlsArgumente(DatentraegerBefehle.GrenzenAbfragen(buchstabe));

        try
        {
            var start = new ProcessStartInfo("powershell.exe", argumente)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var prozess = Process.Start(start);
            if (prozess is null) return null;

            var ausgabe = await prozess.StandardOutput.ReadToEndAsync();
            await prozess.WaitForExitAsync();

            return PartitionGrenzen.Lesen(ausgabe);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    public Task<(bool Erfolg, string Meldung)> FormatierenAsync(
        DatentraegerVolume volume, string dateisystem, string bezeichnung, bool schnell)
    {
        var schutz = DatentraegerSchutz.DarfFormatieren(volume);
        if (!schutz.Erlaubt) return Task.FromResult((false, schutz.Begruendung));

        var pruefung = DateisystemRegeln.Pruefen(dateisystem, volume.GroesseBytes);
        if (!pruefung.Moeglich) return Task.FromResult((false, pruefung.Hinweis));

        var befehl = DatentraegerBefehle.Formatieren(volume.Buchstabe, dateisystem, bezeichnung, schnell);

        return AusfuehrenAsync(volume.Buchstabe, befehl,
            Loc.T($"Laufwerk {volume.Buchstabe}: wurde mit {dateisystem} formatiert.",
                  $"Drive {volume.Buchstabe}: was formatted with {dateisystem}."));
    }

    public Task<(bool Erfolg, string Meldung)> BezeichnungAendernAsync(DatentraegerVolume volume, string bezeichnung) =>
        AusfuehrenAsync(volume.Buchstabe, DatentraegerBefehle.BezeichnungAendern(volume.Buchstabe, bezeichnung),
            Loc.T($"Laufwerk {volume.Buchstabe}: heisst jetzt „{bezeichnung}\".",
                  $"Drive {volume.Buchstabe}: is now called \"{bezeichnung}\"."));

    public Task<(bool Erfolg, string Meldung)> BuchstabeAendernAsync(DatentraegerVolume volume, char neu) =>
        AusfuehrenAsync(volume.Buchstabe, DatentraegerBefehle.BuchstabeAendern(volume.Buchstabe, neu),
            Loc.T($"Das Laufwerk ist jetzt unter {char.ToUpperInvariant(neu)}: erreichbar.",
                  $"The drive is now accessible at {char.ToUpperInvariant(neu)}:."));

    public async Task<(bool Erfolg, string Meldung)> GroesseAendernAsync(DatentraegerVolume volume, long neueGroesse)
    {
        var schutz = DatentraegerSchutz.DarfGroesseAendern(volume);
        if (!schutz.Erlaubt) return (false, schutz.Begruendung);

        if (await GrenzenAsync(volume.Buchstabe) is { } grenzen)
        {
            var pruefung = DatentraegerSchutz.GroessePruefen(neueGroesse, grenzen.Min, grenzen.Max);
            if (!pruefung.Erlaubt) return (false, pruefung.Begruendung);
        }

        return await AusfuehrenAsync(volume.Buchstabe, DatentraegerBefehle.GroesseAendern(volume.Buchstabe, neueGroesse),
            Loc.T($"Die Größe von Laufwerk {volume.Buchstabe}: wurde geändert. Die Daten sind unverändert vorhanden.",
                  $"The size of drive {volume.Buchstabe}: was changed. The data is unchanged."));
    }

    /// <summary>
    /// Führt einen Speicherbefehl mit gezielter Rechteanhebung aus. Wie überall in der
    /// Toolbox: Die App selbst läuft ohne Admin-Rechte, die Nachfrage kommt nur für
    /// genau diese eine Aktion.
    ///
    /// Schlägt der erste Versuch fehl, wird automatisch EIN zweiter Versuch unternommen -
    /// davor löst DatentraegerBefehle.LaufwerkFreigeben die häufigste Ursache ("Laufwerk wird
    /// verwendet", meist nur ein offenes Explorer-Fenster) auf. Das läuft in derselben
    /// bereits erhöhten PowerShell-Sitzung mit, kostet also keine zweite Windows-Sicherheits-
    /// abfrage. Erst wenn auch das nicht hilft, wird ehrlich ein Fehler gemeldet.
    /// </summary>
    private static Task<(bool Erfolg, string Meldung)> AusfuehrenAsync(char buchstabe, string befehl, string erfolgsMeldung) =>
        Task.Run(() =>
        {
            try
            {
                if (BefehlAusfuehren(DatentraegerBefehle.AlsArgumente(befehl)))
                    return (true, erfolgsMeldung);

                var freigabeUndBefehl = DatentraegerBefehle.LaufwerkFreigeben(buchstabe) + "; " + befehl;
                if (BefehlAusfuehren(DatentraegerBefehle.AlsArgumente(freigabeUndBefehl)))
                    return (true, erfolgsMeldung);

                return (false, Loc.T(
                    "Windows hat den Vorgang auch nach einem automatischen zweiten Versuch (Laufwerkssperre " +
                    "lösen) nicht ausgeführt. Meist hilft: den Stick kurz abziehen und neu einstecken, oder " +
                    "ein Programm im Hintergrund schließen, das noch darauf zugreift (z.B. ein Virenscan). " +
                    "Es wurde nichts verändert.",
                    "Windows did not carry out the operation even after an automatic second attempt " +
                    "(releasing the drive lock). Usually helps: unplug and replug the stick, or close a " +
                    "background program still accessing it (e.g. antivirus). Nothing was changed."));
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return (false, Loc.T(
                    "Ohne die Windows-Bestätigung (Administratorrechte) geht das nicht. " +
                    "Es wurde nichts verändert.",
                    "This isn't possible without the Windows confirmation (administrator rights). " +
                    "Nothing was changed."));
            }
        });

    private static bool BefehlAusfuehren(string argumente)
    {
        var start = new ProcessStartInfo("powershell.exe", argumente)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var prozess = Process.Start(start);
        if (prozess is null) return false;

        prozess.WaitForExit();
        return prozess.ExitCode == 0;
    }
}
