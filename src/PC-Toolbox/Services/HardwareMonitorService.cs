using System.IO;
using LibreHardwareMonitor.Hardware;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest echte CPU/GPU-Sensorwerte über LibreHardwareMonitorLib (MPL-2.0), die seit
/// Herbst 2025 standardmäßig PawnIO statt des älteren WinRing0 nutzt (s. HERKUNFT.txt
/// in vendor/pawnio/ für die Begründung). Der PawnIO-Treiber selbst liegt als
/// offizieller Installer bei, wird aber NICHT automatisch installiert - dieser Service
/// meldet nur, ob er verfügbar ist, und liefert sonst leere Ergebnisse statt zu crashen.
/// </summary>
public class HardwareMonitorService
{
    private static readonly string[] BekanntePawnIoPfade =
    [
        @"C:\Windows\System32\drivers\PawnIO.sys",
        @"C:\Program Files\PawnIO\PawnIOLib.dll"
    ];

    public bool IstPawnIoInstalliert() => BekanntePawnIoPfade.Any(File.Exists);

    private static string MitgelieferterInstallerPfad =>
        Path.Combine(AppContext.BaseDirectory, "hardware-tools", "pawnio", "PawnIO_setup.exe");

    public bool InstallerVorhanden() => File.Exists(MitgelieferterInstallerPfad);

    public bool InstallerStarten()
    {
        if (!InstallerVorhanden()) return false;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(MitgelieferterInstallerPfad) { UseShellExecute = true });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Liefert Temperatur-Sensoren für CPU und GPU. Gibt eine leere Liste zurück (statt
    /// zu werfen), wenn PawnIO fehlt oder das jeweilige Gerät keine Temperatursensoren
    /// meldet - der Aufrufer (TemperatureService) entscheidet dann selbst, welchen
    /// Hinweistext er dafür anzeigt.
    /// </summary>
    public async Task<List<TempReading>> GetTemperaturenAsync()
    {
        if (!IstPawnIoInstalliert()) return [];

        return await Task.Run(() =>
        {
            var readings = new List<TempReading>();
            Computer? computer = null;

            try
            {
                computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
                computer.Open();
                computer.Accept(new AktualisierungsBesucher());

                foreach (var hardware in computer.Hardware)
                    SensorenSammeln(hardware, readings);
            }
            catch
            {
                // PawnIO-DLL vorhanden, aber Treiber z.B. nicht geladen/kein Zugriff -
                // leere Liste zurückgeben, TemperatureService faellt dann auf den
                // ACPI-Weg bzw. "nicht verfuegbar" zurueck.
            }
            finally
            {
                computer?.Close();
            }

            return readings;
        });
    }

    private static void SensorenSammeln(IHardware hardware, List<TempReading> readings)
    {
        var bauteilTyp = hardware.HardwareType switch
        {
            HardwareType.Cpu => "CPU",
            HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
            _ => null
        };

        if (bauteilTyp is not null)
        {
            foreach (var sensor in hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature))
            {
                if (sensor.Value is not { } wert) continue;
                readings.Add(new TempReading
                {
                    Bauteil = $"{bauteilTyp} ({sensor.Name})",
                    Celsius = Math.Round(wert, 1),
                    Ampel = BewerteTemperatur((double)wert),
                    Hinweis = wert > 85 ? Loc.T("Kritisch heiß → Kühlung prüfen, Wärmeleitpaste erneuern, Gehäuselüftung kontrollieren.",
                                                 "Critically hot → check cooling, replace thermal paste, check case ventilation.") : ""
                });
            }
        }

        foreach (var sub in hardware.SubHardware)
            SensorenSammeln(sub, readings);
    }

    private static AmpelStatus BewerteTemperatur(double celsius) => celsius switch
    {
        < 70 => AmpelStatus.Gruen,
        <= 85 => AmpelStatus.Gelb,
        _ => AmpelStatus.Rot
    };

    private sealed class AktualisierungsBesucher : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
