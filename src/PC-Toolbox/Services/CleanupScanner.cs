using System.IO;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

// Ermittelt NUR Größe/Anzahl je Kategorie (rein lesend) - löscht nichts. Bewusst schlanke
// Auswahl an Kategorien (kein Feature-Anhäufen): nur Orte, die als OS-Konvention wirklich
// sicher/regenerierbar sind. Browser-Caches u.ä. sind bewusst NICHT dabei (V1-Scope).
public class CleanupScanner
{
    public Task<List<CleanupBefund>> ScanAsync() => Task.Run(() =>
    {
        var befunde = new List<CleanupBefund>();

        BefundHinzufuegen(befunde, CleanupKategorie.WindowsTemp, Loc.T("Temporäre Dateien", "Temporary files"),
            AmpelStatus.Gruen,
            Loc.T(
                "Dateien, die Windows und Programme beim Arbeiten kurzfristig anlegen (z.B. beim " +
                "Entpacken, Installieren, Drucken) und eigentlich selbst wieder aufräumen sollten, " +
                "es aber nicht immer tun.",
                "Files that Windows and programs create briefly while working (e.g. when " +
                "extracting, installing, printing) and are supposed to clean up themselves, " +
                "but don't always do."),
            Loc.T(
                "Gefahrlos entfernbar - Programme legen bei Bedarf einfach neue temporäre Dateien " +
                "an. Landet beim Entfernen im Papierkorb, nichts wird sofort endgültig gelöscht.",
                "Safe to remove - programs simply create new temporary files when " +
                "needed. Goes to the recycle bin when removed, nothing is immediately permanently deleted."),
            new[] { Path.GetTempPath(), @"C:\Windows\Temp" });

        BefundHinzufuegen(befunde, CleanupKategorie.Miniaturansichten, Loc.T("Miniaturansichten-Cache", "Thumbnail cache"),
            AmpelStatus.Gruen,
            Loc.T(
                "Kleine Vorschaubilder, die der Windows-Explorer für Fotos/Videos/Dokumente " +
                "zwischenspeichert, damit sie beim nächsten Öffnen des Ordners schneller angezeigt " +
                "werden.",
                "Small preview images that Windows Explorer caches for photos/videos/documents " +
                "so they display faster the next time you open the " +
                "folder."),
            Loc.T(
                "Gefahrlos entfernbar - Windows baut die Vorschaubilder beim nächsten Anschauen des " +
                "Ordners einfach neu auf (kurz als generisches Icon sichtbar, dann wieder da).",
                "Safe to remove - Windows simply rebuilds the previews the next time you view the " +
                "folder (briefly shown as a generic icon, then back)."),
            new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Explorer") },
            dateimuster: "thumbcache_*.db",
            nurDateien: true);

        BefundHinzufuegen(befunde, CleanupKategorie.WindowsUpdateCache, Loc.T("Windows-Update-Downloadcache", "Windows Update download cache"),
            AmpelStatus.Gelb,
            Loc.T(
                "Bereits heruntergeladene bzw. installierte Windows-Update-Pakete, die nach der " +
                "Installation eigentlich nicht mehr gebraucht werden.",
                "Already downloaded or installed Windows Update packages that aren't actually " +
                "needed anymore after installation."),
            Loc.T(
                "Meist gefahrlos. Falls Windows Update gerade selbst auf die Dateien zugreift, " +
                "werden einzelne Dateien übersprungen statt einen Fehler zu werfen - Windows lädt " +
                "bei Bedarf einfach neu herunter.",
                "Usually safe. If Windows Update is currently accessing the files itself, " +
                "individual files are skipped instead of throwing an error - Windows simply " +
                "downloads them again if needed."),
            new[] { @"C:\Windows\SoftwareDistribution\Download" });

        BefundHinzufuegen(befunde, CleanupKategorie.WindowsOld, Loc.T("Alte Windows-Version (Windows.old)", "Old Windows version (Windows.old)"),
            AmpelStatus.Rot,
            Loc.T(
                "Die komplette vorherige Windows-Installation, die Windows nach einem großen " +
                "Funktions-Update (z.B. Windows 10 → 11) automatisch aufhebt - kann sehr groß sein " +
                "(oft 20 GB und mehr).",
                "The complete previous Windows installation that Windows automatically keeps after a big " +
                "feature update (e.g. Windows 10 → 11) - can be very large " +
                "(often 20 GB and more)."),
            Loc.T(
                "NUR entfernen, wenn du sicher bist, dass du NICHT mehr zur alten Windows-Version " +
                "zurückwechseln willst - danach ist der Rückweg nicht mehr möglich. Der Ordner " +
                "gehört teilweise dem System selbst (TrustedInstaller) - falls hier Dateien " +
                "übersprungen werden, hilft notfalls Windows' eigene \"Datenträgerbereinigung\" " +
                "(als Administrator, Option \"Vorherige Windows-Installation(en)\").",
                "Only remove if you're sure you do NOT want to switch back " +
                "to the old Windows version - afterward there's no going back. The folder " +
                "partially belongs to the system itself (TrustedInstaller) - if files here " +
                "are skipped, Windows' own \"Disk Cleanup\" can help as a last resort " +
                "(as administrator, option \"Previous Windows installation(s)\")."),
            new[] { @"C:\Windows.old" });

        return befunde;
    });

