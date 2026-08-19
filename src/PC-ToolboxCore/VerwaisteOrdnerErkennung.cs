namespace PCToolboxCore;

/// <summary>
/// Reine Entscheidungslogik für "welcher Ordnername könnte ein Rest eines längst deinstallierten
/// Programms sein, dessen Namen ich nicht mehr weiß" (s. DatenresteService.VerwaisteOrdnerVorschlagenAsync,
/// Pro) - dreht die normale Reste-Suche um: statt dass der Nutzer einen Namen eintippen muss, den
/// er sich merken müsste, schlägt die App Kandidaten vor (Ordner, die zu KEINEM aktuell
/// installierten Programm passen).
/// </summary>
public static class VerwaisteOrdnerErkennung
{
    /// <summary>Bekannte Windows-/Hersteller-Sammelordner, die niemals als "verwaister Rest"
    /// vorgeschlagen werden - das sind gemeinsam genutzte Infrastruktur-Ordner, kein Programm für
    /// sich, auch wenn ihr Name zu keinem einzelnen installierten Programm passt.</summary>
    public static readonly string[] BekannteSystemOrdner =
    {
        "Common Files", "Microsoft", "Microsoft.NET", "dotnet", "MSBuild", "Reference Assemblies",
        "Windows Defender", "Windows Defender Advanced Threat Protection", "WindowsApps",
        "Internet Explorer", "Windows Mail", "Windows Media Player", "WindowsPowerShell",
        "PackageManagement", "Google", "Intel", "NVIDIA Corporation", "NVIDIA", "AMD",
        "Realtek", "Package Cache", "Windows Portable Devices", "Uninstall Information",
        "Windows Sidebar", "Windows NT", "windows kits",
    };

    public static bool IstBekannterSystemOrdner(string ordnerName) =>
        BekannteSystemOrdner.Any(s => string.Equals(s, ordnerName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Ist dieser Ordnername durch eines der übergebenen (Anzeigename, Hersteller)-Paare
    /// erklärt? Nutzt dieselben Kandidaten wie NameMatcher (die normale Reste-Suche für ein
    /// BEKANNTES Programm), nur umgekehrt: hier wird ein Ordner gegen ALLE installierten
    /// Programme geprüft statt ein Programm gegen einen einzelnen Ordner.</summary>
    public static bool IstErklaertDurch(string ordnerName, IEnumerable<(string DisplayName, string Publisher)> installierteProgramme)
    {
        foreach (var (displayName, publisher) in installierteProgramme)
        {
            foreach (var kandidat in NameMatcher.Kandidaten(displayName, publisher))
            {
                if (ordnerName.Contains(kandidat, StringComparison.OrdinalIgnoreCase) ||
                    kandidat.Contains(ordnerName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>true = wahrscheinlich ein Rest eines nicht mehr installierten Programms - weder
    /// System-Sammelordner noch durch ein aktuell installiertes Programm erklärt.</summary>
    public static bool IstVerdaechtig(string ordnerName, IEnumerable<(string DisplayName, string Publisher)> installierteProgramme) =>
        !IstBekannterSystemOrdner(ordnerName) && !IstErklaertDurch(ordnerName, installierteProgramme);
}
