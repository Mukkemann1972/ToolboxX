using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class LaufwerksbuchstabenProPlatteTests
{
    private const string BeispielAusgabe = """
        [{"DiskNumber":0,"DriveLetter":"D"},{"DiskNumber":2,"DriveLetter":"C"},{"DiskNumber":2,"DriveLetter":"E"}]
        """;

    [Fact]
    public void Lesen_GruppiertNachPlatte()
    {
        var ergebnis = LaufwerksbuchstabenProPlatte.Lesen(BeispielAusgabe);

        Assert.Equal(['D'], ergebnis[0]);
        Assert.Equal(['C', 'E'], ergebnis[2]);
    }

    [Fact]
    public void Lesen_PlatteOhneBuchstabenFehltImErgebnis()
    {
        var ergebnis = LaufwerksbuchstabenProPlatte.Lesen(BeispielAusgabe);

        Assert.False(ergebnis.ContainsKey(1));
    }

    [Fact]
    public void Lesen_EinzelnesObjektStattArray()
    {
        var ergebnis = LaufwerksbuchstabenProPlatte.Lesen("""{"DiskNumber":0,"DriveLetter":"C"}""");

        Assert.Equal(['C'], ergebnis[0]);
    }

    [Fact]
    public void Lesen_LeereOderUngueltigeEingabeGibtLeeresErgebnis()
    {
        Assert.Empty(LaufwerksbuchstabenProPlatte.Lesen(""));
        Assert.Empty(LaufwerksbuchstabenProPlatte.Lesen("nicht-json"));
    }

    [Fact]
    public void Anzeige_MehrereBuchstabenKommagetrennt()
    {
        Assert.Equal("C:, D:", LaufwerksbuchstabenProPlatte.Anzeige(['C', 'D']));
    }

    [Fact]
    public void Anzeige_OhneBuchstabenEhrlicherPlatzhalter()
    {
        Assert.NotEmpty(LaufwerksbuchstabenProPlatte.Anzeige([]));
        Assert.NotEmpty(LaufwerksbuchstabenProPlatte.Anzeige(null));
    }
}
