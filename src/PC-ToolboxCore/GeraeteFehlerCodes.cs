namespace PCToolboxCore;

/// <summary>Erklärung zu einem Gerätefehler: was los ist und was man tun kann.</summary>
public record GeraeteFehlerErklaerung(string Kurzform, string WasEsIst, string Empfehlung);

/// <summary>
/// Übersetzt die Fehlernummern des Windows-Geräte-Managers in Klartext.
///
/// Warum das ein eigenes Modul wert ist: Der Geräte-Manager zeigt ein gelbes
/// Ausrufezeichen und einen Satz wie "Dieses Gerät kann nicht gestartet werden.
/// (Code 10)". Was man deswegen TUN soll, steht nirgends. Genau diese Lücke füllt
/// diese Tabelle - für jede Nummer ein konkreter nächster Schritt.
///
/// Bewusst NICHT dabei: automatisches Herunterladen von Treibern. Genau damit
/// verdienen die einschlägigen "Driver Updater" ihr Geld, und genau dabei fangen
/// sich Leute den meisten Ärger ein. Wir sagen, welcher Treiber fehlt - geholt wird
/// er beim Hersteller.
/// </summary>
public static class GeraeteFehlerCodes
{
    private static readonly string TreiberHolen = Loc.T(
        "Lade den passenden Treiber direkt beim Hersteller des Geräts oder deines PCs/Notebooks " +
        "(Support-Seite, Modellnummer eingeben) und installiere ihn. Nimm keine \"Treiber-Updater\"-" +
        "Programme aus dem Netz - die machen erfahrungsgemäß mehr kaputt als heil.",
        "Download the matching driver directly from the manufacturer of the device or your " +
        "PC/notebook (support page, enter the model number) and install it. Don't use \"driver updater\" " +
        "programs from the internet - experience shows they cause more harm than good.");

