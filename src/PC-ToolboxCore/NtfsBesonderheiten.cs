using System.IO;

namespace PCToolboxCore;

/// <summary>
/// Reine Entscheidungslogik zu zwei NTFS-Besonderheiten (Rang 8 aus der NirSoft-Recherche,
/// Vorbild NTFSLinksView/AlternateStreamView): Junction Points und die "aus dem Internet
/// heruntergeladen"-Markierung (Zone.Identifier Alternate Data Stream). Beide sind fuer
/// Nutzer unsichtbar, haben aber echte Auswirkungen - ein Junction Point kann beim Kopieren
/// in eine Schleife laufen, und die Markierung ist genau der Mechanismus hinter der eigenen
/// SmartScreen-Warnung des unsignierten Toolbox-X-Setups.
/// </summary>
public static class NtfsBesonderheiten
{
    /// <summary>
    /// Ob ein Ordner ein Reparse Point ist (Junction, symbolischer Link, o.ä.) - der Punkt an
    /// dem ein Kopiervorgang, der naiv jedem Unterordner folgt, in eine Endlosschleife laufen
    /// kann (ein Junction kann auf einen eigenen Vorfahren zeigen).
    /// </summary>
    public static bool IstReparsePoint(FileAttributes attribute) => attribute.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>Der Pfad des Alternate Data Stream, in dem Windows die Herkunfts-Zone einer heruntergeladenen Datei speichert.</summary>
    public static string ZoneIdentifierPfad(string dateiPfad) => dateiPfad + ":Zone.Identifier";

    public static string JunctionErklaerung() => Loc.T(
        "Das ist kein normaler Ordner, sondern eine Verknüpfung (Junction/symbolischer Link) auf einen anderen " +
        "Ort - der Inhalt liegt eigentlich woanders. Toolbox X folgt solchen Verknüpfungen beim Kopieren " +
        "bewusst nicht automatisch weiter, weil eine Verknüpfung theoretisch auf einen eigenen übergeordneten " +
        "Ordner zeigen könnte - das würde sonst zu einer Endlosschleife führen.",
        "This isn't a regular folder but a link (junction/symbolic link) to another location - the actual " +
        "content lives elsewhere. Toolbox X deliberately doesn't follow such links when copying, because a " +
        "link could theoretically point back to one of its own parent folders - which would otherwise cause " +
        "an infinite loop.");

    public static string HerkunftsmarkierungErklaerung() => Loc.T(
        "Diese Datei trägt Windows' unsichtbare Markierung \"aus dem Internet heruntergeladen\" (gespeichert in " +
        "einem zusätzlichen NTFS-Datenstrom, nicht in der Datei selbst sichtbar). Genau dieser Mechanismus ist " +
        "es auch, der bei unserem eigenen, unsignierten Setup die SmartScreen-Warnung auslöst - kein Fehler, " +
        "sondern Windows' Standardverhalten bei jeder heruntergeladenen Datei ohne bekannten Herausgeber. " +
        "Über Rechtsklick → Eigenschaften → \"Zulassen\" lässt sich die Markierung entfernen.",
        "This file carries Windows' invisible \"downloaded from the internet\" mark (stored in an extra NTFS " +
        "data stream, not visible in the file itself). This exact mechanism is also what triggers the " +
        "SmartScreen warning on our own unsigned installer - not a bug, just Windows' standard behavior for " +
        "any downloaded file without a known publisher. It can be removed via right-click → Properties → " +
        "\"Unblock\".");
}
