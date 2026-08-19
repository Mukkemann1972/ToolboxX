namespace PCToolboxCore;

public enum FirmwareTyp { Unbekannt, Uefi, LegacyBios }

// Reine Erklaertexte zum Firmware-Typ - unabhaengig von der eigentlichen Windows-API-Abfrage
// (die liegt in PC-Toolbox/Services/FirmwareTypService.cs, da sie ein natives P/Invoke braucht
// und PCToolboxCore OS-neutral bleiben muss). Praktischer Alltagsnutzen: Tools wie Rufus fragen
// beim Erstellen eines bootfaehigen Sticks nach genau dieser Unterscheidung (GPT/UEFI vs.
// MBR/BIOS) - viele Nutzer wissen es nicht auswendig und waehlen blind falsch.
public static class FirmwareTypErklaerung
{
    public static string RufusHinweis(FirmwareTyp typ) => typ switch
    {
        FirmwareTyp.Uefi => Loc.T(
            "Dieser PC startet über UEFI. Bei Rufus (und ähnlichen Tools zum Erstellen bootfähiger " +
            "USB-Sticks) entsprechend „GPT\" als Partitionsschema und „UEFI (non CSM)\" als Zielsystem " +
            "wählen.",
            "This PC boots via UEFI. In Rufus (and similar tools for creating bootable USB sticks), " +
            "choose \"GPT\" as the partition scheme and \"UEFI (non CSM)\" as the target system " +
            "accordingly."),
        FirmwareTyp.LegacyBios => Loc.T(
            "Dieser PC startet über das ältere Legacy-BIOS (kein UEFI). Bei Rufus (und ähnlichen " +
            "Tools) entsprechend „MBR\" als Partitionsschema und „BIOS (or UEFI-CSM)\" als Zielsystem " +
            "wählen.",
            "This PC boots via the older legacy BIOS (no UEFI). In Rufus (and similar tools), choose " +
            "\"MBR\" as the partition scheme and \"BIOS (or UEFI-CSM)\" as the target system " +
            "accordingly."),
        _ => Loc.T(
            "Konnte nicht ermittelt werden.",
            "Could not be determined.")
    };
}
