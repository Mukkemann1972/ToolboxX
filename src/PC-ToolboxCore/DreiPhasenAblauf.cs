namespace PCToolboxCore;

// Allgemeiner Dreiphasen-Ablauf (Sichern -> Ausfuehren -> Nachpruefen) fuer jede Aktion, die etwas
// am System aendert. Plattformneutral und ohne UI, damit er mit Test-Doubles geprueft werden kann.
// Zwei Regeln sind der Kern und sind getestet:
//  1. Gelingt die Sicherung nicht (Fehler, Ausnahme oder Abbruch), wird NICHT ausgefuehrt.
//  2. "Erfolg" gibt es nur, wenn der vorab festgelegte Nachtest bestanden wird. Ein Befehl, der ohne
//     Fehler endet, beweist nicht, dass das Problem behoben ist.

public enum AblaufPhase { Sichern, Ausfuehren, Nachpruefen, Fertig }

public enum NachtestStatus { Bestanden, NichtBestanden, Unklar, NeustartNoetig }

public enum AblaufErgebnisArt
{
    /// <summary>Nachtest bestanden: nachweislich behoben.</summary>
    Verifiziert,
    /// <summary>Sicherung, Ausfuehrung oder Nachtest sind fehlgeschlagen.</summary>
    Fehlgeschlagen,
    /// <summary>Es lief durch, aber der Erfolg ist nicht belegt (unklar, Neustart noetig, Nachtest nicht lauffaehig, Abbruch waehrend der Ausfuehrung).</summary>
    NichtVerifiziert,
}

/// <summary>Was erreicht werden soll und woran man es erkennt - wird VOR der Ausfuehrung festgelegt.</summary>
public record AblaufPlan(string Name, string Sollkriterium);

/// <summary>Ergebnis der Sicherungsphase. Beleg beschreibt, was gesichert wurde (z. B. Pfad oder Umfang).</summary>
public record SicherungsErgebnis(bool Erfolgreich, string Beleg);

/// <summary>Ergebnis des Nachtests. Befund ist der Messwert oder Ausgabetext, der die Entscheidung stuetzt.</summary>
public record NachtestErgebnis(NachtestStatus Status, string Befund);

public record AblaufErgebnis(
    AblaufPlan Plan,
    AblaufErgebnisArt Art,
    AblaufPhase LetztePhase,
    SicherungsErgebnis? Sicherung,
    NachtestErgebnis? Nachtest,
    string Meldung)
{
    public bool IstErfolg => Art == AblaufErgebnisArt.Verifiziert;
}

public static class DreiPhasenAblauf
{
    public static async Task<AblaufErgebnis> LaufenAsync(
        AblaufPlan plan,
        Func<CancellationToken, Task<SicherungsErgebnis>> sichern,
        Func<CancellationToken, Task> ausfuehren,
        Func<CancellationToken, Task<NachtestErgebnis>> nachtest,
        IProgress<string>? fortschritt = null,
        CancellationToken abbruch = default)
    {
        AblaufErgebnis Ende(AblaufErgebnisArt art, AblaufPhase phase, SicherungsErgebnis? s, NachtestErgebnis? n, string meldung) =>
            new(plan, art, phase, s, n, meldung);

        // Phase 1: Sichern. Ohne gelungene Sicherung wird nichts veraendert.
        SicherungsErgebnis sicherung;
        try
        {
            fortschritt?.Report(Loc.T("Phase 1/3: Sichern...", "Phase 1/3: Backing up..."));
            sicherung = await sichern(abbruch).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Ende(AblaufErgebnisArt.Fehlgeschlagen, AblaufPhase.Sichern, null, null,
                Loc.T("Abgebrochen, bevor etwas verändert wurde.", "Cancelled before anything was changed."));
        }
        catch (Exception ex)
        {
            return Ende(AblaufErgebnisArt.Fehlgeschlagen, AblaufPhase.Sichern, null, null,
                Loc.T($"Die Sicherung ist fehlgeschlagen ({ex.Message}). Zur Sicherheit wurde nichts verändert.",
                      $"The backup failed ({ex.Message}). Nothing was changed, to be safe."));
        }

        if (!sicherung.Erfolgreich)
            return Ende(AblaufErgebnisArt.Fehlgeschlagen, AblaufPhase.Sichern, sicherung, null,
                Loc.T("Die Sicherung ist nicht gelungen. Zur Sicherheit wurde nichts verändert.",
                      "The backup did not succeed. Nothing was changed, to be safe."));

        // Phase 2: Ausfuehren.
        try
        {
            fortschritt?.Report(Loc.T("Phase 2/3: Ausführen...", "Phase 2/3: Executing..."));
            await ausfuehren(abbruch).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Ende(AblaufErgebnisArt.NichtVerifiziert, AblaufPhase.Ausfuehren, sicherung, null,
                Loc.T("Während der Ausführung abgebrochen. Der Zustand ist unklar, die Sicherung steht zur Wiederherstellung bereit.",
                      "Cancelled during execution. The state is unclear, the backup is available for restoring."));
        }
        catch (Exception ex)
        {
            return Ende(AblaufErgebnisArt.Fehlgeschlagen, AblaufPhase.Ausfuehren, sicherung, null,
                Loc.T($"Die Ausführung ist fehlgeschlagen ({ex.Message}). Die Sicherung steht zur Wiederherstellung bereit.",
                      $"Execution failed ({ex.Message}). The backup is available for restoring."));
        }

        // Phase 3: Nachpruefen - der vorab festgelegte Nachtest entscheidet.
        NachtestErgebnis ergebnis;
        try
        {
            fortschritt?.Report(Loc.T("Phase 3/3: Nachprüfen...", "Phase 3/3: Verifying..."));
            ergebnis = await nachtest(abbruch).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Ende(AblaufErgebnisArt.NichtVerifiziert, AblaufPhase.Nachpruefen, sicherung, null,
                Loc.T($"Die Nachprüfung konnte nicht ausgeführt werden ({ex.Message}). Der Erfolg ist nicht belegt.",
                      $"The verification could not run ({ex.Message}). Success is not proven."));
        }

        return ergebnis.Status switch
        {
            NachtestStatus.Bestanden => Ende(AblaufErgebnisArt.Verifiziert, AblaufPhase.Fertig, sicherung, ergebnis,
                Loc.T("Behoben und nachgeprüft.", "Fixed and verified.")),
            NachtestStatus.NichtBestanden => Ende(AblaufErgebnisArt.Fehlgeschlagen, AblaufPhase.Nachpruefen, sicherung, ergebnis,
                Loc.T("Der Nachtest wurde nicht bestanden: das Problem besteht weiter.",
                      "The verification failed: the problem still exists.")),
            NachtestStatus.NeustartNoetig => Ende(AblaufErgebnisArt.NichtVerifiziert, AblaufPhase.Nachpruefen, sicherung, ergebnis,
                Loc.T("Ein Neustart ist nötig, bevor der Erfolg geprüft werden kann.",
                      "A restart is needed before success can be verified.")),
            _ => Ende(AblaufErgebnisArt.NichtVerifiziert, AblaufPhase.Nachpruefen, sicherung, ergebnis,
                Loc.T("Der Nachtest ist unklar. Der Erfolg ist nicht belegt.",
                      "The verification is inconclusive. Success is not proven.")),
        };
    }
}
