using System.Diagnostics;
using System.IO;
using PCToolboxCore;

namespace PCToolbox.Safety;

// Phase 1 der 3-Phasen-Sicherheit (SICHERN): nichts wird geloescht, bevor es hier gesichert ist.
// Registry-Schluessel werden als .reg-Datei exportiert, Ordner werden VERSCHOBEN statt geloescht -
// beides per RollbackEngine wieder rueckgaengig zu machen.
public static class SnapshotManager
{
    public static string BasisOrdner =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PC-Toolbox", "Restore");

    public static string NeuerSnapshotOrdner(string aktionsName)
    {
        var sicherName = SnapshotNaming.SicherenOrdnernamen(aktionsName, Path.GetInvalidFileNameChars());
        var ordner = Path.Combine(BasisOrdner, $"{sicherName}_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(ordner);
        return ordner;
    }

    public static bool RegistrySchluesselSichern(string snapshotOrdner, string registryPfad, string dateiName)
    {
        if (string.IsNullOrWhiteSpace(registryPfad)) return false;

        var zielDatei = Path.Combine(snapshotOrdner, $"{dateiName}.reg");
        var psi = new ProcessStartInfo("reg.exe", $"export \"{registryPfad}\" \"{zielDatei}\" /y")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var prozess = Process.Start(psi);
        if (prozess is null) return false;
        prozess.WaitForExit(15000);
        return prozess.ExitCode == 0 && File.Exists(zielDatei);
    }

    // Verschiebt einen Ordner in den Snapshot (nicht kopieren - bei grossen Programmordnern
    // waere eine Kopie zu teuer, und Verschieben allein macht den Ordner am alten Ort schon "weg").
    // Legt einen Merkzettel (<guid>.pfad) mit dem Ursprungsort daneben, den RollbackEngine braucht.
    public static string? OrdnerSichernUndEntfernen(string snapshotOrdner, string quellPfad)
    {
        if (!Directory.Exists(quellPfad)) return null;

        var ordnerBasis = Path.Combine(snapshotOrdner, "Ordner");
        Directory.CreateDirectory(ordnerBasis);

        var id = Guid.NewGuid().ToString("N");
        var zielOrdner = Path.Combine(ordnerBasis, id);

        if (!VersucheVerschieben(quellPfad, zielOrdner))
            return null;

        File.WriteAllText(Path.Combine(ordnerBasis, $"{id}.pfad"), quellPfad);

        return zielOrdner;
    }

    // Zweiter Versuch NUR nach ausdruecklicher Zustimmung des Nutzers (die UI zeigt vorher eine
    // Erklaerung, warum Windows hier Administratorrechte verlangt, und fragt nach - kein
    // automatisches, unerklaertes UAC-Fenster). Wichtig fuer die Haftungsfrage: der Nutzer soll
    // immer wissen, WAS er da gerade freigibt, nicht einfach ueberrumpelt werden.
    public static string? OrdnerMitErhoehtenRechtenSichernUndEntfernen(string snapshotOrdner, string quellPfad)
    {
        if (!Directory.Exists(quellPfad)) return null;

        var ordnerBasis = Path.Combine(snapshotOrdner, "Ordner");
        Directory.CreateDirectory(ordnerBasis);

        var id = Guid.NewGuid().ToString("N");
        var zielOrdner = Path.Combine(ordnerBasis, id);

        if (!VersucheEleviertVerschieben(quellPfad, zielOrdner))
            return null;

        File.WriteAllText(Path.Combine(ordnerBasis, $"{id}.pfad"), quellPfad);

        return zielOrdner;
    }

    private static bool VersucheVerschieben(string quelle, string ziel)
    {
        try
        {
            Directory.Move(quelle, ziel);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // z.B. eine Datei im Ordner ist gerade in Benutzung, oder es fehlen Rechte (z.B.
            // ProgramData-Ordner, deren ACL nur Administratoren das Entfernen erlaubt - live
            // erlebt bei einem KORG-Ordner) - live gebracht bis hierher die ganze App zum
            // Absturz statt nur "konnte nicht entfernt werden" zu melden. Die UI entscheidet,
            // ob/wie sie dem Nutzer den erklaerten Zweitversuch mit Rechte-Anhebung anbietet.
            return false;
        }
    }

    private static bool VersucheEleviertVerschieben(string quelle, string ziel)
    {
        try
        {
            var befehl = "$ErrorActionPreference = 'Stop'; " +
                         $"Move-Item -LiteralPath '{QuoteFuerPowerShell(quelle)}' -Destination '{QuoteFuerPowerShell(ziel)}' -Force";
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{befehl.Replace("\"", "\\\"")}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var prozess = Process.Start(psi);
            if (prozess is null) return false;
            prozess.WaitForExit();

            // Nicht nur den Exitcode glauben - wirklich nachsehen, ob die Quelle weg UND das
            // Ziel da ist (dieselbe Vorsicht wie beim Rest der App: erst verifizieren, dann als
            // erfolgreich gelten lassen).
            return prozess.ExitCode == 0 && !Directory.Exists(quelle) && Directory.Exists(ziel);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // Nutzer hat die UAC-Abfrage abgelehnt
        }
    }

    private static string QuoteFuerPowerShell(string pfad) => pfad.Replace("'", "''");
}
