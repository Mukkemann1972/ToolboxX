using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SicherheitsAuditExtensionsTests
{
    [Fact]
    public void WirktRiskant_MaechtigeBerechtigungPlusAlleSeiten_GibtTrue()
    {
        var eintrag = new ExtensionEintrag("X", "Chrome", "1.0", false, ["webRequest"], ["<all_urls>"]);

        Assert.True(SicherheitsAuditExtensions.WirktRiskant(eintrag));
    }

    [Fact]
    public void WirktRiskant_NurMaechtigeBerechtigungOhneAlleSeiten_GibtFalse()
    {
        var eintrag = new ExtensionEintrag("X", "Chrome", "1.0", false, ["webRequest"], ["https://example.com/*"]);

        Assert.False(SicherheitsAuditExtensions.WirktRiskant(eintrag));
    }

    [Fact]
    public void WirktRiskant_NurAlleSeitenOhneMaechtigeBerechtigung_GibtFalse()
    {
        var eintrag = new ExtensionEintrag("X", "Chrome", "1.0", false, ["storage"], ["<all_urls>"]);

        Assert.False(SicherheitsAuditExtensions.WirktRiskant(eintrag));
    }

    [Fact]
    public void Zusammenfassen_LeereListe_GibtGruen()
    {
        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditExtensions.Zusammenfassen([]).Status);
    }

    [Fact]
    public void Zusammenfassen_KeineRiskanten_GibtGruen()
    {
        var extensions = new List<ExtensionEintrag> { new("A", "Chrome", "1.0", false, ["storage"], []) };

        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditExtensions.Zusammenfassen(extensions).Status);
    }

    [Fact]
    public void Zusammenfassen_MitRiskanter_GibtGelb()
    {
        var extensions = new List<ExtensionEintrag>
        {
            new("A", "Chrome", "1.0", false, ["storage"], []),
            new("Riskant", "Edge", "2.0", false, ["proxy"], ["<all_urls>"]),
        };

        var befund = SicherheitsAuditExtensions.Zusammenfassen(extensions);
        Assert.Equal(AmpelStatus.Gelb, befund.Status);
        Assert.Contains("Riskant", befund.Text);
    }
}
