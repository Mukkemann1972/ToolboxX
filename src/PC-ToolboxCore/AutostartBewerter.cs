using PCToolbox.Models;
using PCToolboxCore.Models;

namespace PCToolboxCore;

/// <summary>
/// Ordnet einen Autostart-Eintrag in die Ampel ein und liefert die Erklaerung dazu.
///
/// Zwei Grundsaetze, die den Unterschied zu den ueblichen "Autostart-Optimierern" machen:
/// 1. Es wird nicht geraten. Was nicht im Regelkatalog steht, heisst "kennen wir nicht" -
///    mit einer Anleitung, wie man das selbst gefahrlos herausfindet.
/// 2. Kein Eintrag wird als "loeschen!" dargestellt. Ausschalten ist immer rueckholbar,
///    und genau so steht es auch in den Texten.
/// </summary>
public static class AutostartBewerter
{
    /// <summary>Ordner, in denen normale Autostart-Programme nichts verloren haben.</summary>
    private static readonly string[] VerdaechtigeOrdner =
    [
        @"\appdata\local\temp\",
        @"\windows\temp\",
        @"\downloads\",
        @"\users\public\",
        @"\recycle"
    ];

    private static readonly string[] UpdaterWortteile = ["update", "upgrade"];

    public static AutostartBewertung Bewerten(AutostartEintrag eintrag)
    {
        // 1. Karteileiche zuerst: Ein Eintrag, der ins Leere zeigt, muss gar nicht
        //    erst eingeordnet werden - da startet ohnehin nichts mehr.
        if (eintrag.DateiFehlt)
            return new AutostartBewertung
            {
                Kategorie = AutostartKategorie.Karteileiche,
                Ampel = AmpelStatus.Gelb,
                Kurzurteil = Loc.T("Zeigt ins Leere", "Points to nothing"),
                WasEsIst = Loc.T(
                    "Hier ist ein Startbefehl hinterlegt, aber das Programm dazu gibt es auf diesem PC " +
                    "nicht mehr - meist ein Rest einer alten Deinstallation.",
                    "A startup command is stored here, but the program for it no longer exists on this PC " +
                    "- usually a leftover from an old uninstallation."),
                Empfehlung = Loc.T(
                    "Gefahrlos zum Ausschalten. Es passiert dadurch nichts, denn es startet sowieso " +
                    "nichts mehr. Windows versucht bei jedem Start trotzdem kurz, es zu öffnen - " +
                    "das kostet unnötig Zeit.",
                    "Safe to turn off. Nothing happens as a result, since nothing starts anyway. " +
                    "Windows still briefly tries to open it at every startup - " +
                    "which costs unnecessary time.")
            };

        // 2. Bekanntes Programm aus dem Katalog (schlaegt alle Heuristiken)
        var regel = AutostartRegeln.Finde(eintrag.DateiName);
        if (regel is not null)
            return new AutostartBewertung
            {
                Kategorie = regel.Kategorie,
                Ampel = AmpelZu(regel.Kategorie),
                Kurzurteil = KurzurteilZu(regel.Kategorie, regel.Anzeige),
                WasEsIst = $"{regel.Anzeige}: {regel.WasEsIst}",
                Empfehlung = regel.Empfehlung
            };

        // 3. Ungewoehnlicher Ort. Absichtlich VOR der Updater-Regel: Ein "Updater",
        //    der aus dem Temp-Ordner startet, ist eher ein Problem als eine Bremse.
        if (AusVerdaechtigemOrdner(eintrag.ProgrammPfad))
            return new AutostartBewertung
            {
                Kategorie = AutostartKategorie.Verdaechtig,
                Ampel = AmpelStatus.Rot,
                Kurzurteil = Loc.T("Startet von einem ungewöhnlichen Ort", "Starts from an unusual location"),
                WasEsIst = Loc.T(
                    "Dieses Programm startet aus einem Ordner für Zwischendateien oder Downloads. " +
                    "Ordentlich installierte Programme liegen dort nicht - sie liegen unter " +
                    "\"Programme\" oder im Programmordner deines Benutzerkontos.",
                    "This program starts from a folder for temp files or downloads. " +
                    "Properly installed programs don't live there - they live under " +
                    "\"Program Files\" or in your user account's program folder."),
                Empfehlung = Loc.T(
                    "Das ist kein Beweis für etwas Schlechtes, aber es ist auffällig. Schalte es aus " +
                    "und starte den PC neu - fehlt dir nichts, lass es aus. Wenn du den Namen gar " +
                    "nicht kennst, lass zusätzlich einen Virenscan laufen (Windows-Sicherheit reicht).",
                    "This isn't proof of anything bad, but it is unusual. Turn it off " +
                    "and restart the PC - if you don't miss anything, leave it off. If you don't " +
                    "recognize the name at all, also run a virus scan (Windows Security is enough).")
            };

        // 4. Reine Updateprueferei (haeufigste unnoetige Startbremse ueberhaupt)
        if (IstUpdater(eintrag.Name) || IstUpdater(eintrag.DateiName))
            return new AutostartBewertung
            {
                Kategorie = AutostartKategorie.Bremse,
                Ampel = AmpelStatus.Rot,
                Kurzurteil = Loc.T("Sucht nur nach Updates", "Only checks for updates"),
                WasEsIst = Loc.T(
                    "Dieser Eintrag prüft im Hintergrund, ob es für ein Programm eine neuere Version gibt. " +
                    "Er wird dafür bei jedem Start mitgeladen.",
                    "This entry checks in the background whether a newer version of a program is available. " +
                    "It's loaded at every startup just for that."),
                Empfehlung = Loc.T(
                    "Kannst du ausschalten. Das eigentliche Programm läuft normal weiter - es sucht dann " +
                    "eben erst dann nach Updates, wenn du es selbst öffnest.",
                    "Safe to turn off. The actual program keeps working normally - it just checks " +
                    "for updates only when you open it yourself.")
            };

        // 5. Ehrlich: kennen wir nicht.
        return new AutostartBewertung
        {
            Kategorie = AutostartKategorie.Unbekannt,
            Ampel = AmpelStatus.Gelb,
            Kurzurteil = Loc.T("Kennen wir nicht", "We don't recognize this"),
            WasEsIst = string.IsNullOrWhiteSpace(eintrag.Herausgeber)
                ? Loc.T(
                    "Dieses Programm steht nicht in unserer Liste, und es verrät auch selbst nicht, von wem es stammt. " +
                    "Das muss nichts Schlechtes heißen - es gibt sehr viele kleine Programme.",
                    "This program isn't in our list, and it doesn't reveal who it's from either. " +
                    "That doesn't have to mean anything bad - there are very many small programs.")
                : Loc.T(
                    $"Dieses Programm steht nicht in unserer Liste. Als Herausgeber ist \"{eintrag.Herausgeber}\" " +
                    "hinterlegt - erkennst du den Namen, ist es vermutlich etwas, das du selbst installiert hast.",
                    $"This program isn't in our list. \"{eintrag.Herausgeber}\" is listed as the publisher " +
                    "- if you recognize the name, it's probably something you installed yourself."),
            Empfehlung = Loc.T(
                "So findest du es gefahrlos selbst heraus: ausschalten, PC neu starten, einen Tag normal " +
                "arbeiten. Fehlt nichts, lass es aus. Fehlt etwas, schaltest du es hier mit einem Klick " +
                "wieder ein - der Eintrag wird nicht gelöscht, nur stillgelegt.",
                "Here's how to safely find out for yourself: turn it off, restart the PC, work normally " +
                "for a day. If nothing's missing, leave it off. If something's missing, switch it back on here " +
                "with one click - the entry is not deleted, only disabled.")
        };
    }

