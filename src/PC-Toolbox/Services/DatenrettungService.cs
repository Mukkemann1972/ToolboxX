using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using PCToolboxCore;

namespace PCToolbox.Services;

public record RettungsDiskInfo(int Nummer, string Name, long GroesseBytes, string BusTyp, IReadOnlyList<char>? Laufwerksbuchstaben = null);

/// <summary>NeueFunde: nur die in diesem einzelnen Fortschritts-Schritt neu entdeckten Dateien
/// (meist leer), nicht alle bisherigen - damit die Oberfläche sie live einzeln in die Liste
/// einfügen kann, statt am Ende alles auf einmal zu zeigen.</summary>
public record RettungsScanFortschritt(long GelesenBytes, long GesamtBytes, int GefundenBisher, IReadOnlyList<GefundeneDatei>? NeueFunde = null)
{
    public double Prozent => GesamtBytes > 0 ? Math.Min(100.0, GelesenBytes * 100.0 / GesamtBytes) : 0;
}

public record RettungsScanErgebnis(bool Erfolgreich, IReadOnlyList<GefundeneDatei> Funde, string? Fehlermeldung);

public record RettenFortschritt(int Fertig, int Gesamt)
{
    public double Prozent => Gesamt > 0 ? Math.Min(100.0, Fertig * 100.0 / Gesamt) : 0;
}

/// <summary>FehlgeschlageneIndizes: welche der übergebenen Funde (nach Position in der Liste)
/// beim Retten selbst fehlgeschlagen sind - damit die Oberfläche genau diese Zeilen rot markieren
/// kann, statt nur eine Gesamtzahl zu kennen.</summary>
public record RettenErgebnis(bool Erfolgreich, int Gerettet, int Fehlgeschlagen, string? Fehlermeldung, IReadOnlyList<int>? FehlgeschlageneIndizes = null);

/// <summary>
/// Datenrettung (Free): findet Dateien anhand von Signaturen (s. PCToolboxCore.Dateiwiederherstellung/
/// CarvingScanner) auf einer ganzen physischen Platte, unabhängig vom Dateisystem - funktioniert
/// also auch bei gelöschten Dateien oder einer formatierten/RAW-Platte. Rein lesend gegenüber der
/// Quelle, wie bei toolstar®recoveryPRO (Schreibschutz-Prinzip): nichts auf der Quelle wird je
/// verändert, gefundene Dateien landen ausschließlich im gewählten Zielordner.
///
/// Läuft in zwei elevierten Durchgängen statt einem, bewusst (s. Kommentar in RettungsSkript/
/// ExtraktionsSkript): die Carving-Engine ist .NET 8, Windows PowerShell 5.1 kann diese DLL nicht
/// laden (dieselbe Einschränkung wie beim Rettungsmodus-Klonen), und die Engine ist zu komplex, um
/// sie ein zweites Mal fehleranfällig in PowerShell nachzubauen. Deshalb: ein schlankes elevated
/// Skript liefert nur rohe Bytes über eine Named Pipe an den eigenen, NICHT elevierten Prozess
/// (dort läuft die echte, getestete Carving-Engine), ein zweites elevated Skript liest anschließend
/// gezielt nur die gefundenen Bereiche und schreibt sie in den Zielordner. Kostet zwei
/// UAC-Abfragen statt einer - ehrlicher, einfacher Kompromiss für V1.
/// </summary>
public class DatenrettungService
{
    private const string DisksTrenner = "<<<TOOLBOXX-RETTUNG-DISKS-TRENNER>>>";

    public async Task<IReadOnlyList<RettungsDiskInfo>> DisksLadenAsync()
    {
        var ausgabe = await PowerShellLesenAsync(
            "Get-Disk | Select-Object Number,FriendlyName,Size,BusType | ConvertTo-Json -Compress; " +
            $"Write-Output '{DisksTrenner}'; " +
            LaufwerksbuchstabenProPlatte.Abfragebefehl);

        var teile = ausgabe.Split(DisksTrenner, 2, StringSplitOptions.None);
        var buchstaben = teile.Length == 2 ? LaufwerksbuchstabenProPlatte.Lesen(teile[1]) : new Dictionary<int, IReadOnlyList<char>>();

        return DisksAusJson(teile[0]).Select(d => d with { Laufwerksbuchstaben = buchstaben.GetValueOrDefault(d.Nummer) }).ToList();
    }

