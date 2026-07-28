namespace PCToolboxCore;

/// <summary>Wie eine Systemprüfung ausgegangen ist - unabhängig vom Werkzeug.</summary>
public enum PruefBefund
{
    /// <summary>Nichts gefunden, alles in Ordnung.</summary>
    Sauber,
    /// <summary>Beschädigungen gefunden, aber reparierbar.</summary>
    BeschaedigtReparierbar,
    /// <summary>Beschädigungen gefunden UND bereits behoben.</summary>
    BeschaedigtBehoben,
    /// <summary>Beschädigt und mit diesem Werkzeug NICHT behebbar.</summary>
    BeschaedigtNichtBehebbar,
    /// <summary>Prüfung lief nicht durch (abgebrochen, keine Rechte, Dienst aus).</summary>
    NichtGelaufen,
    /// <summary>Ausgabe war nicht eindeutig zuzuordnen - dann wird sie im Original gezeigt.</summary>
    Unklar
}

/// <summary>
/// Ergebnis einer Prüfung: Befund, Klartext-Erklärung, nächster Schritt - und wo
/// sinnvoll auch die <b>Ursache</b>.
///
/// Die Ursache ist bewusst ein eigenes Feld und nicht in den Klartext gemischt:
/// Unser Grundsatz ist "erklärt WARUM etwas kaputt ist und WIE man es fixt".
/// Ohne das Warum repariert man denselben Schaden in einem halben Jahr wieder -
/// und weiß immer noch nicht, woran es lag.
/// </summary>
public record PruefErgebnis(PruefBefund Befund, string Klartext, string NaechsterSchritt, string Ursache = "");

/// <summary>
/// Übersetzt die Ausgabe von <c>sfc</c> und <c>DISM</c> in Klartext.
///
/// Warum das ein eigenes Modul wert ist: Windows liefert diese Prüfungen mit, aber
/// die Ausgabe ist für Normalnutzer wertlos. "Der Windows-Ressourcenschutz hat
/// beschädigte Dateien gefunden, konnte jedoch einige von ihnen nicht reparieren"
/// sagt nicht, was man jetzt tun soll. Und die Windows-eigene Problembehandlung
/// meldet meistens nur "Fehler konnte nicht identifiziert werden" - genau der
/// Grund, warum Leute glauben, Windows sei nicht reparierbar.
///
/// Zwei Fallen, die hier bewusst behandelt werden:
///
/// 1. <b>sfc gibt UTF-16 aus.</b> Wird die Ausgabe umgeleitet, stehen zwischen
///    allen Buchstaben Null-Bytes ("W\0i\0n\0..."). Ohne Bereinigung greift kein
///    einziger Textvergleich. <see cref="Bereinigen"/> kümmert sich darum.
///
/// 2. <b>Die Ausgabe ist übersetzt.</b> Auf deutschem Windows steht etwas anderes
///    als auf englischem. Deshalb werden beide Sprachen erkannt. Was nicht
///    eindeutig zuzuordnen ist, wird als <see cref="PruefBefund.Unklar"/>
///    zurückgegeben - dann zeigt das Modul die Originalausgabe, statt zu raten.
///    Lieber ehrlich "das kann ich nicht deuten" als eine erfundene Diagnose.
/// </summary>
public static class SystemPruefungAusgabe
{
    /// <summary>
    /// Warum Systemdateien bzw. der Komponentenspeicher überhaupt kaputtgehen.
    /// Bewusst nach Häufigkeit sortiert und ohne Schuldzuweisung formuliert - die
    /// häufigste Ursache ist schlicht Pech (Stromausfall im falschen Moment).
    /// </summary>
    private static readonly string UrsacheBeschaedigung = Loc.T(
        "Wie so etwas entsteht - meist ist es keine böse Absicht und kein Virus:\n" +
        "• Ein Windows-Update wurde unterbrochen. Stromausfall, Akku leer oder \"jetzt reicht's\" " +
        "und den Ausschaltknopf gedrückt, während Windows im Hintergrund noch an sich selbst gearbeitet hat. " +
        "Das ist mit Abstand der häufigste Grund.\n" +
        "• Die Festplatte hat einen Lesefehler an genau dieser Stelle. Deshalb lohnt nach so einem Fund " +
        "ein Blick ins Modul \"Laufwerke\" - wenn dort SMART-Warnungen stehen, ist die Platte die eigentliche Ursache " +
        "und die Reparatur hält nicht lange.\n" +
        "• Ein \"Tuning\"- oder \"Cleaner\"-Programm hat im Ordner WinSxS aufgeräumt. Der sieht riesig aus und " +
        "verlockt zum Löschen - genau dort liegen aber die Ersatzdateien. Solche Programme sind ein " +
        "häufiger Auslöser.\n" +
        "• Ein Virenscanner hat eine Windows-Datei in Quarantäne verschoben, weil er sie für verdächtig hielt.\n" +
        "• Die Platte war während eines Updates voll.",
        "How this usually happens - mostly it's neither malice nor a virus:\n" +
        "• A Windows update was interrupted. Power outage, dead battery, or hitting the power button " +
        "out of impatience while Windows was still working on itself in the background. " +
        "This is by far the most common reason.\n" +
        "• The hard drive has a read error at exactly this spot. That's why it's worth " +
        "checking the \"Drives\" module after a finding like this - if there are SMART warnings there, the drive is the actual cause " +
        "and the repair won't last long.\n" +
        "• A \"tuning\" or \"cleaner\" program cleaned up the WinSxS folder. It looks huge and " +
        "tempts people to delete it - but that's exactly where the replacement files live. Such programs are a " +
        "common trigger.\n" +
        "• An antivirus scanner quarantined a Windows file because it considered it suspicious.\n" +
        "• The drive was full during an update.");

