using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DreiPhasenAblaufTests
{
    private static readonly AblaufPlan Plan = new("Testreparatur", "Pruefwert ist wieder in Ordnung");

    private static Func<CancellationToken, Task<SicherungsErgebnis>> SicherungOk => _ => Task.FromResult(new SicherungsErgebnis(true, "Sicherung A"));
    private static Func<CancellationToken, Task<NachtestErgebnis>> Nachtest(NachtestStatus s) => _ => Task.FromResult(new NachtestErgebnis(s, "Befund"));

    [Fact]
    public async Task SicherungFehlgeschlagen_FuehrtNichtAus()
    {
        var ausgefuehrt = false;
        var e = await DreiPhasenAblauf.LaufenAsync(Plan,
            _ => Task.FromResult(new SicherungsErgebnis(false, "")),
            _ => { ausgefuehrt = true; return Task.CompletedTask; },
            Nachtest(NachtestStatus.Bestanden));

        Assert.False(ausgefuehrt);
        Assert.False(e.IstErfolg);
        Assert.Equal(AblaufErgebnisArt.Fehlgeschlagen, e.Art);
        Assert.Equal(AblaufPhase.Sichern, e.LetztePhase);
    }

    [Fact]
    public async Task SicherungWirftAusnahme_FuehrtNichtAus()
    {
        var ausgefuehrt = false;
        var e = await DreiPhasenAblauf.LaufenAsync(Plan,
            _ => throw new IOException("Platte voll"),
            _ => { ausgefuehrt = true; return Task.CompletedTask; },
            Nachtest(NachtestStatus.Bestanden));

        Assert.False(ausgefuehrt);
        Assert.Equal(AblaufErgebnisArt.Fehlgeschlagen, e.Art);
        Assert.Contains("Platte voll", e.Meldung);
    }

    [Fact]
    public async Task AbbruchWaehrendSicherung_VeraendertNichts()
    {
        var ausgefuehrt = false;
        var e = await DreiPhasenAblauf.LaufenAsync(Plan,
            _ => throw new OperationCanceledException(),
            _ => { ausgefuehrt = true; return Task.CompletedTask; },
            Nachtest(NachtestStatus.Bestanden));

        Assert.False(ausgefuehrt);
        Assert.Equal(AblaufErgebnisArt.Fehlgeschlagen, e.Art);
        Assert.Equal(AblaufPhase.Sichern, e.LetztePhase);
    }

    [Fact]
    public async Task AusfuehrungOhneFehlerAberNachtestNichtBestanden_IstKeinErfolg()
    {
        // Kern der Idee: "Befehl ohne Fehler beendet" ist nicht "Problem behoben".
        var e = await DreiPhasenAblauf.LaufenAsync(Plan, SicherungOk, _ => Task.CompletedTask, Nachtest(NachtestStatus.NichtBestanden));

        Assert.False(e.IstErfolg);
        Assert.Equal(AblaufErgebnisArt.Fehlgeschlagen, e.Art);
        Assert.Equal(AblaufPhase.Nachpruefen, e.LetztePhase);
    }

    [Fact]
    public async Task AusfuehrungWirftAusnahme_NachtestLaeuftNicht()
    {
        var nachtestGelaufen = false;
        var e = await DreiPhasenAblauf.LaufenAsync(Plan, SicherungOk,
            _ => throw new InvalidOperationException("kaputt"),
            _ => { nachtestGelaufen = true; return Task.FromResult(new NachtestErgebnis(NachtestStatus.Bestanden, "")); });

        Assert.False(nachtestGelaufen);
        Assert.Equal(AblaufErgebnisArt.Fehlgeschlagen, e.Art);
        Assert.Equal(AblaufPhase.Ausfuehren, e.LetztePhase);
        Assert.NotNull(e.Sicherung);
    }

    [Fact]
    public async Task AbbruchWaehrendAusfuehrung_IstNichtVerifiziert()
    {
        var e = await DreiPhasenAblauf.LaufenAsync(Plan, SicherungOk,
            _ => throw new OperationCanceledException(),
            Nachtest(NachtestStatus.Bestanden));

        Assert.Equal(AblaufErgebnisArt.NichtVerifiziert, e.Art);
        Assert.False(e.IstErfolg);
    }

    [Theory]
    [InlineData(NachtestStatus.Unklar)]
    [InlineData(NachtestStatus.NeustartNoetig)]
    public async Task UnklarerNachtest_IstNichtVerifiziertUndKeinErfolg(NachtestStatus status)
    {
        var e = await DreiPhasenAblauf.LaufenAsync(Plan, SicherungOk, _ => Task.CompletedTask, Nachtest(status));

        Assert.Equal(AblaufErgebnisArt.NichtVerifiziert, e.Art);
        Assert.False(e.IstErfolg);
        Assert.Equal(status, e.Nachtest!.Status);
    }

    [Fact]
    public async Task NachtestWirftAusnahme_IstNichtVerifiziert()
    {
        var e = await DreiPhasenAblauf.LaufenAsync(Plan, SicherungOk, _ => Task.CompletedTask,
            _ => throw new TimeoutException("zu lange"));

        Assert.Equal(AblaufErgebnisArt.NichtVerifiziert, e.Art);
        Assert.False(e.IstErfolg);
    }

    [Fact]
    public async Task Bestanden_IstErfolgMitAllenBelegen()
    {
        var phasen = new List<string>();
        var fortschritt = new SyncProgress(phasen);
        var e = await DreiPhasenAblauf.LaufenAsync(Plan, SicherungOk, _ => Task.CompletedTask, Nachtest(NachtestStatus.Bestanden), fortschritt);

        Assert.True(e.IstErfolg);
        Assert.Equal(AblaufPhase.Fertig, e.LetztePhase);
        Assert.Equal("Sicherung A", e.Sicherung!.Beleg);
        Assert.Equal("Pruefwert ist wieder in Ordnung", e.Plan.Sollkriterium);
        Assert.Equal(3, phasen.Count);
    }

    // Progress<T> meldet asynchron; fuer den Test brauchen wir sofortige Rueckmeldung.
    private sealed class SyncProgress(List<string> ziel) : IProgress<string>
    {
        public void Report(string value) => ziel.Add(value);
    }
}
