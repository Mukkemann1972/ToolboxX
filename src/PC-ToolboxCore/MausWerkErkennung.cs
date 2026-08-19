namespace PCToolboxCore;

/// <summary>
/// Reine VID/PID-Erkennung für die MausWerk-Brücke der Maus-Diagnose-Kachel (Auftrag
/// 04.08.2026) - herausgelöst aus PCToolbox.Services.MausWerkBrueckeService, damit diese
/// Logik plattformneutral testbar ist (auch auf dem Pi), genau wie GeisterUsbGeraete für die
/// Pro-USB-Historie.
///
/// VID/PID (248A:8366) aus MausWerks eigenem DeviceProfile.CreateIsyStandard()
/// (isy-iem-1001/src/shared/DeviceProfile.cs) übernommen - dort am 04.08.2026 von der
/// ursprünglich angenommenen 04D9:8008 auf die an echter Hardware verifizierte ID
/// korrigiert (s. UsbDeviceWatcher.cs im MausWerk-Projekt), hier entsprechend nachgezogen.
/// </summary>
public static class MausWerkErkennung
{
    private const string VendorId = "248A";
    private const string ProductId = "8366";

    public static bool IstKompatibel(string? geraeteId)
    {
        if (string.IsNullOrEmpty(geraeteId)) return false;
        var upper = geraeteId.ToUpperInvariant();
        return upper.Contains($"VID_{VendorId}") && upper.Contains($"PID_{ProductId}");
    }
}
