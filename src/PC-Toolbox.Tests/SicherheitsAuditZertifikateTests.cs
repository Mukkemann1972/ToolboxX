using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SicherheitsAuditZertifikateTests
{
    [Fact]
    public void Zusammenfassen_GibtImmerUnbekannt_KeineAutomatischeWertung()
    {
        var zertifikate = new List<ZertifikatEintrag>
        {
            new("Microsoft", "Microsoft Root CA", DateTime.Now.AddYears(5)),
        };

        Assert.Equal(AmpelStatus.Unbekannt, SicherheitsAuditZertifikate.Zusammenfassen(zertifikate).Status);
    }

    [Fact]
    public void Zusammenfassen_LeereListe_NennNullImText()
    {
        var befund = SicherheitsAuditZertifikate.Zusammenfassen([]);

        Assert.Contains("0", befund.Text);
        Assert.Equal(AmpelStatus.Unbekannt, befund.Status);
    }

    [Fact]
    public void Zusammenfassen_NenntAnzahlImText()
    {
        var zertifikate = new List<ZertifikatEintrag>
        {
            new("A", "A", null),
            new("B", "B", null),
            new("C", "C", null),
        };

        Assert.Contains("3", SicherheitsAuditZertifikate.Zusammenfassen(zertifikate).Text);
    }
}
