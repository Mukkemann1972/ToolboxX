using System.Text.RegularExpressions;
using PCToolbox.Models;

namespace PCToolboxCore;

/// <summary>
/// Deutet die WLAN-Signalstärke (Rang 6 aus der NirSoft-Recherche, Vorbild
/// WifiChannelMonitor - hier nur die Signalstärke, kein voller Kanal-Scan). "Internet ist
/// langsam" ist oft ein schwaches WLAN-Signal, nicht die Leitung selbst.
/// </summary>
public static class WlanSignalDeutung
{
    private static readonly Regex SignalMuster = new(@"Signal\S*\s*:\s*(\d{1,3})\s*%", RegexOptions.Compiled);

    /// <summary>Liest den Signalstärke-Prozentwert aus der Textausgabe von "netsh wlan show interfaces".</summary>
    public static int? ProzentAusNetshAusgabe(string ausgabe)
    {
        var treffer = SignalMuster.Match(ausgabe);
        if (!treffer.Success) return null;

        return int.TryParse(treffer.Groups[1].Value, out var prozent) && prozent is >= 0 and <= 100
            ? prozent
            : null;
    }

    public static (string Text, AmpelStatus Status) Deuten(int prozent) => prozent switch
    {
        >= 70 => (Loc.T($"Stark ({prozent}%)", $"Strong ({prozent}%)"), AmpelStatus.Gruen),
        >= 40 => (Loc.T($"Mittel ({prozent}%) - für die meiste Nutzung ausreichend, kann bei größeren Downloads oder Videoanrufen aber schon zu spüren sein.",
                        $"Medium ({prozent}%) - enough for most use, but can already be noticeable for larger downloads or video calls."), AmpelStatus.Gelb),
        _ => (Loc.T($"Schwach ({prozent}%) - wahrscheinlicher Grund für ein \"langsames Internet\", das eigentlich nur ein schwaches Signal ist. Näher an den Router oder eine Repeater/Mesh-Lösung hilft meist mehr als ein neuer Internet-Tarif.",
                    $"Weak ({prozent}%) - a likely reason for \"slow internet\" that's actually just a weak signal. Moving closer to the router or a repeater/mesh solution usually helps more than a faster internet plan."), AmpelStatus.Rot)
    };
}