    private static readonly string UrsacheVorbeugen = Loc.T(
        "Damit es nicht wiederkommt: den PC während \"Updates werden installiert\" nicht ausschalten " +
        "(auch nicht bei scheinbarem Stillstand - der Vorgang läuft oft noch), im Ordner WinSxS nichts " +
        "von Hand oder per Tuning-Tool löschen, und ein paar Gigabyte frei lassen.",
        "To keep it from coming back: don't turn off the PC while \"Installing updates\" is shown " +
        "(even if it seems to be stuck - the process is often still running), don't delete anything " +
        "in the WinSxS folder by hand or with a tuning tool, and keep a few gigabytes free.");

    /// <summary>
    /// Macht die Rohausgabe vergleichbar: Null-Bytes weg (UTF-16-Falle bei sfc),
    /// Zeilenumbrüche vereinheitlicht, Mehrfach-Leerzeichen zusammengefasst.
    /// </summary>
    public static string Bereinigen(string? rohausgabe)
    {
        if (string.IsNullOrEmpty(rohausgabe)) return "";

        var zeichen = rohausgabe.Where(c => c != '\0').ToArray();
        var text = new string(zeichen).Replace("\r\n", "\n").Replace('\r', '\n');

        // Mehrfache Leerzeichen/Tabs zusammenfassen, damit Textvergleiche nicht an
        // unterschiedlicher Formatierung scheitern.
        var sb = new System.Text.StringBuilder(text.Length);
        var letztesWarLeer = false;
        foreach (var c in text)
        {
            var istLeer = c == ' ' || c == '\t';
            if (istLeer && letztesWarLeer) continue;
            sb.Append(istLeer ? ' ' : c);
            letztesWarLeer = istLeer;
        }
        return sb.ToString().Trim();
    }

