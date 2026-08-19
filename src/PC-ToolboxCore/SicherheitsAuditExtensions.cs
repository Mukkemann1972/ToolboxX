using PCToolbox.Models;

namespace PCToolboxCore;

public record ExtensionEintrag(
    string Name, string Browser, string Version, bool? Deaktiviert,
    IReadOnlyList<string> Berechtigungen, IReadOnlyList<string> HostBerechtigungen);

// Check 5 des Security-Audit-Moduls (s. Hermes-Vorschlag 19.08.2026): Browser-Erweiterungen
// auflisten + eine einzige, konservative Heuristik markieren (keine Cloud-Datenbank in V1).
// Riskant markiert wird nur die KOMBINATION aus einer besonders maechtigen Berechtigung (kann
// Netzwerkverkehr lesen/umleiten oder Downloads steuern) UND Zugriff auf ALLE Webseiten
// (<all_urls>) - jede der beiden Bedingungen allein ist fuer viele legitime Extensions normal
// (z.B. ein Werbeblocker braucht <all_urls>, ein Download-Manager braucht "downloads").
public static class SicherheitsAuditExtensions
{
    private static readonly string[] MaechtigeBerechtigungen = ["webRequest", "webRequestBlocking", "proxy", "downloads"];
    private const string AlleSeiten = "<all_urls>";

    public static bool WirktRiskant(ExtensionEintrag eintrag) =>
        eintrag.Berechtigungen.Any(p => MaechtigeBerechtigungen.Contains(p, StringComparer.OrdinalIgnoreCase))
        && eintrag.HostBerechtigungen.Any(h => h.Equals(AlleSeiten, StringComparison.OrdinalIgnoreCase));

    public static SicherheitsBefund Zusammenfassen(IReadOnlyList<ExtensionEintrag> extensions)
    {
        var titel = Loc.T("Browser-Erweiterungen", "Browser extensions");
        var riskant = extensions.Where(WirktRiskant).ToList();

        if (extensions.Count == 0)
            return new(titel, Loc.T(
                "Keine Browser-Erweiterungen gefunden (oder Chrome/Edge/Firefox nicht installiert bzw. keine Profile gefunden).",
                "No browser extensions found (or Chrome/Edge/Firefox not installed, or no profiles found)."),
                AmpelStatus.Gruen);

        if (riskant.Count > 0)
        {
            var namen = string.Join(", ", riskant.Take(5).Select(e => $"{e.Name} ({e.Browser})"));
            return new(titel, Loc.T(
                $"{extensions.Count} Erweiterung(en) gefunden, davon {riskant.Count} mit " +
                $"weitreichenden Berechtigungen (Netzwerkverkehr lesen/umleiten oder Downloads " +
                $"steuern, UND Zugriff auf alle Webseiten): {namen}. Das ist nicht automatisch " +
                "bösartig - z.B. Werbeblocker brauchen das -, aber bei einer Erweiterung, die du " +
                "nicht kennst oder nicht bewusst installiert hast, ist das ein Grund genauer " +
                "hinzuschauen (Browser → Erweiterungen → Details → Berechtigungen).",
                $"{extensions.Count} extension(s) found, {riskant.Count} of them with far-reaching " +
                $"permissions (can read/redirect network traffic or control downloads, AND access to " +
                $"all websites): {namen}. This isn't automatically malicious - e.g. ad blockers need " +
                "this -, but for an extension you don't recognize or didn't knowingly install, it's a " +
                "reason to look closer (browser → extensions → details → permissions)."),
                AmpelStatus.Gelb);
        }

        return new(titel, Loc.T(
            $"{extensions.Count} Erweiterung(en) gefunden, keine mit auffälliger Berechtigungs-Kombination.",
            $"{extensions.Count} extension(s) found, none with a suspicious permission combination."),
            AmpelStatus.Gruen);
    }
}
