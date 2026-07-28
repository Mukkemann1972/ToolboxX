using PCToolbox.Models;
using PCToolboxCore.Models;

namespace PCToolboxCore;

/// <summary>
/// Registry-Intelligenz (neues Free-Modul aus Hermes' 23.07-Liste): erklärt eine kuratierte
/// Auswahl echter Registry-Stellen, statt einen generischen Registry-Browser nachzubauen
/// (den gibt's mit regedit schon, und ohne Erklärung hilft er niemandem). Ausgewählt sind
/// Stellen, die 1) echte, wiederkehrende Verwirrung stiften und 2) sicherheitsrelevant sind -
/// dasselbe Muster wie das BIOS-Kompendium, nur für die Registry statt fürs BIOS.
///
/// Rein lesend, bewusst keine Bearbeitungsfunktion - "Free = lesend + erklärend" gilt auch
/// hier. Wer etwas ändern will, weiß nach der Erklärung, wonach er in den Windows-
/// Einstellungen oder in regedit selbst suchen muss.
/// </summary>
public static class RegistryKompendium
{
    public static readonly IReadOnlyList<RegistryEintrag> Eintraege =
    [
        new("dateiendungen", Loc.T("Dateiendungen anzeigen", "Show file extensions"),
            "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt",
            Loc.T(
                "Windows blendet Dateiendungen standardmäßig aus - \"Rechnung.pdf.exe\" sieht dann aus wie " +
                "\"Rechnung.pdf\". Genau dieser Trick steckt hinter vielen gefälschten E-Mail-Anhängen. " +
                "Eingeblendete Endungen sind einer der wirksamsten Basisschutz-Handgriffe überhaupt.",
                "Windows hides file extensions by default - \"Invoice.pdf.exe\" then looks like " +
                "\"Invoice.pdf\". This exact trick is behind many disguised email attachments. Showing " +
                "extensions is one of the most effective basic protections there is.")),

        new("versteckte-dateien", Loc.T("Versteckte Dateien anzeigen", "Show hidden files"),
            "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden",
            Loc.T(
                "Steuert, ob als \"versteckt\" markierte Dateien/Ordner im Explorer auftauchen. Nützlich zum " +
                "Fehlersuchen, aber kein Sicherheitsmerkmal - jedes Programm kann das Häkchen selbst setzen, " +
                "es ist keine echte Zugriffssperre.",
                "Controls whether files/folders marked \"hidden\" show up in Explorer. Useful for " +
                "troubleshooting, but not a security feature - any program can set that flag itself, it's " +
                "not a real access restriction.")),

        new("uac-stufe", Loc.T("Benutzerkontensteuerung (UAC)", "User Account Control (UAC)"),
            "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin",
            Loc.T(
                "Legt fest, wie streng Windows nachfragt, bevor ein Programm Admin-Rechte bekommt - genau die " +
                "Abfrage, die auch Toolbox X bei einzelnen Aktionen auslöst. Niedrigere Werte fragen seltener " +
                "nach, was bequemer, aber auch riskanter ist: Schadsoftware bekommt dann leichter Systemrechte, " +
                "ohne dass es auffällt.",
                "Controls how strictly Windows asks before a program gets admin rights - the exact prompt " +
                "Toolbox X itself triggers for individual actions. Lower values ask less often, which is more " +
                "convenient but also riskier: malware then gets system rights more easily without it being noticed.")),

        new("autologon", Loc.T("Automatische Anmeldung", "Automatic logon"),
            "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "AutoAdminLogon",
            Loc.T(
                "Wenn aktiviert, meldet sich Windows beim Start ohne Passworteingabe automatisch an einem " +
                "bestimmten Konto an. Bequem, aber: das Passwort liegt dafür meist als fast im Klartext " +
                "lesbarer Wert (\"DefaultPassword\") direkt daneben in derselben Registry-Stelle - wer " +
                "physischen Zugriff auf den PC hat, kann es auslesen.",
                "If enabled, Windows automatically logs into a specific account at startup without a " +
                "password prompt. Convenient, but: the password is usually stored right next to it in the " +
                "same registry location (\"DefaultPassword\") in an almost plain-text-readable value - anyone " +
                "with physical access to the PC can read it.")),

        new("remotedesktop", Loc.T("Remotedesktop", "Remote Desktop"),
            "HKLM", @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections",
            Loc.T(
                "Steuert, ob sich jemand per Remotedesktop (RDP) auf diesen PC verbinden darf. Nützlich für " +
                "Fernwartung, aber ein aktiviertes RDP ohne starkes Passwort ist ein beliebtes Einfallstor - " +
                "RDP-Bruteforce-Versuche aus dem Internet gehören zu den häufigsten Angriffen überhaupt auf " +
                "Windows-PCs mit offenem Zugang.",
                "Controls whether someone can connect to this PC via Remote Desktop (RDP). Useful for remote " +
                "support, but enabled RDP without a strong password is a popular entry point - RDP brute-force " +
                "attempts from the internet are among the most common attacks on Windows PCs with open access.")),

        new("autorun", Loc.T("Automatisches Ausführen von USB-Sticks", "AutoRun for USB drives"),
            "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun",
            Loc.T(
                "Steuert, ob Windows Programme von eingesteckten USB-Sticks/Wechseldatenträgern automatisch " +
                "startet. War über Jahre einer der häufigsten Wege, wie sich Schadsoftware über einen einfach " +
                "eingesteckten USB-Stick selbst verbreitet hat - moderne Windows-Versionen haben das " +
                "standardmäßig schon eingeschränkt.",
                "Controls whether Windows automatically runs programs from inserted USB drives/removable " +
                "media. For years this was one of the most common ways malware spread itself just by " +
                "plugging in a USB stick - modern Windows versions already restrict this by default.")),

        new("windows-info", Loc.T("Windows-Version im Detail", "Windows version in detail"),
            "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion",
            Loc.T(
                "Die genaue Versionsbezeichnung (z.B. \"23H2\"), oft präziser als das, was die Einstellungen-" +
                "App anzeigt - praktisch, wenn ein Fehlerbericht oder eine Anleitung nach der exakten Version fragt.",
                "The exact version label (e.g. \"23H2\"), often more precise than what the Settings app shows - " +
                "handy when a bug report or a guide asks for the exact version.")),
    ];

