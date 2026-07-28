using System.Management;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

// Liest nur die BIOS-Kenndaten (Hersteller/Version/Datum) über WMI - rein lesend, keine
// Möglichkeit über Windows, die tatsächlichen BIOS-EINSTELLUNGEN (Secure Boot an/aus usw.)
// auszulesen. Deshalb bleibt das Kompendium selbst ein Nachschlagewerk, kein Live-Status.
public class BiosInfoService
{
    public Task<BiosInfo> GetBiosInfoAsync() => Task.Run(() =>
    {
        var info = new BiosInfo();
        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");

            foreach (var o in suche.Get())
            {
                info.Hersteller = o["Manufacturer"]?.ToString()?.Trim() ?? "";
                info.Version = o["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "";

                var releaseDateRoh = o["ReleaseDate"]?.ToString();
                info.ReleaseDatum = ReleaseDatumLesbar(releaseDateRoh);
                break; // nur ein BIOS pro System
            }
        }
        catch (Exception ex)
        {
            info.Fehler = Loc.T(
                $"WMI nicht verfügbar: {ex.Message} → Als Administrator starten oder " +
                "'Windows Management Instrumentation'-Dienst prüfen (services.msc).",
                $"WMI not available: {ex.Message} → Start as administrator or " +
                "check the 'Windows Management Instrumentation' service (services.msc).");
        }

        return info;
    });

    // WMI liefert das Datum im CIM-Format "yyyyMMddHHmmss.ffffff+zzz" - hier nur der Datumsteil.
    private static string ReleaseDatumLesbar(string? cimDatum)
    {
        if (string.IsNullOrEmpty(cimDatum) || cimDatum.Length < 8) return "";

        var jahr = cimDatum[..4];
        var monat = cimDatum[4..6];
        var tag = cimDatum[6..8];
        return $"{tag}.{monat}.{jahr}";
    }
}
