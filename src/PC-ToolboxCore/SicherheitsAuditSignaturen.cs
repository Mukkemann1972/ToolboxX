using PCToolbox.Models;

namespace PCToolboxCore;

public enum SignaturErgebnis { GueltigSigniert, UngueltigSigniert, Unsigniert, KeinPfadGefunden }

public record AppSignatur(string ProgrammName, string? ExePfad, SignaturErgebnis Ergebnis);

// Bewertet eine bereits eingesammelte Liste von Signatur-Ergebnissen (Check 3 des
// Security-Audit-Moduls, s. Hermes-Vorschlag 19.08.2026). Unsigniert ist bewusst NICHT
// automatisch "böse" - viele kleine Open-Source-Tools sind unsigniert (kostet Geld), nur eine
// UNGUELTIGE Signatur (z.B. abgelaufen/manipuliert) ist ein echtes Warnsignal.
public static class SicherheitsAuditSignaturen
{
    private static readonly string Titel = Loc.T("Unsignierte/ungültig signierte Programme", "Unsigned/invalidly signed programs");

    public static SicherheitsBefund Auswerten(IReadOnlyList<AppSignatur> apps)
    {
        var ungueltig = apps.Where(a => a.Ergebnis == SignaturErgebnis.UngueltigSigniert).ToList();
        var unsigniert = apps.Where(a => a.Ergebnis == SignaturErgebnis.Unsigniert).ToList();

        if (ungueltig.Count > 0)
        {
            var namen = string.Join(", ", ungueltig.Take(5).Select(a => a.ProgrammName));
            return new(Titel, Loc.T(
                $"{ungueltig.Count} Programm(e) mit UNGÜLTIGER Signatur (abgelaufen oder manipuliert) " +
                $"gefunden: {namen}. Das ist ein echtes Warnsignal - Signatur im Explorer prüfen " +
                "(Rechtsklick auf die .exe → Eigenschaften → Digitale Signaturen) und im Zweifel neu " +
                "vom Hersteller herunterladen.",
                $"{ungueltig.Count} program(s) with an INVALID signature (expired or tampered with) " +
                $"found: {namen}. This is a real warning sign - check the signature in Explorer " +
                "(right-click the .exe → Properties → Digital Signatures) and re-download from the " +
                "vendor if in doubt."), AmpelStatus.Rot);
        }

        if (unsigniert.Count > 0)
        {
            var namen = string.Join(", ", unsigniert.Take(5).Select(a => a.ProgrammName));
            return new(Titel, Loc.T(
                $"{unsigniert.Count} unsignierte(s) Programm(e) gefunden: {namen}. Unsigniert heißt " +
                "NICHT automatisch bösartig - viele kleine Open-Source-Tools sind unsigniert, weil " +
                "eine Signatur Geld kostet. Vorsicht ist vor allem bei unsignierten Programmen " +
                "geboten, an die du dich nicht erinnerst installiert zu haben.",
                $"{unsigniert.Count} unsigned program(s) found: {namen}. Unsigned does NOT " +
                "automatically mean malicious - many small open-source tools are unsigned because a " +
                "signature costs money. Be cautious mainly with unsigned programs you don't remember " +
                "installing."), AmpelStatus.Gelb);
        }

        return new(Titel, Loc.T(
            "Alle geprüften Programme sind gültig signiert.",
            "All checked programs are validly signed."), AmpelStatus.Gruen);
    }
}
