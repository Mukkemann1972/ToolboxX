using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

// Reines Nachschlagewerk (rein lesend, keine Live-Abfrage des echten BIOS möglich - Windows kann
// die meisten BIOS-Einstellungen nicht auslesen). Jeder Eintrag ist bewusst so geschrieben, dass er
// FÜR SICH ALLEIN reicht - ohne Vorwissen, ohne dass jemand zusätzlich fragen muss. Grund: viele
// Nutzer haben niemanden, den sie fragen können (siehe Motivation in der README).
public class BiosKompendiumService
{
    public IReadOnlyList<BiosBegriff> AlleBegriffe { get; } = new List<BiosBegriff>
    {
        new()
        {
            Name = Loc.T("Boot-Reihenfolge (Boot Order)", "Boot order"),
            Kategorie = BiosKategorie.Boot,
            Risiko = AmpelStatus.Gruen,
            WasEsIst = Loc.T(
                "Legt fest, in welcher Reihenfolge der PC nach einem startfähigen Gerät sucht " +
                "- z.B. erst die eingebaute Festplatte, dann ein USB-Stick, dann ein DVD-Laufwerk.",
                "Determines the order in which the PC searches for a bootable device " +
                "- e.g. first the built-in hard drive, then a USB stick, then a DVD drive."),
            WasPassiertBeiAenderung = Loc.T(
                "Nur die Reihenfolge der Suche ändert sich. Es werden dabei " +
                "keine Daten gelöscht oder verändert - der PC probiert einfach ein anderes Gerät zuerst.",
                "Only the search order changes. No data " +
                "is deleted or changed - the PC simply tries a different device first."),
            WannAendern = Loc.T(
                "Wenn du von einem USB-Stick booten willst (z.B. um Windows neu zu " +
                "installieren oder ein Rettungssystem zu starten), muss der Stick vorübergehend " +
                "nach ganz oben gesetzt werden.",
                "If you want to boot from a USB stick (e.g. to reinstall Windows " +
                "or start a rescue system), the stick needs to be temporarily " +
                "moved to the top."),
            FallsProblem = Loc.T(
                "Ungefährlich - einfach die eingebaute Festplatte wieder nach oben " +
                "setzen, oder per CMOS-Reset (siehe eigener Eintrag) alles zurücksetzen.",
                "Not risky - just move the built-in hard drive back to the top, " +
                "or reset everything via a CMOS reset (see its own entry)."),
        },
        new()
        {
            Name = "Secure Boot",
            Kategorie = BiosKategorie.Sicherheit,
            Risiko = AmpelStatus.Gelb,
            WasEsIst = Loc.T(
                "Prüft beim Start, ob das Betriebssystem/der Bootloader eine gültige " +
                "digitale Signatur hat - soll verhindern, dass sich Schadsoftware schon vor " +
                "Windows selbst einnistet.",
                "Checks at startup whether the operating system/bootloader has a valid " +
                "digital signature - meant to prevent malware from embedding itself before " +
                "Windows itself even starts."),
            WasPassiertBeiAenderung = Loc.T(
                "Ausschalten öffnet die Tür für unsignierte Systeme (ältere " +
                "Linux-Versionen, manche Rettungs-USB-Sticks, sehr altes Windows) - aber eben auch " +
                "für Boot-Schadsoftware. Für normalen Windows-10/11-Betrieb sollte es AN bleiben.",
                "Turning it off opens the door for unsigned systems (older " +
                "Linux versions, some rescue USB sticks, very old Windows) - but also " +
                "for boot malware. For normal Windows 10/11 operation it should stay ON."),
            WannAendern = Loc.T(
                "Nur wenn ein bestimmtes Linux/Rettungssystem beim Starten meldet " +
                "\"Secure Boot violation\" oder gar nicht erst bootet.",
                "Only if a specific Linux/rescue system reports " +
                "\"Secure Boot violation\" at startup or doesn't boot at all."),
            FallsProblem = Loc.T(
                "Nach der Aktion Secure Boot wieder EINSCHALTEN, sobald du zurück zu " +
                "normalem Windows-Betrieb wechselst - nicht dauerhaft ausgeschaltet lassen.",
                "Turn Secure Boot back ON after the task, as soon as you switch back to " +
                "normal Windows operation - don't leave it off permanently."),
        },
        new()
        {
            Name = Loc.T("CSM (Legacy-BIOS) vs. UEFI", "CSM (legacy BIOS) vs. UEFI"),
            Kategorie = BiosKategorie.Boot,
            Risiko = AmpelStatus.Rot,
            WasEsIst = Loc.T(
                "Bestimmt, ob der PC im alten \"Legacy-BIOS\"-Modus oder im moderneren " +
                "UEFI-Modus startet. Hängt eng mit dem Partitionsformat der Festplatte zusammen " +
                "(MBR = alt, GPT = modern).",
                "Determines whether the PC boots in the old \"legacy BIOS\" mode or the more modern " +
                "UEFI mode. Closely tied to the hard drive's partition format " +
                "(MBR = old, GPT = modern)."),
            WasPassiertBeiAenderung = Loc.T(
                "Das größte Risiko in diesem ganzen Kompendium: liegt die " +
                "Festplatte im falschen Format zum gewählten Modus, startet Windows danach GAR " +
                "NICHT MEHR (schwarzer Bildschirm, \"kein Bootmedium gefunden\").",
                "The biggest risk in this whole compendium: if the " +
                "hard drive is in the wrong format for the chosen mode, Windows will NOT " +
                "start at all afterward (black screen, \"no boot device found\")."),
            WannAendern = Loc.T(
                "Fast nie bei einem normal laufenden PC nötig - nur bei einer " +
                "Neuinstallation von Windows, wenn der Installations-Assistent das ausdrücklich " +
                "verlangt.",
                "Almost never necessary on a normally running PC - only during a " +
                "fresh Windows installation, when the installer explicitly " +
                "requires it."),
            FallsProblem = Loc.T(
                "Sofort zurückstellen auf den Wert, der vorher eingestellt war (deshalb: " +
                "VORHER notieren, bevor du hier etwas änderst!). Ein CMOS-Reset (siehe eigener " +
                "Eintrag) bringt in der Regel den Standardwert zurück, der meist zum bestehenden " +
                "Windows passt.",
                "Immediately set it back to the value it had before (which is why you should " +
                "write it down BEFORE changing anything here!). A CMOS reset (see its own " +
                "entry) usually restores the default value, which mostly matches the existing " +
                "Windows installation."),
        },
        new()
        {
            Name = Loc.T("XMP / DOCP / EXPO (RAM-Geschwindigkeitsprofil)", "XMP / DOCP / EXPO (RAM speed profile)"),
            Kategorie = BiosKategorie.Leistung,
            Risiko = AmpelStatus.Gelb,
            WasEsIst = Loc.T(
                "Dein Arbeitsspeicher (RAM) kann schneller laufen, als das BIOS ihn " +
                "standardmäßig einstellt (Sicherheitsreserve). XMP (Intel-Name), DOCP oder EXPO " +
                "(AMD-Namen für dasselbe Prinzip) aktivieren die vom Hersteller getestete, höhere " +
                "Geschwindigkeit, die eigentlich schon auf der RAM-Verpackung steht.",
                "Your memory (RAM) can run faster than the BIOS sets it to by " +
                "default (a safety margin). XMP (Intel's name), DOCP, or EXPO " +
                "(AMD's names for the same principle) enable the manufacturer-tested, higher " +
                "speed that's actually already printed on the RAM's packaging."),
            WasPassiertBeiAenderung = Loc.T(
                "Der RAM läuft schneller und minimal wärmer. In seltenen " +
                "Fällen kann der PC danach instabil werden (Bluescreens, Neustarts) - abhängig von " +
                "der genauen Mainboard/CPU-Kombination.",
                "The RAM runs faster and slightly warmer. In rare " +
                "cases the PC can become unstable afterward (blue screens, restarts) - depending on " +
                "the exact mainboard/CPU combination."),
            WannAendern = Loc.T(
                "Direkt nach dem Kauf neuer Arbeitsspeicher-Riegel aktivieren, damit du " +
                "die volle Leistung bekommst, für die du bezahlt hast (ohne XMP läuft neuer, " +
                "schneller RAM oft nur mit Standardgeschwindigkeit).",
                "Enable it right after buying new memory modules, so you get " +
                "the full performance you paid for (without XMP, new, faster " +
                "RAM often only runs at standard speed)."),
            FallsProblem = Loc.T(
                "Wird der PC danach instabil: Profil im BIOS wieder auf \"Auto\"/" +
                "\"Disabled\" zurückstellen, dann läuft der RAM wieder auf der langsameren, aber " +
                "sicheren Standardgeschwindigkeit.",
                "If the PC becomes unstable afterward: set the profile in the BIOS back to \"Auto\"/" +
                "\"Disabled\", then the RAM runs again at the slower, but " +
                "safe, standard speed."),
        },
        new()
        {
            Name = "TPM (Trusted Platform Module)",
            Kategorie = BiosKategorie.Sicherheit,
            Risiko = AmpelStatus.Rot,
            WasEsIst = Loc.T(
                "Ein kleiner Sicherheitschip (oder eine Firmware-Nachbildung davon), der " +
                "Verschlüsselungsschlüssel sicher speichert. Windows 11 verlangt ihn zwingend zur " +
                "Installation.",
                "A small security chip (or a firmware emulation of one) that " +
                "securely stores encryption keys. Windows 11 strictly requires it for " +
                "installation."),
            WasPassiertBeiAenderung = Loc.T(
                "ACHTUNG bei bereits verschlüsselten Laufwerken (BitLocker): " +
                "wird TPM deaktiviert, kann Windows beim nächsten Start den BitLocker-" +
                "Wiederherstellungsschlüssel verlangen - ist der nicht griffbereit gesichert, sind " +
                "die Daten auf der Platte NICHT mehr zugänglich.",
                "WARNING for already-encrypted drives (BitLocker): " +
                "if TPM is disabled, Windows may ask for the BitLocker " +
                "recovery key at the next startup - if it isn't saved somewhere accessible, " +
                "the data on the drive will NOT be accessible anymore."),
            WannAendern = Loc.T(
                "Nur einschalten, wenn Windows 11 die Installation verweigert und TPM " +
                "aktuell aus ist - vorher prüfen, ob eine ältere BitLocker-Verschlüsselung besteht.",
                "Only enable it if Windows 11 refuses to install and TPM is " +
                "currently off - check beforehand whether an older BitLocker encryption exists."),
            FallsProblem = Loc.T(
                "Wird ein Wiederherstellungsschlüssel verlangt: NICHT raten oder " +
                "zurücksetzen versuchen. Der Schlüssel liegt oft online im Microsoft-Konto unter " +
                "\"Geräte verwalten\", oder war beim Einrichten ausgedruckt/gespeichert.",
                "If a recovery key is requested: do NOT guess or try to " +
                "reset it. The key is often stored online in your Microsoft account under " +
                "\"Manage devices\", or was printed/saved during setup."),
        },
        new()
        {
            Name = "Fast Boot",
            Kategorie = BiosKategorie.Boot,
            Risiko = AmpelStatus.Gelb,
            WasEsIst = Loc.T(
                "Überspringt einen Teil der Hardware-Selbsttests beim Einschalten (POST), " +
                "damit der PC schneller hochfährt.",
                "Skips part of the hardware self-tests at power-on (POST) " +
                "so the PC boots faster."),
            WasPassiertBeiAenderung = Loc.T(
                "Der PC startet schneller, aber du hast oft nur einen sehr " +
                "kurzen Moment (oder gar keinen), um per Tastendruck ins BIOS zu gelangen.",
                "The PC starts faster, but you often only have a very " +
                "short moment (or none at all) to press a key to enter the BIOS."),
            WannAendern = Loc.T(
                "Ausschalten, wenn du öfter ins BIOS musst (z.B. während du an " +
                "Einstellungen bastelst), oder wenn ein neu angeschlossenes USB-Gerät beim Start " +
                "nicht erkannt wird.",
                "Turn it off if you need to enter the BIOS often (e.g. while " +
                "tweaking settings), or if a newly connected USB device isn't " +
                "recognized at startup."),
            FallsProblem = Loc.T(
                "Kein Risiko, jederzeit wieder einschaltbar. Kommst du gar nicht mehr " +
                "ins BIOS: PC per Windows \"Neu starten\" (nicht \"Herunterfahren\") starten und " +
                "direkt beim ersten schwarzen Bildschirm mehrfach die BIOS-Taste drücken.",
                "No risk, can be turned back on anytime. If you can no longer " +
                "get into the BIOS: restart the PC via Windows \"Restart\" (not \"Shut down\") and " +
                "press the BIOS key repeatedly right at the first black screen."),
        },
        new()
        {
            Name = Loc.T("Virtualisierung (Intel VT-x / AMD-V / \"SVM Mode\")", "Virtualization (Intel VT-x / AMD-V / \"SVM Mode\")"),
            Kategorie = BiosKategorie.Leistung,
            Risiko = AmpelStatus.Gruen,
            WasEsIst = Loc.T(
                "Erlaubt es, dass ein Programm eine \"virtuelle\", in sich abgeschlossene " +
                "Kopie eines Computers innerhalb des echten PCs laufen lässt - z.B. Windows " +
                "Sandbox, eine virtuelle Maschine, oder manche Android-Emulatoren.",
                "Allows a program to run a \"virtual\", self-contained " +
                "copy of a computer inside the real PC - e.g. Windows " +
                "Sandbox, a virtual machine, or some Android emulators."),
            WasPassiertBeiAenderung = Loc.T(
                "Ohne diese Einstellung starten solche virtuellen Umgebungen " +
                "gar nicht erst oder melden einen Fehler (z.B. \"VT-x is disabled\").",
                "Without this setting, such virtual environments " +
                "don't start at all or report an error (e.g. \"VT-x is disabled\")."),
            WannAendern = Loc.T(
                "Einschalten, wenn du z.B. Windows Sandbox, Hyper-V oder eine andere " +
                "virtuelle Maschine benutzen willst.",
                "Enable it if you want to use, for example, Windows Sandbox, Hyper-V, or another " +
                "virtual machine."),
            FallsProblem = Loc.T(
                "Ungefährlich - kann jederzeit wieder aus- und eingeschaltet werden, " +
                "ohne dass etwas kaputtgeht.",
                "Not risky - can be turned off and back on anytime, " +
                "without anything breaking."),
        },
        new()
        {
            Name = Loc.T("BIOS-Update (Firmware aktualisieren)", "BIOS update (update firmware)"),
            Kategorie = BiosKategorie.Sonstiges,
            Risiko = AmpelStatus.Rot,
            WasEsIst = Loc.T(
                "Die eigentliche Software des Mainboards selbst wird ausgetauscht - anders " +
                "als alle anderen Punkte hier ist das keine Einstellung, sondern ein kompletter " +
                "Firmware-Austausch.",
                "The mainboard's actual software itself gets replaced - unlike " +
                "all the other points here, this is not a setting, but a complete " +
                "firmware replacement."),
            WasPassiertBeiAenderung = Loc.T(
                "Läuft alles glatt, gibt es meist Bugfixes, bessere " +
                "Hardware-Unterstützung oder neue Funktionen. Wird der Vorgang aber unterbrochen " +
                "(Stromausfall, PC ausgeschaltet), kann das Mainboard danach GAR NICHT MEHR starten " +
                "(\"gebrickt\") - das größte Einzelrisiko in der ganzen PC-Wartung.",
                "If everything goes smoothly, there are usually bugfixes, better " +
                "hardware support, or new features. But if the process is interrupted " +
                "(power outage, PC turned off), the mainboard may NOT be able to start at all " +
                "afterward (\"bricked\") - the single biggest risk in all of PC maintenance."),
            WannAendern = Loc.T(
                "Nur wenn ein konkreter Grund vorliegt (z.B. eine neue CPU wird ohne " +
                "Update nicht erkannt, oder der Hersteller behebt damit einen bekannten Fehler) - " +
                "\"weil's neu ist\" reicht nicht als Grund.",
                "Only if there's a concrete reason (e.g. a new CPU isn't recognized without " +
                "the update, or the manufacturer fixes a known bug with it) - " +
                "\"because it's new\" is not a good enough reason."),
            FallsProblem = Loc.T(
                "Vorher unbedingt sicherstellen: PC/Laptop am Netzteil (nicht nur Akku), " +
                "keine geplanten Stromausfälle. Geht es trotzdem schief, hilft meist nur eine " +
                "Mainboard-Einsendung zum Hersteller oder ein spezielles Flash-Rettungsverfahren " +
                "laut Mainboard-Handbuch - das ist dann nichts mehr für \"mal eben selbst machen\".",
                "Make absolutely sure beforehand: PC/laptop plugged into the power supply (not just battery), " +
                "no planned power outages. If it still goes wrong, usually only " +
                "sending the mainboard to the manufacturer or a special flash rescue procedure " +
                "per the mainboard manual helps - that's no longer something for \"just do it yourself\"."),
        },
        new()
        {
            Name = Loc.T("CMOS-Reset (BIOS auf Werkseinstellungen zurücksetzen)", "CMOS reset (reset BIOS to factory settings)"),
            Kategorie = BiosKategorie.Sonstiges,
            Risiko = AmpelStatus.Gruen,
            WasEsIst = Loc.T(
                "Setzt ALLE BIOS-Einstellungen auf die Werte zurück, die das Mainboard ab " +
                "Werk hatte - quasi der \"Reset-Knopf\" für jeden anderen Eintrag in diesem " +
                "Kompendium.",
                "Resets ALL BIOS settings to the values the mainboard had at the " +
                "factory - essentially the \"reset button\" for every other entry in this " +
                "compendium."),
            WasPassiertBeiAenderung = Loc.T(
                "Eigene Änderungen (z.B. XMP, Boot-Reihenfolge) gehen " +
                "verloren und müssen neu gesetzt werden - aber Windows und alle Dateien auf der " +
                "Festplatte bleiben komplett unberührt, das betrifft NUR die BIOS-Einstellungen.",
                "Your own changes (e.g. XMP, boot order) are " +
                "lost and need to be set again - but Windows and all files on the " +
                "hard drive stay completely untouched, this only affects the BIOS settings."),
            WannAendern = Loc.T(
                "Der Notausgang: wenn der PC nach einer BIOS-Änderung gar nicht mehr " +
                "startet oder sich merkwürdig verhält, und du nicht mehr ins BIOS kommst, um es " +
                "manuell rückgängig zu machen.",
                "The emergency exit: when the PC no longer " +
                "starts after a BIOS change or behaves strangely, and you can't get into the BIOS " +
                "anymore to undo it manually."),
            FallsProblem = Loc.T(
                "Geht meist über einen Knopf/Jumper direkt auf dem Mainboard, oder durch " +
                "kurzzeitiges Entfernen der kleinen, runden Knopfzelle (CMOS-Batterie) für ca. " +
                "5 Minuten bei gezogenem Netzstecker. Das genaue Vorgehen steht im Handbuch des " +
                "jeweiligen Mainboards - unterscheidet sich je Hersteller/Modell.",
                "Usually done via a button/jumper directly on the mainboard, or by " +
                "briefly removing the small, round coin cell (CMOS battery) for about " +
                "5 minutes with the power cable unplugged. The exact procedure is in the manual of " +
                "the respective mainboard - it differs by manufacturer/model."),
        },
    };

    public IReadOnlyList<BiosBegriff> Suchen(string? suchtext, BiosKategorie? kategorie)
    {
        IEnumerable<BiosBegriff> ergebnis = AlleBegriffe;

        if (kategorie is not null)
            ergebnis = ergebnis.Where(b => b.Kategorie == kategorie);

        if (!string.IsNullOrWhiteSpace(suchtext))
            ergebnis = ergebnis.Where(b =>
                b.Name.Contains(suchtext, StringComparison.OrdinalIgnoreCase) ||
                b.WasEsIst.Contains(suchtext, StringComparison.OrdinalIgnoreCase));

        return ergebnis.ToList();
    }
}
