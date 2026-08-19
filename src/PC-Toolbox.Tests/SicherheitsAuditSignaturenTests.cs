using PCToolbox.Models;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SicherheitsAuditSignaturenTests
{
    [Fact]
    public void Auswerten_AlleGueltig_GibtGruen()
    {
        var apps = new List<AppSignatur>
        {
            new("A", @"C:\A\a.exe", SignaturErgebnis.GueltigSigniert),
            new("B", @"C:\B\b.exe", SignaturErgebnis.GueltigSigniert),
        };

        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditSignaturen.Auswerten(apps).Status);
    }

    [Fact]
    public void Auswerten_Unsigniert_GibtGelb()
    {
        var apps = new List<AppSignatur> { new("Tool", @"C:\Tool\tool.exe", SignaturErgebnis.Unsigniert) };

        Assert.Equal(AmpelStatus.Gelb, SicherheitsAuditSignaturen.Auswerten(apps).Status);
    }

    [Fact]
    public void Auswerten_UngueltigeSignatur_GibtRot()
    {
        var apps = new List<AppSignatur> { new("Verdaechtig", @"C:\X\x.exe", SignaturErgebnis.UngueltigSigniert) };

        Assert.Equal(AmpelStatus.Rot, SicherheitsAuditSignaturen.Auswerten(apps).Status);
    }

    [Fact]
    public void Auswerten_UngueltigUndUnsigniertGemischt_UngueltigGewinnt()
    {
        var apps = new List<AppSignatur>
        {
            new("Unsigniert", @"C:\A\a.exe", SignaturErgebnis.Unsigniert),
            new("Ungueltig", @"C:\B\b.exe", SignaturErgebnis.UngueltigSigniert),
        };

        Assert.Equal(AmpelStatus.Rot, SicherheitsAuditSignaturen.Auswerten(apps).Status);
    }

    [Fact]
    public void Auswerten_LeereListe_GibtGruen()
    {
        Assert.Equal(AmpelStatus.Gruen, SicherheitsAuditSignaturen.Auswerten([]).Status);
    }

    [Fact]
    public void Auswerten_NenntProgrammnamenImText()
    {
        var apps = new List<AppSignatur> { new("MeinTool", @"C:\a\a.exe", SignaturErgebnis.Unsigniert) };

        Assert.Contains("MeinTool", SicherheitsAuditSignaturen.Auswerten(apps).Text);
    }
}