    /// <summary>Deutet die Ausgabe von <c>sfc /scannow</c> bzw. <c>sfc /verifyonly</c>.</summary>
    public static PruefErgebnis SfcDeuten(string? rohausgabe)
    {
        var t = Bereinigen(rohausgabe).ToLowerInvariant();
        if (t.Length == 0)
            return new(PruefBefund.NichtGelaufen,
                Loc.T("Die Prüfung hat keine Ausgabe geliefert.", "The check produced no output."),
                Loc.T("Meist fehlen Administrator-Rechte. Toolbox X als Administrator neu starten und erneut versuchen.",
                      "Usually administrator rights are missing. Restart Toolbox X as administrator and try again."));

        // Reihenfolge ist wichtig: die "nicht reparieren"-Meldung enthält auch das
        // Wort "gefunden", deshalb zuerst auf den schlechtesten Fall prüfen.
        if (Enthaelt(t, "konnte jedoch einige von ihnen nicht reparieren",
                        "was unable to fix some of them",
                        "konnte einige davon nicht reparieren"))
            return new(PruefBefund.BeschaedigtNichtBehebbar,
                Loc.T("Windows hat beschädigte Systemdateien gefunden und konnte einen Teil davon NICHT reparieren.",
                      "Windows found damaged system files and could NOT repair some of them."),
                Loc.T(
                    "Jetzt die Komponentenspeicher-Reparatur laufen lassen (DISM) und danach diese Prüfung wiederholen - " +
                    "SFC holt sich seine Ersatzdateien aus dem Komponentenspeicher, und wenn der selbst beschädigt ist, " +
                    "kann SFC nichts flicken. Erst DISM, dann SFC: das ist die Reihenfolge, die Windows nirgends erklärt.",
                    "Now run the component store repair (DISM) and then repeat this check - " +
                    "SFC gets its replacement files from the component store, and if that itself is damaged, " +
                    "SFC can't fix anything. DISM first, then SFC: that's the order Windows explains nowhere."),
                UrsacheBeschaedigung + "\n\n" + UrsacheVorbeugen);

        if (Enthaelt(t, "erfolgreich reparariert", "erfolgreich repariert",
                        "successfully repaired them"))
            return new(PruefBefund.BeschaedigtBehoben,
                Loc.T("Windows hat beschädigte Systemdateien gefunden und alle davon reparieren können.",
                      "Windows found damaged system files and was able to repair all of them."),
                Loc.T("Nichts weiter zu tun. Ein Neustart ist sinnvoll, damit die ersetzten Dateien auch überall greifen.",
                      "Nothing more to do. A restart is a good idea so the replaced files take effect everywhere."),
                UrsacheBeschaedigung + "\n\n" + UrsacheVorbeugen);

        if (Enthaelt(t, "keine integritätsverletzungen", "keine integritaetsverletzungen",
                        "did not find any integrity violations"))
            return new(PruefBefund.Sauber,
                Loc.T("Die Windows-Systemdateien sind unbeschädigt.", "The Windows system files are undamaged."),
                Loc.T(
                    "Hier ist nichts zu reparieren. Wenn dein Problem bleibt, liegt es nicht an beschädigten " +
                    "Systemdateien - dann sind Treiber, Autostart oder Hardware die nächsten Kandidaten.",
                    "There's nothing to repair here. If your problem remains, it's not due to damaged " +
                    "system files - drivers, startup programs, or hardware are then the next candidates."));

        if (Enthaelt(t, "kann die angeforderte aktion nicht ausführen",
                        "could not perform the requested operation"))
            return new(PruefBefund.NichtGelaufen,
                Loc.T("Die Prüfung konnte nicht ausgeführt werden.", "The check could not be performed."),
                Loc.T(
                    "Häufigste Ursache: eine andere Reparatur läuft noch, oder ein Neustart steht aus. " +
                    "PC neu starten und erneut versuchen.",
                    "Most common cause: another repair is still running, or a restart is pending. " +
                    "Restart the PC and try again."));

        if (Enthaelt(t, "wird gerade ein anderer wartungsvorgang", "another servicing or repair operation"))
            return new(PruefBefund.NichtGelaufen,
                Loc.T("Es läuft bereits ein anderer Wartungsvorgang von Windows.", "Another Windows servicing operation is already running."),
                Loc.T(
                    "Warten, bis Windows fertig ist (z.B. eine laufende Update-Installation), " +
                    "oder neu starten und dann erneut versuchen.",
                    "Wait until Windows is finished (e.g. a running update installation), " +
                    "or restart and then try again."));

        return new(PruefBefund.Unklar,
            Loc.T("Die Ausgabe der Prüfung war nicht eindeutig zu deuten.", "The check's output could not be clearly interpreted."),
            Loc.T("Die Originalausgabe steht unten - die hilft bei einer Suche im Netz oder in einem Forum weiter.",
                  "The original output is shown below - it helps when searching online or in a forum."));
    }

