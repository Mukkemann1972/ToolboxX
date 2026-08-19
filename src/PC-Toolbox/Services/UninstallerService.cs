using System.Diagnostics;
using Microsoft.Win32;
using PCToolbox.Models;
using PCToolbox.Safety;
using PCToolboxCore;

namespace PCToolbox.Services;

// Orchestriert eine Deinstallation über die 3-Phasen-Sicherheit (SafetyEnforcer):
// Sichern (Registry-Export) -> Ausfuehren (nativer Deinstaller des Herstellers) -> Verifizieren (LeftoverScanner).
public class UninstallerService
{
    private readonly LeftoverScanner _scanner = new();

    public Task<UninstallResult> DeinstallierenAsync(InstalledProgram programm, IProgress<string>? fortschritt = null) =>
        SafetyEnforcer.AusfuehrenAsync(
            aktionsName: $"Deinstallieren_{Kuerzen(programm.DisplayName)}",
            sichern: snapshotOrdner => Task.Run(() =>
                SnapshotManager.RegistrySchluesselSichern(snapshotOrdner, programm.RegistryKeyPfad, "uninstall-eintrag")),
            ausfuehren: () => NativenDeinstallerStartenAsync(programm),
            verifizieren: () => _scanner.ScanLeftoversAsync(programm),
            fortschritt: fortschritt);

    // Entfernt einen einzelnen, vom Nutzer bestaetigten Leftover-Fund - verschiebt/exportiert ihn
    // dabei in denselben Snapshot-Ordner statt ihn endgueltig zu loeschen (RollbackEngine kann
    // ihn zurueckholen).
    public Task<bool> LeftoverEntfernenAsync(LeftoverItem item, string snapshotOrdner) =>
        Task.Run(() =>
        {
            if (item.Art == LeftoverArt.Ordner)
            {
                var ziel = SnapshotManager.OrdnerSichernUndEntfernen(snapshotOrdner, item.Pfad);
                return ziel is not null;
            }

            // Registry-Schluessel: erst exportieren (Rueckhol-Moeglichkeit), dann loeschen.
            var dateiName = $"leftover-{Guid.NewGuid():N}";
            return SnapshotManager.RegistrySchluesselSichern(snapshotOrdner, item.Pfad, dateiName)
                && RegistrySchluesselLoeschen(item.Pfad);
        });

    // Zweitversuch mit Administratorrechten (loest ein Windows-ACL-Problem, z.B. ein
    // ProgramData-Ordner, den nur Administratoren entfernen duerfen) - wird von der UI nur nach
    // ausdruecklicher, erklaerter Zustimmung des Nutzers aufgerufen, s. SnapshotManager-Kommentar.
    public Task<bool> LeftoverMitErhoehtenRechtenEntfernenAsync(LeftoverItem item, string snapshotOrdner) =>
        Task.Run(() =>
        {
            if (item.Art == LeftoverArt.Ordner)
            {
                var ziel = SnapshotManager.OrdnerMitErhoehtenRechtenSichernUndEntfernen(snapshotOrdner, item.Pfad);
                return ziel is not null;
            }

            var dateiName = $"leftover-{Guid.NewGuid():N}";
            return SnapshotManager.RegistrySchluesselSichern(snapshotOrdner, item.Pfad, dateiName)
                && RegistrySchluesselLoeschen(item.Pfad);
        });

    internal static bool RegistrySchluesselLoeschen(string registryPfad)
    {
        var teile = registryPfad.Split('\\', 2);
        if (teile.Length != 2) return false;

        RegistryKey? hive = teile[0] switch
        {
            "HKLM" => Registry.LocalMachine,
            "HKCU" => Registry.CurrentUser,
            _ => null,
        };
        if (hive is null) return false;

        var unterPfad = teile[1];
        var trennstelle = unterPfad.LastIndexOf('\\');
        if (trennstelle < 0) return false;

        var elternPfad = unterPfad[..trennstelle];
        var schluesselName = unterPfad[(trennstelle + 1)..];

        using var eltern = hive.OpenSubKey(elternPfad, writable: true);
        if (eltern is null) return false;

        eltern.DeleteSubKeyTree(schluesselName, throwOnMissingSubKey: false);
        return true;
    }

    // ACHTUNG (bekannte Einschraenkung): viele Deinstaller (v.a. NSIS/InstallShield) kopieren sich
    // selbst in einen Temp-Ordner, starten dort neu und beenden den urspruenglichen Prozess sofort -
    // WaitForExitAsync kehrt dann zurueck, WAEHREND der eigentliche Deinstaller noch laeuft/ein
    // Fenster zeigt. Die UI weist den Nutzer deshalb an, den Deinstaller-Dialog erst abzuschliessen,
    // bevor er "Fertig, jetzt prüfen" bestaetigt.
    private static Task NativenDeinstallerStartenAsync(InstalledProgram programm) => Task.Run(async () =>
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen(programm.UninstallString);
        var psi = new ProcessStartInfo(datei, argumente) { UseShellExecute = true };

        using var prozess = Process.Start(psi) ?? throw new InvalidOperationException(
            Loc.T($"Der Deinstaller von \"{programm.DisplayName}\" konnte nicht gestartet werden.",
                  $"The uninstaller for \"{programm.DisplayName}\" could not be started."));

        await prozess.WaitForExitAsync();
    });

    private static string Kuerzen(string text) => text.Length <= 40 ? text : text[..40];
}
