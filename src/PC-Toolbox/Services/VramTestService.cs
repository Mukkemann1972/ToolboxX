using System.Diagnostics;
using System.IO;
using PCToolboxCore;
using PCToolboxCore.Models;
using PCToolbox.Models;

namespace PCToolbox.Services;

/// <summary>
/// Startet memtest_vulkan (github.com/GpuZelenograd/memtest_vulkan, zlib-Lizenz - erlaubt
/// kommerzielle Nutzung, siehe vendor/memtest_vulkan/LICENSE.txt) als externen Prozess,
/// liest die Konsolenausgabe live mit und wertet sie ueber MemtestVulkanOutputParser
/// (PC-ToolboxCore, dort auch getestet) aus. Die Original-Binary wird automatisch beim
/// Build mitgeliefert (s. PC-Toolbox.csproj) - kein manueller Download/Setup-Schritt fuer
/// Alex/Nutzer noetig, laeuft direkt aus der Toolbox heraus.
///
/// Das Tool selbst hat keine Kommandozeilenoption fuer eine feste Testdauer und keinen
/// dokumentierten Exit-Code - es laeuft bis Ctrl+C. Deshalb beenden WIR den Prozess nach
/// der eingestellten Dauer selbst (Kill), statt auf ein natuerliches Ende zu warten, und
/// werten aus, was bis dahin an Iterationen/Fehlern in der Ausgabe stand.
///
/// Fuer eine spaetere Pro-Version ist ein komplett selbst geschriebener Vulkan-Compute-
/// VRAM-Test als echtes Alleinstellungsmerkmal angedacht (s. GPU_DIAGNOSE_VISION.md) -
/// diese Klasse bleibt bewusst austauschbar (eigene Ergebnis-Typen, kein memtest_vulkan-
/// Detail sickert nach aussen), damit ein Ersatz spaeter ohne Umbau der View moeglich ist.
/// </summary>
public class VramTestService
{
    private const string RelativerToolPfad = "gpu-tools/memtest_vulkan/memtest_vulkan.exe";

    private readonly HardwareMonitorService _hardwareMonitor = new();

    public string ToolPfad => Path.Combine(AppContext.BaseDirectory, RelativerToolPfad);

    public bool ToolVorhanden => File.Exists(ToolPfad);

    public async Task<VramTestResult> TestenAsync(TimeSpan dauer, IProgress<string>? fortschritt, CancellationToken abbrechen = default)
    {
        if (!ToolVorhanden)
        {
            return new VramTestResult
            {
                Ampel = AmpelStatus.Unbekannt,
                ToolGefunden = false,
                Hinweis = Loc.T(
                    "memtest_vulkan.exe fehlt im Installationsordner (sollte eigentlich " +
                    $"immer unter \"{RelativerToolPfad}\" mitgeliefert werden) - Installation " +
                    "reparieren/neu entpacken.",
                    "memtest_vulkan.exe is missing from the installation folder (should always be " +
                    $"included under \"{RelativerToolPfad}\") - repair/re-extract the installation.")
            };
        }

        var parseErgebnis = new VramTestParseResult();
        var psi = new ProcessStartInfo(ToolPfad)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var prozess = new Process { StartInfo = psi };
        prozess.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            MemtestVulkanOutputParser.ZeileVerarbeiten(e.Data, parseErgebnis, DateTime.UtcNow);
            fortschritt?.Report(FortschrittsText(parseErgebnis));
        };

        // Läuft parallel zum eigentlichen Test mit, solange dieser läuft (eigener Token,
        // unabhängig von "abbrechen"/Timeout) - liefert die GPU-Temperaturkurve für den
        // späteren Thermal-Abgleich (s. VramThermalAuswertung). Ohne PawnIO bleibt die Liste
        // leer, der Abgleich trifft dann bewusst keine Aussage statt zu raten.
        using var temperaturToken = new CancellationTokenSource();
        var temperaturAufgabe = TemperaturVerlaufSammelnAsync(temperaturToken.Token);

