using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;
using PCToolbox.Models;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Services;

/// <summary>
/// Sammelt alles ein, was beim Hochfahren bzw. Anmelden automatisch mitstartet - und
/// schaltet es auf Wunsch ab.
///
/// Windows verteilt das auf vier voellig verschiedene Orte, und die meisten Nutzer
/// kennen nur den Task-Manager (der die Aufgabenplanung gar nicht zeigt):
///   1. Registry, nur fuer den angemeldeten Benutzer   (HKCU ...\Run)
///   2. Registry, fuer alle Benutzer                   (HKLM ...\Run, 64- UND 32-Bit-Liste)
///   3. Die beiden Autostart-Ordner                    (eigener + "alle Benutzer")
///   4. Aufgabenplanung mit "Bei Anmeldung"-Ausloeser  (Lieblingsplatz von Updatern)
///
/// AUSSCHALTEN HEISST HIER NIE LOESCHEN: Es wird derselbe Schalter benutzt, den auch der
/// Windows-Task-Manager umlegt (Registry-Bereich "StartupApproved"). Der Eintrag selbst
/// bleibt unangetastet stehen und ist mit einem Klick wieder da - das ist das
/// 3-Phasen-Sicherheitsprinzip von Toolbox X in seiner einfachsten Form.
/// </summary>
public class AutostartService
{
    private const string RunPfad = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOncePfad = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string WinlogonPfad = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string IfeoPfad = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private const string ApprovedPfad = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private const string SchalterRun = "Run";
    private const string SchalterRun32 = "Run32";
    private const string SchalterOrdner = "StartupFolder";

    public Task<List<AutostartEintrag>> LadeAlleAsync() => Task.Run(LadeAlle);

    private List<AutostartEintrag> LadeAlle()
    {
        var eintraege = new List<AutostartEintrag>();

        eintraege.AddRange(RegistryEintraege(
            RegistryHive.CurrentUser, RegistryView.Registry64,
            AutostartQuelle.RegistryBenutzer, SchalterRun, ist32Bit: false));

        eintraege.AddRange(RegistryEintraege(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            AutostartQuelle.RegistrySystem, SchalterRun, ist32Bit: false));

        // 32-Bit-Programme auf 64-Bit-Windows stehen in einer eigenen Registry-Sicht.
        // Wer die weglaesst, uebersieht auf vielen PCs die Haelfte der Autostarts.
        if (Environment.Is64BitOperatingSystem)
            eintraege.AddRange(RegistryEintraege(
                RegistryHive.LocalMachine, RegistryView.Registry32,
                AutostartQuelle.RegistrySystem, SchalterRun32, ist32Bit: true));

        eintraege.AddRange(OrdnerEintraege(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            AutostartQuelle.AutostartOrdnerBenutzer, RegistryHive.CurrentUser));

        eintraege.AddRange(OrdnerEintraege(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            AutostartQuelle.AutostartOrdnerAlle, RegistryHive.LocalMachine));

        eintraege.AddRange(AufgabenEintraege());

        return eintraege;
    }

    // ================= Registry =================

