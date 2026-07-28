using System.Diagnostics;
using System.Management;
using Microsoft.Win32;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Listet alle verbauten Grafikkarten ueber Win32_VideoController (WMI) auf - laeuft
/// ohne Admin-Rechte, herstellerunabhaengig (NVIDIA/AMD/Intel gleichermassen erfasst).
/// Anders als SystemInfoService.GpuName (nur die erste/primaere Karte) werden hier
/// ALLE erkannten Adapter zurueckgegeben, wichtig bei Notebooks/Desktops mit
/// integrierter + dedizierter GPU gleichzeitig.
/// </summary>
public class GpuDetectionService
{
    // WMI Win32_VideoController.AdapterRAM ist ein VORZEICHENLOSER 32-BIT-WERT
    // (max. ~4.295 GB) - Treiber clampen/kappen den gemeldeten Wert bei Karten mit
    // mehr VRAM oft genau an dieser Grenze, statt echt zu ueberlaufen. Ergebnis:
    // eine z.B. 12GB-Karte wird ueber WMI faelschlich als "~4GB" gemeldet - keine
    // echte Zufallszahl, sondern ein bekannter, deterministischer Windows/WMI-
    // Designfehler. Deshalb: reale Groesse zuerst aus der Registry lesen
    // (HardwareInformation.qwMemorySize, ein echter 64-Bit-Wert, seit Windows 10
    // von den meisten GPU-Treibern gepflegt); WMI nur als Fallback, und dort nur
    // fuer Werte klar UNTER der Kipp-Grenze, sonst lieber "unbekannt" als eine
    // nachweislich falsche Zahl (s. gleiches Prinzip in TemperatureService).
    private const string VideoTreiberKlassenPfad =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    // Deutlich unter der 32-Bit-Grenze (4.295 GB) gewaehlt, damit auch leicht
    // abweichend gerundete/gemeldete Werte nahe der echten Grenze nicht faelschlich
    // als vertrauenswuerdig durchgehen.
    private const long WmiVertrauensGrenzeBytes = 3_800_000_000L;

    public async Task<List<GpuInfo>> GetGpusAsync()
    {
        return await Task.Run(() =>
        {
            var gpus = new List<GpuInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, AdapterRAM, DriverVersion, PNPDeviceID FROM Win32_VideoController");

                foreach (var o in searcher.Get())
                {
                    var name = o["Name"]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var vramGb = VramAusRegistryLesen(name) ?? VramAusWmiFallsVertrauenswuerdig(o["AdapterRAM"]);

                    gpus.Add(new GpuInfo
                    {
                        Name = name,
                        VramGb = vramGb,
                        TreiberVersion = o["DriverVersion"]?.ToString(),
                        PnpDeviceId = o["PNPDeviceID"]?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                gpus.Add(new GpuInfo
                {
                    Name = Loc.T($"GPU-Erkennung fehlgeschlagen: {ex.Message}", $"GPU detection failed: {ex.Message}")
                });
            }

            return gpus;
        });
    }

    /// <summary>
    /// Liest die GPU-Live-Auslastung (3D-Engine, in %) ueber die eingebaute Windows-
    /// Performance-Counter-Kategorie "GPU Engine" - dieselbe Datenquelle, die auch der
    /// Task-Manager-GPU-Tab nutzt. Bewusst OHNE Kernel-Treiber/Admin-Rechte (anders als
    /// LibreHardwareMonitor, s. IDEEN_BACKLOG.md fuer die Begruendung, warum das NICHT
    /// eingebaut wurde). Summiert alle "engtype_3D"-Instanzen ueber alle Prozesse
    /// hinweg - bei einer einzelnen aktiven GPU (Alex' RTX 5070) ergibt das die
    /// Gesamt-3D-Auslastung, wie sie auch im Task-Manager erscheint.
    /// </summary>
    public async Task<double?> GetLiveAuslastungProzentAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!PerformanceCounterCategory.Exists("GPU Engine")) return (double?)null;

                var kategorie = new PerformanceCounterCategory("GPU Engine");
                var instanzNamen = kategorie.GetInstanceNames()
                    .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (instanzNamen.Count == 0) return null;

                using var zaehler = new CompositeDisposable(instanzNamen
                    .Select(n => new PerformanceCounter("GPU Engine", "Utilization Percentage", n, true))
                    .ToList());

                // Performance-Counter liefern beim allerersten Aufruf oft 0 (brauchen ein
                // Zeitfenster zwischen zwei Messungen) - deshalb zweimal lesen, wie beim
                // klassischen "% Processor Time"-Muster.
                foreach (var z in zaehler.Items) z.NextValue();
                Thread.Sleep(300);
                var summe = zaehler.Items.Sum(z => z.NextValue());

                return Math.Round(Math.Min(summe, 100.0), 0);
            }
            catch
            {
                return null;
            }
        });
    }

    // Kleiner Helfer, um mehrere PerformanceCounter-Instanzen zusammen zu erzeugen und
    // sicher zu entsorgen (using-Block fuer eine ganze Liste, ohne Boilerplate-Schleife).
    private sealed class CompositeDisposable(List<PerformanceCounter> items) : IDisposable
    {
        public List<PerformanceCounter> Items { get; } = items;
        public void Dispose() { foreach (var i in Items) i.Dispose(); }
    }

    private static double? VramAusWmiFallsVertrauenswuerdig(object? adapterRamWert)
    {
        if (adapterRamWert is null) return null;

        var bytes = Convert.ToInt64(Convert.ToUInt32(adapterRamWert));
        if (bytes <= 0 || bytes >= WmiVertrauensGrenzeBytes) return null;

        return Math.Round(bytes / 1024.0 / 1024 / 1024, 1);
    }

    private static double? VramAusRegistryLesen(string gpuName)
    {
        try
        {
            using var basis = Registry.LocalMachine.OpenSubKey(VideoTreiberKlassenPfad);
            if (basis is null) return null;

            foreach (var unterName in basis.GetSubKeyNames())
            {
                // Nur echte Adapter-Unterschluessel ("0000", "0001", ...) betrachten,
                // nicht "Properties" o.ae. Sonderschluessel in derselben Klasse.
                if (unterName.Length != 4 || !unterName.All(char.IsDigit)) continue;

                using var unter = basis.OpenSubKey(unterName);
                if (unter is null) continue;

                var treiberBeschreibung = unter.GetValue("DriverDesc") as string;
                if (!string.Equals(treiberBeschreibung?.Trim(), gpuName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (unter.GetValue("HardwareInformation.qwMemorySize") is long qwBytes && qwBytes > 0)
                    return Math.Round(qwBytes / 1024.0 / 1024 / 1024, 1);
            }
        }
        catch
        {
            // Registry-Zugriff kann z.B. durch Gruppenrichtlinien eingeschraenkt sein -
            // dann faellt VramAusWmiFallsVertrauenswuerdig() als Fallback ein.
        }

        return null;
    }
}
