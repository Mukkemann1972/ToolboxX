namespace PCToolboxCore;

/// <summary>
/// Legacy-Windows-Werkzeuge (Free, Hermes' WinUtil-2026-Briefing 19.08.2026, "Legacy Windows
/// Panels"): kuratierte Sammlung von Direktzugriffen auf klassische Windows-Systemwerkzeuge,
/// die Microsoft in der modernen Einstellungen-App zunehmend versteckt. Zielgruppe laut
/// Hermes' Briefing: Repair-Café-Kunden/ältere Nutzer, die diese Werkzeuge nicht mehr finden -
/// deshalb bewusst KEINE tiefen IT-Profi-Werkzeuge (Gruppenrichtlinien, Zertifikatverwaltung,
/// Leistungsüberwachung) in der Liste, sondern die, die im Alltag/in der Werkstatt wirklich
/// gebraucht werden. "Lösungen statt Werte" (Hermes' Formulierung): jeder Eintrag erklärt in
/// einfacher Sprache, WOFÜR das Werkzeug da ist, nicht nur wie es heißt.
///
/// Rein lesend im Sinne von "Toolbox X ändert hier nichts selbst" - die Werkzeuge werden nur
/// gestartet, alles Weitere passiert in Windows' eigenen, altbekannten Dialogen.
/// </summary>
public record LegacyWerkzeug(string Id, string Name, string Erklaerung, string Befehl, string Argumente = "");

