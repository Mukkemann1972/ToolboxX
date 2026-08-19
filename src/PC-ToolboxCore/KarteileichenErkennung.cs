namespace PCToolboxCore;

/// <summary>
/// Reine Entscheidungslogik für den Karteileichen-Scan (Pro, s. KarteileichenScanner): erkennt
/// MSI-basierte Deinstallations-Befehle ("MsiExec.exe /X{GUID}"), die bewusst NICHT auf eine
/// fehlende Datei geprüft werden - msiexec.exe selbst ist immer vorhanden (liegt in System32),
/// eine tatsächlich verwaiste MSI-Registrierung zu erkennen bräuchte die Windows-Installer-API
/// (MsiEnumProducts), nicht nur einen Dateipfad-Check. Ohne diese Unterscheidung würde JEDES
/// MSI-installierte Programm fälschlich als "gefunden" (nicht als Karteileiche) durchgehen -
/// das ist zwar richtig, aber aus dem falschen Grund, und ein Bugfix hier ist wichtiger als die
/// Deckungslücke (echte MSI-Karteileichen bleiben unentdeckt statt falsch gemeldet zu werden).
/// </summary>
public static class KarteileichenErkennung
{
    public static bool IstMsiBasiert(string uninstallString) =>
        uninstallString.Contains("msiexec", StringComparison.OrdinalIgnoreCase);
}
