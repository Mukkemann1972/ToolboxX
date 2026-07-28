using System.Diagnostics;
using System.IO;

namespace PCToolbox.Safety;

// Gegenstueck zu SnapshotManager: macht eine Phase-1-Sicherung wieder rueckgaengig.
public static class RollbackEngine
{
    public static bool RegistryWiederherstellen(string regDateiPfad)
    {
        if (!File.Exists(regDateiPfad)) return false;

        var psi = new ProcessStartInfo("reg.exe", $"import \"{regDateiPfad}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var prozess = Process.Start(psi);
        if (prozess is null) return false;
        prozess.WaitForExit(15000);
        return prozess.ExitCode == 0;
    }

    // Holt einen per SnapshotManager.OrdnerSichernUndEntfernen() verschobenen Ordner an seinen
    // Ursprungsort zurueck. snapshotUnterordner = .../Ordner/<guid>, Merkzettel = .../Ordner/<guid>.pfad
    public static bool OrdnerWiederherstellen(string snapshotUnterordner)
    {
        var pfadMerkzettel = snapshotUnterordner + ".pfad";
        if (!Directory.Exists(snapshotUnterordner) || !File.Exists(pfadMerkzettel)) return false;

        var urspruenglicherPfad = File.ReadAllText(pfadMerkzettel).Trim();
        if (string.IsNullOrWhiteSpace(urspruenglicherPfad) || Directory.Exists(urspruenglicherPfad)) return false;

        var elternOrdner = Path.GetDirectoryName(urspruenglicherPfad);
        if (!string.IsNullOrEmpty(elternOrdner)) Directory.CreateDirectory(elternOrdner);

        Directory.Move(snapshotUnterordner, urspruenglicherPfad);
        File.Delete(pfadMerkzettel);
        return true;
    }
}