    private static IEnumerable<AutostartEintrag> RegistryEintraege(
        RegistryHive hive, RegistryView view, AutostartQuelle quelle, string schalterSchluessel, bool ist32Bit)
    {
        var ergebnis = new List<AutostartEintrag>();

        try
        {
            using var basis = RegistryKey.OpenBaseKey(hive, view);
            using var runSchluessel = basis.OpenSubKey(RunPfad);
            if (runSchluessel is null) return ergebnis;

            var schalter = SchalterLesen(hive, schalterSchluessel);

            foreach (var wertName in runSchluessel.GetValueNames())
            {
                var befehl = runSchluessel.GetValue(wertName)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(befehl)) continue;

                var eintrag = EintragBauen(wertName, befehl, quelle,
                    $"{(hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM")}\\{RunPfad}");
                eintrag.Ist32BitSicht = ist32Bit;

                schalter.TryGetValue(wertName, out var zustand);
                eintrag.IstAktiv = StartupApprovedBlob.IstAktiviert(zustand);
                eintrag.DeaktiviertAm = StartupApprovedBlob.DeaktiviertAm(zustand);

                ergebnis.Add(eintrag);
            }
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Kein Zugriff auf diesen Bereich - dann eben ohne. Lieber eine unvollstaendige
            // Liste als ein Absturz mitten im Einlesen.
        }

        return ergebnis;
    }

    private static Dictionary<string, byte[]> SchalterLesen(RegistryHive hive, string unterSchluessel)
    {
        var werte = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var basis = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var schluessel = basis.OpenSubKey($@"{ApprovedPfad}\{unterSchluessel}");
            if (schluessel is null) return werte;

            foreach (var name in schluessel.GetValueNames())
                if (schluessel.GetValue(name) is byte[] daten)
                    werte[name] = daten;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Ohne diese Liste gelten alle Eintraege als aktiv - das ist der ehrliche Standard.
        }

        return werte;
    }

    // ================= Autostart-Ordner =================

    private static IEnumerable<AutostartEintrag> OrdnerEintraege(
        string ordner, AutostartQuelle quelle, RegistryHive hive)
    {
        var ergebnis = new List<AutostartEintrag>();
        if (string.IsNullOrWhiteSpace(ordner) || !Directory.Exists(ordner)) return ergebnis;

        var schalter = SchalterLesen(hive, SchalterOrdner);

        foreach (var datei in Directory.EnumerateFiles(ordner))
        {
            var dateiName = Path.GetFileName(datei);
            if (dateiName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

            // Bei Verknuepfungen interessiert das Ziel, nicht die .lnk-Datei selbst.
            var befehl = datei.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                ? VerknuepfungsZiel(datei) ?? datei
                : datei;

            var eintrag = EintragBauen(Path.GetFileNameWithoutExtension(datei), befehl, quelle, datei);

            schalter.TryGetValue(dateiName, out var zustand);
            eintrag.IstAktiv = StartupApprovedBlob.IstAktiviert(zustand);
            eintrag.DeaktiviertAm = StartupApprovedBlob.DeaktiviertAm(zustand);

            ergebnis.Add(eintrag);
        }

        return ergebnis;
    }

    /// <summary>
    /// Liest das Ziel einer Verknuepfung ueber den Windows-Skripting-Wirt aus. Bewusst
    /// spaet gebunden (kein zusaetzliches Interop-Paket) und mit Rueckfallebene: Klappt es
    /// nicht, zeigen wir eben die .lnk-Datei an, statt gar nichts zu liefern.
    /// </summary>
    private static string? VerknuepfungsZiel(string lnkPfad)
    {
        try
        {
            var typ = Type.GetTypeFromProgID("WScript.Shell");
            if (typ is null) return null;

            dynamic shell = Activator.CreateInstance(typ)!;
            dynamic verknuepfung = shell.CreateShortcut(lnkPfad);
            string ziel = verknuepfung.TargetPath;

            return string.IsNullOrWhiteSpace(ziel) ? null : ziel;
        }
        catch
        {
            return null;
        }
    }

    // ================= Aufgabenplanung =================

    private static IEnumerable<AutostartEintrag> AufgabenEintraege()
    {
        var ergebnis = new List<AutostartEintrag>();

        var basisOrdner = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Tasks");
        if (!Directory.Exists(basisOrdner)) return ergebnis;

        foreach (var datei in DateienSicherAuflisten(basisOrdner))
        {
            var aufgabenPfad = "\\" + Path.GetRelativePath(basisOrdner, datei).Replace('/', '\\');

            // Windows' eigene Aufgaben (mehrere hundert) gehoeren nicht in dieses Modul:
            // Sie sind Systembestandteil, und daran herumzuschalten hilft niemandem.
            if (aufgabenPfad.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase)) continue;

            string xml;
            try
            {
                xml = File.ReadAllText(datei);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var info = AufgabenXmlParser.Auswerten(xml);
            if (info is null || !info.StartetAutomatischMit || string.IsNullOrWhiteSpace(info.Befehl)) continue;

            var befehl = string.IsNullOrWhiteSpace(info.Argumente)
                ? info.Befehl
                : $"{info.Befehl} {info.Argumente}";

            var eintrag = EintragBauen(Path.GetFileName(datei), befehl,
                AutostartQuelle.Aufgabenplanung, aufgabenPfad);

            eintrag.IstAktiv = info.IstAktiviert;
            eintrag.Herausgeber ??= info.Autor;

            ergebnis.Add(eintrag);
        }

        return ergebnis;
    }

    /// <summary>
    /// Laeuft den Ordnerbaum selbst ab, statt SearchOption.AllDirectories zu nutzen:
    /// Ein einziger gesperrter Unterordner wuerde dort die komplette Aufzaehlung abbrechen.
    /// </summary>
    private static IEnumerable<string> DateienSicherAuflisten(string ordner)
    {
        var offen = new Stack<string>();
        offen.Push(ordner);

        while (offen.Count > 0)
        {
            var aktuell = offen.Pop();

            string[] dateien;
            try
            {
                foreach (var unterOrdner in Directory.GetDirectories(aktuell))
                    offen.Push(unterOrdner);

                dateien = Directory.GetFiles(aktuell);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var datei in dateien)
                yield return datei;
        }
    }

    // ================= Gemeinsames =================

    private static AutostartEintrag EintragBauen(
        string name, string befehl, AutostartQuelle quelle, string quelleDetail)
    {
        var rohPfad = BefehlszeileZerleger.ProgrammPfadErmitteln(befehl, DateiVorhanden);
        var pfad = Erweitern(rohPfad);

        var eintrag = new AutostartEintrag
        {
            Name = name,
            Befehl = befehl,
            ProgrammPfad = pfad,
            DateiName = Path.GetFileNameWithoutExtension(pfad).ToLowerInvariant(),
            Quelle = quelle,
            QuelleDetail = quelleDetail,
            DateiFehlt = !DateiVorhanden(pfad)
        };

        eintrag.Herausgeber = HerausgeberLesen(pfad);
        return eintrag;
    }

    private static string Erweitern(string pfad) =>
        string.IsNullOrWhiteSpace(pfad) ? "" : Environment.ExpandEnvironmentVariables(pfad.Trim().Trim('"'));

    private static bool DateiVorhanden(string kandidat)
    {
        if (string.IsNullOrWhiteSpace(kandidat)) return false;

        var pfad = Erweitern(kandidat);

        try
        {
            if (File.Exists(pfad)) return true;

            // Befehle wie "rundll32.exe ..." stehen ohne Pfad drin - die liegen im Systemordner.
            if (!pfad.Contains('\\'))
                return File.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), pfad));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Unsinniger Pfad - gilt als nicht vorhanden.
        }

        return false;
    }

    private static string? HerausgeberLesen(string pfad)
    {
        try
        {
            if (!File.Exists(pfad)) return null;

            var firma = FileVersionInfo.GetVersionInfo(pfad).CompanyName;
            return string.IsNullOrWhiteSpace(firma) ? null : firma.Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    // ================= V0.2: seltenere Orte =================

    /// <summary>
    /// RunOnce (laeuft ein einziges Mal, Windows entfernt den Eintrag danach selbst - deshalb
    /// kein Umschalter wie bei Run) plus zwei Sicherheitspruefungen (Winlogon-Shell,
    /// IFEO-Programmumleitung). Rein lesend, informativ.
    /// </summary>
    public Task<List<SicherheitsBefund>> WeitereOrteAsync() => Task.Run(() =>
    {
        var ergebnis = new List<SicherheitsBefund>();

        ergebnis.AddRange(RunOnceEintraege(RegistryHive.CurrentUser, Loc.T("nur dein Benutzerkonto", "your user account only")));
        ergebnis.AddRange(RunOnceEintraege(RegistryHive.LocalMachine, Loc.T("alle Benutzer", "all users")));

        ergebnis.Add(AutostartSicherheitsPruefung.PruefeWinlogonShell(WinlogonShellLesen()));
        ergebnis.AddRange(IfeoUmleitungenFinden());

        return ergebnis;
    });

    private static IEnumerable<SicherheitsBefund> RunOnceEintraege(RegistryHive hive, string geltungsbereich)
    {
        var ergebnis = new List<SicherheitsBefund>();

        try
        {
            using var basis = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var schluessel = basis.OpenSubKey(RunOncePfad);
            if (schluessel is null) return ergebnis;

            foreach (var wertName in schluessel.GetValueNames())
            {
                var befehl = schluessel.GetValue(wertName)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(befehl)) continue;

                ergebnis.Add(new SicherheitsBefund(
                    Loc.T($"Läuft einmalig: {wertName}", $"Runs once: {wertName}"),
                    Loc.T(
                        $"Startet beim nächsten Anmelden EIN EINZIGES MAL automatisch ({geltungsbereich}), " +
                        $"dann entfernt Windows den Eintrag von selbst - kein Ausschalten nötig. Befehl: {befehl}",
                        $"Starts automatically ONE SINGLE TIME at the next logon ({geltungsbereich}), then " +
                        $"Windows removes the entry on its own - no need to turn it off. Command: {befehl}"),
                    AmpelStatus.Gelb));
            }
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Kein Zugriff - dann eben ohne diese Eintraege.
        }

        return ergebnis;
    }

    private static string? WinlogonShellLesen()
    {
        try
        {
            using var schluessel = Registry.LocalMachine.OpenSubKey(WinlogonPfad);
            return schluessel?.GetValue("Shell")?.ToString();
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static IEnumerable<SicherheitsBefund> IfeoUmleitungenFinden()
    {
        var ergebnis = new List<SicherheitsBefund>();

        try
        {
            using var basis = Registry.LocalMachine.OpenSubKey(IfeoPfad);
            if (basis is null) return ergebnis;

            foreach (var exeName in basis.GetSubKeyNames())
            {
                try
                {
                    using var eintrag = basis.OpenSubKey(exeName);
                    var debugger = eintrag?.GetValue("Debugger")?.ToString();
                    if (!string.IsNullOrWhiteSpace(debugger))
                        ergebnis.Add(AutostartSicherheitsPruefung.IfeoUmleitung(exeName, debugger));
                }
                catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
                {
                    // Einzelner Unterschluessel nicht lesbar - weiter mit dem Rest.
                }
            }
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Kein Zugriff auf den IFEO-Bereich - dann eben ohne diese Pruefung.
        }

        return ergebnis;
    }

    // ================= Ein-/Ausschalten =================

    public Task<(bool Erfolg, string Meldung)> UmschaltenAsync(AutostartEintrag eintrag, bool aktivieren) =>
        Task.Run(() => Umschalten(eintrag, aktivieren));

    private static (bool Erfolg, string Meldung) Umschalten(AutostartEintrag eintrag, bool aktivieren)
    {
        var wort = Loc.T(aktivieren ? "eingeschaltet" : "ausgeschaltet", aktivieren ? "turned on" : "turned off");

        if (eintrag.Quelle == AutostartQuelle.Aufgabenplanung)
        {
            var argumente = AutostartBefehlBuilder.BuildAufgabeUmschalten(eintrag.QuelleDetail, aktivieren);
            return MitAdminrechten("schtasks.exe", argumente)
                ? (true, Loc.T($"„{eintrag.Name}\" wurde {wort}.", $"\"{eintrag.Name}\" was {wort}."))
                : (false, Loc.T(
                      "Ohne die Windows-Bestätigung (Administratorrechte) lässt sich eine geplante " +
                      "Aufgabe nicht ändern. Es wurde nichts verändert.",
                      "A scheduled task cannot be changed without the Windows confirmation " +
                      "(administrator rights). Nothing was changed."));
        }

        var daten = StartupApprovedBlob.Erzeugen(aktivieren, DateTime.Now);
        var (unterSchluessel, wertName) = SchalterFuer(eintrag);

        // Alles, was nur den eigenen Benutzer betrifft, geht ohne Adminrechte -
        // deshalb kommt hier auch keine UAC-Abfrage. Least Privilege.
        if (eintrag.Quelle is AutostartQuelle.RegistryBenutzer or AutostartQuelle.AutostartOrdnerBenutzer)
        {
            try
            {
                using var schluessel = Registry.CurrentUser.CreateSubKey($@"{ApprovedPfad}\{unterSchluessel}", true);
                schluessel.SetValue(wertName, daten, RegistryValueKind.Binary);
                return (true, Loc.T($"„{eintrag.Name}\" wurde {wort}.", $"\"{eintrag.Name}\" was {wort}."));
            }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
            {
                return (false, Loc.T("Der Eintrag konnte nicht geändert werden. Es wurde nichts verändert.",
                                     "The entry could not be changed. Nothing was changed."));
            }
        }

        var systemSchluessel = $@"HKLM\{ApprovedPfad}\{unterSchluessel}";
        var regArgumente = AutostartBefehlBuilder.BuildRegistrySetzen(systemSchluessel, wertName, daten);

        return MitAdminrechten("reg.exe", regArgumente)
            ? (true, Loc.T($"„{eintrag.Name}\" wurde {wort}.", $"\"{eintrag.Name}\" was {wort}."))
            : (false, Loc.T(
                  "Dieser Eintrag gilt für alle Benutzer des PCs - dafür braucht Windows eine " +
                  "Bestätigung mit Administratorrechten. Es wurde nichts verändert.",
                  "This entry applies to all users of the PC - Windows needs a confirmation " +
                  "with administrator rights for that. Nothing was changed."));
    }

    private static (string UnterSchluessel, string WertName) SchalterFuer(AutostartEintrag eintrag) =>
        eintrag.Quelle switch
        {
            AutostartQuelle.AutostartOrdnerBenutzer or AutostartQuelle.AutostartOrdnerAlle
                => (SchalterOrdner, Path.GetFileName(eintrag.QuelleDetail)),
            _ => (eintrag.Ist32BitSicht ? SchalterRun32 : SchalterRun, eintrag.Name)
        };

    /// <summary>
    /// Startet ein Windows-Bordmittel mit gezielter UAC-Abfrage. Wie im RAM-Disk-Modul gilt:
    /// "runas" und das Mitlesen der Ausgabe schliessen sich unter Windows gegenseitig aus -
    /// deshalb wird nur der Rueckgabewert ausgewertet.
    /// </summary>
    private static bool MitAdminrechten(string programm, string argumente)
    {
        try
        {
            var start = new ProcessStartInfo(programm, argumente)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var prozess = Process.Start(start);
            if (prozess is null) return false;

            prozess.WaitForExit();
            return prozess.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Tritt auf, wenn die UAC-Abfrage abgelehnt wird.
            return false;
        }
    }
}
