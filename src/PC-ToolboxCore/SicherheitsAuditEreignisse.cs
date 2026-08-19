using PCToolbox.Models;

namespace PCToolboxCore;

public enum EreignisKategorie
{
    FehlgeschlageneAnmeldungen, // 4625
    NeuesKonto,                 // 4720
    LogGeloescht,                // 1102
    DienstAbsturz,               // 7031/7034
    UnerwarteterNeustart,        // Kernel-Power 41
    UnsauberesHerunterfahren,    // 6008
    AnwendungsAbsturz            // 1000/1002
}

public record EreignisEintrag(DateTime Zeitpunkt, string Quelle, string Beschreibung);

// Bewertet eine bereits eingesammelte Liste von Ereignissen EINER Kategorie (Check 2 des
// Security-Audit-Moduls, s. Hermes-Vorschlag 19.08.2026, letzte 14 Tage). Die eigentliche
// Event-Log-Abfrage liegt im Service (Windows-spezifisch, EventLogReader) - hier nur die
// reine "was bedeutet diese Anzahl"-Logik, damit sie auf dem Pi testbar bleibt.
public static class SicherheitsAuditEreignisse
{
    public static SicherheitsBefund Bewerten(EreignisKategorie kategorie, IReadOnlyList<EreignisEintrag> eintraege)
    {
        var anzahl = eintraege.Count;
        var (titel, status, text) = kategorie switch
        {
            EreignisKategorie.FehlgeschlageneAnmeldungen => Bewerte(anzahl,
                Loc.T("Fehlgeschlagene Anmeldungen", "Failed logons"),
                gruen: Loc.T("Keine fehlgeschlagenen Anmeldeversuche in den letzten 14 Tagen.",
                              "No failed logon attempts in the last 14 days."),
                gelb: Loc.T(
                    $"{anzahl} fehlgeschlagene Anmeldeversuche in 14 Tagen - das ist oft harmlos " +
                    "(z.B. ein WLAN-Drucker oder NAS mit falsch gespeichertem Passwort, das " +
                    "wiederholt versucht sich anzumelden). Nur bei vielen Versuchen von unbekannten " +
                    "Quellen in kurzer Zeit ernst nehmen.",
                    $"{anzahl} failed logon attempts in 14 days - often harmless (e.g. a Wi-Fi printer " +
                    "or NAS with a wrong saved password repeatedly trying to log in). Only take it " +
                    "seriously if there are many attempts from unknown sources in a short time."),
                rot: Loc.T(
                    $"{anzahl} fehlgeschlagene Anmeldeversuche in 14 Tagen - deutlich mehr als " +
                    "üblich. Kann auf einen Brute-Force-Versuch hindeuten. Prüfen, wer/was diese " +
                    "Anmeldungen versucht (Ereignisanzeige → Sicherheit → Ereignis-ID 4625).",
                    $"{anzahl} failed logon attempts in 14 days - notably more than usual. Can indicate " +
                    "a brute-force attempt. Check who/what is attempting these logons (Event Viewer → " +
                    "Security → Event ID 4625)."),
                gelbAb: 1, rotAb: 21),

            EreignisKategorie.NeuesKonto => Bewerte(anzahl,
                Loc.T("Neu angelegte Konten", "Newly created accounts"),
                gruen: Loc.T("Kein neues Benutzerkonto in den letzten 14 Tagen angelegt.",
                              "No new user account created in the last 14 days."),
                gelb: Loc.T(
                    $"{anzahl} neue Benutzerkonto(s) in 14 Tagen angelegt - meist unbedenklich (z.B. " +
                    "eigene Neueinrichtung), aber wenn du das nicht selbst warst: Konten unter " +
                    "Einstellungen → Konten → Familie und andere Benutzer prüfen.",
                    $"{anzahl} new user account(s) created in 14 days - usually harmless (e.g. your own " +
                    "setup), but if it wasn't you: check accounts under Settings → Accounts → Family " +
                    "and other users."),
                rot: "", gelbAb: 1, rotAb: int.MaxValue),

            EreignisKategorie.LogGeloescht => Bewerte(anzahl,
                Loc.T("Ereignisprotokoll gelöscht", "Event log cleared"),
                gruen: Loc.T("Das Ereignisprotokoll wurde nicht manuell gelöscht.",
                              "The event log has not been manually cleared."),
                gelb: "", rot: Loc.T(
                    $"Das Ereignisprotokoll wurde {anzahl}x gelöscht - das ist eine bekannte Technik, " +
                    "um Spuren zu verwischen, kommt aber auch bei manueller Wartung vor. Falls das " +
                    "nicht du warst, ernst nehmen.",
                    $"The event log was cleared {anzahl}x - a known technique to cover tracks, but also " +
                    "happens during manual maintenance. If it wasn't you, take it seriously."),
                gelbAb: int.MaxValue, rotAb: 1),

            EreignisKategorie.DienstAbsturz => Bewerte(anzahl,
                Loc.T("Dienstabstürze", "Service crashes"),
                gruen: Loc.T("Keine Dienstabstürze in den letzten 14 Tagen.",
                              "No service crashes in the last 14 days."),
                gelb: Loc.T($"{anzahl} Dienstabstürze in 14 Tagen - vereinzelt normal, bei einem " +
                             "bestimmten Dienst wiederholt eher ein Hinweis auf ein Problem mit dem.",
                             $"{anzahl} service crashes in 14 days - occasional is normal, repeated for " +
                             "the same service points more toward an issue with it."),
                rot: Loc.T($"{anzahl} Dienstabstürze in 14 Tagen - ungewöhnlich häufig, lohnt einen " +
                            "Blick in die Ereignisanzeige, welcher Dienst betroffen ist.",
                            $"{anzahl} service crashes in 14 days - unusually frequent, worth a look in " +
                            "Event Viewer to see which service is affected."),
                gelbAb: 1, rotAb: 6),

            EreignisKategorie.UnerwarteterNeustart => Bewerte(anzahl,
                Loc.T("Unerwartete Neustarts", "Unexpected restarts"),
                gruen: Loc.T("Keine unerwarteten Neustarts in den letzten 14 Tagen.",
                              "No unexpected restarts in the last 14 days."),
                gelb: Loc.T($"{anzahl} unerwartete(r) Neustart(s) in 14 Tagen - kann Netzteil, " +
                             "Überhitzung oder ein Treiberproblem sein, siehe Temperatur-Modul.",
                             $"{anzahl} unexpected restart(s) in 14 days - can be a power supply, " +
                             "overheating, or driver issue, see the Temperature module."),
                rot: Loc.T($"{anzahl} unerwartete Neustarts in 14 Tagen - deutlich gehäuft, auf " +
                            "Hardware-Ursache prüfen (Temperatur-Modul, Netzteil).",
                            $"{anzahl} unexpected restarts in 14 days - notably frequent, check for a " +
                            "hardware cause (Temperature module, power supply)."),
                gelbAb: 1, rotAb: 4),

            EreignisKategorie.UnsauberesHerunterfahren => Bewerte(anzahl,
                Loc.T("Unsauberes Herunterfahren", "Unclean shutdowns"),
                gruen: Loc.T("Kein unsauberes Herunterfahren in den letzten 14 Tagen.",
                              "No unclean shutdown in the last 14 days."),
                gelb: Loc.T($"{anzahl}x unsauber heruntergefahren in 14 Tagen - vereinzelt normal " +
                             "(z.B. Stromausfall).", $"{anzahl}x shut down uncleanly in 14 days - " +
                             "occasional is normal (e.g. power outage)."),
                rot: Loc.T($"{anzahl}x unsauber heruntergefahren in 14 Tagen - gehäuft, kann auf " +
                            "Instabilität oder Stromversorgungsproblem hindeuten.",
                            $"{anzahl}x shut down uncleanly in 14 days - frequent, can indicate " +
                            "instability or a power issue."),
                gelbAb: 1, rotAb: 4),

            EreignisKategorie.AnwendungsAbsturz => Bewerte(anzahl,
                Loc.T("Anwendungsabstürze", "Application crashes"),
                gruen: Loc.T("Keine Anwendungsabstürze in den letzten 14 Tagen.",
                              "No application crashes in the last 14 days."),
                gelb: Loc.T($"{anzahl} Anwendungsabstürze in 14 Tagen - im normalen Rahmen.",
                             $"{anzahl} application crashes in 14 days - within a normal range."),
                rot: Loc.T($"{anzahl} Anwendungsabstürze in 14 Tagen - ungewöhnlich viele, lohnt " +
                            "einen Blick, ob immer dasselbe Programm betroffen ist.",
                            $"{anzahl} application crashes in 14 days - unusually many, worth checking " +
                            "whether it's always the same program."),
                gelbAb: 1, rotAb: 11),

            _ => (Loc.T("Unbekannt", "Unknown"), AmpelStatus.Unbekannt, "")
        };

        return new SicherheitsBefund(titel, text, status);
    }

    private static (string, AmpelStatus, string) Bewerte(int anzahl, string titel, string gruen, string gelb, string rot, int gelbAb, int rotAb)
    {
        if (anzahl >= rotAb) return (titel, AmpelStatus.Rot, rot);
        if (anzahl >= gelbAb) return (titel, AmpelStatus.Gelb, gelb);
        return (titel, AmpelStatus.Gruen, gruen);
    }
}
