using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class StartupApprovedBlobTests
{
    [Fact]
    public void IstAktiviert_FehlenderWert_GiltAlsAktiv()
    {
        // Windows legt den Wert erst an, wenn zum ersten Mal etwas deaktiviert wird -
        // "kein Eintrag" muss deshalb "laeuft mit" bedeuten, nicht "ist aus".
        Assert.True(StartupApprovedBlob.IstAktiviert(null));
        Assert.True(StartupApprovedBlob.IstAktiviert([]));
    }

    [Fact]
    public void IstAktiviert_EchteWindowsWerte_WerdenRichtigErkannt()
    {
        byte[] ein = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] aus = [0x03, 0, 0, 0, 0x00, 0x9E, 0x11, 0x35, 0xBF, 0x6D, 0xDB, 0x01];

        Assert.True(StartupApprovedBlob.IstAktiviert(ein));
        Assert.False(StartupApprovedBlob.IstAktiviert(aus));
    }

    [Fact]
    public void Erzeugen_Ausgeschaltet_EnthaeltZeitpunktUndLaesstSichWiederLesen()
    {
        var zeitpunkt = new DateTime(2026, 7, 26, 14, 30, 0, DateTimeKind.Local);

        var daten = StartupApprovedBlob.Erzeugen(aktiviert: false, zeitpunkt);

        Assert.Equal(StartupApprovedBlob.Laenge, daten.Length);
        Assert.False(StartupApprovedBlob.IstAktiviert(daten));
        Assert.Equal(zeitpunkt, StartupApprovedBlob.DeaktiviertAm(daten));
    }

    [Fact]
    public void Erzeugen_Eingeschaltet_HatKeinenZeitpunkt()
    {
        var daten = StartupApprovedBlob.Erzeugen(aktiviert: true, DateTime.Now);

        Assert.True(StartupApprovedBlob.IstAktiviert(daten));
        Assert.Null(StartupApprovedBlob.DeaktiviertAm(daten));
        Assert.All(daten.Skip(1), b => Assert.Equal(0, b));
    }

    [Fact]
    public void AlsHex_ErzeugtFuerRegExeVerwertbareSchreibweise()
    {
        var daten = StartupApprovedBlob.Erzeugen(aktiviert: true, DateTime.Now);

        Assert.Equal("020000000000000000000000", StartupApprovedBlob.AlsHex(daten));
    }
}
