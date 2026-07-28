using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class LaufwerksbuchstabenKonflikteTests
{
    [Fact]
    public void BuchstabenAusWertnamen_ExtrahiertNurDosDeviceEintraege()
    {
        var wertnamen = new[]
        {
            @"\DosDevices\C:",
            @"\DosDevices\D:",
            @"\DosDevices\g:",                       // Kleinbuchstabe muss auch erkannt werden
            @"\??\Volume{12345678-1234-1234-1234-123456789012}",  // kein Laufwerksbuchstabe
            "SomeUnrelatedValue",
        };

        var buchstaben = LaufwerksbuchstabenKonflikte.BuchstabenAusWertnamen(wertnamen);

        Assert.Equal(new[] { 'C', 'D', 'G' }, buchstaben);
    }

    [Fact]
    public void BuchstabenAusWertnamen_EntferntDuplikate()
    {
        var wertnamen = new[] { @"\DosDevices\D:", @"\DosDevices\D:" };

        var buchstaben = LaufwerksbuchstabenKonflikte.BuchstabenAusWertnamen(wertnamen);

        Assert.Single(buchstaben);
    }

    [Fact]
    public void GeisterBuchstaben_FindetNurRegistrierteAberNichtAktiveBuchstaben()
    {
        var registriert = new[] { 'C', 'D', 'E', 'F' };
        var aktiv = new[] { 'C', 'D' };

        var geister = LaufwerksbuchstabenKonflikte.GeisterBuchstaben(registriert, aktiv);

        Assert.Equal(new[] { 'E', 'F' }, geister);
    }

    [Fact]
    public void GeisterBuchstaben_BlendetSystemUndDisketteLaufwerkeAus()
    {
        // A/B (historische Disketten) und C (Systemlaufwerk) sind kein echter Befund,
        // selbst wenn sie in der Registry stehen, aber gerade nicht "aktiv" erkannt wurden.
        var registriert = new[] { 'A', 'B', 'C', 'D' };
        var aktiv = Array.Empty<char>();

        var geister = LaufwerksbuchstabenKonflikte.GeisterBuchstaben(registriert, aktiv);

        Assert.Equal(new[] { 'D' }, geister);
    }

    [Fact]
    public void GeisterBuchstaben_LeereListeWennAllesAktiv()
    {
        var registriert = new[] { 'D', 'E' };
        var aktiv = new[] { 'D', 'E' };

        Assert.Empty(LaufwerksbuchstabenKonflikte.GeisterBuchstaben(registriert, aktiv));
    }
}
