using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class LegacyWindowsWerkzeugeTests
{
    [Fact]
    public void Werkzeuge_IstNichtLeer()
    {
        Assert.NotEmpty(LegacyWindowsWerkzeuge.Werkzeuge);
    }

    [Fact]
    public void Werkzeuge_AlleIdsSindEindeutig()
    {
        var ids = LegacyWindowsWerkzeuge.Werkzeuge.Select(w => w.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Werkzeuge_AlleHabenEinenBefehl()
    {
        Assert.All(LegacyWindowsWerkzeuge.Werkzeuge, w => Assert.False(string.IsNullOrWhiteSpace(w.Befehl)));
    }

    [Fact]
    public void Suchen_LeererText_GibtAlle()
    {
        Assert.Equal(LegacyWindowsWerkzeuge.Werkzeuge.Count, LegacyWindowsWerkzeuge.Suchen("").Count);
    }

    [Fact]
    public void Suchen_NullText_GibtAlle()
    {
        Assert.Equal(LegacyWindowsWerkzeuge.Werkzeuge.Count, LegacyWindowsWerkzeuge.Suchen(null!).Count);
    }

    [Fact]
    public void Suchen_TrefferImNamen_FindetGeraeteManager()
    {
        var treffer = LegacyWindowsWerkzeuge.Suchen("Geräte-Manager");
        Assert.Contains(treffer, w => w.Id == "geraete-manager");
    }

    [Fact]
    public void Suchen_TrefferInErklaerung_FindetPassendeEintraege()
    {
        // "Treiberproblem" steht nur in der Erklaerung des Geraete-Managers, nicht im Namen.
        var treffer = LegacyWindowsWerkzeuge.Suchen("Treiberproblem");
        Assert.Contains(treffer, w => w.Id == "geraete-manager");
    }

    [Fact]
    public void Suchen_GrossKleinschreibungEgal()
    {
        var treffer = LegacyWindowsWerkzeuge.Suchen("FIREWALL");
        Assert.Contains(treffer, w => w.Id == "firewall-erweitert");
    }

    [Fact]
    public void Suchen_KeinTreffer_GibtLeereListe()
    {
        Assert.Empty(LegacyWindowsWerkzeuge.Suchen("xyzxyzxyz-nichts-passt-hierzu"));
    }
}
