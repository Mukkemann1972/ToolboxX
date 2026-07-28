using PCToolboxCore.Models;

namespace PCToolboxCore;

/// <summary>Eine Regel fuer ein bekanntes Autostart-Programm.</summary>
/// <param name="DateiNamen">Programmdateien ohne Endung, kleingeschrieben.</param>
public record AutostartRegel(
    string[] DateiNamen,
    string Anzeige,
    AutostartKategorie Kategorie,
    string WasEsIst,
    string Empfehlung);

/// <summary>
/// Katalog bekannter Autostart-Programme. Bewusst handverlesen und erklaert statt
/// einer riesigen Datenbank aus dem Netz: Was hier steht, koennen wir auch begruenden.
/// Alles, was nicht drinsteht, wird ehrlich als "kennen wir nicht" angezeigt und
/// NICHT wild geraten (s. AutostartBewerter).
/// </summary>
public static class AutostartRegeln
{
    public static readonly IReadOnlyList<AutostartRegel> Alle =
    [
        // === Windows selbst / Treiber / Sicherheit =======================================
        new(["securityhealthsystray", "securityhealthservice", "msascuil", "msmpeng"],
            Loc.T("Windows-Sicherheit", "Windows Security"),
            AutostartKategorie.Systemnah,
            Loc.T("Gehört zum Virenschutz von Windows und zeigt dessen Symbol unten rechts an.",
                  "Part of Windows' built-in antivirus protection, shows its icon at the bottom right."),
            Loc.T("Bitte anlassen. Das ist der eingebaute Schutz deines Windows - ohne ihn ist der PC ungeschützt.",
                  "Please leave it on. This is your Windows' built-in protection - without it the PC is unprotected.")),

        new(["rthdvcpl", "rtkaudservice", "rtkngui64", "ravcpl64", "rtkauduservice64"],
            Loc.T("Realtek-Audio", "Realtek audio"),
            AutostartKategorie.Systemnah,
            Loc.T("Bedienprogramm für den eingebauten Soundchip (Lautstärke, Buchsen-Erkennung).",
                  "Control program for the built-in sound chip (volume, jack detection)."),
            Loc.T("Anlassen. Ohne dieses Programm kann es passieren, dass Kopfhörer- oder Mikrofonbuchsen nicht mehr richtig erkannt werden.",
                  "Leave it on. Without this program, headphone or microphone jacks may no longer be detected correctly.")),

        new(["igfxtray", "igfxpers", "igfxhk", "igfxem"],
            Loc.T("Intel-Grafik", "Intel graphics"),
            AutostartKategorie.Systemnah,
            Loc.T("Zubehör zum Intel-Grafiktreiber (Bildschirm-Einstellungen, Tastenkürzel).",
                  "Accessory to the Intel graphics driver (display settings, keyboard shortcuts)."),
            Loc.T("Anlassen. Es bremst kaum und gehört zum Grafiktreiber.",
                  "Leave it on. It barely slows things down and belongs to the graphics driver.")),

        new(["syntpenh", "etdctrl", "apoint", "synaptics", "elanwrapper"],
            Loc.T("Touchpad-Treiber", "Touchpad driver"),
            AutostartKategorie.Systemnah,
            Loc.T("Steuert das Touchpad eines Notebooks - unter anderem Gesten und das Abschalten beim Tippen.",
                  "Controls a notebook's touchpad - including gestures and disabling it while typing."),
            Loc.T("Anlassen. Ohne dieses Programm funktionieren Touchpad-Gesten meist nicht mehr.",
                  "Leave it on. Without this program, touchpad gestures usually stop working.")),

        new(["hkcmd", "dptf", "ipfsvc"],
            Loc.T("Hersteller-Systemdienst", "Manufacturer system service"),
            AutostartKategorie.Systemnah,
            Loc.T("Kleines Hilfsprogramm des Chip- oder Notebook-Herstellers (z.B. Sondertasten, Lüfter-Regelung).",
                  "Small helper program from the chip or notebook manufacturer (e.g. special keys, fan control)."),
            Loc.T("Anlassen. Es kostet fast keine Startzeit und gehört zur Hardware deines Geräts.",
                  "Leave it on. It costs almost no startup time and belongs to your device's hardware.")),

        // === Läuft bewusst mit, weil es sonst seinen Zweck verfehlt ======================
        new(["onedrive"],
            "Microsoft OneDrive",
            AutostartKategorie.Nuetzlich,
            Loc.T("Gleicht deine Dateien mit der Microsoft-Cloud ab.",
                  "Syncs your files with the Microsoft cloud."),
            Loc.T("Wenn du OneDrive nutzt: anlassen, sonst wird nichts mehr gesichert. Wenn du damit gar nichts anfangen kannst: ausschalten ist gefahrlos - deine Dateien bleiben trotzdem auf dem PC liegen.",
                  "If you use OneDrive: leave it on, otherwise nothing gets backed up anymore. If you have no use for it: turning it off is safe - your files stay on the PC regardless.")),

        new(["dropbox", "megasync", "nextcloud", "googledrivefs", "googledrivesync", "pcloud", "syncthing"],
            Loc.T("Cloud-Abgleich", "Cloud sync"),
            AutostartKategorie.Nuetzlich,
            Loc.T("Hält einen Ordner auf deinem PC mit einem Online-Speicher gleich.",
                  "Keeps a folder on your PC in sync with an online storage service."),
            Loc.T("Anlassen, wenn deine Dateien laufend gesichert werden sollen - ein Abgleich, der nicht läuft, sichert auch nichts. Ausschalten ist trotzdem gefahrlos: Es geht nichts verloren, es wird nur nicht mehr automatisch abgeglichen.",
                  "Leave it on if your files should be continuously backed up - a sync that isn't running backs up nothing. Turning it off is still safe: nothing is lost, it just stops syncing automatically.")),

        new(["bestsync", "freefilesync", "syncbackfree", "goodsync"],
            Loc.T("Sicherung / Abgleich", "Backup / sync"),
            AutostartKategorie.Nuetzlich,
            Loc.T("Kopiert oder spiegelt deine Dateien nach Plan (Backup-Programm).",
                  "Copies or mirrors your files on a schedule (backup program)."),
            Loc.T("Anlassen, wenn die Sicherung automatisch laufen soll. Schaltest du es aus, läuft dein Backup nur noch, wenn du es von Hand startest - das wird gern vergessen.",
                  "Leave it on if the backup should run automatically. If you turn it off, your backup only runs when you start it manually - which is easy to forget.")),

        new(["ccc", "amdow", "nvcontainer", "nvdisplay"],
            Loc.T("Grafikkarten-Zubehör", "Graphics card accessory"),
            AutostartKategorie.Nuetzlich,
            Loc.T("Zusatzprogramm des Grafikkartenherstellers (Einstellungen, Aufnahme-Funktionen, Spielprofile).",
                  "Extra program from the graphics card manufacturer (settings, recording features, game profiles)."),
            Loc.T("Der Treiber selbst braucht das nicht - es geht um Zusatzfunktionen. Anlassen, wenn du sie nutzt, sonst gefahrlos aus. Das Bild bleibt in jedem Fall.",
                  "The driver itself doesn't need this - it's about extra features. Leave it on if you use them, otherwise turning it off is safe. The display keeps working either way.")),

        // === Komfort, geht aber auch ohne ===============================================
        new(["steam", "epicgameslauncher", "battle.net", "galaxyclient", "upc", "ubisoftconnect", "riotclientservices", "eadesktop", "origin"],
            Loc.T("Spiele-Plattform", "Game platform"),
            AutostartKategorie.Optional,
            Loc.T("Startet den Laden/Launcher für deine Spiele schon beim Hochfahren mit.",
                  "Starts the store/launcher for your games already at boot."),
            Loc.T("Kannst du gefahrlos ausschalten. Deine Spiele bleiben installiert - der Launcher startet dann eben erst, wenn du ihn anklickst. Das spart oft spürbar Startzeit.",
                  "Safe to turn off. Your games stay installed - the launcher just starts when you click it instead. This often noticeably saves startup time.")),

        new(["discord", "spotify", "teams", "ms-teams", "skype", "whatsapp", "telegram", "signal", "slack", "zoom"],
            Loc.T("Chat / Musik / Videokonferenz", "Chat / music / video conferencing"),
            AutostartKategorie.Optional,
            Loc.T("Meldet dich schon beim Hochfahren an, damit du sofort erreichbar bist.",
                  "Signs you in already at boot so you're immediately reachable."),
            Loc.T("Nur nötig, wenn du sofort nach dem Einschalten erreichbar sein willst. Sonst gefahrlos ausschalten - das Programm funktioniert danach ganz normal, wenn du es selbst öffnest.",
                  "Only needed if you want to be reachable right after powering on. Otherwise safe to turn off - the program works completely normally once you open it yourself.")),

        new(["icue", "lghub", "armourycrate", "msicenter", "aisuite", "openrgb", "synapse", "razer"],
            Loc.T("Beleuchtung / Tuning-Zubehör", "Lighting / tuning accessory"),
            AutostartKategorie.Optional,
            Loc.T("Steuert RGB-Beleuchtung, Maustasten-Belegung oder Lüfterprofile.",
                  "Controls RGB lighting, mouse button assignments, or fan profiles."),
            Loc.T("Wenn du Sondertasten oder Lüfterprofile eingestellt hast: anlassen, sonst gelten sie nicht. Sonst gefahrlos aus - solche Programme sind dafür bekannt, viel Startzeit zu kosten.",
                  "If you've set up special keys or fan profiles: leave it on, otherwise they won't apply. Otherwise safe to turn off - these programs are known for costing a lot of startup time.")),

        new(["creative cloud", "adobe desktop service", "adobeipcbroker"],
            "Adobe Creative Cloud",
            AutostartKategorie.Optional,
            Loc.T("Verwaltungsprogramm für Photoshop & Co. (Anmeldung, Installationen, Schriften).",
                  "Management program for Photoshop & co. (sign-in, installations, fonts)."),
            Loc.T("Zum Arbeiten mit den Adobe-Programmen wird es nur beim Start der Programme selbst gebraucht. Ausschalten ist gefahrlos - falls eine Adobe-Anwendung es doch braucht, startet sie es selbst nach.",
                  "It's only needed when the Adobe programs themselves start. Turning it off is safe - if an Adobe application does need it, it will start it itself.")),

        new(["iastoricon", "iastordatamgrsvc"],
            Loc.T("Intel-Speicher-Symbol", "Intel storage icon"),
            AutostartKategorie.Optional,
            Loc.T("Zeigt ein Symbol für den Intel-Festplattenverbund an. Der Verbund selbst läuft im Treiber, nicht in diesem Programm.",
                  "Shows an icon for the Intel drive array. The array itself runs in the driver, not in this program."),
            Loc.T("Nur ein Anzeige-Symbol - ausschalten ändert nichts an deinen Laufwerken. Wenn du einen RAID-Verbund betreibst und Warnungen sehen willst, lass es an.",
                  "Just a display icon - turning it off changes nothing about your drives. If you run a RAID array and want to see warnings, leave it on.")),

        // === Reine Bremsen ==============================================================
        new(["adobearm", "acrotray", "adobeaam", "readerupdater"],
            Loc.T("Adobe-Updateprüfer", "Adobe update checker"),
            AutostartKategorie.Bremse,
            Loc.T("Prüft im Hintergrund, ob es eine neue Version des Adobe Readers gibt.",
                  "Checks in the background whether a new version of Adobe Reader is available."),
            Loc.T("Kannst du ausschalten. Der Reader prüft auch selbst nach Updates, wenn du ihn öffnest - dieser Dauerläufer im Hintergrund bringt dir nichts.",
                  "Safe to turn off. The Reader also checks for updates itself when you open it - this permanent background process gets you nothing.")),

        new(["jusched", "javaupdate", "jucheck"],
            Loc.T("Java-Updateprüfer", "Java update checker"),
            AutostartKategorie.Bremse,
            Loc.T("Sucht ständig im Hintergrund nach neuen Java-Versionen.",
                  "Constantly searches in the background for new Java versions."),
            Loc.T("Kannst du ausschalten. Java selbst funktioniert weiter - nur die automatische Suche entfällt. Denk dann bitte hin und wieder selbst an ein Update.",
                  "Safe to turn off. Java itself keeps working - only the automatic check goes away. Please remember to update it yourself now and then.")),

        new(["ituneshelper", "ipodservice", "qttask", "applesyncnotifier"],
            Loc.T("Apple-Hilfsprogramm", "Apple helper program"),
            AutostartKategorie.Bremse,
            Loc.T("Wartet im Hintergrund darauf, dass du ein iPhone/iPod ansteckst.",
                  "Waits in the background for you to plug in an iPhone/iPod."),
            Loc.T("Kannst du ausschalten. iTunes startet dieses Hilfsprogramm selbst, sobald du iTunes öffnest oder ein Gerät ansteckst.",
                  "Safe to turn off. iTunes starts this helper itself as soon as you open iTunes or plug in a device.")),

        new(["googleupdate", "googleupdater", "chrome_updater", "edgeupdate", "operaautoupdate", "opera_autoupdate", "braveupdate"],
            Loc.T("Browser-Updateprüfer", "Browser update checker"),
            AutostartKategorie.Bremse,
            Loc.T("Kleines Zusatzprogramm, das nach neuen Browser-Versionen sucht.",
                  "Small extra program that checks for new browser versions."),
            Loc.T("Kannst du ausschalten. Dein Browser aktualisiert sich auch selbst, sobald du ihn benutzt - dafür muss beim Hochfahren nichts extra mitlaufen.",
                  "Safe to turn off. Your browser also updates itself once you use it - nothing extra needs to run at boot for that.")),

        new(["ccleaner", "ccleaner64", "iolo", "advancedsystemcare", "asctray", "driverbooster", "wisecare"],
            Loc.T("Aufräum-/Tuning-Programm", "Cleanup/tuning program"),
            AutostartKategorie.Bremse,
            Loc.T("Überwacht dauerhaft im Hintergrund und möchte regelmäßig aufräumen oder etwas verkaufen.",
                  "Constantly monitors in the background and wants to clean up regularly or sell you something."),
            Loc.T("Kannst du ausschalten. Solche Programme bremsen den Start und sind im Hintergrund unnötig - aufräumen kannst du, wann DU willst (z.B. hier im Saubermacher-Modul).",
                  "Safe to turn off. Such programs slow down startup and are unnecessary in the background - you can clean up whenever YOU want (e.g. here in the Cleanup module).")),

        new(["nerocheck", "toolbar", "askpartnerocheck", "bonjour", "spotifywebhelper"],
            Loc.T("Zubehör ohne Nutzen beim Start", "Accessory with no startup benefit"),
            AutostartKategorie.Bremse,
            Loc.T("Mitgeliefertes Zusatzprogramm, das beim Hochfahren keinen erkennbaren Zweck erfüllt.",
                  "Bundled extra program that serves no recognizable purpose at boot."),
            Loc.T("Kannst du ausschalten. Das Hauptprogramm funktioniert davon unabhängig weiter.",
                  "Safe to turn off. The main program keeps working independently of this."))
    ];

    /// <summary>
    /// Sucht die Regel zu einer Programmdatei. Vergleicht auf ganze Namen, damit
    /// z.B. "steam" nicht versehentlich auf "steamwebhelper-irgendwas" passt -
    /// lieber ehrlich "unbekannt" sagen als falsch raten.
    /// </summary>
    public static AutostartRegel? Finde(string? dateiName)
    {
        if (string.IsNullOrWhiteSpace(dateiName)) return null;

        var gesucht = dateiName.Trim().ToLowerInvariant();

        return Alle.FirstOrDefault(regel =>
            regel.DateiNamen.Any(name => string.Equals(name, gesucht, StringComparison.OrdinalIgnoreCase)));
    }
}
