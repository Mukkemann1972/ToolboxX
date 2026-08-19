using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class EnergieDiagnoseTests
{
    // Echte Ausgabe von diesem Rechner (unelevated getestet).
    private const string RealDatenKeinVerlauf = "Aktivierungsverlaufsanzahl - 0\r\n";

    [Fact]
    public void Parsen_EchteDatenKeinVerlauf_LiestNullAnzahl()
    {
        var ergebnis = AufweckVerlauf.Parsen(RealDatenKeinVerlauf);

        Assert.Equal(0, ergebnis.Anzahl);
        Assert.Equal("", ergebnis.RohDetails);
    }

    [Fact]
    public void Parsen_EnglischeAusgabe_FunktioniertGleich()
    {
        var ergebnis = AufweckVerlauf.Parsen("Wake History Count - 0\r\n");

        Assert.Equal(0, ergebnis.Anzahl);
    }

    [Fact]
    public void Parsen_MitVerlaufsEintraegen_LiestAnzahlUndBehaeltDetailsAlsRohtext()
    {
        var roh = "Aktivierungsverlaufsanzahl - 2\r\n\r\n" +
                   "[0] Aufwachursache:\r\n" +
                   "Der Wecker \"Windows Update\" hat das System reaktiviert.\r\n\r\n" +
                   "[1] Aufwachursache:\r\n" +
                   "Das Gerät \"USB-Tastatur\" hat das System reaktiviert.";

        var ergebnis = AufweckVerlauf.Parsen(roh);

        Assert.Equal(2, ergebnis.Anzahl);
        Assert.Contains("Windows Update", ergebnis.RohDetails);
        Assert.Contains("USB-Tastatur", ergebnis.RohDetails);
    }

    [Fact]
    public void Parsen_UnerwartetesFormat_GibtNullAnzahlStattAbsturz()
    {
        var ergebnis = AufweckVerlauf.Parsen("Irgendein unerwarteter Text ohne Bindestrich");

        Assert.Null(ergebnis.Anzahl);
    }
}
