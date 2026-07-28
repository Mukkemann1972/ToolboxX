using System.IO;
using PCToolbox.Models;
using PCToolbox.Safety;
using PCToolboxCore;

namespace PCToolbox.Services;

// Führt die eigentliche Bereinigung aus. Verschiebt jede Datei/jeden Ordner per RecycleBinDeleter
// in den Windows-Papierkorb, statt endgültig zu löschen - der Nutzer kann alles selbst
// wiederherstellen. Gesperrte/gerade genutzte Dateien werden übersprungen statt abzustürzen.
public class CleanupService
{
    public Task<CleanupErgebnis> BereinigenAsync(CleanupBefund befund, IProgress<string>? fortschritt = null) =>
        Task.Run(() =>
        {
            long freigegeben = 0;
            var entfernt = 0;
            var uebersprungen = 0;

            foreach (var wurzel in befund.WurzelPfade)
            {
                if (!Directory.Exists(wurzel)) continue;

                foreach (var eintrag in UnmittelbareKinder(wurzel, befund))
                {
                    fortschritt?.Report(Loc.T($"Entferne {Path.GetFileName(eintrag)}...", $"Removing {Path.GetFileName(eintrag)}..."));

                    var groesseVorher = GroesseSchaetzen(eintrag);

                    if (RecycleBinDeleter.InDenPapierkorbVerschieben(eintrag))
                    {
                        freigegeben += groesseVorher;
                        entfernt++;
                    }
                    else
                    {
                        uebersprungen++; // z.B. Datei/Ordner gerade in Benutzung oder geschützt
                    }
                }
            }

            return new CleanupErgebnis
            {
                BytesFreigegeben = freigegeben,
                DateienEntfernt = entfernt,
                DateienUebersprungen = uebersprungen,
                Meldung = uebersprungen == 0
                    ? Loc.T($"{entfernt} Element(e) in den Papierkorb verschoben.", $"{entfernt} item(s) moved to the recycle bin.")
                    : Loc.T(
                          $"{entfernt} Element(e) verschoben, {uebersprungen} übersprungen " +
                          "(vermutlich gerade in Benutzung oder geschützt).",
                          $"{entfernt} item(s) moved, {uebersprungen} skipped " +
                          "(probably currently in use or protected)."),
            };
        });

    // Nur die UNMITTELBAREN Kinder des Wurzelordners - der Wurzelordner selbst muss für neue
    // Dateien bestehen bleiben (z.B. %TEMP% braucht Programme weiterhin).
    private static IEnumerable<string> UnmittelbareKinder(string wurzel, CleanupBefund befund)
    {
        var eintraege = new List<string>();
        try
        {
            eintraege.AddRange(Directory.EnumerateFiles(wurzel, befund.Dateimuster));
            if (!befund.NurDateien)
                eintraege.AddRange(Directory.EnumerateDirectories(wurzel));
        }
        catch { /* keine Rechte auf den Wurzelordner selbst - dann eben nichts zu tun hier */ }
        return eintraege;
    }

    private static long GroesseSchaetzen(string pfad)
    {
        try
        {
            if (File.Exists(pfad)) return new FileInfo(pfad).Length;
            if (Directory.Exists(pfad))
                return Directory.EnumerateFiles(pfad, "*", SearchOption.AllDirectories)
                    .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        }
        catch { /* egal, Groesse dient nur der Anzeige */ }
        return 0;
    }
}
