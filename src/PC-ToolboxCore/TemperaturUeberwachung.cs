namespace PCToolboxCore;

public record TemperaturMessung(DateTime Zeitpunkt, string Bauteil, double? Celsius);

public record UeberschreitungsPeriode(string Bauteil, DateTime Start, DateTime? Ende, double SpitzenwertCelsius)
{
    public TimeSpan? Dauer => Ende is { } ende ? ende - Start : null;
}

public record TemperaturAuswertung(
    int AnzahlMessungen,
    IReadOnlyDictionary<string, double> HoechstwertJeBauteil,
    IReadOnlyList<UeberschreitungsPeriode> UeberschreitungsPerioden);

/// <summary>
/// Reine Auswertung einer Zeitreihe von Temperatur-Messungen - gleiches Grundmuster wie
/// VerbindungsUeberwachung (Netzwerk-Ausfallperioden), nur pro Bauteil statt nur "erreichbar
/// ja/nein" und mit einem Grenzwert statt festem Erfolg/Misserfolg. Die bestehende
/// Temperature-Anzeige ist eine Momentaufnahme - ob z.B. während eines Belastungstests die
/// Kühlung mithält oder die Werte zwischendurch über einen kritischen Wert steigen, sieht man
/// erst über Zeit.
///
/// Bewusst getrennt vom eigentlichen Mess-Vorgang (Windows-spezifisch,
/// TemperaturUeberwachungService) - hier nur die Auswertung einer fertigen Messreihe, deshalb
/// auf dem Pi mit erfundenen Zeitreihen testbar.
/// </summary>
public static class TemperaturUeberwachung
{
    public static TemperaturAuswertung Auswerten(IReadOnlyList<TemperaturMessung> messungen, double grenzwertCelsius)
    {
        var hoechstwerte = new Dictionary<string, double>();
        var perioden = new List<UeberschreitungsPeriode>();

        foreach (var gruppe in messungen.Where(m => m.Celsius.HasValue).GroupBy(m => m.Bauteil))
        {
            DateTime? offenerStart = null;
            var offenerSpitzenwert = 0.0;

            foreach (var m in gruppe.OrderBy(x => x.Zeitpunkt))
            {
                var celsius = m.Celsius!.Value;
                hoechstwerte[gruppe.Key] = hoechstwerte.TryGetValue(gruppe.Key, out var bisher) ? Math.Max(bisher, celsius) : celsius;

                if (celsius >= grenzwertCelsius)
                {
                    offenerStart ??= m.Zeitpunkt;
                    offenerSpitzenwert = Math.Max(offenerSpitzenwert, celsius);
                }
                else if (offenerStart is { } start)
                {
                    perioden.Add(new UeberschreitungsPeriode(gruppe.Key, start, m.Zeitpunkt, offenerSpitzenwert));
                    offenerStart = null;
                    offenerSpitzenwert = 0;
                }
            }

            // Laeuft die letzte Ueberschreitung noch (keine Messung darunter danach), bleibt
            // "Ende" leer - das UI zeigt das dann als "seit ... andauernd" statt eine falsche
            // Dauer zu erfinden (wie bei VerbindungsUeberwachung).
            if (offenerStart is { } laufenderStart)
                perioden.Add(new UeberschreitungsPeriode(gruppe.Key, laufenderStart, null, offenerSpitzenwert));
        }

        return new TemperaturAuswertung(
            AnzahlMessungen: messungen.Count,
            HoechstwertJeBauteil: hoechstwerte,
            UeberschreitungsPerioden: perioden.OrderBy(p => p.Start).ToList());
    }
}
