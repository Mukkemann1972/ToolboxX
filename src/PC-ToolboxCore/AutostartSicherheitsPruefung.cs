using PCToolbox.Models;

namespace PCToolboxCore;

public record SicherheitsBefund(string Titel, string Text, AmpelStatus Status);

/// <summary>
/// Deutet die zwei seltenen, aber sicherheitsrelevanten Autostart-Orte aus Autostart V0.2:
/// Winlogon\Shell (ersetzt den kompletten Windows-Desktop) und Image File Execution
/// Options/IFEO (kann JEDEN Programmstart heimlich umleiten - klassische
/// Persistenz-Technik). Anders als Run/RunOnce sind das keine Listen gleichartiger
/// Eintraege, sondern einzelne Sicherheitsprüfungen - deshalb eigene, einfachere Logik statt
/// in AutostartBewerter gequetscht.
/// </summary>
public static class AutostartSicherheitsPruefung
{
    private const string ErwarteteShell = "explorer.exe";

    /// <summary>
    /// Winlogon\Shell bestimmt, welches Programm nach der Anmeldung als Desktop startet -
    /// normal ist ausschliesslich "explorer.exe". Alles andere ersetzt den kompletten
    /// Windows-Desktop durch ein fremdes Programm und ist ein starkes Warnsignal.
    /// </summary>
    public static SicherheitsBefund PruefeWinlogonShell(string? shellWert)
    {
        var wert = (shellWert ?? "").Trim();

        if (string.Equals(wert, ErwarteteShell, StringComparison.OrdinalIgnoreCase))
            return new(
                Loc.T("Windows-Shell (Winlogon)", "Windows shell (Winlogon)"),
                Loc.T("In Ordnung - explorer.exe startet wie erwartet nach der Anmeldung.",
                      "Fine - explorer.exe starts as expected after logon."),
                AmpelStatus.Gruen);

        if (string.IsNullOrWhiteSpace(wert))
            return new(
                Loc.T("Windows-Shell (Winlogon)", "Windows shell (Winlogon)"),
                Loc.T("Kein Wert gesetzt - Windows verwendet dann automatisch explorer.exe.",
                      "No value set - Windows then automatically uses explorer.exe."),
                AmpelStatus.Gruen);

        return new(
            Loc.T("Windows-Shell (Winlogon)", "Windows shell (Winlogon)"),
            Loc.T(
                $"Auffällig: Statt explorer.exe startet nach der Anmeldung \"{wert}\" - das ersetzt " +
                "den kompletten Windows-Desktop durch ein anderes Programm. Legitime Gründe dafür " +
                "gibt es kaum (höchstens spezielle Kiosk-Systeme). Wenn du das nicht selbst " +
                "eingerichtet hast, ist das ein starkes Anzeichen für Schadsoftware.",
                $"Unusual: instead of explorer.exe, \"{wert}\" starts after logon - this replaces the " +
                "entire Windows desktop with a different program. There's hardly any legitimate reason " +
                "for this (at most specialized kiosk systems). If you didn't set this up yourself, " +
                "it's a strong sign of malware."),
            AmpelStatus.Rot);
    }

    /// <summary>
    /// Ein Eintrag unter Image File Execution Options mit gesetztem "Debugger"-Wert leitet
    /// JEDEN Start des benannten Programms heimlich auf ein anderes Programm um - eine
    /// klassische Persistenz-/Tarn-Technik. Der blosse Ordner-Eintrag ohne Debugger-Wert ist
    /// dagegen voellig normal (wird auch fuer harmlose Kompatibilitaets-Einstellungen genutzt)
    /// und wird deshalb gar nicht erst als Fund gemeldet - nur ein gesetzter Debugger-Wert ist einer.
    /// </summary>
    public static SicherheitsBefund IfeoUmleitung(string zielProgramm, string debuggerBefehl) => new(
        Loc.T($"Programmstart-Umleitung: {zielProgramm}", $"Program launch redirect: {zielProgramm}"),
        Loc.T(
            $"Jedes Mal, wenn \"{zielProgramm}\" gestartet werden soll, startet Windows stattdessen " +
            $"\"{debuggerBefehl}\" - unsichtbar für den Nutzer. Das ist eine bekannte Tarn-Technik " +
            "für Schadsoftware, wird aber auch (selten) für echte Debugging-Zwecke genutzt. Falls du " +
            "das nicht selbst eingerichtet hast, ist das ein ernstzunehmender Fund.",
            $"Every time \"{zielProgramm}\" is supposed to start, Windows starts \"{debuggerBefehl}\" " +
            "instead - invisibly to the user. This is a known malware disguise technique, but is also " +
            "(rarely) used for genuine debugging purposes. If you didn't set this up yourself, this is " +
            "a finding worth taking seriously."),
        AmpelStatus.Rot);
}
