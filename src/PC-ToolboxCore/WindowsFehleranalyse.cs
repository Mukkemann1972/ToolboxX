namespace PCToolboxCore;

/// <summary>Reihenfolge der Werte ist absichtlich die Priorität (Kritisch zuerst) - wird direkt
/// zum Sortieren benutzt (enum-Werte aufsteigend = Dringlichkeit absteigend).</summary>
public enum FehlerSchweregrad { Kritisch, Fehler, Warnung }

/// <summary>Ein einzelnes, noch nicht gruppiertes Ereignisprotokoll-Ereignis.</summary>
public record FehlerRohEintrag(
    string Anbieter, int EreignisId, FehlerSchweregrad Schweregrad, string ProtokollName,
    DateTime Zeitpunkt, string Beschreibung);

/// <summary>Eine Gruppe gleichartiger Ereignisse (gleicher Anbieter+ID+Schweregrad+Protokoll) im
/// Zeitraum - Windows protokolliert denselben Fehler oft alle paar Minuten erneut, eine
/// unverdichtete Liste wäre bei hunderten Ereignissen pro Tag unlesbar.</summary>
public record FehlerGruppe(
    string Anbieter, int EreignisId, FehlerSchweregrad Schweregrad, string ProtokollName,
    int Anzahl, DateTime ErstesVorkommen, DateTime LetztesVorkommen, string BeispielText);

/// <summary>
/// Windows-Fehleranalyse (Free, kein Admin nötig - live geprüft: System/Application-Protokoll
/// sind, anders als Security, für normale Benutzer lesbar). Breiter als Modul 2 (Bluescreens,
/// nur Provider Microsoft-Windows-WER-SystemErrorReporting): liest System- UND
/// Application-Protokoll komplett, alle Kritisch/Fehler/Warnung-Einträge im gewählten Zeitraum.
/// Live geprüft (dieser Rechner): 666 Ereignisse allein in 24 Stunden - die Verdichtung nach
/// Anbieter+EreignisID ist deshalb kein Nice-to-have, sondern Voraussetzung für Lesbarkeit (ein
/// wiederkehrender NetBT-Namenskonflikt erzeugte hier allein ~150 Einzeleinträge in 24h).
/// </summary>
public static class WindowsFehleranalyse
{
    public static IReadOnlyList<FehlerGruppe> Gruppieren(IEnumerable<FehlerRohEintrag> rohEintraege) =>
        rohEintraege
            .GroupBy(e => (e.Anbieter, e.EreignisId, e.Schweregrad, e.ProtokollName))
            .Select(g => new FehlerGruppe(
                g.Key.Anbieter, g.Key.EreignisId, g.Key.Schweregrad, g.Key.ProtokollName,
                g.Count(),
                g.Min(e => e.Zeitpunkt),
                g.Max(e => e.Zeitpunkt),
                g.OrderByDescending(e => e.Zeitpunkt).First().Beschreibung))
            .OrderBy(g => g.Schweregrad)
            .ThenByDescending(g => g.LetztesVorkommen)
            .ToList();
}
