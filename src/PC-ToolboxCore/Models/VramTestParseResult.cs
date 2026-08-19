namespace PCToolboxCore.Models;

// Reines Datenobjekt fuer das Ergebnis, das MemtestVulkanOutputParser aus der
// Konsolenausgabe von memtest_vulkan zusammentraegt. Enthaelt bewusst nur das,
// was aus dem Text wirklich herauslesbar ist - keine geschaetzten/erfundenen Werte.
public class VramTestParseResult
{
    public int IterationenAbgeschlossen { get; set; }
    public double GbGeschriebenGesamt { get; set; }
    public double GbGelesenGesamt { get; set; }
    public double? LetzterDurchsatzGbProSek { get; set; }

    public int FehlerAnzahl { get; set; }
    public List<string> FehlerDetails { get; } = new();

    // Zeitpunkt jedes gefundenen Fehlers (best effort - nur gefuellt, wenn der Aufrufer
    // ZeileVerarbeiten mit einem Zeitpunkt fuettert, s. VramTestService). Grundlage fuer den
    // Abgleich mit der parallel mitgeloggten GPU-Temperatur (s. VramThermalAuswertung) -
    // faellt ein Fehler zeitlich mit einer besonders hohen Temperatur zusammen, deutet das eher
    // auf Ueberhitzung als auf einen echten Speicherdefekt hin.
    public List<DateTime> FehlerZeitpunkte { get; } = new();

    public bool Abgestuerzt { get; set; }
    public string? AbsturzMeldung { get; set; }

    // true nur, wenn memtest_vulkan selbst die abschliessende
    // "no any errors, testing PASSed"-Meldung ausgegeben hat (seltener Fall,
    // da wir den Prozess ueblicherweise nach Ablauf der Testdauer selbst beenden,
    // bevor das Tool von sich aus fertig wird).
    public bool ToolMeldetExplizitBestanden { get; set; }
}
