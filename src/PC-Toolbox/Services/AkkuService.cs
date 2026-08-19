using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using PCToolboxCore;

namespace PCToolbox.Services;

public record AkkuErgebnis(bool AkkuVorhanden, AkkuKapazitaet? Kapazitaet, string? Fehlermeldung);

/// <summary>
/// Akku-Gesundheit (Free): nutzt Windows' eigenen "powercfg /batteryreport" statt eigene
/// Akku-Zähler nachzubauen - das ist dieselbe Datenquelle, auf der z.B. die Ladezyklen- und
/// Kapazitätsangaben in den Windows-Energieeinstellungen selbst beruhen. Braucht bewusst KEINE
/// Administratorrechte (anders als die meisten anderen rohen Zugriffe in dieser App) - der Bericht
/// lässt sich als normaler Nutzer erzeugen.
/// </summary>
public class AkkuService
{
    public async Task<AkkuErgebnis> GesundheitAsync()
    {
        var berichtsDatei = Path.Combine(Path.GetTempPath(), "ToolboxX-Akkubericht-" + Guid.NewGuid().ToString("N") + ".xml");

        try
        {
            // Reihenfolge ist bei powercfg nicht egal - "/output <Datei> /xml" ist die einzige
            // Reihenfolge, die funktioniert (live getestet: "/xml /output ..." liefert
            // "Ungueltige Parameter" und erzeugt gar keine Datei).
            var start = new ProcessStartInfo("powercfg.exe", $"/batteryreport /output \"{berichtsDatei}\" /xml")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var prozess = Process.Start(start);
            if (prozess is null)
                return new AkkuErgebnis(false, null, Loc.T("Konnte powercfg nicht starten.", "Could not start powercfg."));

            await prozess.WaitForExitAsync();

            if (!File.Exists(berichtsDatei))
                return new AkkuErgebnis(false, null, Loc.T(
                    "Kein Akkubericht erzeugt - vermutlich hat dieser PC keinen Akku (Desktop-PC).",
                    "No battery report generated - this PC likely has no battery (desktop PC)."));

            var dokument = XDocument.Load(berichtsDatei);
            var hatBatterieEintrag = dokument.Descendants().Any(e => e.Name.LocalName == "Battery");
            if (!hatBatterieEintrag)
                return new AkkuErgebnis(false, null, Loc.T(
                    "Dieser PC hat laut Windows keinen Akku (Desktop-PC oder Akku wird nicht erkannt).",
                    "According to Windows, this PC has no battery (desktop PC, or the battery isn't detected)."));

            var kapazitaet = AusBerichtLesen(dokument);
            return kapazitaet is null
                ? new AkkuErgebnis(false, null, Loc.T(
                    "Akkubericht erzeugt, enthält aber keine auswertbaren Kapazitätsangaben.",
                    "Battery report generated, but contains no usable capacity data."))
                : new AkkuErgebnis(true, kapazitaet, null);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new AkkuErgebnis(false, null, Loc.T(
                $"Akkubericht konnte nicht erzeugt werden: {e.Message}", $"Could not generate battery report: {e.Message}"));
        }
        finally
        {
            try { if (File.Exists(berichtsDatei)) File.Delete(berichtsDatei); } catch (IOException) { }
        }
    }

    // Sucht Elemente ueber den lokalen Namen statt eines exakten Namespace-Vergleichs - der
    // powercfg-XML-Bericht traegt den Namespace "http://schemas.microsoft.com/battery/2012"
    // (live geprueft), aber ueber LocalName zu suchen ist trotzdem robuster gegenueber
    // Versionsunterschieden als die URI fest zu verdrahten.
    //
    // ACHTUNG (ehrliche Einschraenkung): live getestet nur der Fall "kein Akku vorhanden"
    // (<Batteries></Batteries> leer, auf diesem Desktop-PC) - der Fall MIT Akku (Felder
    // DesignCapacity/FullChargeCapacity/CycleCount direkt unter <Battery>) folgt dem
    // dokumentierten Schema, konnte auf dieser Maschine aber nicht gegen einen echten
    // Laptop-Akku verifiziert werden. Beim ersten echten Test auf einem Laptop nachpruefen.
    private static AkkuKapazitaet? AusBerichtLesen(XDocument dokument)
    {
        var batterie = dokument.Descendants().FirstOrDefault(e => e.Name.LocalName == "Battery");
        if (batterie is null) return null;

        var design = LangWert(batterie, "DesignCapacity");
        var voll = LangWert(batterie, "FullChargeCapacity");
        if (design is null || voll is null) return null;

        var zyklen = IntWert(batterie, "CycleCount");
        return new AkkuKapazitaet(design.Value, voll.Value, zyklen);
    }

    private static long? LangWert(XElement eltern, string localName)
    {
        var text = eltern.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return long.TryParse(text, out var wert) ? wert : null;
    }

    private static int? IntWert(XElement eltern, string localName)
    {
        var text = eltern.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return int.TryParse(text, out var wert) ? wert : null;
    }
}
