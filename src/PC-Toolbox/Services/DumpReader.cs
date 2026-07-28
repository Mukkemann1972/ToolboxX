using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text.RegularExpressions;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest die letzten Bluescreens primär über den admin-freien Weg: das Windows-Ereignisprotokoll,
/// Provider "Microsoft-Windows-WER-SystemErrorReporting", Event-ID 1001 ("Der Computer wurde nach
/// einem schwerwiegenden Fehler neu gestartet"). Die Minidump-Dateien in C:\Windows\Minidump
/// werden zusätzlich nur als Zeitstempel-Liste gezeigt (falls das Event-Log leer/deaktiviert ist).
///
/// Seit dem Minidump-Reader-Ausbau (28.07.2026, Rang 1 aus der NirSoft-Recherche, Vorbild
/// BlueScreenView): zusätzlich Best-Effort-Versuch, den wahrscheinlich verursachenden Treiber zu
/// nennen, statt nur "steht im Event-Detail" zu sagen. Ansatz bewusst konservativ, um keine
/// Symbol-Aufloesung (das eigentliche WinDbg-Fachgebiet) nachzubauen:
///   1. Die vier Bugcheck-Parameter (meist Adressen) aus derselben Event-1001-XML lesen, die
///      schon den Bugcheck-Code liefert.
///   2. Die zum Absturz-Zeitpunkt passende .dmp-Datei in C:\Windows\Minidump finden und ihre
///      Modul-Liste lesen (reines Datei-Format-Parsen, s. PC-ToolboxCore.MinidumpModuleListParser
///      - dort auch mit synthetischen Testdaten geprüft, aber NICHT gegen einen echten
///      Windows-Absturz verifiziert, da auf diesem Rechner beim Bauen kein echter Crash-Dump
///      vorlag. Bewusst als "wahrscheinlich" formuliert statt als sichere Diagnose.).
///   3. Ersten Bugcheck-Parameter, der in den Adressbereich eines geladenen Moduls fällt, als
///      Verursacher zeigen. Klappt das nicht (kein Treffer, keine passende Datei, Datei
///      unlesbar), bleibt der bisherige ehrliche Text unverändert - nie eine Vermutung erzwingen.
/// </summary>
public class DumpReader
{
    private static readonly Dictionary<string, (string Name, string Loesung)> BekannteCodes = new()
    {
        ["0x0000001a"] = ("MEMORY_MANAGEMENT", Loc.T(
            "RAM prüfen (z.B. MemTest86 laufen lassen), ggf. einzelne RAM-Riegel einzeln testen.",
            "Check RAM (e.g. run MemTest86), if necessary test individual RAM modules separately.")),
        ["0x0000003b"] = ("SYSTEM_SERVICE_EXCEPTION", Loc.T(
            "Meist ein Treiberproblem – Grafiktreiber (oder den im Dump genannten Treiber) neu installieren.",
            "Usually a driver problem – reinstall the graphics driver (or the driver named in the dump).")),
        ["0x0000007e"] = ("SYSTEM_THREAD_EXCEPTION_NOT_HANDLED", Loc.T(
            "Verursacher-Treiber steht im Event-Detail – diesen Treiber aktualisieren/neu installieren.",
            "The causing driver is in the event detail – update/reinstall that driver.")),
        ["0x0000000a"] = ("IRQL_NOT_LESS_OR_EQUAL", Loc.T(
            "Häufig Treiber- oder RAM-Problem – Treiber aktuell halten, RAM testen.",
            "Often a driver or RAM problem – keep drivers up to date, test RAM.")),
        ["0x00000133"] = ("DPC_WATCHDOG_VIOLATION", Loc.T(
            "Oft SSD-Firmware oder Netzwerk-/Audiotreiber – Firmware & Treiber aktualisieren.",
            "Often SSD firmware or network/audio drivers – update firmware & drivers.")),
        ["0x00000124"] = ("WHEA_UNCORRECTABLE_ERROR", Loc.T(
            "Hardware-Fehler (CPU/RAM/Mainboard) – Temperaturen und RAM prüfen, BIOS aktualisieren.",
            "Hardware error (CPU/RAM/mainboard) – check temperatures and RAM, update BIOS.")),
    };

