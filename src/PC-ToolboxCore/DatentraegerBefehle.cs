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
public static partial class DatentraegerBefehle
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

    /// <summary>
    /// Löst die häufigste Ursache für "Laufwerk wird verwendet" auf: ein offenes Explorer-Fenster
    /// auf dem Laufwerk (reicht schon, um Format-Volume/Set-Volume abzuweisen) - wird geschlossen,
    /// danach das Volume sauber ausgehängt, damit auch von anderen Programmen gehaltene Datei-
    /// Handles freigegeben werden. Absichtlich mit SilentlyContinue/try-catch: das ist ein
    /// Best-Effort-Aufräumschritt vor einem automatischen zweiten Versuch, kein eigener,
    /// fehlschlagfähiger Vorgang - schlägt er fehl, soll trotzdem der eigentliche Befehl danach
    /// laufen (und ehrlich melden, wenn's dann immer noch nicht klappt).
    /// </summary>
    public static string LaufwerkFreigeben(char buchstabe)
    {
        var laufwerk = char.ToUpperInvariant(buchstabe);
        return $"(New-Object -ComObject Shell.Application).Windows() | " +
               $"Where-Object {{ $_.LocationURL -like 'file:///{laufwerk}:*' }} | ForEach-Object {{ $_.Quit() }}; " +
               "Start-Sleep -Milliseconds 500; " +
               $"try {{ Dismount-Volume -DriveLetter {laufwerk} -Confirm:$false -ErrorAction SilentlyContinue }} catch {{}}";
    }

    /// <summary>
    /// Argumente für powershell.exe - ohne Profil, damit nichts dazwischenfunkt.
    ///
    /// $ErrorActionPreference='Stop' + try/catch + "exit 1" ist kein Stilmittel, sondern eine
    /// echte Korrektur: PowerShell.exe gibt bei einem Cmdlet-Fehler in einer Pipeline
    /// (z.B. "New-Partition | Format-Volume" scheitert mitten drin) standardmäßig trotzdem
    /// Exit-Code 0 zurück - der Aufrufer hier hätte "Erfolg" gemeldet, obwohl nur ein Teil
    /// der Aktion durchgelaufen ist. Genau das ist bei der ersten echten Partitions-Erstellung
    /// passiert: eine leere, unpartitionierte Platte statt der erwarteten neuen Partition,
    /// von der Toolbox aber als Erfolg gemeldet.
    /// </summary>
    public static string AlsArgumente(string befehl)
    {
        var innererBefehl = befehl.Replace("\"", "\\\"");
        return "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
               $"\"$ErrorActionPreference = 'Stop'; try {{ {innererBefehl} }} catch {{ Write-Error $_; exit 1 }}\"";
    }
}
