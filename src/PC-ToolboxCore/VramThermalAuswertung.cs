namespace PCToolboxCore;

public record TemperaturMesspunkt(DateTime Zeitpunkt, double Celsius);

/// <summary>Ergebnis des Temperatur-Abgleichs für einen VRAM-Test (s. VramThermalAuswertung).
/// SpitzentemperaturCelsius ist null, wenn während des Tests keine GPU-Temperatur verfügbar war
/// (z.B. PawnIO nicht installiert) - dann kann/darf keine thermische Aussage getroffen werden.</summary>
public record VramThermalErgebnis(double? SpitzentemperaturCelsius, int AnzahlFehlerGesamt, int AnzahlFehlerBeiHoherTemperatur)
{
    public bool AlleFehlerBeiHoherTemperatur => AnzahlFehlerGesamt > 0 && AnzahlFehlerBeiHoherTemperatur == AnzahlFehlerGesamt;
    public bool EinzelneFehlerBeiHoherTemperatur => AnzahlFehlerBeiHoherTemperatur > 0 && !AlleFehlerBeiHoherTemperatur;
}

/// <summary>
/// Ordnet die Fehler-Zeitpunkte eines VRAM-Tests (MemtestVulkanOutputParser.
/// VramTestParseResult.FehlerZeitpunkte) gegen einen parallel mitgeloggten GPU-Temperaturverlauf
/// ein - unterscheidet "Speicherchip wahrscheinlich defekt" von "Karte ist beim Fehler gerade
/// überhitzt/gedrosselt", ein sehr reales Werkstatt-Urteil (z.B. eingetrocknete
/// Wärmeleitpaste sieht in einem reinen Fehler-Log genauso aus wie ein Hardwaredefekt).
///
/// Bewusst konservativ: ohne jeden Temperaturmesspunkt wird gar keine Aussage getroffen (null
/// statt einer geratenen Prozentzahl) - lieber ehrlich "keine Daten" als eine erfundene
/// Korrelation.
/// </summary>
public static class VramThermalAuswertung
{
    // Übliche GPU-Throttle-Schwelle (NVIDIA/AMD-Karten drosseln typischerweise zwischen 83°C
    // und 95°C, je nach Modell) - konservativ am unteren Ende gewählt, damit eher zu viele als
    // zu wenige Fehler als "möglicherweise thermisch" markiert werden.
    private const double ThrottleSchwelleCelsius = 83;

    // Toleranzfenster zwischen einem Fehler-Zeitpunkt und dem nächstgelegenen Temperatur-
    // Messpunkt - die Messungen laufen alle paar Sekunden, nicht exakt im selben Moment wie
    // ein gemeldeter Fehler.
    private static readonly TimeSpan ToleranzFenster = TimeSpan.FromSeconds(15);

    public static VramThermalErgebnis Auswerten(IReadOnlyList<DateTime> fehlerZeitpunkte, IReadOnlyList<TemperaturMesspunkt> temperaturVerlauf)
    {
        if (temperaturVerlauf.Count == 0)
            return new VramThermalErgebnis(null, fehlerZeitpunkte.Count, 0);

        var spitze = temperaturVerlauf.Max(t => t.Celsius);

        var fehlerBeiHoherTemperatur = fehlerZeitpunkte.Count(fz =>
        {
            var naechster = temperaturVerlauf.OrderBy(t => (t.Zeitpunkt - fz).Duration()).First();
            return (naechster.Zeitpunkt - fz).Duration() <= ToleranzFenster && naechster.Celsius >= ThrottleSchwelleCelsius;
        });

        return new VramThermalErgebnis(spitze, fehlerZeitpunkte.Count, fehlerBeiHoherTemperatur);
    }
}
