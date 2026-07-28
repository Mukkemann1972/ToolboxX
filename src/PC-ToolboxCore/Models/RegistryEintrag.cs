using PCToolbox.Models;

namespace PCToolboxCore.Models;

/// <summary>Eine kuratierte Registry-Stelle aus dem Registry-Kompendium (nicht Nutzereingabe, feste Liste).</summary>
public record RegistryEintrag(
    string Id,
    string Titel,
    string Hive,
    string Pfad,
    string Wertname,
    string Erklaerung);

public record RegistryBefund(string Text, AmpelStatus Status);