    private static bool AusVerdaechtigemOrdner(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return false;

        var klein = pfad.Replace('/', '\\').ToLowerInvariant();
        return VerdaechtigeOrdner.Any(ordner => klein.Contains(ordner, StringComparison.Ordinal));
    }

    private static bool IstUpdater(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var klein = text.ToLowerInvariant();
        return UpdaterWortteile.Any(teil => klein.Contains(teil, StringComparison.Ordinal));
    }

    private static AmpelStatus AmpelZu(AutostartKategorie kategorie) => kategorie switch
    {
        AutostartKategorie.Systemnah => AmpelStatus.Gruen,
        AutostartKategorie.Nuetzlich => AmpelStatus.Gruen,
        AutostartKategorie.Optional => AmpelStatus.Gelb,
        AutostartKategorie.Karteileiche => AmpelStatus.Gelb,
        AutostartKategorie.Unbekannt => AmpelStatus.Gelb,
        AutostartKategorie.Bremse => AmpelStatus.Rot,
        AutostartKategorie.Verdaechtig => AmpelStatus.Rot,
        _ => AmpelStatus.Unbekannt
    };

    private static string KurzurteilZu(AutostartKategorie kategorie, string anzeige) => kategorie switch
    {
        AutostartKategorie.Systemnah => Loc.T("Gehört dazu - bitte anlassen", "Belongs here - please leave it on"),
        AutostartKategorie.Nuetzlich => Loc.T("Sinnvoll, wenn du es nutzt", "Useful if you use it"),
        AutostartKategorie.Optional => Loc.T("Bequem, aber nicht nötig", "Convenient, but not necessary"),
        AutostartKategorie.Bremse => Loc.T("Unnötige Startbremse", "Unnecessary startup drag"),
        AutostartKategorie.Verdaechtig => Loc.T("Startet von einem ungewöhnlichen Ort", "Starts from an unusual location"),
        AutostartKategorie.Karteileiche => Loc.T("Zeigt ins Leere", "Points to nothing"),
        _ => anzeige
    };

    /// <summary>
    /// Eine Zeile Zusammenfassung fuer den Kopf des Moduls - damit man auf einen Blick
    /// sieht, ob es ueberhaupt etwas zu tun gibt, ohne die ganze Liste durchzugehen.
    /// </summary>
    public static string Zusammenfassung(IEnumerable<AutostartBewertung> bewertungen, int aktiveEintraege)
    {
        var liste = bewertungen.ToList();
        var bremsen = liste.Count(b => b.Ampel == AmpelStatus.Rot);

        if (aktiveEintraege == 0)
            return Loc.T("Es startet aktuell kein zusätzliches Programm mit Windows mit.",
                         "No additional program currently starts with Windows.");

        var anzahlText = aktiveEintraege == 1
            ? Loc.T("1 Programm startet automatisch mit", "1 program starts automatically")
            : Loc.T($"{aktiveEintraege} Programme starten automatisch mit", $"{aktiveEintraege} programs start automatically");

        return bremsen switch
        {
            0 => Loc.T($"{anzahlText}. Nichts davon bremst unnötig - sieht gut aus.",
                       $"{anzahlText}. None of them slow things down unnecessarily - looks good."),
            1 => Loc.T($"{anzahlText}. 1 davon bremst den Start unnötig.",
                       $"{anzahlText}. 1 of them unnecessarily slows down startup."),
            _ => Loc.T($"{anzahlText}. {bremsen} davon bremsen den Start unnötig.",
                       $"{anzahlText}. {bremsen} of them unnecessarily slow down startup.")
        };
    }
}