    /// <summary>Verweigert Zielordner, die auf derselben physischen Platte wie die Quelle liegen -
    /// sonst könnte das Schreiben gefundener Dateien Bereiche überschreiben, die der Scan noch gar
    /// nicht erreicht hat. Nur lokale, laufwerksbuchstaben-basierte Ziele werden unterstützt (kein
    /// Netzwerkpfad) - bei allem anderen lieber ehrlich ablehnen als raten.</summary>
    public async Task<(bool Ok, string Begruendung)> ZielPruefenAsync(string zielOrdner, int quellDiskNummer)
    {
        var wurzel = Path.GetPathRoot(Path.GetFullPath(zielOrdner));
        if (string.IsNullOrEmpty(wurzel) || wurzel.Length < 2 || wurzel[1] != ':')
            return (false, Loc.T(
                "Der Zielordner muss auf einem lokalen Laufwerk mit Laufwerksbuchstaben liegen (kein Netzwerkpfad) - " +
                "sonst kann nicht sicher geprüft werden, ob er auf derselben Platte wie die Quelle liegt.",
                "The destination folder must be on a local drive with a drive letter (no network path) - " +
                "otherwise it can't be reliably checked whether it's on the same disk as the source."));

        var buchstabe = wurzel[0];
        var ausgabe = await PowerShellLesenAsync(
            $"Get-Partition -DriveLetter {buchstabe} | Select-Object DiskNumber | ConvertTo-Json -Compress");

        int? zielDiskNummer = null;
        try
        {
            using var dokument = JsonDocument.Parse(string.IsNullOrWhiteSpace(ausgabe) ? "{}" : ausgabe);
            if (dokument.RootElement.TryGetProperty("DiskNumber", out var dn) && dn.TryGetInt32(out var wert))
                zielDiskNummer = wert;
        }
        catch (JsonException) { /* zielDiskNummer bleibt null, unten sauber abgefangen */ }

        if (zielDiskNummer is null)
            return (false, Loc.T(
                $"Konnte nicht ermitteln, auf welcher Platte Laufwerk {buchstabe}: liegt.",
                $"Could not determine which disk drive {buchstabe}: is on."));

        if (zielDiskNummer == quellDiskNummer)
            return (false, Loc.T(
                "Der Zielordner liegt auf derselben Platte wie die Quelle - das würde beim Speichern gefundener " +
                "Dateien möglicherweise noch nicht gescannte Bereiche überschreiben. Bitte ein anderes Laufwerk wählen.",
                "The destination folder is on the same disk as the source - saving found files there could " +
                "overwrite areas that haven't been scanned yet. Please choose a different drive."));

        return (true, "");
    }

