using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DateiwiederherstellungTests
{
    private static readonly DateiSignatur Jpeg = Dateiwiederherstellung.BekannteSignaturen.Single(s => s.Typ == DateiTyp.Jpeg);
    private static readonly DateiSignatur Png = Dateiwiederherstellung.BekannteSignaturen.Single(s => s.Typ == DateiTyp.Png);

    private static byte[] JpegBytes(int fuellLaenge = 10) =>
        [.. Jpeg.Kopf, .. Enumerable.Repeat((byte)0x42, fuellLaenge), .. Jpeg.Fuss];

    [Fact]
    public void NaechsterBlock_EinzelneDateiInEinemBlock_WirdGefunden()
    {
        var scanner = new CarvingScanner(Dateiwiederherstellung.BekannteSignaturen);
        var block = JpegBytes();

        var funde = scanner.NaechsterBlock(block);

        var fund = Assert.Single(funde);
        Assert.Equal(DateiTyp.Jpeg, fund.Typ);
        Assert.Equal(0, fund.StartOffset);
        Assert.Equal(block.Length, fund.Laenge);
    }

    [Fact]
    public void NaechsterBlock_KopfAmEndeVonBlockUndFussImNaechsten_WirdTrotzdemGefunden()
    {
        var scanner = new CarvingScanner(Dateiwiederherstellung.BekannteSignaturen);
        var datei = JpegBytes(50);

        // Bewusst mitten in der Datei aufteilen, so dass Kopf UND ein Teil der Nutzdaten im
        // ersten Block liegen, der Fuss aber erst im zweiten - genau der Fall, für den der
        // Überlapp-Mechanismus da ist.
        var teilungspunkt = 5;
        var ersterBlock = datei[..teilungspunkt];
        var zweiterBlock = datei[teilungspunkt..];

        var funde1 = scanner.NaechsterBlock(ersterBlock);
        Assert.Empty(funde1);

        var funde2 = scanner.NaechsterBlock(zweiterBlock);
        var fund = Assert.Single(funde2);
        Assert.Equal(0, fund.StartOffset);
        Assert.Equal(datei.Length, fund.Laenge);
    }

    [Fact]
    public void NaechsterBlock_ZweiDateienHintereinander_BeideWerdenGefunden()
    {
        var scanner = new CarvingScanner(Dateiwiederherstellung.BekannteSignaturen);
        var erste = JpegBytes(10);
        var zweite = JpegBytes(20);
        var block = erste.Concat(zweite).ToArray();

        var funde = scanner.NaechsterBlock(block);

        Assert.Equal(2, funde.Count);
        Assert.Equal(0, funde[0].StartOffset);
        Assert.Equal(erste.Length, funde[0].Laenge);
        Assert.Equal(erste.Length, funde[1].StartOffset);
        Assert.Equal(zweite.Length, funde[1].Laenge);
    }

    [Fact]
    public void NaechsterBlock_UnterschiedlicheDateiartenGemischt_BeideWerdenErkannt()
    {
        var scanner = new CarvingScanner(Dateiwiederherstellung.BekannteSignaturen);
        var jpeg = JpegBytes(5);
        var png = Png.Kopf.Concat(Enumerable.Repeat((byte)0x00, 5)).Concat(Png.Fuss).ToArray();
        var block = jpeg.Concat(png).ToArray();

        var funde = scanner.NaechsterBlock(block);

        Assert.Contains(funde, f => f.Typ == DateiTyp.Jpeg);
        Assert.Contains(funde, f => f.Typ == DateiTyp.Png);
    }

    [Fact]
    public void NaechsterBlock_KeinFussInnerhalbDerGroessengrenze_WirdAlsUnsicherGemeldet()
    {
        var kleineSignatur = new DateiSignatur(DateiTyp.Jpeg, "jpg", Jpeg.Kopf, Jpeg.Fuss, MaxGroesseBytes: 20);
        var scanner = new CarvingScanner([kleineSignatur]);

        // Kopf, dann viel zu viele Fuellbytes ohne Fuss - ueberschreitet die 20-Byte-Grenze.
        var block = kleineSignatur.Kopf.Concat(Enumerable.Repeat((byte)0x00, 100)).ToArray();

        var funde = scanner.NaechsterBlock(block);

        var fund = Assert.Single(funde);
        Assert.False(fund.Sicher);
        Assert.Equal(20, fund.Laenge);
    }

    [Fact]
    public void NaechsterBlock_FussGefunden_IstAlsSicherMarkiert()
    {
        var scanner = new CarvingScanner(Dateiwiederherstellung.BekannteSignaturen);
        var block = JpegBytes();

        var funde = scanner.NaechsterBlock(block);

        Assert.True(Assert.Single(funde).Sicher);
    }

    [Fact]
    public void NaechsterBlock_KeinKopfImBlock_FindetNichts()
    {
        var scanner = new CarvingScanner(Dateiwiederherstellung.BekannteSignaturen);

        var funde = scanner.NaechsterBlock(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });

        Assert.Empty(funde);
    }
}
