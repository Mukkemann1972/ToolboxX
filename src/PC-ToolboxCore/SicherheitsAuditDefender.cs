using PCToolbox.Models;

namespace PCToolboxCore;

// Bewertet den Windows-Defender-Status (Check 1 des Security-Audit-Moduls, s.
// Hermes-Vorschlag 19.08.2026). Nimmt bewusst nur die vier Rohwerte entgegen, die
// "Get-MpComputerStatus" liefert, statt selbst irgendwo nachzufragen - reine
// Bewertungslogik, Windows-seitige Abfrage liegt im Service.
public static class SicherheitsAuditDefender
{
    private static readonly string Titel = Loc.T("Virenschutz (Windows Defender)", "Antivirus (Windows Defender)");

    // null = konnte nicht ermittelt werden (z.B. Drittanbieter-AV meldet sich nicht bei
    // Defender, oder der Dienst antwortet nicht) - lieber ehrlich "unbekannt" als geraten.
    public static SicherheitsBefund Bewerten(bool? antivirusAktiv, bool? echtzeitschutzAktiv, int? signaturAlterTage, bool? manipulationsschutzAktiv)
    {
        if (antivirusAktiv is null)
            return new(Titel, Loc.T(
                "Konnte nicht ermittelt werden - entweder läuft ein Drittanbieter-Virenschutz, der " +
                "sich nicht bei Windows Defender meldet, oder der Sicherheitsdienst antwortet nicht.",
                "Could not be determined - either a third-party antivirus is running that doesn't " +
                "report to Windows Defender, or the security service isn't responding."), AmpelStatus.Gelb);

        if (antivirusAktiv == false)
            return new(Titel, Loc.T(
                "Kein aktiver Virenschutz gefunden. So wird das wieder aktiviert: Windows-Sicherheit " +
                "öffnen → Viren- & Bedrohungsschutz → prüfen, ob dort ein Schutzprogramm gemeldet " +
                "wird und aktiviert ist.",
                "No active antivirus found. To reactivate: open Windows Security → Virus & threat " +
                "protection → check whether a protection program is reported there and enabled."),
                AmpelStatus.Rot);

        if (echtzeitschutzAktiv == false)
            return new(Titel, Loc.T(
                "Virenschutz ist vorhanden, aber der Echtzeitschutz ist ausgeschaltet - neue " +
                "Bedrohungen werden dann nicht automatisch blockiert. So wird das wieder aktiviert: " +
                "Windows-Sicherheit → Viren- & Bedrohungsschutz → Einstellungen verwalten → " +
                "Echtzeitschutz einschalten.",
                "Antivirus is present, but real-time protection is turned off - new threats then " +
                "aren't blocked automatically. To reactivate: Windows Security → Virus & threat " +
                "protection → Manage settings → turn on real-time protection."),
                AmpelStatus.Rot);

        if (signaturAlterTage is > 14)
            return new(Titel, Loc.T(
                $"Virenschutz läuft, aber die Erkennungsdaten sind {signaturAlterTage} Tage alt - " +
                "neuere Bedrohungen werden dadurch eventuell nicht erkannt. So wird das aktualisiert: " +
                "Windows-Sicherheit → Viren- & Bedrohungsschutz → Nach Updates suchen.",
                $"Antivirus is running, but the detection data is {signaturAlterTage} days old - newer " +
                "threats might not be recognized as a result. To update: Windows Security → Virus & " +
                "threat protection → Check for updates."),
                AmpelStatus.Gelb);

        if (manipulationsschutzAktiv == false)
            return new(Titel, Loc.T(
                "Virenschutz und Echtzeitschutz sind aktiv, aber der Manipulationsschutz ist " +
                "ausgeschaltet - Schadsoftware könnte dann versuchen, den Virenschutz selbst " +
                "abzuschalten. So wird das aktiviert: Windows-Sicherheit → Viren- & Bedrohungsschutz " +
                "→ Einstellungen für Manipulationsschutz.",
                "Antivirus and real-time protection are active, but tamper protection is turned off - " +
                "malware could then try to disable the antivirus itself. To enable: Windows Security " +
                "→ Virus & threat protection → Tamper Protection settings."),
                AmpelStatus.Gelb);

        return new(Titel, Loc.T(
            "Virenschutz aktiv, Echtzeitschutz an, Erkennungsdaten aktuell, Manipulationsschutz " +
            "eingeschaltet - alles wie erwartet.",
            "Antivirus active, real-time protection on, detection data current, tamper protection " +
            "enabled - everything as expected."), AmpelStatus.Gruen);
    }
}
