namespace PCToolboxCore;

/// <summary>
/// Schlankes Diagnose-Log fuer die Testphase: schreibt Fehler/wichtige Ereignisse
/// in eine kleine Textdatei unter %AppData%\ToolboxX\logs\toolboxx.log, damit
/// Tester sie bei einem Problem einfach an Alex weitergeben koennen - statt den
/// Fehlertext nur einmalig in einer MessageBox zu sehen, die danach weg ist.
/// Bewusst kein Logging-Framework: eine einzelne Datei, automatisch begrenzte
/// Groesse, keine Rotation ueber mehrere Dateien - fuer die Testphase reicht das.
/// </summary>
public static class DiagnoseLog
{
    private const long MaxBytes = 512 * 1024;

    public static string DefaultPfad => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolboxX", "logs", "toolboxx.log");

    public static void Schreiben(string quelle, string nachricht, string? pfad = null)
    {
        pfad ??= DefaultPfad;

        try
        {
            var verzeichnis = Path.GetDirectoryName(pfad);
            if (!string.IsNullOrEmpty(verzeichnis))
                Directory.CreateDirectory(verzeichnis);

            KuerzenFallsZuGross(pfad);

            var zeile = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{quelle}] {nachricht}{Environment.NewLine}";
            File.AppendAllText(pfad, zeile);
        }
        catch
        {
            // Ein fehlgeschlagenes Log darf die App nicht zusaetzlich zum Absturz bringen.
        }
    }

    /// <summary>
    /// Haelt die Datei klein: statt endlos zu wachsen, wird beim Ueberschreiten der
    /// Grenze nur die juengere Haelfte behalten. Eine echte Rotation ueber mehrere
    /// Dateien waere fuer eine reine Testphasen-Hilfe Overkill.
    /// </summary>
    private static void KuerzenFallsZuGross(string pfad)
    {
        if (!File.Exists(pfad)) return;
        if (new FileInfo(pfad).Length <= MaxBytes) return;

        var text = File.ReadAllText(pfad);
        var behalten = text[(text.Length / 2)..];
        var ersterZeilenumbruch = behalten.IndexOf('\n');
        if (ersterZeilenumbruch >= 0) behalten = behalten[(ersterZeilenumbruch + 1)..];

        File.WriteAllText(pfad, $"[... ältere Einträge gekürzt ...]{Environment.NewLine}{behalten}");
    }
}
