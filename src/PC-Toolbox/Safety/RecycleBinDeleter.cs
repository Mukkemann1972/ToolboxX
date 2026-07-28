using System.Runtime.InteropServices;

namespace PCToolbox.Safety;

// Verschiebt Dateien/Ordner in den Windows-Papierkorb statt sie endgültig zu löschen - der Nutzer
// kann sie dort jederzeit selbst wiederherstellen. Nutzt die klassische Shell-API (SHFileOperation,
// FOF_ALLOWUNDO) statt eines zusätzlichen NuGet-Pakets. Für den System-Saubermacher: hier ÜBERNIMMT
// der Windows-Papierkorb die Rolle, die beim Super-Uninstaller der eigene Snapshot-Ordner hat -
// ein zusätzlicher Kopier-Umweg würde das Ziel (echten Platz freigeben) unterlaufen.
internal static class RecycleBinDeleter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    private const int FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT fileOp);

    // pfad = einzelne Datei ODER einzelner Ordner (Ordner werden inkl. Inhalt verschoben).
    public static bool InDenPapierkorbVerschieben(string pfad)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            // Die Win32-API verlangt einen doppelt Null-terminierten String als Pfadliste.
            pFrom = pfad + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };

        var ergebnis = SHFileOperation(ref op);
        return ergebnis == 0 && !op.fAnyOperationsAborted;
    }
}
