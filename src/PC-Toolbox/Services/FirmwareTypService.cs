using System.Runtime.InteropServices;
using PCToolboxCore;

namespace PCToolbox.Services;

// Fragt ueber die native Windows-API ab, ob dieser PC ueber UEFI oder das aeltere Legacy-BIOS
// startet - GetFirmwareType ist genau dafuer von Microsoft dokumentiert (kernel32.dll), kein
// Registry-Geraet, kein Subprozess noetig. Ergaenzt das bisher rein statische BIOS-Kompendium um
// den einzigen wirklich zuverlaessig auslesbaren Live-Wert zu diesem Thema.
public static class FirmwareTypService
{
    private const uint FirmwareTypeBios = 1;
    private const uint FirmwareTypeUefi = 2;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFirmwareType(out uint firmwareType);

    public static FirmwareTyp Ermitteln()
    {
        try
        {
            if (!GetFirmwareType(out var typ)) return FirmwareTyp.Unbekannt;
            return typ switch
            {
                FirmwareTypeUefi => FirmwareTyp.Uefi,
                FirmwareTypeBios => FirmwareTyp.LegacyBios,
                _ => FirmwareTyp.Unbekannt
            };
        }
        catch
        {
            return FirmwareTyp.Unbekannt;
        }
    }
}
