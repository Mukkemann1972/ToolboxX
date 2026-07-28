using System.Text.RegularExpressions;

namespace PCToolboxCore;

/// <summary>
/// Reine Deutungs-Logik fuer den Windows-Update-Verlauf (Rang 2 aus der NirSoft-Recherche,
/// Vorbild WinUpdatesView): macht aus den rohen COM-Werten der Windows Update Agent API
/// (ResultCode/Operation als Zahl, Titel als Freitext) etwas, das man lesen kann. Bewusst
/// plattformneutral und ohne COM-Aufruf selbst, damit auf dem Pi echt testbar - der eigentliche
/// COM-Zugriff passiert in PC-Toolbox/Services/WindowsUpdateVerlaufService.cs.
/// </summary>
public static class WindowsUpdateDeutung
{
    private static readonly Regex KbMuster = new(@"KB\d{6,7}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Extrahiert die KB-Nummer aus dem Update-Titel, falls vorhanden (z.B. "...(KB5040001)...").</summary>
    public static string? KbNummerAusTitel(string titel)
    {
        var treffer = KbMuster.Match(titel);
        return treffer.Success ? treffer.Value.ToUpperInvariant() : null;
    }

    /// <summary>
    /// Deutet den WUA-ResultCode (siehe IUpdateHistoryEntry.ResultCode):
    /// 0=NotStarted, 1=InProgress, 2=Succeeded, 3=SucceededWithErrors, 4=Failed, 5=Aborted.
    /// </summary>
    public static (string Text, bool IstFehlgeschlagen) DeuteErgebnisCode(int resultCode) => resultCode switch
    {
        2 => (Loc.T("Erfolgreich", "Succeeded"), false),
        3 => (Loc.T("Erfolgreich, aber mit Fehlern", "Succeeded with errors"), true),
        4 => (Loc.T("Fehlgeschlagen", "Failed"), true),
        5 => (Loc.T("Abgebrochen", "Aborted"), true),
        1 => (Loc.T("Lief noch (unterbrochen?)", "Was still running (interrupted?)"), true),
        0 => (Loc.T("Nicht gestartet", "Not started"), false),
        _ => (Loc.T("Unbekannter Status", "Unknown status"), false)
    };

    /// <summary>Deutet IUpdateHistoryEntry.Operation: 1=Installation, 2=Deinstallation.</summary>
    public static string DeuteOperation(int operation) => operation switch
    {
        1 => Loc.T("Installation", "Installation"),
        2 => Loc.T("Deinstallation", "Uninstallation"),
        _ => Loc.T("Unbekannt", "Unknown")
    };
}
