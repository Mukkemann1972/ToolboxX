using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SnapshotNamingTests
{
    [Fact]
    public void SicherenOrdnernamen_EntferntUngueltigeZeichen()
    {
        var ungueltig = new[] { ':', '\\', '/', '*', '?' };

        var ergebnis = SnapshotNaming.SicherenOrdnernamen(
            "Deinstallieren_Mein:Tool/v2*", ungueltig);

        Assert.Equal("Deinstallieren_MeinToolv2", ergebnis);
    }

    [Fact]
    public void SicherenOrdnernamen_OhneUngueltigeZeichen_BleibtUnveraendert()
    {
        var ergebnis = SnapshotNaming.SicherenOrdnernamen("Deinstallieren_Firefox", new[] { ':', '\\' });

        Assert.Equal("Deinstallieren_Firefox", ergebnis);
    }

    [Fact]
    public void SicherenOrdnernamen_LeererText_LiefertLeerenString()
    {
        var ergebnis = SnapshotNaming.SicherenOrdnernamen("", new[] { ':' });

        Assert.Equal("", ergebnis);
    }
}
