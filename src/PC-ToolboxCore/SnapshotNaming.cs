namespace PCToolboxCore;

public static class SnapshotNaming
{
    // Entfernt Zeichen, die in einem Windows-Dateinamen nicht erlaubt sind (Snapshot-Ordnername
    // wird meist aus einem freien Programmnamen gebaut, der z.B. ":" oder "/" enthalten kann).
    public static string SicherenOrdnernamen(string text, IEnumerable<char> ungueltigeZeichen)
    {
        var ungueltig = new HashSet<char>(ungueltigeZeichen);
        return new string(text.Where(c => !ungueltig.Contains(c)).ToArray());
    }
}
