using System.Globalization;
using System.Text.RegularExpressions;
using PCToolboxCore.Models;

namespace PCToolboxCore;

// Parst die Konsolenausgabe von memtest_vulkan (github.com/GpuZelenograd/memtest_vulkan,
// MIT-Lizenz) zeilenweise. Das Tool selbst hat keine Kommandozeilenoptionen und keinen
// definierten Exit-Code fuer Erfolg/Fehler - es laeuft bis Ctrl+C. Deshalb liest
// VramTestService (PC-Toolbox-Projekt) die Ausgabe live mit, fuettert sie hier zeilenweise
// rein und beendet den Prozess selbst nach der eingestellten Testdauer. Diese Klasse bleibt
// bewusst plattformneutral (kein Process/WMI-Zugriff), damit sie echt getestet werden kann.
//
// Bekannte Zeilenformate (Stand v0.3.0-v0.5.0):
//   "      1 iteration. Since last report passed 271.3561ms      written     1.8GB, read:     3.5GB     19.3GB/sec"
//   "Error found. Mode NEXT_RE_READ, total errors 0x3C7EC3 out of 0x3C000000 (0.39384872%)"
//   "Runtime error: ERROR_DEVICE_LOST while getting () in context wait_for_fences"
//   "memtest_vulkan: no any errors, testing PASSed."
public static class MemtestVulkanOutputParser
{
    private static readonly Regex IterationZeile = new(
        @"(?<n>\d+)\s+iteration\.\s+Since last report passed\s+(?<ms>[\d.]+)ms\s+written\s+(?<written>[\d.]+)GB,\s*read:\s+(?<read>[\d.]+)GB\s+(?<rate>[\d.]+)GB/sec",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FehlerZeile = new(
        @"Error found\.\s*(?<detail>.*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AbsturzZeile = new(
        @"Runtime error:\s*(?<detail>.*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static VramTestParseResult Parse(IEnumerable<string> zeilen)
    {
        var ergebnis = new VramTestParseResult();
        foreach (var zeile in zeilen)
            ZeileVerarbeiten(zeile, ergebnis);
        return ergebnis;
    }

    // Wird von VramTestService pro empfangener Konsolenzeile aufgerufen, waehrend
    // memtest_vulkan noch laeuft - mutiert das uebergebene Ergebnisobjekt direkt,
    // damit der Aufrufer den Live-Fortschritt (Iterationen/GB) anzeigen kann. "zeitpunkt"
    // ist optional (Parse() beim Batch-Verarbeiten fertiger Zeilenlisten braucht ihn nicht) -
    // nur wenn er mitgegeben wird, landet er in FehlerZeitpunkte fuer den spaeteren
    // Temperatur-Abgleich (s. VramThermalAuswertung).
    public static void ZeileVerarbeiten(string zeile, VramTestParseResult ergebnis, DateTime? zeitpunkt = null)
    {
        if (string.IsNullOrWhiteSpace(zeile)) return;

        var absturz = AbsturzZeile.Match(zeile);
        if (absturz.Success)
        {
            ergebnis.Abgestuerzt = true;
            ergebnis.AbsturzMeldung = absturz.Groups["detail"].Value.Trim();
            return;
        }

        var fehler = FehlerZeile.Match(zeile);
        if (fehler.Success)
        {
            ergebnis.FehlerAnzahl++;
            ergebnis.FehlerDetails.Add(fehler.Groups["detail"].Value.Trim());
            if (zeitpunkt is { } zp) ergebnis.FehlerZeitpunkte.Add(zp);
            return;
        }

        if (zeile.Contains("no any errors", StringComparison.OrdinalIgnoreCase) &&
            zeile.Contains("PASS", StringComparison.OrdinalIgnoreCase))
        {
            ergebnis.ToolMeldetExplizitBestanden = true;
            return;
        }

        var iteration = IterationZeile.Match(zeile);
        if (iteration.Success)
        {
            ergebnis.IterationenAbgeschlossen++;
            ergebnis.GbGeschriebenGesamt += ParseKommazahl(iteration.Groups["written"].Value);
            ergebnis.GbGelesenGesamt += ParseKommazahl(iteration.Groups["read"].Value);
            ergebnis.LetzterDurchsatzGbProSek = ParseKommazahl(iteration.Groups["rate"].Value);
        }
    }

    private static double ParseKommazahl(string wert) =>
        double.Parse(wert, NumberStyles.Float, CultureInfo.InvariantCulture);
}
