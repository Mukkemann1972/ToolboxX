using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class WindowsFehleranalyseTests
{
    private static FehlerRohEintrag Eintrag(string anbieter, int id, FehlerSchweregrad grad, DateTime zeit, string text = "x") =>
        new(anbieter, id, grad, "System", zeit, text);

    [Fact]
    public void Gruppieren_GleicherAnbieterUndId_WirdZuEinerGruppeVerdichtet()
    {
        // Echtes Muster von diesem Rechner: NetBT 4321 (Fehler) alle paar Minuten wiederholt.
        var basis = new DateTime(2026, 8, 8, 22, 0, 0);
        var roh = new[]
        {
            Eintrag("NetBT", 4321, FehlerSchweregrad.Fehler, basis),
            Eintrag("NetBT", 4321, FehlerSchweregrad.Fehler, basis.AddMinutes(5)),
            Eintrag("NetBT", 4321, FehlerSchweregrad.Fehler, basis.AddMinutes(10)),
        };

        var gruppen = WindowsFehleranalyse.Gruppieren(roh);

        var gruppe = Assert.Single(gruppen);
        Assert.Equal(3, gruppe.Anzahl);
        Assert.Equal(basis, gruppe.ErstesVorkommen);
        Assert.Equal(basis.AddMinutes(10), gruppe.LetztesVorkommen);
    }

    [Fact]
    public void Gruppieren_UnterschiedlicheEreignisId_BleibenGetrennteGruppen()
    {
        var jetzt = DateTime.Now;
        var roh = new[]
        {
            Eintrag("disk", 153, FehlerSchweregrad.Warnung, jetzt),
            Eintrag("disk", 51, FehlerSchweregrad.Warnung, jetzt),
        };

        var gruppen = WindowsFehleranalyse.Gruppieren(roh);

        Assert.Equal(2, gruppen.Count);
    }

    [Fact]
    public void Gruppieren_SortiertKritischVorFehlerVorWarnung()
    {
        var jetzt = DateTime.Now;
        var roh = new[]
        {
            Eintrag("A", 1, FehlerSchweregrad.Warnung, jetzt),
            Eintrag("B", 2, FehlerSchweregrad.Kritisch, jetzt),
            Eintrag("C", 3, FehlerSchweregrad.Fehler, jetzt),
        };

        var gruppen = WindowsFehleranalyse.Gruppieren(roh);

        Assert.Equal(FehlerSchweregrad.Kritisch, gruppen[0].Schweregrad);
        Assert.Equal(FehlerSchweregrad.Fehler, gruppen[1].Schweregrad);
        Assert.Equal(FehlerSchweregrad.Warnung, gruppen[2].Schweregrad);
    }

    [Fact]
    public void Gruppieren_BeiGleichemSchweregrad_NeuesteZuerst()
    {
        var jetzt = DateTime.Now;
        var roh = new[]
        {
            Eintrag("Alt", 1, FehlerSchweregrad.Fehler, jetzt.AddDays(-2)),
            Eintrag("Neu", 2, FehlerSchweregrad.Fehler, jetzt),
        };

        var gruppen = WindowsFehleranalyse.Gruppieren(roh);

        Assert.Equal("Neu", gruppen[0].Anbieter);
        Assert.Equal("Alt", gruppen[1].Anbieter);
    }

    [Fact]
    public void Gruppieren_BeispielText_IstDerNeuesteEintragDerGruppe()
    {
        var jetzt = DateTime.Now;
        var roh = new[]
        {
            Eintrag("NetBT", 4321, FehlerSchweregrad.Fehler, jetzt.AddMinutes(-10), "alter Text"),
            Eintrag("NetBT", 4321, FehlerSchweregrad.Fehler, jetzt, "neuester Text"),
        };

        var gruppen = WindowsFehleranalyse.Gruppieren(roh);

        Assert.Equal("neuester Text", Assert.Single(gruppen).BeispielText);
    }

    [Fact]
    public void Gruppieren_LeereListe_GibtLeereListeZurueck()
    {
        var gruppen = WindowsFehleranalyse.Gruppieren(Array.Empty<FehlerRohEintrag>());

        Assert.Empty(gruppen);
    }
}
