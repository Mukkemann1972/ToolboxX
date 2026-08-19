namespace PCToolboxCore;

/// <summary>Reihenfolge ist absichtlich die Priorität (Absturz zuerst).</summary>
public enum ZuverlaessigkeitSchweregrad { Absturz, Fehler, Info }

/// <summary>Ein einzelner Eintrag aus Win32_ReliabilityRecords, bereits klassifiziert.</summary>
public record ZuverlaessigkeitEintrag(
    DateTime Zeitpunkt, string Quelle, string ProduktName, string Nachricht,
    ZuverlaessigkeitSchweregrad Schweregrad);

/// <summary>Tageszusammenfassung für die Zeitachse - ein Tag kann mehrere Ereignisse jeder
/// Kategorie haben, die Kacheln zeigen nur die Zählung, Details stehen in der Liste darunter.</summary>
public record ZuverlaessigkeitTag(DateOnly Tag, int AnzahlAbsturz, int AnzahlFehler, int AnzahlInfo)
{
    public int Gesamt => AnzahlAbsturz + AnzahlFehler + AnzahlInfo;
}

/// <summary>
/// Zuverlässigkeitsverlauf (Modul 40, Free): Zeitachse, wann Abstürze/fehlgeschlagene
/// Updates/Treiberfehler auftraten - basiert auf Win32_ReliabilityRecords (derselben Quelle wie
/// Windows' eigener "Zuverlässigkeitsverlauf").
///
/// Live an diesem Rechner geprüft: <c>ReliabilityRecordType</c> ist bei allen 273 vorhandenen
/// Einträgen leer - dieses Feld ist trotz Microsoft-Dokumentation nicht verlässlich befüllt und
/// wird deshalb NICHT zur Klassifizierung benutzt. Stattdessen wird über Quelle (SourceName) und,
/// bei Windows Update, die dafür standardisierte Event-ID klassifiziert (19 = Erfolg, 20 =
/// fehlgeschlagen - dokumentierte, sprachunabhängige IDs, kein Text-Pattern-Matching nötig). Bei
/// MSI-Installationen wird zusätzlich der im Nachrichtentext enthaltene Erfolgsstatus ausgewertet
/// ("Erfolg- bzw. Fehlerstatus der Installation: N" - 0 = Erfolg, alles andere = Fehler, Standard-
/// MSI-Konvention). "Application Error"/"Application Hang" sind auf diesem Rechner die einzigen
/// echten Absturz-Quellen (8 bzw. 4 von 273 Einträgen) - Treiberfehler zeigen sich hier nicht
/// gesondert, werden aber über das Schlüsselwort "Treiber" im Nachrichtentext als Fehler erkannt.
/// </summary>
public static class ZuverlaessigkeitsVerlauf
{
    public static ZuverlaessigkeitSchweregrad Klassifizieren(string quelle, int eventId, string nachricht)
    {
        if (quelle is "Application Error" or "Application Hang")
            return ZuverlaessigkeitSchweregrad.Absturz;

        if (quelle == "Microsoft-Windows-WindowsUpdateClient")
            return eventId == 20 ? ZuverlaessigkeitSchweregrad.Fehler : ZuverlaessigkeitSchweregrad.Info;

        if (quelle == "MsiInstaller")
        {
            var index = nachricht.IndexOf("Installation:", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var rest = nachricht[(index + "Installation:".Length)..].TrimStart();
                var ziffern = new string(rest.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(ziffern, out var status) && status != 0)
                    return ZuverlaessigkeitSchweregrad.Fehler;
            }
            return ZuverlaessigkeitSchweregrad.Info;
        }

        if (nachricht.Contains("fehlgeschlagen", StringComparison.OrdinalIgnoreCase) ||
            nachricht.Contains("Treiber", StringComparison.OrdinalIgnoreCase))
            return ZuverlaessigkeitSchweregrad.Fehler;

        return ZuverlaessigkeitSchweregrad.Info;
    }

    /// <summary>Neueste Tage zuerst, damit das aktuelle Bild sofort sichtbar ist.</summary>
    public static IReadOnlyList<ZuverlaessigkeitTag> NachTagGruppieren(
        IEnumerable<ZuverlaessigkeitEintrag> eintraege) =>
        eintraege
            .GroupBy(e => DateOnly.FromDateTime(e.Zeitpunkt))
            .Select(g => new ZuverlaessigkeitTag(
                g.Key,
                g.Count(e => e.Schweregrad == ZuverlaessigkeitSchweregrad.Absturz),
                g.Count(e => e.Schweregrad == ZuverlaessigkeitSchweregrad.Fehler),
                g.Count(e => e.Schweregrad == ZuverlaessigkeitSchweregrad.Info)))
            .OrderByDescending(t => t.Tag)
            .ToList();
}
