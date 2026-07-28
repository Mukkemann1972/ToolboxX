using Microsoft.Win32;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolbox.Services;

public record RegistryZeile(RegistryEintrag Eintrag, string AktuellerWertText, RegistryBefund Befund);

/// <summary>
/// Liest die aktuellen Werte für die kuratierten Stellen aus PCToolboxCore.RegistryKompendium.
/// Rein lesend, keine Admin-Rechte nötig (alle ausgewählten Stellen sind ohne Rechteanhebung
/// lesbar - anders als z.B. SMART-Daten oder Temperatur, die root\WMI/ACPI brauchen).
/// </summary>
public class RegistryKompendiumService
{
    public Task<List<RegistryZeile>> LadeAsync() => Task.Run(() =>
    {
        var ergebnis = new List<RegistryZeile>();

        foreach (var eintrag in RegistryKompendium.Eintraege)
        {
            var rohwert = LiesWert(eintrag);
            var befund = RegistryKompendium.DeuteWert(eintrag.Id, rohwert);
            var anzeige = rohwert?.ToString() ?? Loc.T("(nicht gesetzt)", "(not set)");

            ergebnis.Add(new RegistryZeile(eintrag, anzeige, befund));
        }

        return ergebnis;
    });

    private static object? LiesWert(RegistryEintrag eintrag)
    {
        try
        {
            using var basis = eintrag.Hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
            using var schluessel = basis.OpenSubKey(eintrag.Pfad);
            return schluessel?.GetValue(eintrag.Wertname);
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
    }
}
