using System.Net.NetworkInformation;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Fuehrt die dauerhafte Verbindungsueberwachung aus (Rang 5 aus der NirSoft-Recherche,
/// Vorbild PingInfoView): pingt in festem Abstand ein Ziel und meldet jede Messung per
/// Callback. Die eigentliche Auswertung (Ausfallperioden erkennen) macht bewusst
/// PCToolboxCore.VerbindungsUeberwachung, hier nur der Mess-Vorgang selbst.
///
/// Dasselbe Testziel wie in NetzwerkService (1.1.1.1, feste Nummer statt Name, damit nur die
/// Verbindung geprueft wird und nicht schon die Namensaufloesung mit).
/// </summary>
public class VerbindungsUeberwachungService
{
    private const string TestAdresse = "1.1.1.1";
    private const int ZeitlimitMs = 2000;

    private CancellationTokenSource? _abbruch;

    public bool LaeuftGerade => _abbruch is not null;

    public void Starten(TimeSpan intervall, Action<VerbindungsMessung> beiMessung)
    {
        if (_abbruch is not null) return; // laeuft schon

        _abbruch = new CancellationTokenSource();
        var token = _abbruch.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var erfolgreich = await PingAsync();
                if (!token.IsCancellationRequested)
                    beiMessung(new VerbindungsMessung(DateTime.Now, erfolgreich));

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

    private static async Task<bool> PingAsync()
    {
        try
        {
            using var ping = new Ping();
            var antwort = await ping.SendPingAsync(TestAdresse, ZeitlimitMs);
            return antwort.Status == IPStatus.Success;
        }
        catch (Exception e) when (e is PingException or System.Net.Sockets.SocketException or ArgumentException)
        {
            return false;
        }
    }
}
