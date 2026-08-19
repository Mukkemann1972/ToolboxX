using PCToolbox.Models;

namespace PCToolboxCore;

/// <summary>Die per "Get-StorageReliabilityCounter" gelesenen NVMe-Gesundheitswerte - deckt die
/// Lücke, die die klassische ATA-SMART-Abfrage (MSStorageDriver_FailurePredict*, s. SmartService)
/// bei NVMe-Laufwerken hat: dieser alte WMI-Provider unterstützt nur ATA/IDE, NVMe braucht den
/// neueren Windows-Storage-Management-Pfad.</summary>
public record NvmeZuverlaessigkeitWerte(double? WearProzent, double? TemperaturCelsius, long? PowerOnHours, long? UnkorrigierbareFehler);

/// <summary>
/// Bewertung der NVMe-Gesundheitswerte - eigene Logik statt die ATA-Bewertung
/// (SmartService.BewerteSmart) wiederzuverwenden, weil die Kennzahlen unterschiedlich sind: kein
/// "Reallocated Sectors"-Konzept bei NVMe, dafür "Wear" als direkter Prozentwert der verbrauchten
/// Nutzungsdauer (von Windows selbst so dokumentiert: höherer Wert = mehr verbraucht, nicht wie
/// man intuitiv "Restlebensdauer" vermuten könnte).
/// </summary>
public static class NvmeZuverlaessigkeitBewertung
{
    public static (AmpelStatus Status, string Erklaerung) Bewerten(NvmeZuverlaessigkeitWerte werte)
    {
        if (werte.WearProzent is >= 90)
            return (AmpelStatus.Rot, Loc.T(
                "Abnutzung über 90% der kalkulierten Nutzungsdauer - Backup machen und Ersatz einplanen, " +
                "das Laufwerk nähert sich dem vom Hersteller vorgesehenen Lebensende.",
                "Wear over 90% of rated life - back up and plan a replacement, the drive is nearing the " +
                "end of its manufacturer-rated lifespan."));

        if ((werte.UnkorrigierbareFehler ?? 0) > 0)
            return (AmpelStatus.Gelb, Loc.T(
                "Unkorrigierbare Lese-/Schreibfehler vorhanden - im Auge behalten, kann ein frühes " +
                "Anzeichen für ein alterndes Laufwerk sein.",
                "Uncorrectable read/write errors present - worth keeping an eye on, can be an early sign " +
                "of a drive nearing the end of its life."));

        if (werte.WearProzent is >= 70)
            return (AmpelStatus.Gelb, Loc.T(
                "Abnutzung über 70% der kalkulierten Nutzungsdauer - noch kein Grund zur Sorge, aber " +
                "einen Ersatz langsam einplanen.",
                "Wear over 70% of rated life - no reason to worry yet, but start planning a replacement."));

        return (AmpelStatus.Gruen, Loc.T(
            "Alle NVMe-Gesundheitswerte im grünen Bereich.", "All NVMe health values are in the green."));
    }
}
