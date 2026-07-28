using PCToolbox.Models;

namespace PCToolboxCore;

public record DruckerBefund(string Text, AmpelStatus Status);

/// <summary>
/// Reine Deutung der WMI-Statuscodes von Win32_Printer (neues Free-Modul aus Hermes'
/// 23.07-Liste: "Drucker erkennen + Status", rein lesend). Zwei getrennte Codes, weil WMI sie
/// auch getrennt liefert: PrinterStatus (grober Zustand: druckt/leerlauf/offline) und
/// DetectedErrorState (konkreter Fehler: Papier alle, Tonerstand, Papierstau usw.) - erst die
/// Kombination beider ergibt einen verständlichen Befund statt nur einer Zahl.
/// </summary>
public static class DruckerDeutung
{
    /// <summary>WMI Win32_Printer.PrinterStatus: 1=Other,2=Unknown,3=Idle,4=Printing,5=Warmup,6=StoppedPrinting,7=Offline.</summary>
    public static DruckerBefund DeutePrinterStatus(int status) => status switch
    {
        3 => new(Loc.T("Bereit", "Ready"), AmpelStatus.Gruen),
        4 => new(Loc.T("Druckt gerade", "Currently printing"), AmpelStatus.Gruen),
        5 => new(Loc.T("Wärmt auf", "Warming up"), AmpelStatus.Gelb),
        6 => new(Loc.T("Druckvorgang gestoppt", "Printing stopped"), AmpelStatus.Rot),
        7 => new(Loc.T("Offline", "Offline"), AmpelStatus.Rot),
        1 or 2 => new(Loc.T("Status unbekannt", "Status unknown"), AmpelStatus.Unbekannt),
        _ => new(Loc.T("Status unbekannt", "Status unknown"), AmpelStatus.Unbekannt)
    };

    /// <summary>
    /// WMI Win32_Printer.DetectedErrorState - nur die häufigsten, eindeutigen Werte gedeutet
    /// (0=Unknown, 2=NoError bewusst NICHT als eigener Befund, sondern als "kein zusätzlicher
    /// Fehler" behandelt, damit ein sauberer Drucker nicht zwei widersprüchliche Meldungen zeigt).
    /// </summary>
    public static DruckerBefund? DeuteErrorState(int errorState) => errorState switch
    {
        0 or 2 => null, // Unknown/NoError - nichts Zusätzliches zu sagen
        3 => new(Loc.T("Papier wird knapp", "Paper running low"), AmpelStatus.Gelb),
        4 => new(Loc.T("Kein Papier mehr", "Out of paper"), AmpelStatus.Rot),
        5 => new(Loc.T("Toner/Tinte wird knapp", "Toner/ink running low"), AmpelStatus.Gelb),
        6 => new(Loc.T("Kein Toner/keine Tinte mehr", "Out of toner/ink"), AmpelStatus.Rot),
        7 => new(Loc.T("Klappe/Deckel offen", "Door/cover open"), AmpelStatus.Rot),
        8 => new(Loc.T("Papierstau", "Paper jam"), AmpelStatus.Rot),
        9 => new(Loc.T("Wartung erforderlich", "Service required"), AmpelStatus.Gelb),
        10 => new(Loc.T("Ausgabefach voll", "Output bin full"), AmpelStatus.Rot),
        _ => new(Loc.T("Ein Fehler liegt vor (genauer Grund unbekannt)", "There's an error (exact cause unknown)"), AmpelStatus.Gelb)
    };

    /// <summary>Kombiniert PrinterStatus, DetectedErrorState und WorkOffline zu einem einzigen, verständlichen Befund.</summary>
    public static DruckerBefund GesamtBefund(int printerStatus, int detectedErrorState, bool workOffline)
    {
        if (workOffline)
            return new(Loc.T("Auf \"Offline arbeiten\" gestellt - Windows schickt keine Druckaufträge dorthin.", "Set to \"work offline\" - Windows won't send print jobs there."), AmpelStatus.Rot);

        var fehler = DeuteErrorState(detectedErrorState);
        if (fehler is not null) return fehler;

        return DeutePrinterStatus(printerStatus);
    }
}
