using System.Xml.Linq;

namespace PCToolboxCore;

/// <summary>Das, was uns an einer geplanten Aufgabe interessiert.</summary>
public record AufgabenInfo(
    bool StartetAutomatischMit,
    bool IstAktiviert,
    string Befehl,
    string Argumente,
    string? Autor,
    string? Beschreibung);

/// <summary>
/// Liest die XML-Dateien der Windows-Aufgabenplanung (C:\Windows\System32\Tasks).
///
/// Warum ueber die Dateien statt ueber schtasks.exe: Die Textausgabe von schtasks ist
/// uebersetzt ("Bei Anmeldung" / "At logon time"), man muesste also auf Sprachen raten.
/// Die XML-Struktur ist dagegen in jeder Windows-Sprache identisch - und sie laesst sich
/// hier auf dem Pi echt testen.
///
/// Die Element-Namen werden ohne Namensraum verglichen (LocalName), weil Microsoft den
/// Namensraum je nach Windows-Version unterschiedlich setzt oder ganz weglaesst.
/// </summary>
public static class AufgabenXmlParser
{
    public static AufgabenInfo? Auswerten(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;

        XDocument dokument;
        try
        {
            dokument = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var wurzel = dokument.Root;
        if (wurzel is null || wurzel.Name.LocalName != "Task") return null;

        var ausloeser = Kind(wurzel, "Triggers");
        var startetMit = ausloeser is not null && ausloeser.Elements().Any(IstStartAusloeser);

        // Fehlt die Angabe, ist die Aufgabe laut Windows-Vorgabe aktiv.
        var aktiviert = TextVon(Kind(wurzel, "Settings"), "Enabled") is not "false";

        var exec = Kind(wurzel, "Actions")?.Elements().FirstOrDefault(e => e.Name.LocalName == "Exec");
        var befehl = TextVon(exec, "Command") ?? "";
        var argumente = TextVon(exec, "Arguments") ?? "";

        var info = Kind(wurzel, "RegistrationInfo");

        return new AufgabenInfo(
            startetMit,
            aktiviert,
            befehl.Trim(),
            argumente.Trim(),
            TextVon(info, "Author"),
            TextVon(info, "Description"));
    }

    /// <summary>
    /// Zaehlt nur, was wirklich beim Hochfahren/Anmelden losgeht - nicht taegliche
    /// oder wiederkehrende Aufgaben (die gehoeren nicht in ein Autostart-Modul).
    /// Ein Ausloeser, der selbst abgeschaltet ist, zaehlt ebenfalls nicht.
    /// </summary>
    private static bool IstStartAusloeser(XElement element)
    {
        var name = element.Name.LocalName;
        if (name is not ("LogonTrigger" or "BootTrigger")) return false;

        return TextVon(element, "Enabled") is not "false";
    }

    private static XElement? Kind(XElement? eltern, string name) =>
        eltern?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static string? TextVon(XElement? eltern, string kindName) =>
        Kind(eltern, kindName)?.Value.Trim();
}
