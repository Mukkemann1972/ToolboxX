using System.Diagnostics.Eventing.Reader;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Windows-Fehleranalyse (Free, kein Admin nötig - live geprüft, anders als z.B. Boot-Auswirkung/
/// Wiederherstellungspunkte, die einen speziellen Diagnose-Kanal bzw. eine COM-API brauchen: das
/// normale System-/Application-Protokoll ist für jeden Benutzer lesbar). Nutzt die strukturierte
/// .NET-API <see cref="EventLogReader"/> direkt (keine Textausgabe eines Kommandozeilentools zu
/// parsen nötig, anders als DISM/SFC/powercfg) - Level/Id/ProviderName/TimeCreated kommen als
/// echte typisierte Felder, nicht aus lokalisiertem Text.
/// </summary>
public class WindowsFehleranalyseService
{
    private static readonly string[] Protokolle = { "System", "Application" };

    public Task<IReadOnlyList<FehlerGruppe>> AnalysierenAsync(TimeSpan zeitraum) => Task.Run(
        IReadOnlyList<FehlerGruppe> () =>
        {
            var rohEintraege = new List<FehlerRohEintrag>();

            foreach (var protokoll in Protokolle)
            {
                var xpath =
                    $"*[System[(Level=1 or Level=2 or Level=3) and " +
                    $"TimeCreated[timediff(@SystemTime) <= {(long)zeitraum.TotalMilliseconds}]]]";

                EventLogReader? reader = null;
                try
                {
                    var abfrage = new EventLogQuery(protokoll, PathType.LogName, xpath);
                    reader = new EventLogReader(abfrage);

                    EventRecord? datensatz;
                    while ((datensatz = reader.ReadEvent()) != null)
                    {
                        using (datensatz)
                        {
                            var schweregrad = datensatz.Level switch
                            {
                                1 => FehlerSchweregrad.Kritisch,
                                2 => FehlerSchweregrad.Fehler,
                                _ => FehlerSchweregrad.Warnung
                            };

                            string beschreibung;
                            try { beschreibung = datensatz.FormatDescription() ?? ""; }
                            catch (EventLogException) { beschreibung = ""; }

                            rohEintraege.Add(new FehlerRohEintrag(
                                datensatz.ProviderName ?? "?",
                                datensatz.Id,
                                schweregrad,
                                protokoll,
                                datensatz.TimeCreated ?? DateTime.MinValue,
                                beschreibung));
                        }
                    }
                }
                catch (EventLogNotFoundException)
                {
                    // Protokoll existiert auf diesem System nicht - einfach ueberspringen statt abzustuerzen.
                }
                finally
                {
                    reader?.Dispose();
                }
            }

            return WindowsFehleranalyse.Gruppieren(rohEintraege);
        });
}
