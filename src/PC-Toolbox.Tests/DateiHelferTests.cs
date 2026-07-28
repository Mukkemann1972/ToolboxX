using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DateiHelferTests
{
    [Theory]
    [InlineData(0, "0 Byte")]
    [InlineData(512, "512 Byte")]
    [InlineData(1024, "1,0 KB")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(1048576, "1,0 MB")]
    public void GroesseFormatieren_LiefertLesbareWerte(long bytes, string erwartet)
    {
        // Test laeuft mit deutscher Zahlenschreibweise (Komma) - so sieht es
        // auf Alex' Windows auch aus.
        var vorher = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            Assert.Equal(erwartet, DateiHelfer.GroesseFormatieren(bytes));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = vorher;
        }
    }

    [Fact]
    public void EineEbeneHoeher_LiefertDenElternordner()
    {
        Assert.Equal(@"C:\Users", DateiHelfer.EineEbeneHoeher(@"C:\Users\Alex"));
        Assert.Equal(@"C:\Users", DateiHelfer.EineEbeneHoeher(@"C:\Users\Alex\"));
    }

    [Fact]
    public void EineEbeneHoeher_AnDerWurzel_GibtNull()
    {
        // Wichtig fuer die Oberflaeche: Der "Hoeher"-Knopf muss dann abgeschaltet sein.
        Assert.Null(DateiHelfer.EineEbeneHoeher(@"C:\"));
        Assert.Null(DateiHelfer.EineEbeneHoeher("C:"));
        Assert.Null(DateiHelfer.EineEbeneHoeher(""));
        Assert.Null(DateiHelfer.EineEbeneHoeher(null));
    }

    [Fact]
    public void EineEbeneHoeher_VonEinemOrdnerDirektUnterDerWurzel_FuehrtZurueckZumLaufwerk()
    {
        Assert.Equal(@"C:\", DateiHelfer.EineEbeneHoeher(@"C:\Users"));
    }

    [Fact]
    public void EineEbeneHoeher_Netzwerkfreigabe_IstSelbstSchonGanzOben()
    {
        Assert.Null(DateiHelfer.EineEbeneHoeher(@"\\Server\Freigabe"));
        Assert.Equal(@"\\Server\Freigabe", DateiHelfer.EineEbeneHoeher(@"\\Server\Freigabe\Ordner"));
    }

    [Fact]
    public void FreierZielName_WennNichtsImWegIst_BleibtDerNameGleich()
    {
        var ziel = DateiHelfer.FreierZielName(@"C:\Ziel\Urlaub.jpg", _ => false);

        Assert.Equal(@"C:\Ziel\Urlaub.jpg", ziel);
    }

    [Fact]
    public void FreierZielName_BeiKonflikt_ZaehltHoch()
    {
        var vorhanden = new[] { @"C:\Ziel\Urlaub.jpg", @"C:\Ziel\Urlaub (2).jpg" };

        var ziel = DateiHelfer.FreierZielName(@"C:\Ziel\Urlaub.jpg",
            p => vorhanden.Contains(p, StringComparer.OrdinalIgnoreCase));

        Assert.Equal(@"C:\Ziel\Urlaub (3).jpg", ziel);
    }

    [Fact]
    public void FreierZielName_OrdnerOhneEndung_FunktioniertAuch()
    {
        var ziel = DateiHelfer.FreierZielName(@"C:\Ziel\Bilder",
            p => p.Equals(@"C:\Ziel\Bilder", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(@"C:\Ziel\Bilder (2)", ziel);
    }

    [Theory]
    [InlineData(@"C:\Daten", @"C:\Daten", true)]           // in sich selbst
    [InlineData(@"C:\Daten", @"C:\Daten\Unterordner", true)] // in eigenen Unterordner
    [InlineData(@"C:\Daten", @"C:\Datenbank", false)]       // nur zufaellig aehnlicher Name
    [InlineData(@"C:\Daten", @"D:\Sicherung", false)]
    public void ZielLiegtInQuelle_ErkenntDieEndlosschleife(string quelle, string ziel, bool erwartet)
    {
        // "C:\Daten" nach "C:\Datenbank" kopieren MUSS erlaubt bleiben - ein reiner
        // Textvergleich ohne Trennzeichen wuerde das faelschlich blockieren.
        Assert.Equal(erwartet, DateiHelfer.ZielLiegtInQuelle(quelle, ziel));
    }
}
