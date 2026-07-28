using System.Diagnostics;
using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest die Windows-Dienste aus und stellt ihren Starttyp um - über das Bordmittel
/// sc.exe, mit gezielter Rechteanhebung nur für die einzelne Änderung.
/// </summary>
public class DiensteService
{
    public Task<List<DienstInfo>> LadeAsync() => Task.Run(Lade);

    private static List<DienstInfo> Lade()
    {
        var dienste = new List<DienstInfo>();

        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT Name, DisplayName, Description, StartMode, State FROM Win32_Service");

            foreach (var o in suche.Get())
            {
                var name = o["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                dienste.Add(new DienstInfo
                {
                    Name = name,
                    Anzeigename = o["DisplayName"]?.ToString()?.Trim() ?? name,
                    Beschreibung = o["Description"]?.ToString()?.Trim(),
                    Starttyp = o["StartMode"]?.ToString() ?? "",
                    Laeuft = string.Equals(o["State"]?.ToString(), "Running", StringComparison.OrdinalIgnoreCase)
                });
            }
        }
        catch (ManagementException)
        {
            // WMI antwortet nicht - die Oberfläche meldet das ehrlich.
        }

        return dienste
            .OrderBy(d => d.Anzeigename, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Stellt den Starttyp um. Beim Zurückstellen auf Bedarf/Aus wird der Dienst
    /// zusätzlich gestoppt - sonst läuft er bis zum nächsten Neustart munter weiter,
    /// und man wundert sich, warum sich nichts getan hat.
    /// </summary>
    public Task<(bool Erfolg, string Meldung)> StarttypAendernAsync(DienstInfo dienst, string neuerStarttyp) =>
        Task.Run(() =>
        {
            var bewertung = DiensteBewerter.Bewerten(dienst);
            if (bewertung.Geschuetzt)
                return (false, Loc.T("Dieser Dienst gehört zum System und wird von der Toolbox nicht verändert.",
                                     "This service belongs to the system and is not changed by the toolbox."));

            if (!Ausfuehren(DiensteBefehle.StarttypSetzen(dienst.Name, neuerStarttyp)))
                return (false, Loc.T(
                    "Ohne die Windows-Bestätigung (Administratorrechte) lässt sich der Dienst " +
                    "nicht umstellen. Es wurde nichts verändert.",
                    "The service cannot be changed without the Windows confirmation " +
                    "(administrator rights). Nothing was changed."));

            if (!neuerStarttyp.Equals("Auto", StringComparison.OrdinalIgnoreCase) && dienst.Laeuft)
                Ausfuehren(DiensteBefehle.Stoppen(dienst.Name));

            var wort = neuerStarttyp.ToLowerInvariant() switch
            {
                "auto" => Loc.T("startet jetzt wieder automatisch mit Windows", "now starts automatically with Windows again"),
                "manual" => Loc.T("startet jetzt nur noch bei Bedarf", "now only starts on demand"),
                _ => Loc.T("ist jetzt abgeschaltet", "is now disabled")
            };

            return (true, Loc.T($"„{dienst.Anzeigename}\" {wort}.", $"\"{dienst.Anzeigename}\" {wort}."));
        });

    private static bool Ausfuehren(string argumente)
    {
        try
        {
            var start = new ProcessStartInfo("sc.exe", argumente)
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
            return false;
        }
    }
}
