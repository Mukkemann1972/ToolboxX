using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Zuverlässigkeitsverlauf (Free, kein Admin nötig - Win32_ReliabilityRecords ist für normale
/// Benutzer lesbar, live an diesem Rechner bestätigt).
/// </summary>
public class ZuverlaessigkeitsVerlaufService
{
    public Task<IReadOnlyList<ZuverlaessigkeitEintrag>> LesenAsync(TimeSpan zeitraum) => Task.Run(
        () =>
        {
            var grenze = DateTime.Now - zeitraum;
            var ergebnis = new List<ZuverlaessigkeitEintrag>();

            using var suche = new ManagementObjectSearcher(
                "SELECT TimeGenerated, SourceName, EventIdentifier, ProductName, Message FROM Win32_ReliabilityRecords");
            foreach (ManagementObject mo in suche.Get())
            {
                var zeitRoh = mo["TimeGenerated"] as string;
                if (string.IsNullOrEmpty(zeitRoh)) continue;
                var zeitpunkt = ManagementDateTimeConverter.ToDateTime(zeitRoh);
                if (zeitpunkt < grenze) continue;

                var quelle = mo["SourceName"] as string ?? "";
                var eventId = Convert.ToInt32(mo["EventIdentifier"] ?? 0);
                var produktName = mo["ProductName"] as string ?? "";
                var nachricht = mo["Message"] as string ?? "";

                var schweregrad = ZuverlaessigkeitsVerlauf.Klassifizieren(quelle, eventId, nachricht);
                ergebnis.Add(new ZuverlaessigkeitEintrag(zeitpunkt, quelle, produktName, nachricht, schweregrad));
            }

            return (IReadOnlyList<ZuverlaessigkeitEintrag>)ergebnis;
        });
}
