using System.IO;
using Microsoft.Win32;
using PCToolboxCore;

namespace PCToolbox.Services;

/// <summary>
/// Liest HKLM\SYSTEM\MountedDevices (jeder je vergebene Laufwerksbuchstabe steht dort
/// dauerhaft, auch nach Entfernen des Geräts) und vergleicht mit den gerade aktiven
/// Laufwerken, um "Geister"-Buchstaben zu finden. Rein lesend, keine Admin-Rechte nötig
/// (empirisch geprüft). Die eigentliche Vergleichslogik steckt in
/// PCToolboxCore.LaufwerksbuchstabenKonflikte, hier nur der Registry-Zugriff.
/// </summary>
public class LaufwerksbuchstabenKonfliktService
{
    public Task<IReadOnlyList<char>> GeisterBuchstabenAsync() =>
        Task.Run(() =>
        {
            try
            {
                using var schluessel = Registry.LocalMachine.OpenSubKey(@"SYSTEM\MountedDevices");
                if (schluessel is null) return Array.Empty<char>();

                var registriert = LaufwerksbuchstabenKonflikte.BuchstabenAusWertnamen(schluessel.GetValueNames());
                var aktiv = DriveInfo.GetDrives()
                    .Where(d => d.Name.Length > 0)
                    .Select(d => d.Name[0])
                    .ToList();

                return LaufwerksbuchstabenKonflikte.GeisterBuchstaben(registriert, aktiv);
            }
            catch
            {
                return Array.Empty<char>();
            }
        });
}
