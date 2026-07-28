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
            MemtestVulkanOutputParser.ZeileVerarbeiten(e.Data, parseErgebnis);
            fortschritt?.Report(FortschrittsText(parseErgebnis));
        };

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
            return new VramTestResult
            {
                Ampel = AmpelStatus.Unbekannt,
                ToolGefunden = true,
                Details = parseErgebnis,
                Hinweis = Loc.T($"memtest_vulkan konnte nicht gestartet werden: {ex.Message}",
                                $"memtest_vulkan could not be started: {ex.Message}")
            };
        }

        return Bewerten(parseErgebnis);
    }

    private static string FortschrittsText(VramTestParseResult p) =>
        p.FehlerAnzahl > 0
            ? Loc.T($"{p.IterationenAbgeschlossen} Durchläufe, ⚠️ {p.FehlerAnzahl} Fehler gefunden ({p.GbGeschriebenGesamt:0.#} GB geschrieben)",
                    $"{p.IterationenAbgeschlossen} passes, ⚠️ {p.FehlerAnzahl} errors found ({p.GbGeschriebenGesamt:0.#} GB written)")
            : Loc.T($"{p.IterationenAbgeschlossen} Durchläufe, bisher keine Fehler ({p.GbGeschriebenGesamt:0.#} GB geschrieben)",
                    $"{p.IterationenAbgeschlossen} passes, no errors so far ({p.GbGeschriebenGesamt:0.#} GB written)");

    private static VramTestResult Bewerten(VramTestParseResult p)
    {
        if (p.Abgestuerzt)
        {
            return new VramTestResult
            {
                Ampel = AmpelStatus.Rot,
                ToolGefunden = true,
                Details = p,
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
            return new VramTestResult
            {
                Ampel = AmpelStatus.Rot,
                ToolGefunden = true,
                Details = p,
                Hinweis = Loc.T(
                    $"{p.FehlerAnzahl} Speicherfehler gefunden nach {p.GbGeschriebenGesamt:0.#} GB getestetem " +
                    "VRAM. Deutet auf defekte Speicherchips hin. Bei AMD-Karten vorher prüfen: Resizable BAR " +
                    "im BIOS einmal aus- und wieder einschalten und erneut testen, bevor du von einem " +
                    "Hardwaredefekt ausgehst (bekannter Sonderfall, kein Bug in diesem Test).",
                    $"{p.FehlerAnzahl} memory errors found after {p.GbGeschriebenGesamt:0.#} GB of tested " +
                    "VRAM. Indicates defective memory chips. On AMD cards, check first: turn Resizable BAR " +
                    "off and back on in the BIOS and test again before assuming a " +
                    "hardware defect (a known special case, not a bug in this test).")
            };
        }

        if (p.IterationenAbgeschlossen == 0)
        {
            return new VramTestResult
            {
                Ampel = AmpelStatus.Gelb,
                ToolGefunden = true,
                Details = p,
                Hinweis = Loc.T(
                    "Testdauer zu kurz für auch nur einen vollständigen Durchlauf bei dieser Kartengröße. " +
                    "Kein Fehler gefunden, aber auch keine belastbare Aussage - Testdauer erhöhen und erneut versuchen.",
                    "Test duration too short for even a single complete pass at this card size. " +
                    "No error found, but also no reliable conclusion - increase the test duration and try again.")
            };
        }

        return new VramTestResult
        {
            Ampel = AmpelStatus.Gruen,
            ToolGefunden = true,
            Details = p,
            Hinweis = Loc.T(
                $"Keine Fehler in {p.IterationenAbgeschlossen} Durchlauf/Durchläufen ({p.GbGeschriebenGesamt:0.#} GB " +
                "getestet). Kein Beweis für 100% fehlerfreien Speicher (länger laufen lassen erhöht die " +
                "Sicherheit), aber ein klar positives Zeichen.",
                $"No errors in {p.IterationenAbgeschlossen} pass(es) ({p.GbGeschriebenGesamt:0.#} GB " +
                "tested). Not proof of 100% error-free memory (running longer increases the " +
                "confidence), but a clearly positive sign.")
        };
    }
}
