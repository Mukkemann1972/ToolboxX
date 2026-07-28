using PCToolboxCore;

namespace PCToolbox.Models;

/// <summary>Eine Zeile in einer der beiden Dateilisten des Commanders.</summary>
public class DateiEintrag
{
    public string Name { get; set; } = "";
    public string VollerPfad { get; set; } = "";
    public bool IstOrdner { get; set; }

    /// <summary>Null bei Ordnern - deren Größe zu berechnen würde bei jedem Ordnerwechsel bremsen.</summary>
    public long? GroesseBytes { get; set; }

    public DateTime? Geaendert { get; set; }

    /// <summary>True für den Eintrag "..", mit dem man eine Ebene höher geht.</summary>
    public bool IstNachOben { get; set; }

    /// <summary>Ordner ist ein Reparse Point (Junction/symbolischer Link) - Toolbox X folgt dem beim Kopieren nicht (Schleifengefahr).</summary>
    public bool IstJunction { get; set; }

    /// <summary>Datei trägt die "aus dem Internet heruntergeladen"-Markierung (Zone.Identifier).</summary>
    public bool HatHerkunftsmarkierung { get; set; }

    public string Symbol => IstNachOben ? "⬆️" : IstJunction ? "🔗" : IstOrdner ? "📁" : HatHerkunftsmarkierung ? "🌐" : "📄";

    public string GroesseAnzeige => IstNachOben
        ? ""
        : IstOrdner
            ? (IstJunction ? Loc.T("<Verknüpfung>", "<Link>") : Loc.T("<Ordner>", "<Folder>"))
            : GroesseBytes is { } bytes ? DateiHelfer.GroesseFormatieren(bytes) : "";

    public string GeaendertAnzeige => Geaendert is { } zeit ? zeit.ToString("dd.MM.yyyy HH:mm") : "";

    public string? Hinweis => IstJunction
        ? NtfsBesonderheiten.JunctionErklaerung()
        : HatHerkunftsmarkierung
            ? NtfsBesonderheiten.HerkunftsmarkierungErklaerung()
            : null;
}
