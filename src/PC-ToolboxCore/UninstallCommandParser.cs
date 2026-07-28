namespace PCToolboxCore;

// UninstallString aus der Registry kommt oft als "C:\Pfad mit Leerzeichen\uninst.exe" /S o.ae. -
// fuer Process.Start(fileName, arguments) muessen Datei und Argumente getrennt werden.
public static class UninstallCommandParser
{
    public static (string Datei, string Argumente) Zerlegen(string uninstallString)
    {
        var trimmed = uninstallString.Trim();
        if (trimmed.Length == 0) return ("", "");

        if (trimmed.StartsWith('"'))
        {
            var endeAnfuehrung = trimmed.IndexOf('"', 1);
            if (endeAnfuehrung > 0)
            {
                var datei = trimmed[1..endeAnfuehrung];
                var rest = trimmed[(endeAnfuehrung + 1)..].Trim();
                return (datei, rest);
            }
        }

        var leerzeichen = trimmed.IndexOf(' ');
        return leerzeichen < 0 ? (trimmed, "") : (trimmed[..leerzeichen], trimmed[(leerzeichen + 1)..]);
    }
}