        try
        {
            prozess.Start();
            prozess.BeginOutputReadLine();

            using var timeout = new CancellationTokenSource(dauer);
            using var verknuepft = CancellationTokenSource.CreateLinkedTokenSource(abbrechen, timeout.Token);
            try
            {
                await prozess.WaitForExitAsync(verknuepft.Token);
            }
            catch (OperationCanceledException)
            {
                // Erwarteter Fall: memtest_vulkan hat keinen eigenen Timer, wir beenden
                // es nach Ablauf der eingestellten Testdauer (oder Nutzer-Abbruch) selbst.
            }

            if (!prozess.HasExited)
            {
                try { prozess.Kill(entireProcessTree: true); }
                catch { /* Prozess kann zwischen HasExited-Check und Kill schon beendet sein */ }
            }
        }
        catch (Exception ex)
        {
            temperaturToken.Cancel();
            await temperaturAufgabe;
            return new VramTestResult
            {
                Ampel = AmpelStatus.Unbekannt,
                ToolGefunden = true,
                Details = parseErgebnis,
                Hinweis = Loc.T($"memtest_vulkan konnte nicht gestartet werden: {ex.Message}",
                                $"memtest_vulkan could not be started: {ex.Message}")
            };
        }

        temperaturToken.Cancel();
        var temperaturVerlauf = await temperaturAufgabe;
        var thermal = VramThermalAuswertung.Auswerten(parseErgebnis.FehlerZeitpunkte, temperaturVerlauf);

