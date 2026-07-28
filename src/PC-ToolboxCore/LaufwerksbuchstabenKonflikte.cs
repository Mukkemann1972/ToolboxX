using System.Text.RegularExpressions;

namespace PCToolboxCore;

/// <summary>
/// Reine Logik zur Erkennung von "Geister"-Laufwerksbuchstaben (Rang 3 aus der NirSoft-
/// Recherche, Vorbild DriveLetterView): Windows merkt sich in der Registry
/// (HKLM\SYSTEM\MountedDevices) JEDEN je vergebenen Laufwerksbuchstaben dauerhaft, auch wenn
/// das Gerät längst nicht mehr angeschlossen ist. Genau das ist die häufigste, fuer Nutzer
/// unsichtbare Ursache fuer "mein Stick bekommt komische Buchstaben" oder "der Stick wird gar
/// nicht angezeigt" - der eigentlich erwartete Buchstabe ist noch fuer ein Phantom-Geraet
/// reserviert.
///
/// Bewusst nur String-Verarbeitung der Registry-WERTNAMEN (z.B. "\DosDevices\D:"), kein
/// Parsen des binaeren Wertinhalts (Volume-Signatur) - fuer die Erklaerung "dieser Buchstabe
/// ist noch reserviert" reicht der Name allein, und bleibt so auf dem Pi mit erfundenen
/// Testlisten pruefbar. Der eigentliche Registry-Zugriff passiert in
/// PC-Toolbox/Services/LaufwerksbuchstabenKonfliktService.cs.
/// </summary>
public static class LaufwerksbuchstabenKonflikte
{
    private static readonly Regex DosDeviceMuster = new(@"^\\DosDevices\\([A-Za-z]):$", RegexOptions.Compiled);

    /// <summary>Extrahiert die Laufwerksbuchstaben aus MountedDevices-Wertnamen wie "\DosDevices\D:".</summary>
    public static IReadOnlyList<char> BuchstabenAusWertnamen(IEnumerable<string> registryWertnamen) =>
        registryWertnamen
            .Select(n => DosDeviceMuster.Match(n))
            .Where(m => m.Success)
            .Select(m => char.ToUpperInvariant(m.Groups[1].Value[0]))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

    /// <summary>
    /// Vergleicht alle in der Registry je vergebenen Buchstaben mit den gerade aktiven
    /// (echten) Laufwerken - was in der Registry steht, aber gerade nicht aktiv ist, ist ein
    /// "Geister"-Buchstabe: reserviert fuer ein Geraet, das momentan nicht angeschlossen ist.
    /// C und die typischen A/B (historische Diskettenlaufwerke) werden bewusst ausgeblendet,
    /// die sind kein echter Befund und wuerden nur verunsichern.
    /// </summary>
    public static IReadOnlyList<char> GeisterBuchstaben(IReadOnlyList<char> registriert, IReadOnlyList<char> aktiv)
    {
        var aktivSet = aktiv.Select(char.ToUpperInvariant).ToHashSet();
        return registriert
            .Where(b => !aktivSet.Contains(b) && b != 'A' && b != 'B' && b != 'C')
            .OrderBy(b => b)
            .ToList();
    }
}
