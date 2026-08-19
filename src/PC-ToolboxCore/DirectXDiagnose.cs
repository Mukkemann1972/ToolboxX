using System.Text.RegularExpressions;

namespace PCToolboxCore;

/// <summary>Ein von dxdiag selbst festgestellter Befund zu einem Gerätebereich (Anzeige/Sound/
/// Eingabe) - "No problems found." ist der weit überwiegende, unauffällige Normalfall.</summary>
public record DirectXNotiz(string Bereich, string Befund)
{
    public bool IstUnauffaellig => Befund.Equals("No problems found.", StringComparison.OrdinalIgnoreCase);
}

public record DirectXDiagnoseAuswertung(string? Version, IReadOnlyList<DirectXNotiz> Notizen);

/// <summary>
/// Parser für den Textbericht von "dxdiag /t &lt;Datei&gt;" (Free, kein Admin nötig, ergänzt Modul 8
/// GPU-Erkennung um DirectX-Version und Windows' eigene Problem-Zusammenfassung). Live geprüft
/// (dieser Rechner, deutsches Windows 11): der dxdiag-Textbericht ist, ein seit Jahren bekannter
/// Sonderfall dieses Tools, komplett auf ENGLISCH - unabhängig von der Windows-Anzeigesprache.
/// Deshalb wird hier direkt gegen die englischen Marker geparst, kein Sprachumschalter nötig - nur
/// auf dieser einen Maschine live geprüft, andere Windows-Versionen könnten theoretisch abweichen
/// (dann liefert der Parser defensiv null/eine leere Liste statt abzustürzen, kein Rätselraten).
/// </summary>
public static class DirectXDiagnose
{
    private static readonly Regex VersionRegex = new(@"DirectX Version:\s*(.+)$", RegexOptions.Multiline);

    private static readonly Regex NotesBlockRegex = new(
        @"-+\r?\nDxDiag Notes\r?\n-+\r?\n(.*?)\r?\n\r?\n-+", RegexOptions.Singleline);

    private static readonly Regex NoteLineRegex = new(@"^\s*(.+?):\s*(.+)$");

    public static DirectXDiagnoseAuswertung Auswerten(string rohbericht)
    {
        var versionMatch = VersionRegex.Match(rohbericht);
        var version = versionMatch.Success ? versionMatch.Groups[1].Value.Trim() : null;

        var notizen = new List<DirectXNotiz>();
        var notesMatch = NotesBlockRegex.Match(rohbericht);
        if (notesMatch.Success)
        {
            foreach (var rohzeile in notesMatch.Groups[1].Value.Split('\n'))
            {
                var zeile = rohzeile.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(zeile)) continue;

                var treffer = NoteLineRegex.Match(zeile);
                if (treffer.Success)
                    notizen.Add(new DirectXNotiz(treffer.Groups[1].Value.Trim(), treffer.Groups[2].Value.Trim()));
            }
        }

        return new DirectXDiagnoseAuswertung(version, notizen);
    }
}
