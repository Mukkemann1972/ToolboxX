using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Führt die dauerhafte Temperatur-Überwachung aus (gleiches Muster wie
/// VerbindungsUeberwachungService): misst in festem Abstand über TemperatureService und meldet
/// jede Runde per Callback. Die eigentliche Auswertung (Überschreitungsperioden erkennen) macht
/// bewusst PCToolboxCore.TemperaturUeberwachung, hier nur der Mess-Vorgang selbst.
///
/// Die Laufwerksliste wird einmal beim Start eingelesen, nicht bei jeder Runde neu - für eine
/// laufende Überwachung (typisch: während eines Belastungstests) ändert sich die Menge der
/// Laufwerke praktisch nie, ein wiederholtes Neu-Einlesen wäre nur unnötige Last.
/// </summary>
public class TemperaturUeberwachungService
{
    private readonly TemperatureService _temperaturDienst = new();
    private CancellationTokenSource? _abbruch;

    public bool LaeuftGerade => _abbruch is not null;

    public void Starten(TimeSpan intervall, IReadOnlyList<DiskInfo> disks, Action<IReadOnlyList<TemperaturMessung>> beiMessung)
    {
        if (_abbruch is not null) return; // laeuft schon

        _abbruch = new CancellationTokenSource();
        var token = _abbruch.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var readings = await _temperaturDienst.GetTemperaturenAsync(disks);
                if (!token.IsCancellationRequested)
                {
                    var zeitpunkt = DateTime.Now;
                    var messungen = readings
                        .Select(r => new TemperaturMessung(zeitpunkt, r.Bauteil, r.Celsius))
                        .ToList();
                    beiMessung(messungen);
                }

                try { await Task.Delay(intervall, token); }
                catch (TaskCanceledException) { }
            }
        }, token);
    }

    public void Stoppen()
    {
        _abbruch?.Cancel();
        _abbruch?.Dispose();
        _abbruch = null;
    }
}
