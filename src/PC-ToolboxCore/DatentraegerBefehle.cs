namespace PCToolboxCore;

/// <summary>
/// Baut die PowerShell-Befehle für die Datenträger-Aktionen. Windows bringt dafür
/// eigene, gut abgesicherte Befehle mit (Format-Volume, Set-Volume, Set-Partition,
/// Resize-Partition) - die sind erprobter, als es ein selbstgeschriebener Zugriff auf
/// die Partitionstabelle je sein könnte. Genau wie beim ImDisk-Modul gilt: den
/// vorhandenen, sicheren Motor nehmen und eine verständliche Oberfläche darüber bauen.
///
/// Liegt im plattformneutralen Kern, damit sich der heikelste Teil - was genau
/// ausgeführt wird - auf dem Pi wirklich testen lässt, ohne je einen Datenträger
/// anzufassen.
/// </summary>
public static class DatentraegerBefehle
{
    /// <summary>
    /// Setzt Text sicher in einfache Anführungszeichen. In PowerShell wird ein
    /// Anführungszeichen darin durch Verdoppeln entwertet - ohne das könnte eine
    /// Bezeichnung wie "Alex' Platte" den Befehl aufbrechen.
    /// </summary>
    public static string AlsText(string wert) => "'" + (wert ?? "").Replace("'", "''") + "'";

    public static string Formatieren(char buchstabe, string dateisystem, string bezeichnung, bool schnell)
    {
        var laufwerk = char.ToUpperInvariant(buchstabe);
        var art = schnell ? "" : " -Full";

        return $"Format-Volume -DriveLetter {laufwerk} -FileSystem {dateisystem} " +
               $"-NewFileSystemLabel {AlsText(bezeichnung)}{art} -Force -Confirm:$false";
    }

    public static string BezeichnungAendern(char buchstabe, string bezeichnung) =>
        $"Set-Volume -DriveLetter {char.ToUpperInvariant(buchstabe)} " +
        $"-NewFileSystemLabel {AlsText(bezeichnung)}";

    public static string BuchstabeAendern(char alt, char neu) =>
        $"Set-Partition -DriveLetter {char.ToUpperInvariant(alt)} " +
        $"-NewDriveLetter {char.ToUpperInvariant(neu)}";

    public static string GroesseAendern(char buchstabe, long neueGroesseBytes) =>
        $"Resize-Partition -DriveLetter {char.ToUpperInvariant(buchstabe)} -Size {neueGroesseBytes}";

    /// <summary>Fragt ab, wie klein bzw. groß die Partition überhaupt werden darf.</summary>
    public static string GrenzenAbfragen(char buchstabe) =>
        $"Get-PartitionSupportedSize -DriveLetter {char.ToUpperInvariant(buchstabe)} | " +
        "ConvertTo-Json -Compress";

    /// <summary>Argumente für powershell.exe - ohne Profil, damit nichts dazwischenfunkt.</summary>
    public static string AlsArgumente(string befehl) =>
        $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{befehl.Replace("\"", "\\\"")}\"";
}
