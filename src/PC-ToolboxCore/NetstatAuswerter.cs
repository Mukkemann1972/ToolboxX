using System.Text.RegularExpressions;

namespace PCToolboxCore;

public record NetstatVerbindung(string Protokoll, string LokaleAdresse, string RemoteAdresse, int RemotePort, int Pid);

/// <summary>
/// Reine Auswertung der `netstat -ano`-Textausgabe (Rang 4 aus der NirSoft-Recherche,
/// Vorbild CurrPorts): "welches Programm redet hier eigentlich mit draußen?" - ergänzt die
/// Netzwerk-Diagnose, die bisher nur die Kette bis zum Internet prüft, nicht wer sie benutzt.
///
/// Bewusst NICHT am Status-Text ("HERGESTELLT"/"ESTABLISHED" usw.) festgemacht, weil netstat
/// in der Sprache des Systems ausgibt - stattdessen an der Struktur: eine "lauschende" Zeile
/// hat immer die Remote-Adresse 0.0.0.0:0 (bzw. [::]:0 bei IPv6), alles mit einer echten
/// Remote-Adresse ist eine tatsächliche oder versuchte Verbindung nach draußen. Das bleibt
/// auf jeder Windows-Sprache korrekt.
/// </summary>
public static class NetstatAuswerter
{
    private static readonly Regex ZeileMuster = new(
        @"^\s*(TCP6?|UDP6?)\s+(\S+)\s+(\S+)\s+(?:\S+\s+)?(\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<NetstatVerbindung> Parse(string netstatAusgabe)
    {
        var ergebnis = new List<NetstatVerbindung>();

        foreach (var zeile in netstatAusgabe.Split('\n'))
        {
            var treffer = ZeileMuster.Match(zeile.TrimEnd('\r'));
            if (!treffer.Success) continue;

            var protokoll = treffer.Groups[1].Value.ToUpperInvariant();
            var lokal = treffer.Groups[2].Value;
            var remote = treffer.Groups[3].Value;
            if (!int.TryParse(treffer.Groups[4].Value, out var pid)) continue;

            var (remoteAdresse, remotePort) = AdresseUndPortTrennen(remote);
            if (remoteAdresse is null) continue;

            ergebnis.Add(new NetstatVerbindung(protokoll, lokal, remoteAdresse, remotePort, pid));
        }

        return ergebnis;
    }

    /// <summary>
    /// Nur Zeilen mit einer echten Remote-Adresse (kein "0.0.0.0:0"/"[::]:0"/"*:*" - das sind
    /// lauschende Sockets, keine tatsächlichen Verbindungen) und einer erkennbaren PID.
    /// </summary>
    public static IReadOnlyList<NetstatVerbindung> AktiveVerbindungen(IReadOnlyList<NetstatVerbindung> alle) =>
        alle.Where(v => v.Pid != 0
                        && v.RemoteAdresse != "0.0.0.0"
                        && v.RemoteAdresse != "::"
                        && v.RemoteAdresse != "*")
            .ToList();

    private static (string? Adresse, int Port) AdresseUndPortTrennen(string adressePort)
    {
        if (adressePort == "*:*") return ("*", 0);

        // IPv6 steht in eckigen Klammern: "[::1]:445". IPv4/Namen: "192.168.1.5:445".
        var trennstelle = adressePort.LastIndexOf(':');
        if (trennstelle <= 0) return (null, 0);

        var adresse = adressePort[..trennstelle].Trim('[', ']');
        var portText = adressePort[(trennstelle + 1)..];

        return int.TryParse(portText, out var port) ? (adresse, port) : (null, 0);
    }
}
