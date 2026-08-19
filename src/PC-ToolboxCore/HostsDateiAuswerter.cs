namespace PCToolboxCore;

/// <summary>
/// Reine Textauswertung des Inhalts einer Hosts-Datei - das Einlesen der echten Datei
/// (Windows-spezifischer Pfad, Adminrechte für den Ordner) übernimmt die Service-Schicht,
/// hier steckt nur das Format-Verständnis, damit es ohne echte Datei testbar ist.
///
/// Format je Zeile: "Adresse Name1 [Name2 ...] [# Kommentar]". Leerzeilen und reine
/// Kommentarzeilen werden übersprungen, wie es auch Windows selbst beim Einlesen tut.
/// </summary>
public static class HostsDateiAuswerter
{
    /// <summary>
    /// Sucht die erste aktive Zuordnung für <paramref name="ziel"/> und gibt deren Adresse
    /// zurück, oder null wenn keine gefunden wurde. Groß-/Kleinschreibung spielt beim
    /// Domainnamen keine Rolle (Windows behandelt Hosts-Einträge ebenfalls case-insensitiv).
    /// </summary>
    public static string? SucheEintrag(string inhalt, string ziel)
    {
        foreach (var rohzeile in inhalt.Split('\n'))
        {
            var zeile = rohzeile.Split('#')[0].Trim();
            if (zeile.Length == 0) continue;

            var teile = zeile.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (teile.Length < 2) continue;

            var adresse = teile[0];
            var namen = teile.Skip(1);

            if (namen.Any(n => string.Equals(n, ziel, StringComparison.OrdinalIgnoreCase)))
                return adresse;
        }

        return null;
    }
}