        return Bewerten(parseErgebnis, thermal);
    }

    private async Task<List<TemperaturMesspunkt>> TemperaturVerlaufSammelnAsync(CancellationToken token)
    {
        var verlauf = new List<TemperaturMesspunkt>();
        if (!_hardwareMonitor.IstPawnIoInstalliert()) return verlauf;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var readings = await _hardwareMonitor.GetTemperaturenAsync();
                foreach (var r in readings.Where(r => r.Bauteil.StartsWith("GPU", StringComparison.Ordinal) && r.Celsius.HasValue))
                    verlauf.Add(new TemperaturMesspunkt(DateTime.UtcNow, r.Celsius!.Value));

                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Erwartet: temperaturToken wird abgebrochen, sobald der eigentliche Test endet.
        }

        return verlauf;
    }

    private static string FortschrittsText(VramTestParseResult p) =>
        p.FehlerAnzahl > 0
            ? Loc.T($"{p.IterationenAbgeschlossen} Durchläufe, ⚠️ {p.FehlerAnzahl} Fehler gefunden ({p.GbGeschriebenGesamt:0.#} GB geschrieben)",
                    $"{p.IterationenAbgeschlossen} passes, ⚠️ {p.FehlerAnzahl} errors found ({p.GbGeschriebenGesamt:0.#} GB written)")
            : Loc.T($"{p.IterationenAbgeschlossen} Durchläufe, bisher keine Fehler ({p.GbGeschriebenGesamt:0.#} GB geschrieben)",
                    $"{p.IterationenAbgeschlossen} passes, no errors so far ({p.GbGeschriebenGesamt:0.#} GB written)");

    private static VramTestResult Bewerten(VramTestParseResult p, VramThermalErgebnis thermal)
    {
        if (p.Abgestuerzt)
        {
            return new VramTestResult
            {
                Ampel = AmpelStatus.Rot,
                ToolGefunden = true,
                Details = p,
                SpitzentemperaturCelsius = thermal.SpitzentemperaturCelsius,
                Hinweis = Loc.T(
                    $"Test abgebrochen (Treiber-/GPU-Fehler): {p.AbsturzMeldung}. Kann auf einen veralteten " +
                    "Treiber oder instabiles Übertakten hindeuten, nicht zwangsläufig auf defekten VRAM - " +
                    "Treiber aktualisieren, ggf. Übertaktung zurücksetzen und erneut testen.",
                    $"Test aborted (driver/GPU error): {p.AbsturzMeldung}. May indicate an outdated " +
                    "driver or unstable overclocking, not necessarily defective VRAM - " +
                    "update the driver, reset overclocking if applicable, and test again.")
            };
        }

        if (p.FehlerAnzahl > 0)
        {
            var basisText = Loc.T(
                $"{p.FehlerAnzahl} Speicherfehler gefunden nach {p.GbGeschriebenGesamt:0.#} GB getestetem VRAM.",
                $"{p.FehlerAnzahl} memory errors found after {p.GbGeschriebenGesamt:0.#} GB of tested VRAM.");

            string zusatz;
            if (thermal.AlleFehlerBeiHoherTemperatur)
            {
                zusatz = Loc.T(
                    $" Alle Fehler fielen zeitlich mit einer hohen GPU-Temperatur zusammen (Spitze " +
                    $"{thermal.SpitzentemperaturCelsius:0}°C) - das spricht eher für Überhitzung/Drosselung " +
                    "(z.B. eingetrocknete Wärmeleitpaste, verstaubte Kühlung) als für einen echten " +
                    "Speicherdefekt. Kühlung prüfen und den Test nach dem Reinigen/Erneuern der " +
                    "Wärmeleitpaste wiederholen, bevor du von defekten Chips ausgehst.",
                    $" All errors coincided with a high GPU temperature (peak {thermal.SpitzentemperaturCelsius:0}°C) " +
                    "- that points more toward overheating/throttling (e.g. dried-out thermal paste, dusty " +
                    "cooling) than an actual memory defect. Check the cooling and repeat the test after " +
                    "cleaning/replacing the thermal paste before assuming defective chips.");
            }
            else if (thermal.EinzelneFehlerBeiHoherTemperatur)
            {
                zusatz = Loc.T(
                    $" {thermal.AnzahlFehlerBeiHoherTemperatur} von {thermal.AnzahlFehlerGesamt} Fehlern fielen mit " +
                    $"einer hohen GPU-Temperatur zusammen (Spitze {thermal.SpitzentemperaturCelsius:0}°C) - die " +
                    "Kühlung mitprüfen lohnt sich, auch wenn nicht alle Fehler damit erklärt sind.",
                    $" {thermal.AnzahlFehlerBeiHoherTemperatur} of {thermal.AnzahlFehlerGesamt} errors coincided " +
                    $"with a high GPU temperature (peak {thermal.SpitzentemperaturCelsius:0}°C) - checking the " +
                    "cooling is worthwhile too, even though it doesn't explain every error.");
            }
            else if (thermal.SpitzentemperaturCelsius is { } spitze)
            {
                zusatz = Loc.T(
                    $" Die GPU-Temperatur blieb während der Fehler im normalen Bereich (Spitze {spitze:0}°C) - " +
                    "ein thermisches Problem ist damit unwahrscheinlich, das deutet stärker auf defekte " +
                    "Speicherchips hin.",
                    $" The GPU temperature stayed in a normal range during the errors (peak {spitze:0}°C) - a " +
                    "thermal cause is therefore unlikely, this points more toward defective memory chips.");
            }
            else
            {
                zusatz = "";
            }

            return new VramTestResult
            {
                Ampel = AmpelStatus.Rot,
                ToolGefunden = true,
                Details = p,
                SpitzentemperaturCelsius = thermal.SpitzentemperaturCelsius,
                Hinweis = basisText + zusatz + " " + Loc.T(
                    "Bei AMD-Karten vorher prüfen: Resizable BAR im BIOS einmal aus- und wieder einschalten und " +
                    "erneut testen, bevor du von einem Hardwaredefekt ausgehst (bekannter Sonderfall, kein Bug " +
                    "in diesem Test).",
                    "On AMD cards, check first: turn Resizable BAR off and back on in the BIOS and test again " +
                    "before assuming a hardware defect (a known special case, not a bug in this test).")
            };
        }

        if (p.IterationenAbgeschlossen == 0)
        {
            return new VramTestResult
            {
                Ampel = AmpelStatus.Gelb,
                ToolGefunden = true,
                Details = p,
                SpitzentemperaturCelsius = thermal.SpitzentemperaturCelsius,
                Hinweis = Loc.T(
                    "Testdauer zu kurz für auch nur einen vollständigen Durchlauf bei dieser Kartengröße. " +
                    "Kein Fehler gefunden, aber auch keine belastbare Aussage - Testdauer erhöhen und erneut versuchen.",
                    "Test duration too short for even a single complete pass at this card size. " +
                    "No error found, but also no reliable conclusion - increase the test duration and try again.")
            };
        }

        var temperaturHinweis = thermal.SpitzentemperaturCelsius is { } maxTemp
            ? Loc.T($" Höchste GPU-Temperatur während des Tests: {maxTemp:0}°C.", $" Highest GPU temperature during the test: {maxTemp:0}°C.")
            : "";

        return new VramTestResult
        {
            Ampel = AmpelStatus.Gruen,
            ToolGefunden = true,
            Details = p,
            SpitzentemperaturCelsius = thermal.SpitzentemperaturCelsius,
            Hinweis = Loc.T(
                $"Keine Fehler in {p.IterationenAbgeschlossen} Durchlauf/Durchläufen ({p.GbGeschriebenGesamt:0.#} GB " +
                "getestet). Kein Beweis für 100% fehlerfreien Speicher (länger laufen lassen erhöht die " +
                "Sicherheit), aber ein klar positives Zeichen." + temperaturHinweis,
                $"No errors in {p.IterationenAbgeschlossen} pass(es) ({p.GbGeschriebenGesamt:0.#} GB " +
                "tested). Not proof of 100% error-free memory (running longer increases the " +
                "confidence), but a clearly positive sign." + temperaturHinweis)
        };
    }
}