    public static RegistryBefund DeuteWert(string eintragId, object? rohwert) => eintragId switch
    {
        "dateiendungen" => rohwert switch
        {
            0 => new(Loc.T("Dateiendungen werden angezeigt - guter Zustand.", "File extensions are shown - good state."), AmpelStatus.Gruen),
            1 => new(Loc.T("Dateiendungen sind ausgeblendet.", "File extensions are hidden."), AmpelStatus.Gelb),
            null => new(Loc.T("Windows-Standard (Endungen ausgeblendet).", "Windows default (extensions hidden)."), AmpelStatus.Gelb),
            _ => Unbekannt()
        },

        "versteckte-dateien" => rohwert switch
        {
            1 => new(Loc.T("Versteckte Dateien werden angezeigt.", "Hidden files are shown."), AmpelStatus.Gruen),
            2 or null => new(Loc.T("Versteckte Dateien werden nicht angezeigt (Windows-Standard).", "Hidden files are not shown (Windows default)."), AmpelStatus.Unbekannt),
            _ => Unbekannt()
        },

        "uac-stufe" => rohwert switch
        {
            0 => new(Loc.T("Niedrigste Stufe - Admin-Aktionen laufen ohne Rückfrage durch. Nicht empfohlen.", "Lowest level - admin actions run without confirmation. Not recommended."), AmpelStatus.Rot),
            5 or 2 => new(Loc.T("Windows-Standardstufe - fragt bei Admin-Aktionen nach.", "Windows default level - asks for confirmation on admin actions."), AmpelStatus.Gruen),
            null => new(Loc.T("Windows-Standardstufe.", "Windows default level."), AmpelStatus.Gruen),
            _ => new(Loc.T("Angepasste Stufe.", "Customized level."), AmpelStatus.Gelb)
        },

        "autologon" => rohwert switch
        {
            "1" => new(Loc.T("Aktiviert - das Passwort liegt vermutlich lesbar in derselben Registry-Stelle.", "Enabled - the password is likely readable in the same registry location."), AmpelStatus.Rot),
            "0" or null => new(Loc.T("Deaktiviert - Windows fragt beim Start nach dem Passwort.", "Disabled - Windows asks for the password at startup."), AmpelStatus.Gruen),
            _ => Unbekannt()
        },

        "remotedesktop" => rohwert switch
        {
            0 => new(Loc.T("Aktiviert - Verbindungen von außen möglich. Nur mit starkem Passwort sinnvoll.", "Enabled - connections from outside are possible. Only sensible with a strong password."), AmpelStatus.Gelb),
            1 or null => new(Loc.T("Deaktiviert - kein Remotedesktop-Zugriff möglich.", "Disabled - no Remote Desktop access possible."), AmpelStatus.Gruen),
            _ => Unbekannt()
        },

        "autorun" => rohwert switch
        {
            255 => new(Loc.T("Automatisches Ausführen ist für alle Laufwerkstypen deaktiviert - guter Zustand.", "Auto-run is disabled for all drive types - good state."), AmpelStatus.Gruen),
            null => new(Loc.T("Windows-Standard (moderne Windows-Versionen schränken AutoRun bereits stark ein).", "Windows default (modern Windows versions already restrict AutoRun heavily)."), AmpelStatus.Gruen),
            _ => new(Loc.T("Automatisches Ausführen ist für manche Laufwerkstypen aktiv.", "Auto-run is active for some drive types."), AmpelStatus.Gelb)
        },

        "windows-info" => new(rohwert?.ToString() ?? Loc.T("Nicht ermittelbar.", "Could not be determined."), AmpelStatus.Unbekannt),

        _ => Unbekannt()
    };

    private static RegistryBefund Unbekannt() =>
        new(Loc.T("Wert nicht wie erwartet - keine Deutung möglich.", "Value not as expected - no interpretation possible."), AmpelStatus.Unbekannt);
}
