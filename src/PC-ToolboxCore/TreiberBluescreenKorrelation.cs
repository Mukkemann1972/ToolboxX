namespace PCToolboxCore;

/// <summary>Ein Treffer: ein Gerät, dessen Treiberdatei als wahrscheinlicher Verursacher eines
/// Absturzes identifiziert wurde (Abgleich per Dateiname, s. TreiberBluescreenKorrelation).</summary>
public record TreiberVerdacht(string GeraeteName, string TreiberDateiname, DateTime AbsturzZeitpunkt, DateTime? TreiberInstalliertAm)
{
    /// <summary>True, wenn der Treiber innerhalb von 30 Tagen VOR dem Absturz installiert wurde -
    /// das ist der Fall, der den Verdacht wirklich stützt ("kurz nach dem Update ging's los").
    /// Ein Treiber von vor Jahren, der zufällig denselben Dateinamen trägt, beweist nichts.</summary>
    public bool KuerzlichVorDemAbsturzAktualisiert =>
        TreiberInstalliertAm is { } datum && datum <= AbsturzZeitpunkt && (AbsturzZeitpunkt - datum).TotalDays <= 30;
}

/// <summary>
/// Verbindet zwei bisher getrennte Momentaufnahmen: den Treiber-Check (Modules/TreiberView) und
/// die Bluescreen-Liste (Modules/BluescreenView). DumpReader ermittelt pro Absturz best-effort
/// einen "wahrscheinlichen Verursacher"-Dateinamen aus der Minidump-Moduladressliste (z.B.
/// "nvlddmkm.sys") - bisher blieb der isoliert im Bluescreen-Text stehen. Hier wird dieser
/// Dateiname gegen die installierten Treiber abgeglichen (Win32_PnPSignedDriver.DriverName), damit
/// eine konkrete Frage beantwortbar wird: "wurde GENAU dieser Treiber kurz vor dem Absturz
/// aktualisiert?" statt nur zwei unabhängige Listen nebeneinander zu zeigen.
///
/// Bewusst als reiner Dateiname-Abgleich gehalten, keine erneute Adress-Analyse (die steckt schon
/// in DumpReader/MinidumpModuleListParser) - hier nur das Verknüpfen zweier bereits vorhandener
/// Ergebnislisten. BluescreenEntry lebt in PC-Toolbox (WPF-Projekt), deshalb nimmt Ermitteln()
/// bewusst nur (Dateiname, Zeitpunkt)-Tupel entgegen statt den Typ selbst zu referenzieren - so
/// bleibt diese Logik plattformneutral und auf dem Pi testbar.
/// </summary>
public static class TreiberBluescreenKorrelation
{
    public static IReadOnlyList<TreiberVerdacht> Ermitteln(
        IReadOnlyList<GeraetBefund> geraete,
        IReadOnlyList<(string? TreiberDateiname, DateTime Zeitpunkt)> abstuerze)
    {
        var ergebnis = new List<TreiberVerdacht>();

        foreach (var absturz in abstuerze)
        {
            if (string.IsNullOrWhiteSpace(absturz.TreiberDateiname)) continue;

            foreach (var geraet in geraete)
            {
                if (string.IsNullOrWhiteSpace(geraet.TreiberDateiname)) continue;
                if (!string.Equals(geraet.TreiberDateiname, absturz.TreiberDateiname, StringComparison.OrdinalIgnoreCase)) continue;

                ergebnis.Add(new TreiberVerdacht(geraet.Name, geraet.TreiberDateiname!, absturz.Zeitpunkt, geraet.TreiberDatum));
            }
        }

        return ergebnis;
    }
}
