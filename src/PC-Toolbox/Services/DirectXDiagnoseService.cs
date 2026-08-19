using System.Diagnostics;
using System.IO;
using System.Text;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// DirectX-Diagnosebericht (Free, kein Admin nötig): ruft Windows' eigenes "dxdiag /t" auf und
/// wertet die Textdatei aus (s. DirectXDiagnose-Kommentar zur Sprachbesonderheit). Die erzeugte
/// Rohdatei bleibt erhalten (Pfad wird zurückgegeben) statt sofort gelöscht zu werden - der
/// vollständige Bericht ist für Werkstatt-/Support-Zwecke oft nützlicher als unsere eigene
/// Kurzauswertung.
/// </summary>
public class DirectXDiagnoseService
{
    public async Task<(DirectXDiagnoseAuswertung Auswertung, string RohBerichtPfad)> BerichtErstellenAsync()
    {
        var pfad = Path.Combine(Path.GetTempPath(), "ToolboxX-DxDiag-" + Guid.NewGuid().ToString("N") + ".txt");

        var start = new ProcessStartInfo("dxdiag.exe", $"/t \"{pfad}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var prozess = Process.Start(start);
        if (prozess != null) await prozess.WaitForExitAsync();

        // Defensive kurze Wartezeit: dxdiag beendet den Prozess, sobald die Datei geschrieben wird,
        // ein sofortiges Lesen direkt danach war beim Live-Test zuverlässig - der kurze Poll bleibt
        // trotzdem als Sicherheitsnetz stehen statt sich blind auf den exakten Zeitpunkt zu verlassen.
        for (var i = 0; i < 10 && !File.Exists(pfad); i++)
            await Task.Delay(300);

        if (!File.Exists(pfad))
            return (new DirectXDiagnoseAuswertung(null, Array.Empty<DirectXNotiz>()), "");

        var text = await File.ReadAllTextAsync(pfad, new UTF8Encoding(false));
        return (DirectXDiagnose.Auswerten(text), pfad);
    }
}

/// <summary>
/// Windows-Speicherdiagnose (Free, kein Admin-Gate nötig): startet Windows' eigenes "mdsched.exe"
/// direkt. Bewusst KEINE eigene Automatisierung des Planungsschritts - "mdsched.exe" hat keine
/// dokumentierten Kommandozeilenschalter zum stillen Einplanen eines Tests (recherchiert, nicht
/// vorhanden), nur die native 2-Knopf-Auswahl ("Jetzt neu starten" / "Beim nächsten Start prüfen").
/// Toolbox X ruft das offizielle Werkzeug auf und erklärt, was passiert - genau das Prinzip, das
/// WindowsReparaturService schon für DISM/SFC beschreibt: kein Ersatz für Windows' eigene
/// Werkzeuge bauen, wo eines schon existiert.
/// </summary>
public class SpeicherdiagnoseService
{
    public void Starten() =>
        Process.Start(new ProcessStartInfo("mdsched.exe") { UseShellExecute = true });
}
