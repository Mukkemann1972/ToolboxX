using PCToolboxCore;

namespace PCToolboxCore.Models;

/// <summary>
/// Woher ein Autostart-Eintrag stammt. Windows hat dafuer historisch gewachsen
/// mehrere voellig verschiedene Orte - genau deshalb uebersehen die meisten
/// Aufraeum-Tools (und der Windows-Task-Manager) einen Teil davon.
/// </summary>
public enum AutostartQuelle
{
    RegistryBenutzer,        // HKCU ...\CurrentVersion\Run - gilt nur fuer den angemeldeten Benutzer
    RegistrySystem,          // HKLM ...\CurrentVersion\Run - gilt fuer alle Benutzer (braucht Adminrechte)
    AutostartOrdnerBenutzer, // Verknuepfungen im eigenen Autostart-Ordner
    AutostartOrdnerAlle,     // Verknuepfungen im Autostart-Ordner fuer alle Benutzer
    Aufgabenplanung          // Aufgabe mit "Bei Anmeldung"/"Beim Start"-Ausloeser
}

public static class AutostartQuelleExtensions
{
    /// <summary>Klartext fuer die Anzeige - kein Registry-Pfad-Kauderwelsch.</summary>
    public static string Klartext(this AutostartQuelle quelle) => quelle switch
    {
        AutostartQuelle.RegistryBenutzer => Loc.T("Registry (nur dein Benutzerkonto)", "Registry (your user account only)"),
        AutostartQuelle.RegistrySystem => Loc.T("Registry (alle Benutzer)", "Registry (all users)"),
        AutostartQuelle.AutostartOrdnerBenutzer => Loc.T("Autostart-Ordner (dein Benutzerkonto)", "Startup folder (your user account)"),
        AutostartQuelle.AutostartOrdnerAlle => Loc.T("Autostart-Ordner (alle Benutzer)", "Startup folder (all users)"),
        AutostartQuelle.Aufgabenplanung => Loc.T("Aufgabenplanung", "Task Scheduler"),
        _ => Loc.T("Unbekannt", "Unknown")
    };

    /// <summary>
    /// True, wenn zum Aendern Administratorrechte noetig sind (alles, was fuer ALLE
    /// Benutzer gilt). Wird in der Oberflaeche angekuendigt, damit die UAC-Abfrage
    /// niemanden ueberrascht.
    /// </summary>
    public static bool BrauchtAdminrechte(this AutostartQuelle quelle) => quelle switch
    {
        AutostartQuelle.RegistrySystem => true,
        AutostartQuelle.AutostartOrdnerAlle => true,
        AutostartQuelle.Aufgabenplanung => true,
        _ => false
    };
}

/// <summary>
/// Ein einzelnes Programm, das beim Hochfahren/Anmelden automatisch mitstartet.
/// Reines Datenmodell ohne Windows-Bezug, damit die Bewertungslogik
/// (<see cref="AutostartBewerter"/>) auf jedem System testbar bleibt.
/// </summary>
public class AutostartEintrag
{
    /// <summary>Anzeigename: Registry-Wertname, Dateiname der Verknuepfung oder Aufgabenname.</summary>
    public string Name { get; set; } = "";

    /// <summary>Die komplette Befehlszeile inkl. Parameter, so wie sie hinterlegt ist.</summary>
    public string Befehl { get; set; } = "";

    /// <summary>Nur die Programmdatei aus <see cref="Befehl"/>, Umgebungsvariablen bereits aufgeloest.</summary>
    public string ProgrammPfad { get; set; } = "";

    /// <summary>Dateiname ohne Endung, kleingeschrieben - Grundlage fuers Erkennen bekannter Programme.</summary>
    public string DateiName { get; set; } = "";

    public AutostartQuelle Quelle { get; set; }

    /// <summary>Der genaue Ort (Registry-Schluessel, Ordner- oder Aufgabenpfad) - fuer Nachvollziehbarkeit.</summary>
    public string QuelleDetail { get; set; } = "";

    /// <summary>False = bereits ausgeschaltet (Eintrag existiert noch, startet aber nicht mit).</summary>
    public bool IstAktiv { get; set; } = true;

    /// <summary>Wann der Eintrag ausgeschaltet wurde (falls Windows das vermerkt hat).</summary>
    public DateTime? DeaktiviertAm { get; set; }

    /// <summary>True, wenn die hinterlegte Programmdatei gar nicht mehr existiert (Karteileiche).</summary>
    public bool DateiFehlt { get; set; }

    /// <summary>Aus der Programmdatei ausgelesen, wenn vorhanden - hilft beim Einordnen unbekannter Eintraege.</summary>
    public string? Herausgeber { get; set; }

    /// <summary>Nur bei 32-Bit-Eintraegen auf 64-Bit-Windows (eigene Registry-Sicht) relevant.</summary>
    public bool Ist32BitSicht { get; set; }
}
