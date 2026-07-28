namespace PCToolboxCore;

/// <summary>
/// Übersetzt Windows' PNPClass-Werte in lesbare Kategorienamen fürs Gruppieren im
/// Treiber-Check - bei vielen Funden hilft eine Gruppierung nach Kategorie mehr als eine
/// lange flache Liste. Bewusst nur die haeufigsten Klassen mit eigenem Klartext, alles
/// andere kommt unveraendert durch (PNPClass ist meist schon einigermassen lesbar) statt
/// eine riesige Uebersetzungstabelle zu pflegen.
/// </summary>
public static class GeraeteKategorien
{
    public static string Klartext(string? pnpKlasse)
    {
        if (string.IsNullOrWhiteSpace(pnpKlasse))
            return Loc.T("Sonstige", "Other");

        return pnpKlasse.Trim().ToLowerInvariant() switch
        {
            "net" => Loc.T("Netzwerk", "Network"),
            "usb" => "USB",
            "hidclass" => Loc.T("Eingabegeräte (Tastatur/Maus)", "Input devices (keyboard/mouse)"),
            "mouse" => Loc.T("Maus", "Mouse"),
            "keyboard" => Loc.T("Tastatur", "Keyboard"),
            "display" => Loc.T("Grafik", "Display"),
            "monitor" => Loc.T("Bildschirm", "Monitor"),
            "mediadevices" or "media" => Loc.T("Audio/Video", "Audio/video"),
            "diskdrive" => Loc.T("Laufwerke", "Drives"),
            "printer" => Loc.T("Drucker", "Printers"),
            "bluetooth" => "Bluetooth",
            "system" => Loc.T("Systemgeräte", "System devices"),
            "softwaredevice" => Loc.T("Software-Geräte", "Software devices"),
            _ => pnpKlasse.Trim()
        };
    }
}
