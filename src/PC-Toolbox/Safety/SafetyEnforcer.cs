using PCToolbox.Models;
using PCToolboxCore;

namespace PCToolbox.Safety;

// Erzwingt fuer jede schreibende Modul-Aktion den Ablauf SICHERN -> AUSFUEHREN -> VERIFIZIEREN
// aus dem 3-Phasen-Prinzip (siehe README). Absichtlich generisch gehalten - wiederverwendbar
// fuer kuenftige Module (Registry-Cleaner, Datentraeger-Tool), nicht nur den Super-Uninstaller.
public static class SafetyEnforcer
{
    public static async Task<UninstallResult> AusfuehrenAsync(
        string aktionsName,
        Func<string, Task> sichern,
        Func<Task> ausfuehren,
        Func<Task<List<LeftoverItem>>> verifizieren,
        IProgress<string>? fortschritt = null)
    {
        var snapshotOrdner = SnapshotManager.NeuerSnapshotOrdner(aktionsName);

        try
        {
            fortschritt?.Report(Loc.T("Phase 1/3: Sichern...", "Phase 1/3: Backing up..."));
            await sichern(snapshotOrdner);

            fortschritt?.Report(Loc.T("Phase 2/3: Ausführen...", "Phase 2/3: Executing..."));
            await ausfuehren();

            fortschritt?.Report(Loc.T("Phase 3/3: Prüfen...", "Phase 3/3: Verifying..."));
            var leftovers = await verifizieren();

            return new UninstallResult
            {
                Erfolg = true,
                Phase = UninstallPhase.Verifiziert,
                SnapshotOrdner = snapshotOrdner,
                Leftovers = leftovers,
                Meldung = leftovers.Count == 0
                    ? Loc.T("Restlos entfernt.", "Removed completely.")
                    : Loc.T($"Deinstalliert, aber {leftovers.Count} Rückstand/Rückstände gefunden.",
                            $"Uninstalled, but {leftovers.Count} leftover item(s) found."),
            };
        }
        catch (Exception ex)
        {
            return new UninstallResult
            {
                Erfolg = false,
                Phase = UninstallPhase.Fehlgeschlagen,
                SnapshotOrdner = snapshotOrdner,
                Meldung = Loc.T($"Fehlgeschlagen: {ex.Message}", $"Failed: {ex.Message}"),
            };
        }
    }
}
