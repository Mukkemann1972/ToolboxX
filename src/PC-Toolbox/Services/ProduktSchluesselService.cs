using System.Management;
using PCToolboxCore;

namespace PCToolbox.Services;

// Liest den werksseitig in der Firmware hinterlegten OEM-Produktschluessel aus
// (SoftwareLicensingService.OA3xOriginalProductKey, WMI) - die von Microsoft selbst dafuer
// dokumentierte Eigenschaft, seit Windows 8 eingefuehrt genau fuer den Fall "Windows neu
// aufsetzen und den urspruenglichen Schluessel wiederverwenden". Bewusst NICHT der aeltere Weg
// ueber den in der Registry gespeicherten "DigitalProductId"-Wert - dessen Dekodier-Algorithmus
// liesse sich nur zuverlaessig nachbauen, indem man sich zu nah an vorhandenen fremden
// Implementierungen orientiert; das vermeiden wir lieber, als etwas potenziell Falsches zu
// zeigen (s. Team-Konvention "kein unbelegtes Versprechen").
//
// Liefert bewusst KEINEN Schluessel bei digitalen Lizenzen ohne Firmware-Schluessel - das ist
// bei vielen Windows-10/11-Installationen (Gratis-Upgrade, digitale Lizenz an ein
// Microsoft-Konto/die Hardware selbst gebunden) der normale, erwartete Fall, kein Fehler.
public static class ProduktSchluesselService
{
    public static Task<string?> OemSchluesselLadenAsync() => Task.Run(() =>
    {
        try
        {
            using var suche = new ManagementObjectSearcher(
                "SELECT OA3xOriginalProductKey FROM SoftwareLicensingService");

            foreach (var o in suche.Get())
            {
                var wert = o["OA3xOriginalProductKey"]?.ToString()?.Trim();
                return string.IsNullOrEmpty(wert) ? null : wert;
            }
        }
        catch
        {
            // WMI kann aus vielen Gruenden scheitern (Dienst deaktiviert, Berechtigungen o.ä.) -
            // in jedem Fall lieber "nichts gefunden" als eine Fehlermeldung, die wie ein
            // Defekt aussieht, wo eigentlich nur kein Schluessel abrufbar ist.
        }

        return null;
    });
}
