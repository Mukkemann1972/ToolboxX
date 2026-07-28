using PCToolboxCore;
using Xunit;
using static PCToolboxCore.ZipVerzeichnisListe;

namespace PCToolboxCore.Tests;

public class ZipVerzeichnisListeTests
{
    [Fact]
    public void KindEintraege_WurzelZeigtDirekteDateienUndOrdnerAusPfaden()
    {
        var alle = new List<ZipQuellEintrag>
        {
            new("Bilder/urlaub.jpg", 1000),
            new("Bilder/Unterordner/nacht.jpg", 2000),
            new("liesmich.txt", 42),
        };

        var kinder = KindEintraege(alle, "");

        Assert.Equal(2, kinder.Count);
        Assert.Equal("Bilder", kinder[0].Name);
        Assert.True(kinder[0].IstOrdner);
        Assert.Null(kinder[0].GroesseBytes);
        Assert.Equal("liesmich.txt", kinder[1].Name);
        Assert.False(kinder[1].IstOrdner);
        Assert.Equal(42, kinder[1].GroesseBytes);
    }

    [Fact]
    public void KindEintraege_GehtEineEbeneTieferNurDirekteKinder()
    {
        var alle = new List<ZipQuellEintrag>
        {
            new("Bilder/urlaub.jpg", 1000),
            new("Bilder/Unterordner/nacht.jpg", 2000),
        };

        var kinder = KindEintraege(alle, "Bilder");

        Assert.Equal(2, kinder.Count);
        Assert.Contains(kinder, k => k.Name == "urlaub.jpg" && !k.IstOrdner && k.GroesseBytes == 1000);
        Assert.Contains(kinder, k => k.Name == "Unterordner" && k.IstOrdner);
    }

    [Fact]
    public void KindEintraege_ExpliziterOrdnerEintragMitSchraegstrichWirdErkannt()
    {
        var alle = new List<ZipQuellEintrag>
        {
            new("LeererOrdner/", 0),
            new("liesmich.txt", 10),
        };

        var kinder = KindEintraege(alle, "");

        Assert.Contains(kinder, k => k.Name == "LeererOrdner" && k.IstOrdner);
    }

    [Fact]
    public void KindEintraege_OrdnerErscheintNurEinmalTrotzMehrererDateienDarin()
    {
        var alle = new List<ZipQuellEintrag>
        {
            new("Ordner/a.txt", 1),
            new("Ordner/b.txt", 2),
            new("Ordner/c.txt", 3),
        };

        var kinder = KindEintraege(alle, "");

        Assert.Single(kinder);
        Assert.Equal("Ordner", kinder[0].Name);
    }

    [Fact]
    public void KindEintraege_OrdnerVorDateienUndAlphabetischSortiert()
    {
        var alle = new List<ZipQuellEintrag>
        {
            new("zzz.txt", 1),
            new("aaa.txt", 1),
            new("MOrdner/x.txt", 1),
            new("AOrdner/y.txt", 1),
        };

        var kinder = KindEintraege(alle, "");

        Assert.Equal(["AOrdner", "MOrdner", "aaa.txt", "zzz.txt"], kinder.Select(k => k.Name));
    }

    [Fact]
    public void KindEintraege_UnpassenderPfadLiefertLeereListe()
    {
        var alle = new List<ZipQuellEintrag> { new("Bilder/urlaub.jpg", 1000) };

        var kinder = KindEintraege(alle, "Dokumente");

        Assert.Empty(kinder);
    }

    [Fact]
    public void KindEintraege_LeereEingabeLiefertLeereListe()
    {
        Assert.Empty(KindEintraege([], ""));
    }

    [Fact]
    public void KindEintraege_BackslashPfadeWerdenWieVorwaertsSchraegstrichBehandelt()
    {
        var alle = new List<ZipQuellEintrag> { new(@"Ordner\datei.txt", 5) };

        var kinder = KindEintraege(alle, "Ordner");

        Assert.Single(kinder);
        Assert.Equal("datei.txt", kinder[0].Name);
    }

    [Fact]
    public void KindEintraege_NameAlsOrdnerUndDateiZaehltAlsOrdner()
    {
        var alle = new List<ZipQuellEintrag>
        {
            new("Zwiespalt", 5),
            new("Zwiespalt/innen.txt", 3),
        };

        var kinder = KindEintraege(alle, "");

        Assert.Single(kinder);
        Assert.True(kinder[0].IstOrdner);
    }
}