public static class LegacyWindowsWerkzeuge
{
    public static readonly IReadOnlyList<LegacyWerkzeug> Werkzeuge =
    [
        new("geraete-manager", Loc.T("Geräte-Manager", "Device Manager"),
            Loc.T(
                "Alle Hardware-Komponenten auf einen Blick - Grafikkarte, Netzwerkkarte, USB-Controller " +
                "und mehr. Ein gelbes Warndreieck zeigt sofort, welches Gerät ein Treiberproblem hat.",
                "All hardware components at a glance - graphics card, network adapter, USB controllers " +
                "and more. A yellow warning triangle immediately shows which device has a driver problem."),
            "devmgmt.msc"),

        new("datentraegerverwaltung", Loc.T("Datenträgerverwaltung", "Disk Management"),
            Loc.T(
                "Partitionen anlegen, löschen, verkleinern oder vergrößern, ein neues Laufwerk einrichten. " +
                "Deutlich mehr Kontrolle als der Windows-Explorer, aber auch riskanter - nur nutzen, wer " +
                "weiß, welches Laufwerk gemeint ist.",
                "Create, delete, shrink or extend partitions, set up a new drive. Noticeably more control " +
                "than Windows Explorer, but also riskier - only for use when you're sure which drive is meant."),
            "diskmgmt.msc"),

        new("ereignisanzeige", Loc.T("Ereignisanzeige", "Event Viewer"),
            Loc.T(
                "Das komplette Windows-Protokoll - jeder Absturz, jeder Fehler, jeder Systemstart wird hier " +
                "aufgezeichnet. Unser eigenes Security-Audit-Modul zeigt nur eine sicherheitsrelevante " +
                "Auswahl davon - hier steht wirklich alles.",
                "The complete Windows log - every crash, every error, every startup gets recorded here. " +
                "Our own Security Audit module only shows a security-relevant selection of this - here is " +
                "genuinely everything."),
            "eventvwr.msc"),

        new("computerverwaltung", Loc.T("Computerverwaltung", "Computer Management"),
            Loc.T(
                "Sammelwerkzeug: Geräte-Manager, Datenträgerverwaltung, Dienste und Ereignisanzeige in " +
                "einem einzigen Fenster mit Baumansicht - praktisch, wenn man zwischen mehreren dieser " +
                "Werkzeuge hin- und herspringen muss.",
                "A collection tool: Device Manager, Disk Management, Services and Event Viewer in a " +
                "single window with a tree view - handy when jumping between several of these tools."),
            "compmgmt.msc"),

        new("aufgabenplanung", Loc.T("Aufgabenplanung", "Task Scheduler"),
            Loc.T(
                "Zeigt alle geplanten Aufgaben, die automatisch zu bestimmten Zeiten oder Ereignissen " +
                "laufen - auch die, die Toolbox X selbst anlegt (z.B. für Backup & Sync oder den HDD-" +
                "Health-Monitor, sofern aktiviert). Hilfreich, um zu sehen, was sich sonst noch im " +
                "Hintergrund meldet.",
                "Shows all scheduled tasks that run automatically at certain times or events - including " +
                "the ones Toolbox X itself creates (e.g. for Backup & Sync or the HDD Health Monitor, if " +
                "enabled). Useful for seeing what else checks in in the background."),
            "taskschd.msc"),

        new("systemkonfiguration", Loc.T("Systemkonfiguration (msconfig)", "System Configuration (msconfig)"),
            Loc.T(
                "Startet Windows testweise im abgesicherten Modus oder mit reduziertem Systemstart - " +
                "klassischer erster Schritt, um einzugrenzen, ob ein Problem an einem Autostart-Programm " +
                "oder Treiber liegt.",
                "Test-start Windows in Safe Mode, or with a reduced startup - a classic first step to " +
                "narrow down whether a problem is caused by a startup program or a driver."),
            "msconfig.exe"),

        new("registrierungs-editor", Loc.T("Registrierungs-Editor", "Registry Editor"),
            Loc.T(
                "Direkter Zugriff auf die Windows-Registrierungsdatenbank - nur für erfahrene Nutzer, " +
                "falsche Änderungen können Windows unbenutzbar machen. Unser Registry-Kompendium erklärt " +
                "einzelne, ausgewählte Stellen darin in einfacher Sprache.",
                "Direct access to the Windows registry database - for experienced users only, wrong " +
                "changes can make Windows unusable. Our Registry Compendium explains selected individual " +
                "locations in it in plain language."),
            "regedit.exe"),

        new("systemeigenschaften", Loc.T("Systemeigenschaften", "System Properties"),
            Loc.T(
                "Computername ändern, Systemwiederherstellung ein-/ausschalten, Remotedesktop-Zugriff " +
                "erlauben, Leistungsoptionen - eine Sammlung grundlegender Systemeinstellungen, die es in " +
                "der modernen Einstellungen-App nur verstreut oder gar nicht gibt.",
                "Change the computer name, turn System Restore on/off, allow Remote Desktop access, " +
                "performance options - a collection of basic system settings that the modern Settings app " +
                "only has scattered around, or not at all."),
            "control.exe", "sysdm.cpl"),

        new("systemwiederherstellung", Loc.T("Systemwiederherstellung", "System Restore"),
            Loc.T(
                "Setzt Windows-Systemdateien und Einstellungen auf einen früheren Wiederherstellungspunkt " +
                "zurück, persönliche Dateien bleiben unangetastet. Guter erster Versuch, wenn ein Update " +
                "oder eine neue Software das System durcheinandergebracht hat.",
                "Resets Windows system files and settings to an earlier restore point, personal files stay " +
                "untouched. A good first attempt when an update or new software has thrown the system " +
                "off balance."),
            "rstrui.exe"),

        new("netzwerkverbindungen", Loc.T("Netzwerkverbindungen", "Network Connections"),
            Loc.T(
                "Alle Netzwerkadapter (WLAN, LAN, virtuelle Adapter) auf einen Blick, einzeln " +
                "deaktivierbar oder neu einrichtbar - hilfreich, wenn eine Netzwerkverbindung \"komisch\" " +
                "hängt und ein einfacher Neustart der Verbindung helfen könnte.",
                "All network adapters (Wi-Fi, LAN, virtual adapters) at a glance, can be disabled or " +
                "reconfigured individually - useful when a network connection is acting \"stuck\" and a " +
                "simple restart of the connection might help."),
            "control.exe", "ncpa.cpl"),

        new("energieoptionen", Loc.T("Energieoptionen", "Power Options"),
            Loc.T(
                "Legt fest, wann der Bildschirm oder der ganze PC in den Ruhemodus geht, und was die " +
                "Netz-/Einschalttaste bewirkt. Oft die Lösung, wenn ein PC \"von selbst\" einschläft oder " +
                "gar nicht erst richtig aufwacht.",
                "Controls when the screen or the whole PC goes to sleep, and what the power button does. " +
                "Often the fix when a PC falls asleep \"on its own\" or doesn't properly wake up at all."),
            "control.exe", "powercfg.cpl"),

        new("benutzerkonten-erweitert", Loc.T("Benutzerkonten (erweitert)", "User Accounts (advanced)"),
            Loc.T(
                "Automatische Anmeldung ohne Passworteingabe einrichten (z.B. für einen reinen Werkstatt-" +
                "PC), Kennwort eines Kontos ändern, Kontotyp anpassen - Funktionen, die in der modernen " +
                "Konten-Einstellungsseite fehlen.",
                "Set up automatic sign-in without a password prompt (e.g. for a dedicated workshop PC), " +
                "change an account's password, adjust the account type - features missing from the modern " +
                "Accounts settings page."),
            "control.exe", "userpasswords2"),

        new("farbverwaltung", Loc.T("Farbverwaltung", "Color Management"),
            Loc.T(
                "Farbprofile eines Monitors ansehen und wechseln (Hersteller- vs. generisches Profil) - " +
                "der erste Ansatzpunkt, wenn Farben auf einem Bildschirm sichtbar falsch aussehen " +
                "(zu warm, zu kalt, zu blass).",
                "View and switch a monitor's color profiles (manufacturer vs. generic profile) - the first " +
                "thing to check when colors on a screen visibly look wrong (too warm, too cool, too washed out)."),
            "colorcpl.exe"),

        new("ordneroptionen", Loc.T("Ordneroptionen", "Folder Options"),
            Loc.T(
                "Versteckte Dateien und Dateiendungen ein-/ausblenden - der direkte GUI-Weg zu den zwei " +
                "Einstellungen, die unser Registry-Kompendium unter \"Versteckte Dateien anzeigen\"/" +
                "\"Dateiendungen anzeigen\" erklärt.",
                "Show/hide hidden files and file extensions - the direct GUI path to the two settings our " +
                "Registry Compendium explains under \"Show hidden files\"/\"Show file extensions\"."),
            "control.exe", "folders"),

        new("datum-uhrzeit", Loc.T("Datum und Uhrzeit", "Date and Time"),
            Loc.T(
                "Uhrzeit, Zeitzone und automatische Internet-Synchronisierung einstellen. Eine falsche " +
                "Uhrzeit/Zeitzone kann übrigens echte Folgefehler auslösen - z.B. bei verschlüsselten " +
                "Verbindungen (HTTPS-Zertifikate gelten nur für einen bestimmten Zeitraum).",
                "Set the clock, time zone and automatic internet synchronization. An incorrect time/time " +
                "zone can actually cause real follow-on errors - e.g. with encrypted connections (HTTPS " +
                "certificates are only valid for a specific time window)."),
            "control.exe", "timedate.cpl"),

        new("firewall-erweitert", Loc.T("Windows-Firewall mit erweiterter Sicherheit", "Windows Firewall with Advanced Security"),
            Loc.T(
                "Die vollständigen, einzelnen Firewall-Regeln - welches Programm darf worüber " +
                "kommunizieren, eingehend wie ausgehend. Die normale Firewall-Einstellungsseite zeigt nur " +
                "einen groben Ein/Aus-Schalter, hier steckt die eigentliche Feinsteuerung.",
                "The complete, individual firewall rules - which program may communicate over what, both " +
                "inbound and outbound. The normal firewall settings page only shows a rough on/off switch - " +
                "the real fine-grained control is here."),
            "wf.msc"),
    ];

    public static IReadOnlyList<LegacyWerkzeug> Suchen(string suchtext)
    {
        if (string.IsNullOrWhiteSpace(suchtext)) return Werkzeuge;

        return Werkzeuge
            .Where(w => w.Name.Contains(suchtext, StringComparison.OrdinalIgnoreCase)
                     || w.Erklaerung.Contains(suchtext, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