    private static void BefundHinzufuegen(List<CleanupBefund> befunde, CleanupKategorie kategorie,
        string titel, AmpelStatus ampel, string wasEsIst, string erklaerung, string[] pfade,
        string dateimuster = "*", bool nurDateien = false)
    {
        long groesse = 0;
        var vorhandenePfade = new List<string>();
        var anzahl = 0;

        foreach (var pfad in pfade)
        {
            if (string.IsNullOrWhiteSpace(pfad) || !Directory.Exists(pfad)) continue;
            vorhandenePfade.Add(pfad);

            foreach (var datei in SicherAufzaehlen(pfad, dateimuster))
            {
                try
                {
                    groesse += new FileInfo(datei).Length;
                    anzahl++;
                }
                catch { /* Datei zwischenzeitlich weg/keine Rechte - betrifft nur die Anzeige */ }
            }
        }

        if (vorhandenePfade.Count == 0) return; // Kategorie existiert auf diesem System nicht

        befunde.Add(new CleanupBefund
        {
            Kategorie = kategorie,
            Titel = titel,
            Ampel = ampel,
            WasEsIst = wasEsIst,
            Erklaerung = erklaerung,
            GroesseBytes = groesse,
            DateiAnzahl = anzahl,
            WurzelPfade = vorhandenePfade,
            Dateimuster = dateimuster,
            NurDateien = nurDateien,
        });
    }

    // Directory.EnumerateFiles(..., AllDirectories) ist "lazy": der eigentliche Ordnerzugriff
    // (und damit z.B. UnauthorizedAccessException bei gesperrten Systemordnern wie
    // C:\Windows\Temp\DiagOutputDir) passiert erst beim Iterieren im Aufrufer, nicht beim Aufruf
    // hier - ein try/catch um nur den Aufruf fängt das also nicht ab. Deshalb hier pro Unterordner
    // einzeln und sofort (nicht deferred) aufzählen, damit ein gesperrter Ordner nur sich selbst
    // überspringt statt die komplette Aufzählung abzubrechen.
    private static IEnumerable<string> SicherAufzaehlen(string ordner, string muster)
    {
        var ergebnis = new List<string>();
        SicherAufzaehlenRekursiv(ordner, muster, ergebnis);
        return ergebnis;
    }

    private static void SicherAufzaehlenRekursiv(string ordner, string muster, List<string> ergebnis)
    {
        try
        {
            ergebnis.AddRange(Directory.EnumerateFiles(ordner, muster, SearchOption.TopDirectoryOnly));
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        List<string> unterordner;
        try
        {
            unterordner = Directory.EnumerateDirectories(ordner).ToList();
        }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }

        foreach (var sub in unterordner)
        {
            SicherAufzaehlenRekursiv(sub, muster, ergebnis);
        }
    }
}
