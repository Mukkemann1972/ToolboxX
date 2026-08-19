using PCToolbox.Models;

namespace PCToolboxCore;

public record ZertifikatEintrag(string Aussteller, string Subjekt, DateTime? Ablaufdatum);

// Check 4 des Security-Audit-Moduls (s. Hermes-Vorschlag 19.08.2026): listet vertrauenswürdige
// Root-Zertifikate auf. Bewusst OHNE automatische Ampel-Bewertung in V1 - welche Root-CAs auf
// einem Windows-System normal sind, unterscheidet sich je nach Windows-Version/Hersteller-
// Vorinstallation zu stark, um eine verlässliche "bekannte CA"-Liste zu pflegen. Lieber ehrlich
// nur zeigen+erklären als eine falsche Sicherheit vortäuschen. Status bleibt daher immer
// "Unbekannt" (= informativ, keine Wertung) statt geraten Grün/Gelb/Rot zu vergeben.
public static class SicherheitsAuditZertifikate
{
    private static readonly string Titel = Loc.T("Vertrauenswürdige Root-Zertifikate", "Trusted root certificates");

    public static SicherheitsBefund Zusammenfassen(IReadOnlyList<ZertifikatEintrag> zertifikate) => new(
        Titel,
        Loc.T(
            $"{zertifikate.Count} vertrauenswürdige Root-Zertifikate gefunden (reine Auflistung, " +
            "keine automatische Bewertung - siehe Erklärung). Ein Root-Zertifikat bestimmt, wem " +
            "Windows grundsätzlich vertraut (z.B. für sichere Webseiten). Ein zusätzliches, " +
            "unbekanntes Root-Zertifikat kann bedeuten, dass eine Firmensoftware (z.B. " +
            "Antivirus/Firmen-Proxy) das absichtlich installiert hat - oder ein Warnsignal sein, " +
            "wenn du es nicht zuordnen kannst. Nicht selbst entfernen, wenn unsicher: erst prüfen, " +
            "welche Software das installiert haben könnte.",
            $"{zertifikate.Count} trusted root certificates found (plain listing, no automatic " +
            "assessment - see explanation). A root certificate determines what Windows fundamentally " +
            "trusts (e.g. for secure websites). An extra, unfamiliar root certificate can mean some " +
            "company software (e.g. antivirus/corporate proxy) installed it on purpose - or be a " +
            "warning sign if you can't place it. Don't remove it yourself if unsure: first check what " +
            "software might have installed it."),
        AmpelStatus.Unbekannt);
}
