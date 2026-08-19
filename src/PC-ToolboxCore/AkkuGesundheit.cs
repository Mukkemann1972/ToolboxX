using PCToolbox.Models;

namespace PCToolboxCore;

/// <summary>Design- und aktuelle Vollladekapazität eines Akkus (in mWh, wie sie auch der
/// Windows-eigene "powercfg /batteryreport" ausgibt) plus optionaler Ladezyklenzähler (nicht auf
/// jedem System verfügbar - manche Hersteller/Treiber melden ihn nicht).</summary>
public record AkkuKapazitaet(long DesignKapazitaetMwh, long VollladeKapazitaetMwh, int? Ladezyklen);

/// <summary>
/// Reine Bewertungslogik für Akku-Gesundheit: das Verhältnis von aktueller zu ursprünglicher
/// Vollladekapazität ist der Standard-Gesundheitsindikator (dieselbe Kennzahl, die z.B. macOS in
/// den Systeminformationen zeigt) - ein Akku, der nur noch 60% seiner Werkskapazität schafft, hält
/// auch bei 100% Akkustand nur noch 60% der ursprünglichen Laufzeit.
/// </summary>
public static class AkkuGesundheit
{
    public static double GesundheitsProzent(AkkuKapazitaet kapazitaet) =>
        kapazitaet.DesignKapazitaetMwh <= 0
            ? 0
            : Math.Round(kapazitaet.VollladeKapazitaetMwh * 100.0 / kapazitaet.DesignKapazitaetMwh, 1);

    public static AmpelStatus Bewerten(AkkuKapazitaet kapazitaet)
    {
        var prozent = GesundheitsProzent(kapazitaet);
        return prozent switch
        {
            >= 80 => AmpelStatus.Gruen,
            >= 60 => AmpelStatus.Gelb,
            _ => AmpelStatus.Rot
        };
    }

    public static string Erklaerung(AkkuKapazitaet kapazitaet)
    {
        var prozent = GesundheitsProzent(kapazitaet);
        var zyklenText = kapazitaet.Ladezyklen is { } z
            ? Loc.T($" Ladezyklen bisher: {z}.", $" Charge cycles so far: {z}.")
            : "";

        return Bewerten(kapazitaet) switch
        {
            AmpelStatus.Gruen => Loc.T(
                $"{prozent:0.#}% der ursprünglichen Kapazität - noch in gutem Zustand.{zyklenText}",
                $"{prozent:0.#}% of original capacity - still in good shape.{zyklenText}"),
            AmpelStatus.Gelb => Loc.T(
                $"{prozent:0.#}% der ursprünglichen Kapazität - spürbar nachgelassen, für unterwegs aber " +
                $"meist noch brauchbar. Akkus altern durch Nutzung, kein Defekt.{zyklenText}",
                $"{prozent:0.#}% of original capacity - noticeably reduced, but usually still usable on the " +
                $"go. Batteries age with use, this isn't a fault.{zyklenText}"),
            _ => Loc.T(
                $"{prozent:0.#}% der ursprünglichen Kapazität - deutlich verschlissen, ein Austausch würde " +
                $"die Akkulaufzeit spürbar verbessern.{zyklenText}",
                $"{prozent:0.#}% of original capacity - significantly worn, a replacement would noticeably " +
                $"improve battery life.{zyklenText}")
        };
    }
}
