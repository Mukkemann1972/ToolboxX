using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Führt die beiden Windows-eigenen Systemreparaturen aus und übersetzt ihre
/// Ausgabe in Klartext (Deutung: <see cref="SystemPruefungAusgabe"/>).
///
/// Was hier bewusst NICHT passiert: kein eigenes Herumschreiben in Systemdateien.
/// Windows bringt mit <c>sfc</c> und <c>DISM</c> die richtigen Werkzeuge schon mit -
/// sie sind nur unbenutzbar dokumentiert. Toolbox X ruft sie in der richtigen
/// Reihenfolge auf und erklärt das Ergebnis. Etwas Eigenes an Systemdateien
/// vorbeizuschreiben wäre gefährlicher, nicht besser.
///
/// Die Reihenfolge ist der eigentliche Mehrwert: <b>zuerst DISM, dann SFC</b>.
/// SFC ersetzt beschädigte Systemdateien aus dem Komponentenspeicher. Ist dieser
/// Speicher selbst beschädigt, kann SFC nichts reparieren - man bekommt endlos
/// "konnte einige nicht reparieren". Windows erklärt das nirgends, und deshalb
/// scheitern die meisten Reparaturversuche im Netz an dieser Reihenfolge.
/// </summary>
public class WindowsReparaturService
{
    /// <summary>Prüfschritte, die nichts verändern (nur lesen).</summary>
    public const string DismPruefen = "/Online /Cleanup-Image /ScanHealth";
    public const string SfcPruefen = "/verifyonly";

    /// <summary>Reparaturschritte - verändern das System, nur nach Bestätigung.</summary>
    public const string DismReparieren = "/Online /Cleanup-Image /RestoreHealth";
    public const string SfcReparieren = "/scannow";

