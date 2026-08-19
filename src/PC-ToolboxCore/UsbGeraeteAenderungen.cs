namespace PCToolboxCore;

public enum GeraeteAenderungsArt
{
    Erschienen,
    Verschwunden
}

/// <summary>
/// Ein einzelnes An-/Absteck-Ereignis fuer die Geraete-Aenderungsueberwachung. Anzeige zeigt
/// IMMER die rohe PNPDeviceID mit dazu (nicht nur als Fallback bei fehlendem Namen) - der Name
/// allein ist oft ein generischer Windows-Text wie "USB-Eingabegerät", der sich hinterher nicht
/// nachschlagen laesst. Die PNPDeviceID enthaelt VID_.../PID_..., das ist der Teil, nach dem man
/// tatsaechlich suchen kann, um herauszufinden, was fuer ein Geraet das war (echter Praxisfall
/// 11.08.2026: ein Geraet meldete sich nur als "USB-Eingabegerät", ohne Herstellernamen).
/// </summary>
public record GeraeteAenderung(DateTime Zeitpunkt, GeraeteAenderungsArt Art, string PnpDeviceId, string? Name)
{
    public string Anzeige => string.IsNullOrWhiteSpace(Name) ? PnpDeviceId : $"{Name}  ·  {PnpDeviceId}";
}

/// <summary>
/// Reine Klassifizierungs-/Anzeigelogik fuer die Geraete-Aenderungsueberwachung im USB-Geraete-
/// Modul. Entstand aus einem echten Fall (11.08.2026): ein USB-Geraet blitzte beim An-/Abstecken
/// kurz im Geraete-Manager auf, bekam aber nie einen benannten Eintrag - eine reine
/// Abfrage-Momentaufnahme (wie UsbGeraeteService.LadeAsync) haette den ganzen Vorgang verpasst,
/// weil zwischen zwei Momentaufnahmen wieder alles "normal" aussah.
/// </summary>
public static class UsbGeraeteAenderungen
{
    public static bool IstUsbGeraet(string? pnpDeviceId) =>
        !string.IsNullOrWhiteSpace(pnpDeviceId) && pnpDeviceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase);

    public static string ArtText(GeraeteAenderungsArt art) => art switch
    {
        GeraeteAenderungsArt.Erschienen => Loc.T("Erschienen", "Appeared"),
        GeraeteAenderungsArt.Verschwunden => Loc.T("Verschwunden", "Disappeared"),
        _ => ""
    };
}
