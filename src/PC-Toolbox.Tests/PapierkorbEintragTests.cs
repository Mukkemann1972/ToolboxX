using System.Text;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class PapierkorbEintragTests
{
    private static byte[] BauVersion2(string pfad, long groesse, long geloeschtFileTime)
    {
        var nameBytes = Encoding.Unicode.GetBytes(pfad + "\0");
        var puffer = new byte[28 + nameBytes.Length];
        BitConverter.GetBytes(2L).CopyTo(puffer, 0);
        BitConverter.GetBytes(groesse).CopyTo(puffer, 8);
        BitConverter.GetBytes(geloeschtFileTime).CopyTo(puffer, 16);
        BitConverter.GetBytes(pfad.Length + 1).CopyTo(puffer, 24);
        nameBytes.CopyTo(puffer, 28);
        return puffer;
    }

    private static byte[] BauVersion1(string pfad, long groesse, long geloeschtFileTime)
    {
        var puffer = new byte[24 + 520];
        BitConverter.GetBytes(1L).CopyTo(puffer, 0);
        BitConverter.GetBytes(groesse).CopyTo(puffer, 8);
        BitConverter.GetBytes(geloeschtFileTime).CopyTo(puffer, 16);
        var nameBytes = Encoding.Unicode.GetBytes(pfad + "\0");
        nameBytes.CopyTo(puffer, 24);
        return puffer;
    }

    [Fact]
    public void Lesen_Version2_LiestPfadGroesseUndZeit()
    {
        var zeit = DateTime.Now.ToFileTime();
        var bytes = BauVersion2(@"C:\Users\Alex\Desktop\Urlaubsfoto.jpg", 12345, zeit);

        var eintrag = PapierkorbIDatei.Lesen(bytes);

        Assert.NotNull(eintrag);
        Assert.Equal(@"C:\Users\Alex\Desktop\Urlaubsfoto.jpg", eintrag!.OriginalPfad);
        Assert.Equal("Urlaubsfoto.jpg", eintrag.OriginalName);
        Assert.Equal(12345, eintrag.GroesseBytes);
        Assert.NotNull(eintrag.GeloeschtAm);
    }

    [Fact]
    public void Lesen_Version1_LiestPfadAusFestemBlock()
    {
        var bytes = BauVersion1(@"D:\Dokumente\Vertrag.pdf", 999, DateTime.Now.ToFileTime());

        var eintrag = PapierkorbIDatei.Lesen(bytes);

        Assert.NotNull(eintrag);
        Assert.Equal(@"D:\Dokumente\Vertrag.pdf", eintrag!.OriginalPfad);
        Assert.Equal("Vertrag.pdf", eintrag.OriginalName);
    }

    [Fact]
    public void Lesen_UnbekannteVersion_GibtNullZurueck()
    {
        var bytes = BauVersion2(@"C:\Test.txt", 1, DateTime.Now.ToFileTime());
        BitConverter.GetBytes(99L).CopyTo(bytes, 0);

        Assert.Null(PapierkorbIDatei.Lesen(bytes));
    }

    [Fact]
    public void Lesen_ZuKurzeDatei_GibtNullZurueck()
    {
        Assert.Null(PapierkorbIDatei.Lesen(new byte[10]));
    }

    [Fact]
    public void Lesen_GeloeschtZeitNull_GibtNullstattFehlerhafterZeit()
    {
        var bytes = BauVersion2(@"C:\Test.txt", 1, 0);

        var eintrag = PapierkorbIDatei.Lesen(bytes);

        Assert.NotNull(eintrag);
        Assert.Null(eintrag!.GeloeschtAm);
    }
}
