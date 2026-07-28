// Bewusst im Namensraum "PCToolbox.Models" statt "PCToolboxCore.Models":
// Die Ampel ist das Herzstueck von Toolbox X und wird von fast jedem Modul genutzt.
// Sie ist am 26.07.2026 aus dem Windows-Projekt hierher in die plattformneutrale
// Bibliothek gewandert, damit auch Bewertungs-Logik im Kern (und damit echte,
// auf dem Pi lauffaehige Tests) die Ampel verwenden kann - z.B. AutostartBewerter.
// Der Namensraum blieb dabei absichtlich unveraendert, damit KEIN bestehendes
// Modul angefasst werden musste (erweitern statt ersetzen).
namespace PCToolbox.Models;

public enum AmpelStatus
{
    Gruen,
    Gelb,
    Rot,
    Unbekannt
}

public static class AmpelStatusExtensions
{
    public static string Symbol(this AmpelStatus status) => status switch
    {
        AmpelStatus.Gruen => "✅",
        AmpelStatus.Gelb => "⚠️",
        AmpelStatus.Rot => "❌",
        _ => "❔"
    };
}
