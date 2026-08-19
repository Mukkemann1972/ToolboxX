using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SystemmonitorVerlaufTests
{
    [Fact]
    public void Hinzufuegen_UnterhalbDesLimits_BehaeltAlleWerte()
    {
        var verlauf = new SystemmonitorVerlauf(5);

        for (var i = 0; i < 3; i++)
            verlauf.Hinzufuegen(new SystemmonitorMesswert(DateTime.Now, i, i, i, i));

        Assert.Equal(3, verlauf.Werte.Count);
    }

    [Fact]
    public void Hinzufuegen_UeberDemLimit_WirftAeltesteWerteRaus()
    {
        var verlauf = new SystemmonitorVerlauf(3);

        for (var i = 0; i < 5; i++)
            verlauf.Hinzufuegen(new SystemmonitorMesswert(DateTime.Now, i, 0, 0, 0));

        Assert.Equal(3, verlauf.Werte.Count);
        // Die ersten beiden (CpuProzent 0 und 1) muessen raus sein, uebrig: 2,3,4
        Assert.Equal(2, verlauf.Werte[0].CpuProzent);
        Assert.Equal(4, verlauf.Werte[2].CpuProzent);
    }

    [Fact]
    public void Konstruktor_UngueltigesLimit_Wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemmonitorVerlauf(0));
    }
}

public class SystemmonitorCsvTests
{
    [Fact]
    public void ZuCsv_LeererVerlauf_NurKopfzeile()
    {
        var csv = SystemmonitorCsv.ZuCsv(Array.Empty<SystemmonitorMesswert>());

        Assert.Equal("Zeitpunkt;CPU_Prozent;RAM_Prozent;Disk_Prozent;Netzwerk_KBps\r\n", csv);
    }

    [Fact]
    public void ZuCsv_EinMesswert_VerwendetSemikolonUndPunktDezimal()
    {
        var zeit = new DateTime(2026, 8, 8, 22, 30, 0);
        var csv = SystemmonitorCsv.ZuCsv(new[] { new SystemmonitorMesswert(zeit, 12.34, 56.7, 0.0, 128.5) });

        Assert.Contains("2026-08-08 22:30:00;12.3;56.7;0.0;128.5", csv);
    }

    [Fact]
    public void ZuCsv_UnabhaengigVonSystemKultur_NutztPunktAlsDezimaltrennzeichen()
    {
        var vorherigeKultur = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var csv = SystemmonitorCsv.ZuCsv(new[] { new SystemmonitorMesswert(DateTime.Now, 50.5, 0, 0, 0) });

            Assert.Contains("50.5", csv);
            Assert.DoesNotContain("50,5", csv);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = vorherigeKultur;
        }
    }
}
