using PCToolbox.Models;

namespace PCToolboxCore.Models;

/// <summary>
/// Grobe Einordnung eines Autostart-Eintrags. Die Kategorie bestimmt die Ampelfarbe,
/// die Texte kommen entweder aus dem Regelkatalog (bekanntes Programm) oder aus der
/// allgemeinen Erklaerung zur Kategorie.
/// </summary>
public enum AutostartKategorie
{
    /// <summary>Windows selbst, Treiber-Zubehoer, Sicherheit - sollte anbleiben.</summary>
    Systemnah,

    /// <summary>Kein Systemteil, muss aber mitstarten um seinen Zweck zu erfuellen (z.B. Cloud-Abgleich).</summary>
    Nuetzlich,

    /// <summary>Komfort-Programm: laeuft gerne mit, geht aber auch ohne (Spiele-Launcher, Chat).</summary>
    Optional,

    /// <summary>Reine Bremse: Updater, "Helper", Tray-Symbole ohne echten Nutzen beim Start.</summary>
    Bremse,

    /// <summary>Startet aus einem Ordner, in dem normale Programme nichts zu suchen haben.</summary>
    Verdaechtig,

    /// <summary>Der Eintrag zeigt auf eine Datei, die es gar nicht mehr gibt.</summary>
    Karteileiche,

    /// <summary>Kennen wir nicht - bewusst NICHT als "schlecht" dargestellt.</summary>
    Unbekannt
}

/// <summary>
/// Das Ergebnis der Bewertung: Ampel + drei Texte, die zusammen fuer sich allein
/// ausreichen muessen. Kernprinzip von Toolbox X: Wer hier steht, hat KEINE
/// Moeglichkeit, jemanden zu fragen - "frag im Zweifel eine KI" ist keine Antwort.
/// </summary>
public class AutostartBewertung
{
    public AutostartKategorie Kategorie { get; init; }
    public AmpelStatus Ampel { get; init; }

    /// <summary>Ein Satz Urteil, z.B. "Gehoert dazu - bitte anlassen".</summary>
    public string Kurzurteil { get; init; } = "";

    /// <summary>Was dieses Programm ueberhaupt ist/macht.</summary>
    public string WasEsIst { get; init; } = "";

    /// <summary>Was zu tun ist - in ganzen Saetzen, ohne Fachbegriffe.</summary>
    public string Empfehlung { get; init; } = "";
}
