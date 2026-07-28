namespace PCToolboxCore;

/// <summary>Ergänzende Erklärtexte fürs USB-Geräte-Modul, die nicht an ein einzelnes Gerät hängen.</summary>
public static class UsbErklaerungen
{
    /// <summary>
    /// Direkter Bezug zum Schwesterprojekt isy-iem-1001: dort waren USB-Aussetzer das
    /// Kernproblem, Ursache war der USB-Energiesparmodus (Selective Suspend). Dieses Wissen
    /// gehört ins USB-Modul, nicht nur in ein Maustreiber-Projekt.
    /// </summary>
    public static string EnergiesparmodusVerdacht() => Loc.T(
        "Verschwindet ein USB-Gerät sporadisch und taucht von selbst wieder auf, ist oft Windows' " +
        "eigener USB-Energiesparmodus (\"Selective Suspend\") die Ursache: Windows schaltet das Gerät " +
        "bei Nichtnutzung ab, um Strom zu sparen, und weckt es bei Bedarf wieder auf - das klappt nicht " +
        "bei jedem Gerät zuverlässig. Zu finden und abschaltbar unter Energieoptionen → Erweiterte " +
        "Energieeinstellungen → USB-Einstellungen → USB-Selective Suspend-Einstellung.",
        "If a USB device sporadically disappears and comes back on its own, Windows' own USB power " +
        "management (\"Selective Suspend\") is often the cause: Windows turns the device off when idle " +
        "to save power and wakes it back up on demand - this doesn't work reliably for every device. " +
        "Found and can be turned off under Power Options → Advanced power settings → USB settings → " +
        "USB selective suspend setting.");
}