    public async Task<RettungsScanErgebnis> ScanAsync(
        RettungsDiskInfo quelle, IReadOnlyList<DateiTyp> typen, IProgress<RettungsScanFortschritt>? fortschritt, CancellationToken abbruch)
    {
        var signaturen = Dateiwiederherstellung.BekannteSignaturen.Where(s => typen.Contains(s.Typ)).ToList();
        if (signaturen.Count == 0)
            return new RettungsScanErgebnis(false, [], Loc.T("Keine Dateiart ausgewählt.", "No file type selected."));

        var pipeName = "ToolboxX-Rettung-" + Guid.NewGuid().ToString("N");
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var arbeitsOrdner = Path.Combine(Path.GetTempPath(), "ToolboxX-Rettung-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(arbeitsOrdner);
        var skriptDatei = Path.Combine(arbeitsOrdner, "scan.ps1");
        File.WriteAllText(skriptDatei, ScanSkript);

        var scanner = new CarvingScanner(signaturen);
        var funde = new List<GefundeneDatei>();

        Process? prozess = null;
        try
        {
            var start = new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " +
                            $"{AlsCliArgument(skriptDatei)} -Nummer {quelle.Nummer} -PipeName {AlsCliArgument(pipeName)}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            prozess = Process.Start(start);
            if (prozess is null)
                return new RettungsScanErgebnis(false, [], Loc.T(
                    "Der Scan konnte nicht gestartet werden.", "The scan could not be started."));

            using var verbindungsAbbruch = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
            verbindungsAbbruch.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await pipe.WaitForConnectionAsync(verbindungsAbbruch.Token);
            }
            catch (OperationCanceledException) when (!abbruch.IsCancellationRequested)
            {
                return new RettungsScanErgebnis(false, [], Loc.T(
                    "Keine Verbindung zum elevierten Lesevorgang zustande gekommen (Sicherheitsabfrage abgelehnt oder Zeitüberschreitung).",
                    "Could not connect to the elevated read process (security prompt declined, or timed out)."));
            }

            var puffer = new byte[4 * 1024 * 1024];
            long gelesen = 0;

            while (true)
            {
                abbruch.ThrowIfCancellationRequested();
                int n;
                try
                {
                    n = await pipe.ReadAsync(puffer, abbruch);
                }
                catch (IOException)
                {
                    break; // Gegenseite (elevated Skript) hat die Pipe geschlossen - Platte fertig gelesen.
                }
                if (n == 0) break;

                var block = n == puffer.Length ? puffer : puffer[..n];
                var neueFunde = scanner.NaechsterBlock(block);
                funde.AddRange(neueFunde);
                gelesen += n;
                fortschritt?.Report(new RettungsScanFortschritt(gelesen, quelle.GroesseBytes, funde.Count, neueFunde));
            }

            return new RettungsScanErgebnis(true, funde, null);
        }
        catch (OperationCanceledException)
        {
            // Abbruch behaelt, was bis dahin gefunden wurde, statt es wegzuwerfen - vorher gingen
            // bei "Abbrechen" alle bereits gefundenen Dateien verloren, obwohl sie schon da waren
            // (Nutzerwunsch: wie bei GetDataBack abbrechen und trotzdem sehen koennen, was bis dahin
            // gefunden wurde). Erfolgreich bleibt true, weil die Funde verwendbar sind - die
            // Oberflaeche unterscheidet ueber ihr eigenes Abbruch-Flag, ob sie "fertig" oder
            // "abgebrochen" anzeigt.
            return new RettungsScanErgebnis(true, funde, null);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new RettungsScanErgebnis(false, [], Loc.T(
                "Ohne die Windows-Bestätigung (Administratorrechte) geht das nicht.",
                "This isn't possible without the Windows confirmation (administrator rights)."));
        }
        finally
        {
            try { prozess?.Kill(); } catch (InvalidOperationException) { }
            try { Directory.Delete(arbeitsOrdner, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public async Task<RettenErgebnis> RettenAsync(
        RettungsDiskInfo quelle, IReadOnlyList<GefundeneDatei> funde, string zielOrdner,
        IProgress<RettenFortschritt>? fortschritt, CancellationToken abbruch)
    {
        var (ok, begruendung) = await ZielPruefenAsync(zielOrdner, quelle.Nummer);
        if (!ok) return new RettenErgebnis(false, 0, 0, begruendung);

        Directory.CreateDirectory(zielOrdner);

        var arbeitsOrdner = Path.Combine(Path.GetTempPath(), "ToolboxX-Retten-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(arbeitsOrdner);
        var skriptDatei = Path.Combine(arbeitsOrdner, "retten.ps1");
        var statusDatei = Path.Combine(arbeitsOrdner, "status.json");
        var fundeDatei = Path.Combine(arbeitsOrdner, "funde.json");
        File.WriteAllText(skriptDatei, ExtraktionsSkript);

        var fundeExport = funde.Select((f, i) => new { index = i, start = f.StartOffset, laenge = f.Laenge, endung = f.Endung }).ToList();
        File.WriteAllText(fundeDatei, JsonSerializer.Serialize(fundeExport));

        try
        {
            var start = new ProcessStartInfo("powershell.exe")
            {
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " +
                            $"{AlsCliArgument(skriptDatei)} -Nummer {quelle.Nummer} -FundeDatei {AlsCliArgument(fundeDatei)} " +
                            $"-ZielOrdner {AlsCliArgument(zielOrdner)} -StatusDatei {AlsCliArgument(statusDatei)}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var prozess = Process.Start(start);
            if (prozess is null)
                return new RettenErgebnis(false, 0, 0, Loc.T(
                    "Die Rettung konnte nicht gestartet werden.", "The recovery could not be started."));

            while (!prozess.HasExited)
            {
                if (abbruch.IsCancellationRequested)
                {
                    try { prozess.Kill(); } catch (InvalidOperationException) { }
                    return new RettenErgebnis(false, 0, 0, Loc.T(
                        "Abgebrochen - bereits gerettete Dateien bleiben im Zielordner erhalten.",
                        "Cancelled - files already recovered remain in the destination folder."));
                }

                StatusLesenUndMelden(statusDatei, fortschritt);
                await Task.Delay(300, CancellationToken.None);
            }

            var status = StatusLesenUndMelden(statusDatei, fortschritt);
            if (status is null)
            {
                // Kein "abgeschlossen"-Status vorhanden, obwohl der Prozess beendet ist - haeufigste
                // ehrliche Ursache waere UAC-Ablehnung, aber dann wuerde Process.Start schon eine
                // Win32Exception werfen (s. catch unten), kaeme also gar nicht bis hierher. Landet der
                // Code TROTZDEM hier, ist die Erklaerung ungenau (deckt auch andere Fehler zwischen
                // erfolgreichem Start und Skriptende ab) - der Exit-Code ist die einzige greifbare
                // Spur dafuer, was tatsaechlich passiert ist.
                DiagnoseLog.Schreiben(nameof(DatenrettungService),
                    $"RettenAsync: kein Status nach Prozessende. ExitCode={prozess.ExitCode}, " +
                    $"StatusDatei vorhanden={File.Exists(statusDatei)} (vorhanden+unvollstaendig = Skript ist " +
                    "gestartet, aber mittendrin abgebrochen; nicht vorhanden = Skript kam nicht mal bis zum " +
                    "ersten eigenen Codepunkt).");
                return new RettenErgebnis(false, 0, 0, Loc.T(
                    "Der Rettungsvorgang wurde beendet, ohne ein Ergebnis zu melden - das deutet auf einen " +
                    "Fehler im Hintergrund hin, nicht zwingend auf eine abgelehnte Sicherheitsabfrage. " +
                    "Details stehen im Diagnose-Log.",
                    "The recovery process ended without reporting a result - this points to a background " +
                    "error, not necessarily a declined security prompt. Details are in the diagnostic log."));
            }

            if (status.Value.Fehler is { } fehlertext)
                return new RettenErgebnis(false, status.Value.Gerettet, status.Value.Fehlgeschlagen, fehlertext, status.Value.FehlgeschlageneIndizes);

            return new RettenErgebnis(true, status.Value.Gerettet, status.Value.Fehlgeschlagen, null, status.Value.FehlgeschlageneIndizes);
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            DiagnoseLog.Schreiben(nameof(DatenrettungService), $"RettenAsync: Win32Exception beim Start. {e}");
            return new RettenErgebnis(false, 0, 0, Loc.T(
                "Ohne die Windows-Bestätigung (Administratorrechte) geht das nicht. Es wurde nichts gerettet.",
                "This isn't possible without the Windows confirmation (administrator rights). Nothing was recovered."));
        }
        finally
        {
            try { Directory.Delete(arbeitsOrdner, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static (int Gerettet, int Fehlgeschlagen, string? Fehler, IReadOnlyList<int> FehlgeschlageneIndizes)? StatusLesenUndMelden(string statusDatei, IProgress<RettenFortschritt>? fortschritt)
    {
        if (!File.Exists(statusDatei)) return null;

        string inhalt;
        try
        {
            using var stream = new FileStream(statusDatei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var leser = new StreamReader(stream);
            inhalt = leser.ReadToEnd();
        }
        catch (IOException) { return null; }
        if (string.IsNullOrWhiteSpace(inhalt)) return null;

        try
        {
            using var dokument = JsonDocument.Parse(inhalt);
            var wurzel = dokument.RootElement;
            var fertig = wurzel.TryGetProperty("fertig", out var f) ? f.GetInt32() : 0;
            var gesamt = wurzel.TryGetProperty("gesamt", out var g) ? g.GetInt32() : 0;
            var abgeschlossen = wurzel.TryGetProperty("abgeschlossen", out var a) && a.ValueKind == JsonValueKind.True;

            fortschritt?.Report(new RettenFortschritt(fertig, gesamt));

            if (!abgeschlossen) return null;

            var gerettet = wurzel.TryGetProperty("gerettet", out var ge) ? ge.GetInt32() : 0;
            var fehlgeschlagen = wurzel.TryGetProperty("fehlgeschlagen", out var fs) ? fs.GetInt32() : 0;
            var fehler = wurzel.TryGetProperty("fehler", out var fe) && fe.ValueKind == JsonValueKind.String ? fe.GetString() : null;

            var fehlgeschlageneIndizes = new List<int>();
            if (wurzel.TryGetProperty("fehlgeschlageneIndizes", out var fi) && fi.ValueKind == JsonValueKind.Array)
                foreach (var el in fi.EnumerateArray())
                    if (el.TryGetInt32(out var idx)) fehlgeschlageneIndizes.Add(idx);

            return (gerettet, fehlgeschlagen, string.IsNullOrEmpty(fehler) ? null : fehler, fehlgeschlageneIndizes);
        }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<RettungsDiskInfo> DisksAusJson(string ausgabe)
    {
        var ergebnis = new List<RettungsDiskInfo>();
        foreach (var element in AlsElemente(ausgabe))
        {
            if (!element.TryGetProperty("Number", out var nummerEl) || !nummerEl.TryGetInt32(out var nummer)) continue;

            ergebnis.Add(new RettungsDiskInfo(
                nummer,
                element.TryGetProperty("FriendlyName", out var n) ? n.GetString() ?? "" : "",
                element.TryGetProperty("Size", out var s) && s.TryGetInt64(out var size) ? size : 0,
                element.TryGetProperty("BusType", out var bt) ? bt.GetString() ?? "" : ""));
        }
        return ergebnis;
    }

    /// <summary>
    /// Quotiert einen Pfad fuer die Kommandozeile. Ein einzelner Backslash direkt vor dem
    /// schliessenden Anfuehrungszeichen wird von Windows' Kommandozeilen-Parsing als "escaptes"
    /// Anfuehrungszeichen gelesen statt als Textende - bei einem Laufwerks-Wurzelpfad wie "H:\"
    /// als Zielordner (kein Unterordner, endet also auf genau einen Backslash) hat das live dazu
    /// gefuehrt, dass das naechste Kommandozeilen-Argument mit verschluckt wurde und das gesamte
    /// elevierte Skript lautlos abgebrochen ist, noch bevor es die erste eigene Zeile ausfuehren
    /// konnte. Ein zweiter Backslash vor dem Anfuehrungszeichen behebt das (gerade Anzahl
    /// Backslashes vor einem Anfuehrungszeichen wird als "N/2 Backslashes, Zeichen schliesst"
    /// gelesen - s. Windows-Regeln fuer CommandLineToArgvW).
    /// </summary>
    private static string AlsCliArgument(string pfad) =>
        "\"" + (pfad.EndsWith('\\') ? pfad + "\\" : pfad) + "\"";

    private static IEnumerable<JsonElement> AlsElemente(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;

        JsonDocument dokument;
        try { dokument = JsonDocument.Parse(json); }
        catch (JsonException) { yield break; }

        using (dokument)
        {
            var wurzel = dokument.RootElement;
            if (wurzel.ValueKind == JsonValueKind.Object) yield return wurzel;
            else if (wurzel.ValueKind == JsonValueKind.Array)
                foreach (var element in wurzel.EnumerateArray()) yield return element;
        }
    }

    private static async Task<string> PowerShellLesenAsync(string befehl)
    {
        try
        {
            var start = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                $"\"$ErrorActionPreference = 'Stop'; try {{ {befehl} }} catch {{ Write-Error $_; exit 1 }}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var prozess = Process.Start(start);
            if (prozess is null) return "";

            var ausgabe = await prozess.StandardOutput.ReadToEndAsync();
            await prozess.WaitForExitAsync();
            return ausgabe;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "";
        }
    }

    /// <summary>
    /// Läuft eleviert. Absichtlich SEHR schlank: liest die Platte nur roh und sequenziell und
    /// schickt die Bytes unverändert über eine Named Pipe an den eigenen, nicht elevierten
    /// Prozess - keine Carving-Logik hier drin (s. Klassenkommentar für den Grund). Oeffne-Platte
    /// ist dieselbe kleine P/Invoke-Funktion wie in WischSkript/KlonSkript, bewusst dupliziert statt
    /// gemeinsam genutzt (ein PowerShell-Skript kann keine Funktionen aus einer anderen
    /// eingebetteten Skript-Konstante importieren).
    /// </summary>
    private const string ScanSkript = """
        param(
            [int]$Nummer,
            [string]$PipeName
        )
        $ErrorActionPreference = 'Stop'

        Add-Type -Namespace ToolboxX -Name Win32Datei -MemberDefinition @'
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        '@

        function Oeffne-Platte([int]$nummer) {
            $zugriff = [uint32]2147483648
            $handle = [ToolboxX.Win32Datei]::CreateFile("\\.\PhysicalDrive$nummer", $zugriff, 3, [IntPtr]::Zero, 3, 0x80, [IntPtr]::Zero)
            if ($handle.IsInvalid) {
                $fehlercode = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
                throw "Konnte PhysicalDrive$nummer nicht oeffnen (Windows-Fehlercode $fehlercode)."
            }
            return New-Object System.IO.FileStream($handle, [System.IO.FileAccess]::Read, 1)
        }

        try {
            $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $PipeName, [System.IO.Pipes.PipeDirection]::Out)
            $pipe.Connect(15000)

            $quelle = Oeffne-Platte $Nummer
            $blockGroesse = 4MB
            $puffer = New-Object byte[] $blockGroesse

            while ($true) {
                $gelesen = $quelle.Read($puffer, 0, $blockGroesse)
                if ($gelesen -le 0) { break }
                $pipe.Write($puffer, 0, $gelesen)
                $pipe.Flush()
            }
        }
        catch {
            # Abbruch/Verbindungsende auf der Gegenseite ist der normale Weg, den Scan zu beenden -
            # kein Fehlerprotokoll noetig, das Ergebnis (gefundene Dateien) hat der andere Prozess schon.
        }
        finally {
            if ($quelle) { $quelle.Dispose() }
            if ($pipe) { $pipe.Dispose() }
        }
        """;

    /// <summary>
    /// Läuft eleviert. Bekommt die im Scan-Durchgang gefundenen Bereiche als Liste, springt gezielt
    /// zu jedem und schreibt ihn als eigene Datei in den Zielordner - liest dabei NUR diese
    /// Bereiche, nicht die ganze Platte nochmal.
    /// </summary>
    private const string ExtraktionsSkript = """
        param(
            [int]$Nummer,
            [string]$FundeDatei,
            [string]$ZielOrdner,
            [string]$StatusDatei
        )
        $ErrorActionPreference = 'Stop'

        # Schreibe-Status muss als Erstes und ausserhalb jeder Gefahrenzone definiert UND einmal
        # aufgerufen werden - ein frueher "gestartet"-Status beweist, dass das Skript ueberhaupt
        # bis hierher gekommen ist. Schlug bisher alles vor dem ersten Schreibversuch fehl (Add-Type,
        # Funktionsdefinitionen, Parameterbindung), gab es NIE eine Status-Datei, und die Toolbox
        # wartete auf ein Ergebnis, das nie kommen konnte (live so aufgetreten: Exit-Code 0, aber
        # kein Status - das Skript kam nicht mal bis zum ersten eigenen Codepunkt).
        function Schreibe-Status([int]$fertig, [int]$gesamt, [bool]$abgeschlossen, $gerettet, $fehlgeschlagen, [string]$fehler = "", $fehlgeschlageneIndizes = @()) {
            $obj = [ordered]@{ fertig = $fertig; gesamt = $gesamt; abgeschlossen = $abgeschlossen; gerettet = $gerettet; fehlgeschlagen = $fehlgeschlagen; fehler = $fehler; fehlgeschlageneIndizes = @($fehlgeschlageneIndizes) }
            $tmp = "$StatusDatei.tmp"
            $obj | ConvertTo-Json -Compress | Set-Content -Path $tmp -Encoding UTF8
            Move-Item -Path $tmp -Destination $StatusDatei -Force
        }

        $gerettet = 0
        $fehlgeschlagen = 0
        $gesamtAnzahl = 0
        $fehlgeschlageneIndizes = New-Object System.Collections.Generic.List[int]
        Schreibe-Status 0 0 $false 0 0

        try {
            Add-Type -Namespace ToolboxX -Name Win32Datei -MemberDefinition @'
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
                string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
                uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        '@

            function Oeffne-Platte([int]$nummer) {
                $zugriff = [uint32]2147483648
                $handle = [ToolboxX.Win32Datei]::CreateFile("\\.\PhysicalDrive$nummer", $zugriff, 3, [IntPtr]::Zero, 3, 0x80, [IntPtr]::Zero)
                if ($handle.IsInvalid) {
                    $fehlercode = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
                    throw "Konnte PhysicalDrive$nummer nicht oeffnen (Windows-Fehlercode $fehlercode)."
                }
                return New-Object System.IO.FileStream($handle, [System.IO.FileAccess]::Read, 1)
            }

            # Rohe Platten (\\.\PhysicalDriveN) lassen nur an Sektorgrenzen (hier: 512 Byte, der
            # ueberall garantierte logische Sektorwert, auch bei 4Kn-Platten) lesen/springen - ein
            # Fund-Offset mitten in einem Sektor (der Normalfall) wuerde beim direkten Seek+Read sonst
            # stillschweigend 0 Bytes liefern statt eines Fehlers, und es wurde trotzdem eine (leere)
            # Datei geschrieben. Deshalb: auf die Sektorgrenze davor springen, etwas mehr lesen, und
            # den gewuenschten Bereich danach im Speicher herausschneiden.
            $sektorGroesse = 512

            # KEIN @()-Wrapper - s. FestplattenWischService-Kommentar zum selben Thema.
            $funde = Get-Content $FundeDatei -Raw | ConvertFrom-Json
            $gesamtAnzahl = $funde.Count

            $quelle = Oeffne-Platte $Nummer
            for ($i = 0; $i -lt $funde.Count; $i++) {
                $fund = $funde[$i]
                try {
                    $alignStart = [long]([math]::Floor([double]$fund.start / $sektorGroesse) * $sektorGroesse)
                    $vorlauf = [long]($fund.start - $alignStart)
                    $leseLaenge = [long]([math]::Ceiling(($vorlauf + $fund.laenge) / [double]$sektorGroesse) * $sektorGroesse)

                    $quelle.Seek($alignStart, [System.IO.SeekOrigin]::Begin) | Out-Null
                    $lesePuffer = New-Object byte[] $leseLaenge
                    $gelesenGesamt = 0
                    while ($gelesenGesamt -lt $lesePuffer.Length) {
                        $n = $quelle.Read($lesePuffer, $gelesenGesamt, $lesePuffer.Length - $gelesenGesamt)
                        if ($n -le 0) { break }
                        $gelesenGesamt += $n
                    }

                    if ($gelesenGesamt -lt ($vorlauf + $fund.laenge)) {
                        throw "Nur $gelesenGesamt von $leseLaenge Byte gelesen."
                    }

                    $ausgabe = New-Object byte[] ([long]$fund.laenge)
                    [System.Array]::Copy($lesePuffer, $vorlauf, $ausgabe, 0, [long]$fund.laenge)

                    $zielDatei = Join-Path $ZielOrdner ("wiederhergestellt-{0:D4}.{1}" -f ($i + 1), $fund.endung)
                    [System.IO.File]::WriteAllBytes($zielDatei, $ausgabe)
                    $gerettet++
                }
                catch {
                    $fehlgeschlagen++
                    $fehlgeschlageneIndizes.Add($i)
                }
                Schreibe-Status ($i + 1) $funde.Count $false $gerettet $fehlgeschlagen "" $fehlgeschlageneIndizes
            }
            Schreibe-Status $funde.Count $funde.Count $true $gerettet $fehlgeschlagen "" $fehlgeschlageneIndizes
        }
        catch {
            $meldung = $_.Exception.Message
            try {
                Schreibe-Status ($gerettet + $fehlgeschlagen) $gesamtAnzahl $true $gerettet $fehlgeschlagen "Unerwarteter Fehler: $meldung"
            }
            catch {
                # Selbst das Schreiben der Status-Datei ist fehlgeschlagen (z.B. Zielordner nicht
                # erreichbar) - dann bleibt nur ein von 0 verschiedener Exit-Code als letztes Signal,
                # damit die Toolbox-Seite wenigstens WEISS, dass etwas schiefging.
                exit 1
            }
        }
        finally {
            if ($quelle) { $quelle.Dispose() }
        }
        """;
}
