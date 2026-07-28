namespace PCToolboxCore;

/// <summary>
/// Baut die Kommandozeilen fuer die Aenderungen, die Administratorrechte brauchen
/// (alles, was fuer ALLE Benutzer gilt). Sie werden ueber die Windows-Bordmittel
/// reg.exe bzw. schtasks.exe mit einer gezielten UAC-Abfrage ausgefuehrt - die App
/// selbst laeuft bewusst OHNE Adminrechte (geringstmoegliche Rechte).
///
/// Wie beim ImDiskCommandBuilder liegt das hier im plattformneutralen Kern: So laesst
/// sich der heikelste Teil (was genau in die Registry geschrieben wird) auf dem Pi
/// wirklich testen, statt ihn nur zu kompilieren.
/// </summary>
public static class AutostartBefehlBuilder
{
    /// <summary>
    /// reg.exe-Argumente, um den Ein/Aus-Zustand eines Autostart-Eintrags zu setzen.
    /// </summary>
    /// <param name="registrySchluessel">
    /// Voller Schluessel, z.B. HKLM\SOFTWARE\...\StartupApproved\Run. Fuer 32-Bit-Programme
    /// auf 64-Bit-Windows endet er auf "Run32" statt "Run" - Windows fuehrt dafuer bewusst
    /// zwei getrennte Listen. Wer beides in denselben Schluessel schreibt, sieht ein
    /// erfolgreiches "Ausgeschaltet", das Programm startet aber trotzdem weiter mit.
    /// </param>
    /// <param name="wertName">Name des Autostart-Eintrags</param>
    /// <param name="daten">Die 12 Bytes aus <see cref="StartupApprovedBlob"/></param>
    public static string BuildRegistrySetzen(string registrySchluessel, string wertName, byte[] daten) =>
        $"add \"{registrySchluessel}\" /v \"{wertName}\" /t REG_BINARY " +
        $"/d {StartupApprovedBlob.AlsHex(daten)} /f";

    /// <summary>schtasks.exe-Argumente, um eine geplante Aufgabe ein- oder auszuschalten.</summary>
    public static string BuildAufgabeUmschalten(string aufgabenPfad, bool aktivieren)
    {
        var pfad = aufgabenPfad.StartsWith('\\') ? aufgabenPfad : "\\" + aufgabenPfad;
        return $"/change /tn \"{pfad}\" /{(aktivieren ? "enable" : "disable")}";
    }
}
