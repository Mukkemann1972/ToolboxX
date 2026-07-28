using System.Management;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// CPU/GPU-Temperatur bevorzugt über HardwareMonitorService (LibreHardwareMonitorLib +
/// PawnIO, echte Werte) - wenn PawnIO nicht installiert ist, ehrlicher Rückfall auf die
/// ACPI-WMI-Klasse (root\WMI, MSAcpi_ThermalZoneTemperature), die auf vielen modernen
/// Mainboards KEINE oder unplausible Werte liefert. "Nicht verfügbar" ist in beiden
/// Fällen besser als ein erfundener Wert. Laufwerks-Temperatur kommt weiterhin aus den
/// SMART-Daten (SmartService), die hier nur weitergereicht werden.
/// </summary>
public class TemperatureService
{
    private readonly HardwareMonitorService _hardwareMonitor = new();

    public async Task<List<TempReading>> GetTemperaturenAsync(IEnumerable<DiskInfo> disks)
    {
        var echteWerte = await _hardwareMonitor.GetTemperaturenAsync();
        if (echteWerte.Count > 0)
            return await MitLaufwerkenErgaenzenAsync(echteWerte, disks);

        return await Task.Run(() =>
        {
            var readings = new List<TempReading>();

            // CPU/Mainboard über ACPI-Thermalzone
            try
            {
                var scope = new ManagementScope(@"root\WMI");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(scope,
                    new ObjectQuery("SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"));

                bool gefunden = false;
                foreach (var o in searcher.Get())
                {
                    gefunden = true;
                    // Wert liegt in Zehntel-Kelvin
                    var zehntelKelvin = Convert.ToDouble(o["CurrentTemperature"]);
                    var celsius = Math.Round(zehntelKelvin / 10.0 - 273.15, 1);
                    readings.Add(BewerteCpu(Loc.T("CPU/Board (ACPI-Zone)", "CPU/Board (ACPI zone)"), celsius));
                }

                if (!gefunden)
                {
                    readings.Add(new TempReading
                    {
                        Bauteil = "CPU",
                        Celsius = null,
                        Ampel = AmpelStatus.Unbekannt,
                        Hinweis = Loc.T(
                            "Keine ACPI-Thermalzone gemeldet. Üblich bei vielen Mainboards ohne ACPI-Temperatursensor-Unterstützung " +
                            "→ für zuverlässige Werte den PawnIO-Treiber installieren (Knopf oben in diesem Modul).",
                            "No ACPI thermal zone reported. Common on many mainboards without ACPI temperature sensor support " +
                            "→ for reliable values, install the PawnIO driver (button at the top of this module).")
                    });
                }
            }
            catch (Exception ex)
            {
                readings.Add(new TempReading
                {
                    Bauteil = "CPU",
                    Celsius = null,
                    Ampel = AmpelStatus.Unbekannt,
                    Hinweis = Loc.T(
                        $"ACPI-Temperatur nicht lesbar ({ex.Message}) → als Administrator starten, oder PawnIO installieren (Knopf oben in diesem Modul).",
                        $"ACPI temperature not readable ({ex.Message}) → start as Administrator, or install PawnIO (button at the top of this module).")
                });
            }

            // GPU-Temperatur: kein zuverlässiger herstellerunabhängiger WMI-Weg vorhanden (NVIDIA/AMD/Intel je eigenes API)
            readings.Add(new TempReading
            {
                Bauteil = "GPU",
                Celsius = null,
                Ampel = AmpelStatus.Unbekannt,
                Hinweis = Loc.T(
                    "GPU-Temperatur braucht herstellerspezifisches API → PawnIO installieren (Knopf oben in diesem Modul) für echte Werte.",
                    "GPU temperature needs a vendor-specific API → install PawnIO (button at the top of this module) for real values.")
            });

            // Laufwerks-Temperaturen aus SMART übernehmen
            foreach (var disk in disks)
            {
                readings.Add(new TempReading
                {
                    Bauteil = Loc.T($"Laufwerk {disk.DeviceId}", $"Drive {disk.DeviceId}"),
                    Celsius = disk.TemperaturCelsius,
                    Ampel = BewerteLaufwerk(disk.TemperaturCelsius),
                    Hinweis = disk.TemperaturCelsius == null ? Loc.T("Keine SMART-Temperatur verfügbar.", "No SMART temperature available.") : ""
                });
            }

            return readings;
        });
    }

    private static async Task<List<TempReading>> MitLaufwerkenErgaenzenAsync(
        List<TempReading> hardwareReadings, IEnumerable<DiskInfo> disks)
    {
        return await Task.Run(() =>
        {
            foreach (var disk in disks)
            {
                hardwareReadings.Add(new TempReading
                {
                    Bauteil = Loc.T($"Laufwerk {disk.DeviceId}", $"Drive {disk.DeviceId}"),
                    Celsius = disk.TemperaturCelsius,
                    Ampel = BewerteLaufwerk(disk.TemperaturCelsius),
                    Hinweis = disk.TemperaturCelsius == null ? Loc.T("Keine SMART-Temperatur verfügbar.", "No SMART temperature available.") : ""
                });
            }
            return hardwareReadings;
        });
    }

    private static TempReading BewerteCpu(string bauteil, double celsius) => new()
    {
        Bauteil = bauteil,
        Celsius = celsius,
        Ampel = celsius switch
        {
            < 70 => AmpelStatus.Gruen,
            <= 85 => AmpelStatus.Gelb,
            _ => AmpelStatus.Rot
        },
        Hinweis = celsius > 85 ? Loc.T("Kritisch heiß → Kühlung prüfen, Wärmeleitpaste erneuern, Gehäuselüftung kontrollieren.",
                                       "Critically hot → check cooling, replace thermal paste, check case ventilation.") : ""
    };

    private static AmpelStatus BewerteLaufwerk(double? celsius)
    {
        if (celsius == null) return AmpelStatus.Unbekannt;
        return celsius switch
        {
            < 50 => AmpelStatus.Gruen,
            <= 60 => AmpelStatus.Gelb,
            _ => AmpelStatus.Rot
        };
    }
}