    /// <summary>Deutet die Ausgabe von <c>DISM /Online /Cleanup-Image /ScanHealth</c> bzw. <c>/RestoreHealth</c>.</summary>
    public static PruefErgebnis DismDeuten(string? rohausgabe)
    {
        var t = Bereinigen(rohausgabe).ToLowerInvariant();
        if (t.Length == 0)
            return new(PruefBefund.NichtGelaufen,
                Loc.T("Die Prüfung hat keine Ausgabe geliefert.", "The check produced no output."),
                Loc.T("Meist fehlen Administrator-Rechte. Toolbox X als Administrator neu starten und erneut versuchen.",
                      "Usually administrator rights are missing. Restart Toolbox X as administrator and try again."));

        // Auch hier: der reparierbare Fall enthaelt ebenfalls "beschaedigt",
        // deshalb zuerst die spezifischeren Meldungen pruefen.
        if (Enthaelt(t, "der speicher ist reparierbar", "the component store is repairable"))
            return new(PruefBefund.BeschaedigtReparierbar,
                Loc.T("Der Windows-Komponentenspeicher ist beschädigt, lässt sich aber reparieren.",
                      "The Windows component store is damaged, but can be repaired."),
                Loc.T(
                    "Jetzt die Reparatur starten (DISM RestoreHealth). Das dauert je nach PC 10 bis 30 Minuten " +
                    "und braucht eine Internetverbindung, weil Windows sich die Ersatzdateien holt.",
                    "Start the repair now (DISM RestoreHealth). Depending on the PC this takes 10 to 30 minutes " +
                    "and needs an internet connection, since Windows fetches the replacement files."),
                UrsacheBeschaedigung + "\n\n" + UrsacheVorbeugen);

        if (Enthaelt(t, "der wiederherstellungsvorgang wurde erfolgreich abgeschlossen",
                        "the restore operation completed successfully",
                        "wiederherstellungsvorgang wurde abgeschlossen"))
            return new(PruefBefund.BeschaedigtBehoben,
                Loc.T("Der Komponentenspeicher wurde erfolgreich repariert.", "The component store was successfully repaired."),
                Loc.T(
                    "Jetzt die Systemdateiprüfung (SFC) laufen lassen - erst danach werden die beschädigten " +
                    "Systemdateien selbst ersetzt. DISM repariert nur das Lager, SFC die Dateien daraus.",
                    "Now run the system file check (SFC) - only that step replaces the damaged " +
                    "system files themselves. DISM only repairs the storage, SFC the files from it."),
                UrsacheBeschaedigung + "\n\n" + UrsacheVorbeugen);

        if (Enthaelt(t, "kein komponentenspeicherfehler", "keine komponentenspeicherbeschädigung",
                        "no component store corruption detected"))
            return new(PruefBefund.Sauber,
                Loc.T("Der Windows-Komponentenspeicher ist unbeschädigt.", "The Windows component store is undamaged."),
                Loc.T(
                    "Hier ist nichts zu reparieren. Falls SFC trotzdem Fehler meldet, kann SFC sie jetzt " +
                    "auch tatsächlich beheben - das Lager mit den Ersatzdateien ist ja in Ordnung.",
                    "There's nothing to repair here. If SFC still reports errors, SFC can now " +
                    "actually fix them too - the storage with the replacement files is fine after all."));

        if (Enthaelt(t, "die quelldateien konnten nicht gefunden werden",
                        "the source files could not be found",
                        "0x800f081f"))
            return new(PruefBefund.BeschaedigtNichtBehebbar,
                Loc.T("Windows hat die Ersatzdateien für die Reparatur nicht gefunden (Fehler 0x800F081F).",
                      "Windows could not find the replacement files for the repair (error 0x800F081F)."),
                Loc.T(
                    "Zwei häufige Ursachen: (1) keine Internetverbindung oder Windows Update ist blockiert - " +
                    "Verbindung prüfen, Windows-Update-Dienst nicht abgeschaltet lassen. (2) Windows Update ist selbst " +
                    "beschädigt. Dann hilft eine Reparatur-Installation über das offizielle Windows-Installationsmedium, " +
                    "die Programme und Daten behält.",
                    "Two common causes: (1) no internet connection, or Windows Update is blocked - " +
                    "check the connection, don't leave the Windows Update service disabled. (2) Windows Update itself is " +
                    "damaged. In that case, a repair installation via the official Windows installation media helps, " +
                    "which keeps programs and data."));

        if (Enthaelt(t, "0x800f0906", "die quelldateien konnten nicht heruntergeladen werden",
                        "the source files could not be downloaded"))
            return new(PruefBefund.BeschaedigtNichtBehebbar,
                Loc.T("Windows konnte die Ersatzdateien nicht herunterladen (Fehler 0x800F0906).",
                      "Windows could not download the replacement files (error 0x800F0906)."),
                Loc.T(
                    "Internetverbindung prüfen. Falls der PC in einem Firmennetz hängt, blockiert oft eine " +
                    "Update-Richtlinie (WSUS) den Download - dann ist die IT-Abteilung zuständig.",
                    "Check the internet connection. If the PC is on a company network, an " +
                    "update policy (WSUS) often blocks the download - the IT department is responsible in that case."));

        return new(PruefBefund.Unklar,
            Loc.T("Die Ausgabe der Prüfung war nicht eindeutig zu deuten.", "The check's output could not be clearly interpreted."),
            Loc.T("Die Originalausgabe steht unten - die hilft bei einer Suche im Netz oder in einem Forum weiter.",
                  "The original output is shown below - it helps when searching online or in a forum."));
    }

    private static bool Enthaelt(string text, params string[] bausteine) =>
        bausteine.Any(b => text.Contains(b, StringComparison.Ordinal));
}
