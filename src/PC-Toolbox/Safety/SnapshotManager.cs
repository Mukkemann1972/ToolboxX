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
        Directory.Move(quellPfad, zielOrdner);
        File.WriteAllText(Path.Combine(ordnerBasis, $"{id}.pfad"), quellPfad);

        return zielOrdner;
    }
}
