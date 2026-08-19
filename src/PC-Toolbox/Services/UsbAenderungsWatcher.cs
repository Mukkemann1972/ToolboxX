using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Beobachtet in Echtzeit, wenn ein USB-Geraet an- oder abgesteckt wird - per WMI-Ereignis
/// (__InstanceCreationEvent/__InstanceDeletionEvent auf Win32_PnPEntity), nicht per
/// Abfrage-Momentaufnahme wie UsbGeraeteService.LadeAsync. Entstand aus einem echten Fall
/// (11.08.2026): ein USB-Bildschirm blitzte beim An-/Abstecken kurz im Geraete-Manager auf,
/// bekam aber nie einen benannten Eintrag oder einen Eintrag im Ereignisprotokoll - eine
/// Momentaufnahme haette den ganzen Vorgang verpasst, weil zwischen zwei Abfragen wieder
/// alles "normal" aussah.
///
/// Bewusst nur Erschienen/Verschwunden (kein "Geaendert"): Win32_PnPEntity-Aenderungsereignisse
/// feuern auch bei trivialen internen Eigenschaftsaenderungen sehr haeufig und wuerden die Liste
/// mit Rauschen fuellen, ohne fuer die Werkstatt-Diagnose einen echten Mehrwert zu bringen. Kann
/// bei Bedarf spaeter ergaenzt werden (z.B. ConfigManagerErrorCode-Wechsel gezielt verfolgen).
///
/// "WITHIN 2": WMI prueft intern alle 2 Sekunden auf neue Instanzen - keine echte Push-
/// Benachrichtigung, aber deutlich feiner als manuelles Neuladen und fuer diesen Zweck
/// ausreichend (dieselbe Groessenordnung, die vergleichbare Werkzeuge nutzen).
/// </summary>
public class UsbAenderungsWatcher : IDisposable
{
    private ManagementEventWatcher? _erschienenWatcher;
    private ManagementEventWatcher? _verschwundenWatcher;

    public bool LaeuftGerade { get; private set; }

    public void Starten(Action<GeraeteAenderung> beiAenderung, Action<string>? beiFehler = null)
    {
        if (LaeuftGerade) return;

        try
        {
            _erschienenWatcher = NeuerWatcher("__InstanceCreationEvent", GeraeteAenderungsArt.Erschienen, beiAenderung);
            _verschwundenWatcher = NeuerWatcher("__InstanceDeletionEvent", GeraeteAenderungsArt.Verschwunden, beiAenderung);
            LaeuftGerade = true;
        }
        catch (ManagementException ex)
        {
            Stoppen();
            beiFehler?.Invoke(ex.Message);
        }
    }

    private static ManagementEventWatcher NeuerWatcher(
        string ereignisKlasse, GeraeteAenderungsArt art, Action<GeraeteAenderung> beiAenderung)
    {
        var watcher = new ManagementEventWatcher(
            new WqlEventQuery($"SELECT * FROM {ereignisKlasse} WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'"));

        watcher.EventArrived += (_, e) =>
        {
            if (e.NewEvent["TargetInstance"] is not ManagementBaseObject ziel) return;

            var pnpId = ziel["PNPDeviceID"]?.ToString();
            if (!UsbGeraeteAenderungen.IstUsbGeraet(pnpId)) return;

            beiAenderung(new GeraeteAenderung(DateTime.Now, art, pnpId!, ziel["Name"]?.ToString()));
        };

        watcher.Start();
        return watcher;
    }

    public void Stoppen()
    {
        _erschienenWatcher?.Stop();
        _erschienenWatcher?.Dispose();
        _erschienenWatcher = null;

        _verschwundenWatcher?.Stop();
        _verschwundenWatcher?.Dispose();
        _verschwundenWatcher = null;

        LaeuftGerade = false;
    }

    public void Dispose() => Stoppen();
}
