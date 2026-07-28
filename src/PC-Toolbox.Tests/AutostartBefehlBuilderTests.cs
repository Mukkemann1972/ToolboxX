using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class AutostartBefehlBuilderTests
{
    private const string SystemSchluessel =
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    [Fact]
    public void BuildRegistrySetzen_Ausschalten_ErzeugtRegBinaerBefehl()
    {
        var daten = StartupApprovedBlob.Erzeugen(aktiviert: false, new DateTime(2026, 7, 26, 12, 0, 0, DateTimeKind.Local));

        var argumente = AutostartBefehlBuilder.BuildRegistrySetzen(SystemSchluessel, "Beispiel", daten);

        Assert.StartsWith($"add \"{SystemSchluessel}\" /v \"Beispiel\" /t REG_BINARY /d 03000000", argumente);
        Assert.EndsWith("/f", argumente);
    }

    [Fact]
    public void BuildRegistrySetzen_32BitEintrag_LandetImRun32Schluessel()
    {
        // 32-Bit-Programme fuehrt Windows in einer eigenen Liste ("Run32"). Wer sie in
        // "Run" eintraegt, sieht ein erfolgreiches Ausschalten - das Programm startet aber weiter.
        var daten = StartupApprovedBlob.Erzeugen(aktiviert: false, DateTime.Now);

        var argumente = AutostartBefehlBuilder.BuildRegistrySetzen(SystemSchluessel + "32", "Beispiel", daten);

        Assert.Contains(@"StartupApproved\Run32", argumente);
    }

    [Fact]
    public void BuildRegistrySetzen_NameMitLeerzeichen_WirdInAnfuehrungszeichenGesetzt()
    {
        var daten = StartupApprovedBlob.Erzeugen(aktiviert: true, DateTime.Now);

        var argumente = AutostartBefehlBuilder.BuildRegistrySetzen(SystemSchluessel, "Adobe ARM", daten);

        Assert.Contains("/v \"Adobe ARM\"", argumente);
    }

    [Theory]
    [InlineData(@"\GoogleUpdateTaskMachineUA", false, "/change /tn \"\\GoogleUpdateTaskMachineUA\" /disable")]
    [InlineData("GoogleUpdateTaskMachineUA", true, "/change /tn \"\\GoogleUpdateTaskMachineUA\" /enable")]
    public void BuildAufgabeUmschalten_ErgaenztFuehrendenBackslashUndSchaltetRichtig(
        string pfad, bool aktivieren, string erwartet)
    {
        Assert.Equal(erwartet, AutostartBefehlBuilder.BuildAufgabeUmschalten(pfad, aktivieren));
    }
}
