using PCToolbox.Models;

namespace PCToolboxCore;

/// <summary>Ein Gerät samt Treiberangaben, so wie Windows es meldet.</summary>
public class GeraetBefund
{
    public string Name { get; set; } = "";
    public string? Hersteller { get; set; }

    /// <summary>Gerätekategorie laut Windows (z.B. "Net", "Display", "USB").</summary>
    public string? Klasse { get; set; }

    /// <summary>0 = alles in Ordnung, alles andere ist eine Fehlernummer des Geräte-Managers.</summary>
    public int FehlerCode { get; set; }

    public string? TreiberVersion { get; set; }
    public DateTime? TreiberDatum { get; set; }
}

/// <summary>Fertige Einschätzung eines Geräts für die Anzeige.</summary>
public class TreiberBewertung
{
    public AmpelStatus Ampel { get; init; }
    public string Kurzform { get; init; } = "";
    public string WasEsIst { get; init; } = "";
    public string Empfehlung { get; init; } = "";
}

/// <summary>
/// Ordnet Geräte und ihre Treiber ein.
///
/// Wichtigste Design-Entscheidung: <b>Alter allein ist kein Fehler.</b> Ein Treiber von
/// 2016 für eine Tastatur ist völlig in Ordnung - Tastaturen haben sich nicht geändert.
/// Genau mit dieser Angstmache ("47 veraltete Treiber gefunden!") arbeiten die üblichen
/// Treiber-Updater. Ein altes Datum wird hier deshalb nur als Hinweis gezeigt, und auch
/// das nur, wenn es wirklich alt ist - niemals als roter Alarm.
/// </summary>
public static class TreiberBewerter
{
    /// <summary>Ab hier lohnt der Hinweis - vorher ist "alt" schlicht normal.</summary>
    private const int HinweisAbJahren = 8;

    public static TreiberBewertung Bewerten(GeraetBefund geraet, DateTime heute)
    {
        if (geraet.FehlerCode != 0)
        {
            var erklaerung = GeraeteFehlerCodes.Erklaeren(geraet.FehlerCode);

            // Code 22 (abgeschaltet), 24 und 45 (nicht angeschlossen) sind bewusst gelb:
            // Das ist meist ein gewollter Zustand, kein Defekt.
            var harmlos = geraet.FehlerCode is 22 or 24 or 45 or 21;

            return new TreiberBewertung
            {
                Ampel = harmlos ? AmpelStatus.Gelb : AmpelStatus.Rot,
                Kurzform = erklaerung.Kurzform,
                WasEsIst = erklaerung.WasEsIst,
                Empfehlung = erklaerung.Empfehlung
            };
        }

        if (geraet.TreiberDatum is { } datum && Jahre(datum, heute) >= HinweisAbJahren)
            return new TreiberBewertung
            {
                Ampel = AmpelStatus.Gelb,
                Kurzform = Loc.T($"Funktioniert - Treiber ist von {datum:MMMM yyyy}", $"Works - driver is from {datum:MMMM yyyy}"),
                WasEsIst = Loc.T("Das Gerät läuft ohne Fehler. Der Treiber ist nur schon recht betagt.",
                                 "The device runs without errors. The driver is just quite old already."),
                Empfehlung = Loc.T(
                    "Kein Handlungsbedarf, solange alles funktioniert - alt heisst nicht " +
                    "schlecht. Falls du aber ausgerechnet mit diesem Gerät Probleme hast, " +
                    "ist ein neuerer Treiber vom Hersteller der erste sinnvolle Versuch.",
                    "No action needed as long as everything works - old doesn't mean " +
                    "bad. But if you're having problems with this specific device, " +
                    "a newer driver from the manufacturer is the first sensible thing to try.")
            };

        return new TreiberBewertung
        {
            Ampel = AmpelStatus.Gruen,
            Kurzform = Loc.T("Funktioniert", "Works"),
            WasEsIst = Loc.T("Windows meldet zu diesem Gerät keinerlei Probleme.", "Windows reports no problems at all with this device."),
            Empfehlung = Loc.T("Nichts zu tun.", "Nothing to do.")
        };
    }

    private static int Jahre(DateTime von, DateTime bis)
    {
        var jahre = bis.Year - von.Year;
        if (bis.Month < von.Month || (bis.Month == von.Month && bis.Day < von.Day)) jahre--;
        return jahre;
    }

    /// <summary>Ein Satz Gesamturteil über alle geprüften Geräte.</summary>
    public static string Zusammenfassung(int geprueft, int mitProblem, int nurHinweis)
    {
        if (geprueft == 0)
            return Loc.T("Es konnten keine Geräte ausgelesen werden.", "No devices could be read.");

        if (mitProblem == 0 && nurHinweis == 0)
            return Loc.T($"{geprueft} Geräte geprüft - Windows meldet bei keinem ein Problem.",
                         $"{geprueft} devices checked - Windows reports no problem with any of them.");

        if (mitProblem == 0)
            return Loc.T($"{geprueft} Geräte geprüft - kein Fehler. {nurHinweis} Punkt(e) nur zur Kenntnis.",
                         $"{geprueft} devices checked - no errors. {nurHinweis} point(s) for information only.");

        return mitProblem == 1
            ? Loc.T($"{geprueft} Geräte geprüft - bei 1 Gerät meldet Windows ein Problem.",
                    $"{geprueft} devices checked - Windows reports a problem with 1 device.")
            : Loc.T($"{geprueft} Geräte geprüft - bei {mitProblem} Geräten meldet Windows ein Problem.",
                    $"{geprueft} devices checked - Windows reports a problem with {mitProblem} devices.");
    }
}