    public async Task<List<BluescreenEntry>> GetLetzteBluescreensAsync(int anzahl = 5)
    {
        return await Task.Run(() =>
        {
            var ergebnisse = new List<BluescreenEntry>();

            try
            {
                var query = new EventLogQuery("System", PathType.LogName,
                    "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and (EventID=1001)]]")
                {
                    ReverseDirection = true
                };

                using var reader = new EventLogReader(query);
                EventRecord? record;
                while (ergebnisse.Count < anzahl && (record = reader.ReadEvent()) != null)
                {
                    using (record)
                    {
                        var eintrag = ParseEreignis(record);
                        if (eintrag != null)
                            ergebnisse.Add(eintrag);
                    }
                }
            }
            catch (Exception ex)
            {
                ergebnisse.Add(new BluescreenEntry
                {
                    Zeitpunkt = DateTime.Now,
                    BugCheckCode = "-",
                    BugCheckName = Loc.T("Event-Log nicht lesbar", "Event log not readable"),
                    Loesungsvorschlag = Loc.T($"Fehler: {ex.Message} → Als Administrator starten, oder Minidump-Ordner direkt prüfen.",
                                              $"Error: {ex.Message} → Start as administrator, or check the minidump folder directly."),
                    Quelle = Loc.T("Fehler", "Error")
                });
            }

            if (ergebnisse.Count == 0)
            {
                // Fallback: nur Zeitstempel der Minidump-Dateien, kein Code-Parsing in V0.01
                try
                {
                    var minidumpPfad = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows", "Minidump");
                    if (Directory.Exists(minidumpPfad))
                    {
                        var dateien = Directory.GetFiles(minidumpPfad, "*.dmp")
                            .OrderByDescending(File.GetLastWriteTime)
                            .Take(anzahl);

                        foreach (var datei in dateien)
                        {
                            ergebnisse.Add(new BluescreenEntry
                            {
                                Zeitpunkt = File.GetLastWriteTime(datei),
                                BugCheckCode = Loc.T("unbekannt", "unknown"),
                                BugCheckName = Path.GetFileName(datei),
                                Loesungsvorschlag = Loc.T(
                                    "Für den genauen Fehlercode: Datei mit WinDbg öffnen (kostenlos, Microsoft Store) - ein eigener Minidump-Parser ist geplant, noch nicht umgesetzt.",
                                    "For the exact error code: open the file with WinDbg (free, Microsoft Store) - a built-in minidump parser is planned but not yet implemented."),
                                Quelle = Loc.T("Minidump-Datei (nur Zeitstempel)", "Minidump file (timestamp only)")
                            });
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    ergebnisse.Add(new BluescreenEntry
                    {
                        Zeitpunkt = DateTime.Now,
                        BugCheckCode = "-",
                        BugCheckName = Loc.T("Minidump-Ordner nicht lesbar", "Minidump folder not readable"),
                        Loesungsvorschlag = Loc.T(
                            "Zugriff auf C:\\Windows\\Minidump verweigert → PC-Toolbox als Administrator starten, um Minidump-Dateien zu sehen.",
                            "Access to C:\\Windows\\Minidump denied → start PC-Toolbox as administrator to see minidump files."),
                        Quelle = Loc.T("Fehler", "Error")
                    });
                }
                catch (Exception ex)
                {
                    ergebnisse.Add(new BluescreenEntry
                    {
                        Zeitpunkt = DateTime.Now,
                        BugCheckCode = "-",
                        BugCheckName = Loc.T("Minidump-Ordner nicht lesbar", "Minidump folder not readable"),
                        Loesungsvorschlag = Loc.T($"Fehler: {ex.Message}", $"Error: {ex.Message}"),
                        Quelle = Loc.T("Fehler", "Error")
                    });
                }
            }

            return ergebnisse;
        });
    }

    private static BluescreenEntry? ParseEreignis(EventRecord record)
    {
        // EventData bei ID 1001 enthält u.a. den Bug-Check-Code als erstes Parameter (Format variiert je Windows-Version).
        string? bugcheck = null;
        string? xml = null;
        try
        {
            xml = record.ToXml();
            var marker = "0x000000";
            var idx = xml.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                bugcheck = xml.Substring(idx, Math.Min(10, xml.Length - idx));
        }
        catch { /* best effort */ }

        bugcheck ??= "unbekannt";
        var bekannt = BekannteCodes.GetValueOrDefault(bugcheck.ToLowerInvariant());
        var zeitpunkt = record.TimeCreated ?? DateTime.Now;

        var loesung = bekannt.Loesung ?? Loc.T(
            "Code + Bug-Check-Namen bei Microsoft nachschlagen (docs.microsoft.com/windows-hardware Bug-Check-Codes) oder mit WinDbg öffnen.",
            "Look up the code + bug check name at Microsoft (docs.microsoft.com/windows-hardware bug check codes) or open with WinDbg.");

        var verursacher = xml != null ? ErmittleWahrscheinlichenVerursacher(xml, bugcheck, zeitpunkt) : null;
        if (verursacher != null)
        {
            loesung = Loc.T(
                $"Wahrscheinlich verursacht durch: {verursacher} (Best-Effort-Zuordnung aus der Minidump-Moduldatei, keine sichere Diagnose - für Gewissheit die .dmp-Datei mit WinDbg öffnen). {loesung}",
                $"Likely caused by: {verursacher} (best-effort match from the minidump module list, not a certain diagnosis - open the .dmp file with WinDbg to be sure). {loesung}");
        }

        return new BluescreenEntry
        {
            Zeitpunkt = zeitpunkt,
            BugCheckCode = bugcheck,
            BugCheckName = bekannt.Name ?? Loc.T(
                "Unbekannter Code – aktuell nur eine kleine Liste der häufigsten Codes hinterlegt, dieser fehlt noch.",
                "Unknown code – currently only a small list of the most common codes is stored, this one is still missing."),
            VerursacherTreiber = verursacher,
            Loesungsvorschlag = loesung,
            Quelle = Loc.T("Event-Log", "Event log")
        };
    }

    // Sucht die vier Bugcheck-Parameter (typischerweise Adressen) in derselben Event-XML, die
    // schon den Bugcheck-Code liefert, findet die zeitlich passende .dmp-Datei und prueft, ob
    // einer der Parameter in den Adressbereich eines darin geladenen Moduls faellt.
    private static string? ErmittleWahrscheinlichenVerursacher(string xml, string bugcheckCode, DateTime zeitpunkt)
    {
        try
        {
            var parameter = ExtrahiereHexParameter(xml, bugcheckCode);
            if (parameter.Count == 0) return null;

            var dumpDatei = FindePassendeDumpDatei(zeitpunkt);
            if (dumpDatei is null) return null;

            var bytes = File.ReadAllBytes(dumpDatei);
            var module = MinidumpModuleListParser.Parse(bytes);
            return MinidumpModuleListParser.FindeWahrscheinlichenVerursacher(module, parameter);
        }
        catch
        {
            return null; // lieber der bisherige ehrliche Text als eine erzwungene, evtl. falsche Vermutung
        }
    }

    // Liest bis zu vier Hex-Zahlen (0x...), die NACH dem Bugcheck-Code in der Event-XML stehen -
    // das sind die vom WER-Provider mitgelieferten Bugcheck-Parameter 1-4.
    private static List<ulong> ExtrahiereHexParameter(string xml, string bugcheckCode)
    {
        var ergebnis = new List<ulong>();
        var startIdx = xml.IndexOf(bugcheckCode, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return ergebnis;

        var rest = xml[(startIdx + bugcheckCode.Length)..];
        foreach (Match treffer in Regex.Matches(rest, "0x[0-9a-fA-F]{1,16}"))
        {
            if (ergebnis.Count >= 4) break;
            if (ulong.TryParse(treffer.Value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var wert))
                ergebnis.Add(wert);
        }
        return ergebnis;
    }

    // Die Windows-Minidump-Dateinamen tragen kein exaktes Sekunden-Zeitstempel-Format, das sich
    // zuverlässig parsen liesse - deshalb per Datei-Zeitstempel die zeitlich naechste zum
    // Event-Zeitpunkt suchen (Toleranz: 10 Minuten, ein Absturz + Neustart dauert idR deutlich
    // kuerzer, aber ein bisschen Puffer schadet nicht).
    private static string? FindePassendeDumpDatei(DateTime zeitpunkt)
    {
        var minidumpPfad = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows", "Minidump");
        if (!Directory.Exists(minidumpPfad)) return null;

        return Directory.GetFiles(minidumpPfad, "*.dmp")
            .Select(f => (Pfad: f, Abstand: (File.GetLastWriteTime(f) - zeitpunkt).Duration()))
            .Where(x => x.Abstand <= TimeSpan.FromMinutes(10))
            .OrderBy(x => x.Abstand)
            .Select(x => x.Pfad)
            .FirstOrDefault();
    }
}
