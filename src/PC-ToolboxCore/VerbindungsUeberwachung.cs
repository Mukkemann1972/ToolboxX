namespace PCToolboxCore;

public record VerbindungsMessung(DateTime Zeitpunkt, bool Erfolgreich);

public record AusfallPeriode(DateTime Start, DateTime? Ende)
{
    public TimeSpan? Dauer => Ende is { } ende ? ende - Start : null;
}

public record VerbindungsAuswertung(
    int AnzahlMessungen,
    int AnzahlAusfaelle,
    IReadOnlyList<AusfallPeriode> AusfallPerioden);

/// <summary>
/// Reine Auswertung einer Zeitreihe von Ping-Messungen (Rang 5 aus der NirSoft-Recherche,
/// Vorbild PingInfoView): die bestehende Netzwerk-Diagnose ist eine Momentaufnahme und kann
/// den haeufigsten echten Beschwerdefall - "das Internet geht MANCHMAL weg" - grundsaetzlich
/// nicht finden. Erst eine Messung ueber Zeit macht sporadische Aussetzer sichtbar.
///
/// Bewusst getrennt vom eigentlichen Ping-Vorgang (der ist Windows-spezifisch, siehe
/// PC-Toolbox/Services/VerbindungsUeberwachungService.cs) - hier nur die Auswertung einer
/// fertigen Messreihe, deshalb auf dem Pi mit erfundenen Zeitreihen testbar.
/// </summary>
public static class VerbindungsUeberwachung
{
    public static VerbindungsAuswertung Auswerten(IReadOnlyList<VerbindungsMessung> messungen)
    {
        var perioden = new List<AusfallPeriode>();
        DateTime? laufenderAusfallStart = null;

        foreach (var messung in messungen.OrderBy(m => m.Zeitpunkt))
        {
            if (!messung.Erfolgreich)
            {
                laufenderAusfallStart ??= messung.Zeitpunkt;
            }
            else if (laufenderAusfallStart is { } start)
            {
                perioden.Add(new AusfallPeriode(start, messung.Zeitpunkt));
                laufenderAusfallStart = null;
            }
        }

        // Laeuft der letzte Ausfall noch (keine erfolgreiche Messung danach), bleibt "Ende" leer -
        // das UI zeigt das dann als "seit ... andauernd" statt eine falsche Dauer zu erfinden.
        if (laufenderAusfallStart is { } offenerStart)
            perioden.Add(new AusfallPeriode(offenerStart, null));

        return new VerbindungsAuswertung(
            AnzahlMessungen: messungen.Count,
            AnzahlAusfaelle: messungen.Count(m => !m.Erfolgreich),
            AusfallPerioden: perioden);
    }
}