    private static readonly Dictionary<int, GeraeteFehlerErklaerung> Tabelle = new()
    {
        [1] = new(Loc.T("Gerät ist nicht richtig eingerichtet", "Device is not set up correctly"),
            Loc.T("Windows kennt das Gerät, hat aber keine vollständige Anleitung (Treiber) dafür.",
                  "Windows knows the device, but doesn't have complete instructions (driver) for it."),
            TreiberHolen),

        [3] = new(Loc.T("Treiber ist beschädigt", "Driver is corrupted"),
            Loc.T("Die Steuersoftware des Geräts ist unvollständig oder es fehlte beim Laden an Arbeitsspeicher.",
                  "The device's control software is incomplete, or memory was short while loading it."),
            Loc.T("Starte den PC einmal neu. Bleibt der Fehler, installiere den Treiber neu. ",
                  "Restart the PC once. If the error remains, reinstall the driver. ") + TreiberHolen),

        [10] = new(Loc.T("Gerät lässt sich nicht starten", "Device cannot start"),
            Loc.T(
                "Der häufigste Fehler überhaupt. Windows versucht das Gerät zu starten und bekommt " +
                "keine brauchbare Antwort - das kann am Treiber liegen, aber auch an einem lockeren " +
                "Stecker oder am Gerät selbst.",
                "The single most common error. Windows tries to start the device and gets " +
                "no usable response - this can be the driver's fault, but also a loose " +
                "connector or the device itself."),
            Loc.T(
                "Der Reihe nach: 1. PC neu starten. 2. Bei Geräten zum Anstecken: abziehen, andere Buchse " +
                "probieren. 3. Treiber neu installieren. Hilft nichts davon und ist das Gerät schon älter, " +
                "ist es vermutlich wirklich defekt.",
                "In order: 1. Restart the PC. 2. For pluggable devices: unplug, try a different port. " +
                "3. Reinstall the driver. If none of this helps and the device is already old, " +
                "it's probably really defective.")),

        [12] = new(Loc.T("Zwei Geräte kommen sich in die Quere", "Two devices are conflicting"),
            Loc.T(
                "Zwei Geräte wollen denselben Platz im System belegen. Das kommt fast nur bei älteren " +
                "Steckkarten vor.",
                "Two devices want to occupy the same spot in the system. This happens almost only with older " +
                "add-in cards."),
            Loc.T(
                "Wenn möglich, stecke die Karte in einen anderen Steckplatz. Alternativ lässt sich im " +
                "BIOS oft ein nicht benötigtes Bauteil (z.B. eine alte Schnittstelle) abschalten und " +
                "macht damit Platz - schau dazu ins BIOS-Kompendium in dieser Toolbox.",
                "If possible, move the card to a different slot. Alternatively, the " +
                "BIOS can often disable an unneeded component (e.g. an old interface), freeing up " +
                "space - check the BIOS compendium in this toolbox for that.")),

        [14] = new(Loc.T("Neustart nötig", "Restart required"),
            Loc.T("Das Gerät ist eingerichtet, wird aber erst nach einem Neustart wirklich benutzt.",
                  "The device is set up, but will only actually be used after a restart."),
            Loc.T("Starte den PC neu. Danach sollte der Punkt verschwunden sein.",
                  "Restart the PC. This point should then disappear.")),

        [18] = new(Loc.T("Treiber muss neu installiert werden", "Driver needs to be reinstalled"),
            Loc.T("Windows meldet ausdrücklich, dass die Steuersoftware erneut eingerichtet werden muss.",
                  "Windows explicitly reports that the control software needs to be set up again."),
            TreiberHolen),

        [19] = new(Loc.T("Die Einstellungen des Geräts sind beschädigt", "The device's settings are corrupted"),
            Loc.T("Die zu diesem Gerät gespeicherten Angaben in Windows sind unvollständig oder widersprüchlich.",
                  "The information stored for this device in Windows is incomplete or contradictory."),
            Loc.T(
                "Deinstalliere das Gerät im Geräte-Manager (Rechtsklick, \"Gerät deinstallieren\") und " +
                "starte den PC neu - Windows richtet es dann von vorn ein. Deine Daten sind davon nicht " +
                "betroffen, es geht nur um die Steuersoftware.",
                "Uninstall the device in Device Manager (right-click, \"Uninstall device\") and " +
                "restart the PC - Windows will then set it up again from scratch. Your data is not " +
                "affected by this, it's only about the control software.")),

        [21] = new(Loc.T("Windows entfernt das Gerät gerade", "Windows is currently removing the device"),
            Loc.T("Ein Übergangszustand, kein echter Fehler.", "A transitional state, not a real error."),
            Loc.T("Warte ein paar Sekunden und lies hier neu ein. Bleibt es stehen, hilft ein Neustart.",
                  "Wait a few seconds and reload here. If it stays like this, a restart helps.")),

        [22] = new(Loc.T("Gerät ist abgeschaltet", "Device is disabled"),
            Loc.T(
                "Das Gerät ist da und funktionsfähig, wurde aber bewusst deaktiviert - entweder in " +
                "Windows oder im BIOS.",
                "The device is present and functional, but was deliberately disabled - either in " +
                "Windows or in the BIOS."),
            Loc.T(
                "Wenn du es brauchst: im Geräte-Manager per Rechtsklick wieder aktivieren. Klappt das " +
                "nicht, ist es im BIOS abgeschaltet. Wenn du es gar nicht brauchst, ist das völlig in " +
                "Ordnung so - dann ignoriere diesen Punkt.",
                "If you need it: re-enable it in Device Manager via right-click. If that " +
                "doesn't work, it's disabled in the BIOS. If you don't need it at all, that's completely " +
                "fine - just ignore this point.")),

        [24] = new(Loc.T("Gerät ist nicht (mehr) angeschlossen", "Device is not (or no longer) connected"),
            Loc.T(
                "Windows kennt das Gerät noch von früher, findet es aber gerade nicht. Typisch für " +
                "Geräte, die man nur gelegentlich ansteckt.",
                "Windows still knows the device from before, but can't find it right now. Typical for " +
                "devices that are only plugged in occasionally."),
            Loc.T(
                "Wenn du das Gerät gerade nicht benutzt: nichts zu tun, das ist normal. Soll es " +
                "angeschlossen sein, prüfe Stecker und Kabel.",
                "If you're not currently using the device: nothing to do, this is normal. If it should " +
                "be connected, check the connector and cable.")),

        [28] = new(Loc.T("Es ist gar kein Treiber installiert", "No driver is installed at all"),
            Loc.T(
                "Der Klassiker hinter dem gelben Ausrufezeichen: Windows sieht ein Gerät, weiss aber " +
                "überhaupt nicht, was es damit anfangen soll.",
                "The classic behind the yellow exclamation mark: Windows sees a device, but has " +
                "no idea what to do with it."),
            Loc.T("Genau hier lohnt es sich. ", "This is exactly where it's worth it. ") + TreiberHolen),

        [29] = new(Loc.T("Das Gerät ist im BIOS abgeschaltet", "The device is disabled in the BIOS"),
            Loc.T(
                "Nicht Windows, sondern die Grundeinstellung des Mainboards stellt dem Gerät nichts " +
                "zur Verfügung.",
                "Not Windows, but the mainboard's base configuration doesn't provide the device with " +
                "anything."),
            Loc.T(
                "Das lässt sich nur im BIOS ändern (beim Einschalten meist Entf oder F2). Suche dort " +
                "den Eintrag zu diesem Gerät und schalte ihn ein. Das BIOS-Kompendium in dieser Toolbox " +
                "erklärt die üblichen Einstellungen.",
                "This can only be changed in the BIOS (usually Del or F2 at power-on). Look for the " +
                "entry for this device there and enable it. The BIOS compendium in this toolbox " +
                "explains the common settings.")),

        [31] = new(Loc.T("Windows kann den Treiber nicht laden", "Windows cannot load the driver"),
            Loc.T("Die Steuersoftware ist zwar vorhanden, lässt sich aber nicht starten.",
                  "The control software is present, but cannot be started."),
            Loc.T("Deinstalliere das Gerät im Geräte-Manager und starte neu. Bleibt es dabei: ",
                  "Uninstall the device in Device Manager and restart. If it persists: ") + TreiberHolen),

        [32] = new(Loc.T("Der Treiber ist abgeschaltet", "The driver is disabled"),
            Loc.T(
                "Der Dienst, der dieses Gerät steuert, wurde deaktiviert - meist durch ein anderes " +
                "Programm oder von Hand.",
                "The service that controls this device was disabled - usually by another " +
                "program or manually."),
            Loc.T("Installiere den Treiber neu, damit sein Dienst wieder richtig eingetragen wird. ",
                  "Reinstall the driver so its service is registered correctly again. ") + TreiberHolen),

        [33] = new(Loc.T("Windows kann nicht ermitteln, was das Gerät braucht", "Windows cannot determine what the device needs"),
            Loc.T("Das deutet auf einen echten Hardware-Defekt hin, nicht auf ein Software-Problem.",
                  "This points to a real hardware defect, not a software problem."),
            Loc.T(
                "Prüfe Sitz und Kabel des Geräts. Ist es fest angeschlossen und bleibt der Fehler, ist " +
                "das Gerät sehr wahrscheinlich defekt.",
                "Check the device's seating and cable. If it's firmly connected and the error remains, " +
                "the device is very likely defective.")),

        [37] = new(Loc.T("Der Treiber konnte nicht starten", "The driver could not start"),
            Loc.T("Die Steuersoftware hat beim Start selbst einen Fehler gemeldet.",
                  "The control software itself reported an error while starting."),
            Loc.T("Treiber neu installieren. ", "Reinstall the driver. ") + TreiberHolen),

        [38] = new(Loc.T("Ein Rest des Treibers hängt noch im Speicher", "A remnant of the driver is still stuck in memory"),
            Loc.T("Eine frühere Fassung des Treibers wurde nicht sauber beendet.",
                  "A previous version of the driver was not shut down cleanly."),
            Loc.T(
                "Ein vollständiger Neustart des PCs räumt das auf. Wichtig: wirklich \"Neu starten\" " +
                "wählen, nicht nur den Deckel zuklappen oder herunterfahren - moderne Windows-Versionen " +
                "fahren beim Herunterfahren nicht mehr komplett herunter.",
                "A full restart of the PC clears this up. Important: really choose \"Restart\", " +
                "not just closing the lid or shutting down - modern Windows versions " +
                "no longer fully shut down on \"Shut down\".")),

        [39] = new(Loc.T("Treiber fehlt oder ist beschädigt", "Driver is missing or corrupted"),
            Loc.T("Die zum Gerät gehörende Datei ist nicht auffindbar oder unbrauchbar.",
                  "The file belonging to the device cannot be found or is unusable."),
            TreiberHolen),

        [43] = new(Loc.T("Windows hat das Gerät angehalten", "Windows has stopped the device"),
            Loc.T(
                "Das Gerät selbst hat einen Fehler gemeldet, woraufhin Windows es abgeschaltet hat. " +
                "Sehr häufig bei USB-Geräten und Grafikkarten.",
                "The device itself reported an error, causing Windows to shut it down. " +
                "Very common with USB devices and graphics cards."),
            Loc.T(
                "Bei angesteckten Geräten: abziehen, PC neu starten, in eine andere Buchse stecken - " +
                "am besten direkt am PC statt über einen Verteiler (Hub). Bei einer Grafikkarte: Treiber " +
                "neu installieren; bleibt es dabei, prüfe Sitz der Karte und Stromstecker.",
                "For plugged-in devices: unplug, restart the PC, plug into a different port - " +
                "ideally directly on the PC instead of via a hub. For a graphics card: reinstall the " +
                "driver; if it persists, check the card's seating and power connector.")),

        [45] = new(Loc.T("Gerät ist gerade nicht da", "Device is currently not present"),
            Loc.T(
                "Ein Eintrag von einem Gerät, das früher einmal angeschlossen war. Eine Karteileiche, " +
                "kein Fehler.",
                "An entry from a device that was once connected. A leftover entry, " +
                "not an error."),
            Loc.T("Nichts zu tun. Der Eintrag verschwindet von selbst oder bleibt harmlos stehen.",
                  "Nothing to do. The entry disappears on its own or stays harmlessly in place.")),

        [48] = new(Loc.T("Diese Software wird von Windows blockiert", "This software is blocked by Windows"),
            Loc.T("Windows kennt zu genau diesem Treiber ein Problem und lässt ihn absichtlich nicht zu.",
                  "Windows knows of a problem with this exact driver and deliberately doesn't allow it."),
            Loc.T(
                "Hier hilft nur eine neuere Fassung des Treibers vom Hersteller. Umgehen solltest du " +
                "diese Sperre nicht - sie hat einen Grund.",
                "Only a newer version of the driver from the manufacturer helps here. You shouldn't " +
                "bypass this block - it exists for a reason.")),

        [52] = new(Loc.T("Die Herkunft des Treibers ist nicht überprüfbar", "The driver's origin cannot be verified"),
            Loc.T(
                "Windows kann nicht bestätigen, dass der Treiber wirklich vom angegebenen Hersteller " +
                "stammt und unverändert ist.",
                "Windows cannot confirm that the driver really comes from the stated manufacturer " +
                "and is unmodified."),
            Loc.T(
                "Hole den Treiber noch einmal direkt von der offiziellen Hersteller-Seite. Stammt er " +
                "aus einer anderen Quelle, installiere ihn lieber nicht - eine fehlende Signatur ist " +
                "genau das Einfallstor, das man nicht offen lassen will.",
                "Get the driver again directly from the official manufacturer site. If it comes " +
                "from another source, better not install it - a missing signature is " +
                "exactly the entry point you don't want to leave open."))
    };

