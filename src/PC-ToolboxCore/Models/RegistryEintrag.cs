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

/// <summary>Welche Registry-Werttyp die Empfehlung braucht - bewusst ein eigenes,
/// plattformneutrales Enum statt Microsoft.Win32.RegistryValueKind, damit PCToolboxCore ohne
/// Windows-Abhängigkeit bleibt (s. Projektkommentar in PC-ToolboxCore.csproj).</summary>
public enum RegistryWertArt { Zahl, Text }

/// <summary>Der empfohlene ("sichere") Wert für einen RegistryEintrag, samt Werttyp fürs
/// spätere Schreiben. Nur für Stellen definiert, wo eine klare Empfehlung überhaupt Sinn ergibt
/// (s. RegistryKompendium.EmpfehlungFuer) - nicht jede Registry-Stelle hat eine.</summary>
public record RegistryEmpfehlung(object Wert, RegistryWertArt Art);
