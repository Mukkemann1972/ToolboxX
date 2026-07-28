namespace PCToolboxCore;

// Schutzregeln fuer den LeftoverScanner: verhindert, dass ein Ordner-Fund in einem geschuetzten
// Systempfad oder (durch eine fehlerhafte/leere InstallLocation) in einem kompletten Wurzelordner
// landet. Nimmt die bekannten Pfade als Parameter entgegen (statt sie selbst per
// Environment.GetFolderPath zu ermitteln), damit die Logik plattformneutral testbar bleibt.
public static class PathSafety
{
    public static bool IstGeschuetzterPfad(string pfad, IEnumerable<string> geschuetztePfadAnfaenge) =>
        geschuetztePfadAnfaenge.Any(g =>
            !string.IsNullOrEmpty(g) && pfad.StartsWith(g, StringComparison.OrdinalIgnoreCase));

    // Schutz gegen fehlerhafte/leere InstallLocation-Werte, die sonst auf einen kompletten
    // Wurzelordner (z.B. "C:\Program Files") statt auf den eigentlichen Programmordner zeigen.
    public static bool IstZuAllgemeinerPfad(string ordner, IEnumerable<string> bekannteWurzeln)
    {
        var bereinigt = ordner.TrimEnd('\\');
        if (bereinigt.Length <= 3) return true; // z.B. "C:" oder "C:\"

        return bekannteWurzeln.Any(w =>
            !string.IsNullOrEmpty(w) && string.Equals(bereinigt, w.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IstGeschuetzterRegistryPfad(string pfad) =>
        pfad.Contains(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase) ||
        pfad.EndsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase);
}
