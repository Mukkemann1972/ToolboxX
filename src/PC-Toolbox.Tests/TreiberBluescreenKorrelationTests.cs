using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class TreiberBluescreenKorrelationTests
{
    private static readonly DateTime Absturz = new(2026, 8, 1, 10, 0, 0);

    private static GeraetBefund Geraet(string name, string? dateiname, DateTime? treiberDatum) => new()
    {
        Name = name,
        TreiberDateiname = dateiname,
        TreiberDatum = treiberDatum,
    };

    [Fact]
    public void Ermitteln_TreiberDateiPasstZuAbsturz_LiefertTreffer()
    {
        var geraete = new[] { Geraet("NVIDIA GeForce RTX", "nvlddmkm.sys", new DateTime(2026, 7, 30)) };
        var abstuerze = new[] { ((string?)"nvlddmkm.sys", Absturz) };

        var treffer = TreiberBluescreenKorrelation.Ermitteln(geraete, abstuerze);

        var einzelner = Assert.Single(treffer);
        Assert.Equal("NVIDIA GeForce RTX", einzelner.GeraeteName);
        Assert.True(einzelner.KuerzlichVorDemAbsturzAktualisiert);
    }

    [Fact]
    public void Ermitteln_DateinameGrossKleinschreibungUnterschiedlich_WirdTrotzdemErkannt()
    {
        var geraete = new[] { Geraet("Realtek Audio", "RTKVHD64.sys", null) };
        var abstuerze = new[] { ((string?)"rtkvhd64.sys", Absturz) };

        var treffer = TreiberBluescreenKorrelation.Ermitteln(geraete, abstuerze);

        Assert.Single(treffer);
    }

    [Fact]
    public void Ermitteln_KeinPassenderTreiber_LeereListe()
    {
        var geraete = new[] { Geraet("Intel WLAN", "netwtw10.sys", new DateTime(2020, 1, 1)) };
        var abstuerze = new[] { ((string?)"nvlddmkm.sys", Absturz) };

        Assert.Empty(TreiberBluescreenKorrelation.Ermitteln(geraete, abstuerze));
    }

    [Fact]
    public void Ermitteln_TreiberDatumLangeVorDemAbsturz_KeinKuerzlichAktualisiert()
    {
        var geraete = new[] { Geraet("Alte Netzwerkkarte", "oldnet.sys", new DateTime(2018, 1, 1)) };
        var abstuerze = new[] { ((string?)"oldnet.sys", Absturz) };

        var treffer = Assert.Single(TreiberBluescreenKorrelation.Ermitteln(geraete, abstuerze));
        Assert.False(treffer.KuerzlichVorDemAbsturzAktualisiert);
    }

    [Fact]
    public void Ermitteln_TreiberDatumNachDemAbsturz_KeinKuerzlichAktualisiert()
    {
        // Ein Treiber, der ERST NACH dem Absturz installiert wurde, kann diesen Absturz nicht
        // verursacht haben (z.B. weil er inzwischen aktualisiert wurde) - reine Zeitlogik.
        var geraete = new[] { Geraet("GPU", "gpu.sys", Absturz.AddDays(5)) };
        var abstuerze = new[] { ((string?)"gpu.sys", Absturz) };

        var treffer = Assert.Single(TreiberBluescreenKorrelation.Ermitteln(geraete, abstuerze));
        Assert.False(treffer.KuerzlichVorDemAbsturzAktualisiert);
    }

    [Fact]
    public void Ermitteln_LeererVerursacherName_WirdIgnoriert()
    {
        var geraete = new[] { Geraet("GPU", "gpu.sys", null) };
        var abstuerze = new[] { ((string?)null, Absturz), ((string?)"", Absturz) };

        Assert.Empty(TreiberBluescreenKorrelation.Ermitteln(geraete, abstuerze));
    }
}
