using System.Reflection;
using System.Runtime.InteropServices;
using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest den Windows-Update-Verlauf über die Windows Update Agent API (COM,
/// "Microsoft.Update.Session"/IUpdateSearcher.QueryHistory) - dieselbe Quelle, die auch
/// Einstellungen &gt; Windows Update &gt; Updateverlauf anzeigt. Rein lesend, braucht keine
/// Administrator-Rechte.
///
/// Bewusst spät gebunden (Type.GetTypeFromProgID + InvokeMember) statt über eine generierte
/// COM-Interop-Assembly oder "dynamic": kein zusätzliches NuGet-Paket nötig (dynamic würde
/// Microsoft.CSharp voraussetzen), passt zum Anti-Bloat-Prinzip. Die Deutung der rohen
/// ResultCode/Operation-Zahlen steckt bewusst NICHT hier, sondern im plattformneutralen
/// <see cref="PCToolboxCore.WindowsUpdateDeutung"/> - so bleibt die eigentliche Logik auf dem
/// Pi testbar, nur der COM-Zugriff selbst ist Windows-spezifisch.
/// </summary>
public class WindowsUpdateVerlaufService
{
    public Task<List<WindowsUpdateEintrag>> GetVerlaufAsync(int maxEintraege = 15) =>
        Task.Run(() =>
        {
            var ergebnis = new List<WindowsUpdateEintrag>();
            object? session = null;

            try
            {
                var sessionTyp = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Windows Update Agent API nicht verfügbar.");
                session = Activator.CreateInstance(sessionTyp)!;

                var searcher = Invoke(session, "CreateUpdateSearcher")!;
                var gesamtAnzahl = (int)Invoke(searcher, "GetTotalHistoryCount")!;
                var anzahl = Math.Min(gesamtAnzahl, maxEintraege);
                if (anzahl <= 0) return ergebnis;

                var historie = Invoke(searcher, "QueryHistory", 0, anzahl)!;

                for (var i = 0; i < anzahl; i++)
                {
                    try
                    {
                        var eintrag = GetProperty(historie, "Item", i);
                        if (eintrag is null) continue;

                        var titel = (string)(GetProperty(eintrag, "Title") ?? "");
                        var datum = (DateTime)(GetProperty(eintrag, "Date") ?? DateTime.MinValue);
                        var resultCode = (int)(GetProperty(eintrag, "ResultCode") ?? 0);
                        var operation = (int)(GetProperty(eintrag, "Operation") ?? 0);

                        var (ergebnisText, fehlgeschlagen) = WindowsUpdateDeutung.DeuteErgebnisCode(resultCode);

                        ergebnis.Add(new WindowsUpdateEintrag
                        {
                            Datum = datum,
                            Titel = titel,
                            KbNummer = WindowsUpdateDeutung.KbNummerAusTitel(titel),
                            Operation = WindowsUpdateDeutung.DeuteOperation(operation),
                            Ergebnis = ergebnisText,
                            IstFehlgeschlagen = fehlgeschlagen
                        });
                    }
                    catch
                    {
                        // ein einzelner kaputter Eintrag soll nicht den ganzen Verlauf kosten
                    }
                }
            }
            catch
            {
                // Kein Update-Verlauf verfügbar (z.B. Dienst deaktiviert) - dann bleibt die
                // Liste leer, das UI zeigt dafür einen ehrlichen Hinweis statt eines Fehlers.
            }
            finally
            {
                if (session is not null && Marshal.IsComObject(session))
                    Marshal.ReleaseComObject(session);
            }

            return ergebnis.OrderByDescending(e => e.Datum).ToList();
        });

    private static object? Invoke(object target, string member, params object[] args) =>
        target.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, target, args);

    private static object? GetProperty(object target, string member, params object[] args) =>
        target.GetType().InvokeMember(member, BindingFlags.GetProperty, null, target, args);
}