    public static bool HatAdminRechte()
    {
        try
        {
            using var identitaet = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identitaet).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Öffnet die Windows-11-Einstellungsseite "System &gt; Wiederherstellung", auf der Windows
    /// (wenn es einen Grund dazu sieht) "Probleme mittels Windows Update beheben" anbietet -
    /// eine cloudbasierte Neuinstallation der Systemdateien, die Dateien/Programme/Einstellungen
    /// behält. Der nächste sinnvolle Schritt, wenn DISM+SFC oben (<see cref="DismReparieren"/>,
    /// <see cref="SfcReparieren"/>) nicht ausreichen, aber vor einer echten Neuinstallation über
    /// Installationsmedium. Toolbox X öffnet nur die offizielle Windows-Seite - ob der Button
    /// dort erscheint, entscheidet Windows selbst (abhängig von Build/Zustand), deshalb kein
    /// eigener Reparatur-Button dafür, nur eine ehrliche Weiterleitung + Erklärung.
    /// </summary>
    public static void OeffneWindowsUpdateReparatur() =>
        Process.Start(new ProcessStartInfo("ms-settings:recovery") { UseShellExecute = true });

    // Die zwei Werkzeuge geben UNTERSCHIEDLICH kodiert aus - das ist die Falle, die
    // beim ersten Test zu unlesbarem Zeichensalat gefuehrt hat:
    //   sfc.exe  schreibt UTF-16. Ohne Encoding.Unicode kommen Null-Bytes zwischen
    //            allen Buchstaben an.
    //   dism.exe schreibt in der Konsolen-Codepage (auf deutschem Windows 850),
    //            NICHT UTF-16. Mit Encoding.Unicode wird daraus Zeichensalat.
    //
    // Loesung fuer DISM: der offizielle Schalter /English. Damit ist die Ausgabe
    // reines ASCII - die Kodierungsfrage entfaellt komplett. Zwei Umwege, die NICHT
    // funktionieren und deshalb hier dokumentiert sind, damit sie niemand erneut
    // probiert: "chcp 65001" wirkt bei DISM nicht (Umlaute bleiben kaputt), und die
    // OEM-Codepage 850 liefert .NET 8 gar nicht mit (braeuchte ein Extra-Paket).
    //
    // Nebeneffekt, der sogar erwuenscht ist: die Auswertung haengt damit nicht mehr
    // an der Windows-Sprache. Und wer die Originalausgabe zum Suchen braucht, findet
    // zu den englischen DISM-Meldungen deutlich mehr Treffer.
    public Task<(PruefErgebnis Ergebnis, string Rohausgabe)> DismAusfuehrenAsync(
        string argumente, Action<string>? fortschritt = null, CancellationToken abbruch = default) =>
        AusfuehrenAsync("dism.exe", $"/English {argumente}",
            new UTF8Encoding(false), SystemPruefungAusgabe.DismDeuten, fortschritt, abbruch);

    public Task<(PruefErgebnis Ergebnis, string Rohausgabe)> SfcAusfuehrenAsync(
        string argumente, Action<string>? fortschritt = null, CancellationToken abbruch = default) =>
        AusfuehrenAsync("sfc.exe", argumente,
            Encoding.Unicode, SystemPruefungAusgabe.SfcDeuten, fortschritt, abbruch);

    private static async Task<(PruefErgebnis, string)> AusfuehrenAsync(
        string programm,
        string argumente,
        Encoding kodierung,
        Func<string, PruefErgebnis> deuter,
        Action<string>? fortschritt,
        CancellationToken abbruch)
    {
        if (!HatAdminRechte())
        {
            return (new PruefErgebnis(PruefBefund.NichtGelaufen,
                Loc.T("Diese Prüfung braucht Administrator-Rechte.", "This check needs administrator rights."),
                Loc.T(
                    "Toolbox X schließen und per Rechtsklick \"Als Administrator ausführen\" neu starten. " +
                    "Ohne diese Rechte lässt Windows die Systemprüfung gar nicht erst zu - das ist eine " +
                    "Schutzmaßnahme von Windows, kein Fehler von Toolbox X.",
                    "Close Toolbox X and restart it via right-click \"Run as administrator\". " +
                    "Without these rights, Windows doesn't allow the system check at all - this is a " +
                    "protection measure by Windows, not a bug in Toolbox X.")), "");
        }

        var start = new ProcessStartInfo
        {
            FileName = programm,
            Arguments = argumente,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = kodierung,
            StandardErrorEncoding = kodierung
        };

        var gesammelt = new StringBuilder();

        try
        {
            using var prozess = new Process { StartInfo = start };

            prozess.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var zeile = SystemPruefungAusgabe.Bereinigen(e.Data);
                if (zeile.Length == 0) return;
                lock (gesammelt) gesammelt.AppendLine(zeile);
                fortschritt?.Invoke(zeile);
            };
            prozess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                var zeile = SystemPruefungAusgabe.Bereinigen(e.Data);
                if (zeile.Length == 0) return;
                lock (gesammelt) gesammelt.AppendLine(zeile);
            };

            prozess.Start();
            prozess.BeginOutputReadLine();
            prozess.BeginErrorReadLine();

            await prozess.WaitForExitAsync(abbruch);

            string roh;
            lock (gesammelt) roh = gesammelt.ToString();
            return (deuter(roh), roh);
        }
        catch (OperationCanceledException)
        {
            string roh;
            lock (gesammelt) roh = gesammelt.ToString();
            return (new PruefErgebnis(PruefBefund.NichtGelaufen,
                Loc.T("Die Prüfung wurde abgebrochen.", "The check was cancelled."),
                Loc.T(
                    "Ein Abbruch richtet keinen Schaden an - die Prüfung kann jederzeit neu gestartet werden. " +
                    "Eine bereits begonnene Reparatur setzt Windows beim nächsten Durchlauf fort.",
                    "Cancelling doesn't cause any harm - the check can be restarted anytime. " +
                    "Windows continues an already-started repair on the next run.")), roh);
        }
        catch (Exception ex)
        {
            string roh;
            lock (gesammelt) roh = gesammelt.ToString();
            return (new PruefErgebnis(PruefBefund.NichtGelaufen,
                Loc.T($"Die Prüfung konnte nicht gestartet werden: {ex.Message}", $"The check could not be started: {ex.Message}"),
                Loc.T(
                    "Das deutet auf ein beschädigtes Windows hin, wenn sfc.exe oder dism.exe fehlen. " +
                    "Dann hilft nur noch eine Reparatur-Installation über das offizielle Windows-Installationsmedium.",
                    "This suggests a damaged Windows if sfc.exe or dism.exe are missing. " +
                    "In that case, only a repair installation via the official Windows installation media helps.")), roh);
        }
    }
}