    /// <summary>
    /// Liefert die Erklärung zu einer Fehlernummer. Unbekannte Nummern bekommen eine
    /// ehrliche Standardantwort statt einer erfundenen Diagnose.
    /// </summary>
    public static GeraeteFehlerErklaerung Erklaeren(int code)
    {
        if (Tabelle.TryGetValue(code, out var erklaerung)) return erklaerung;

        return new GeraeteFehlerErklaerung(
            Loc.T($"Windows meldet Fehler {code}", $"Windows reports error {code}"),
            Loc.T("Diese Fehlernummer steht nicht in unserer Liste. Sie kommt selten vor.",
                  "This error number is not in our list. It occurs rarely."),
            Loc.T(
                "Zwei Schritte helfen bei fast allen Gerätefehlern: PC einmal vollständig neu starten, " +
                "und wenn das nichts bringt, das Gerät im Geräte-Manager deinstallieren und neu starten - " +
                "Windows richtet es dann von vorn ein. ",
                "Two steps help with almost all device errors: fully restart the PC once, " +
                "and if that doesn't help, uninstall the device in Device Manager and restart - " +
                "Windows will then set it up again from scratch. ") + TreiberHolen);
    }

    public static bool IstBekannt(int code) => Tabelle.ContainsKey(code);
}
