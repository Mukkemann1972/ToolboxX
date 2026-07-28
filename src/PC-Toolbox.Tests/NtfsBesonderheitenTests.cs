using System.IO;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class NtfsBesonderheitenTests
{
    [Fact]
    public void IstReparsePoint_ErkenntGesetztesFlag()
    {
        Assert.True(NtfsBesonderheiten.IstReparsePoint(FileAttributes.ReparsePoint));
        Assert.True(NtfsBesonderheiten.IstReparsePoint(FileAttributes.Directory | FileAttributes.ReparsePoint));
    }

    [Fact]
    public void IstReparsePoint_VerneintFehlendesFlag()
    {
        Assert.False(NtfsBesonderheiten.IstReparsePoint(FileAttributes.Directory));
        Assert.False(NtfsBesonderheiten.IstReparsePoint(FileAttributes.Normal));
    }

    [Theory]
    [InlineData(@"C:\Users\Alex\Downloads\setup.exe", @"C:\Users\Alex\Downloads\setup.exe:Zone.Identifier")]
    [InlineData(@"C:\Datei ohne Endung", @"C:\Datei ohne Endung:Zone.Identifier")]
    public void ZoneIdentifierPfad_HaengtStreamnamenAn(string dateiPfad, string erwartet)
    {
        Assert.Equal(erwartet, NtfsBesonderheiten.ZoneIdentifierPfad(dateiPfad));
    }

    [Fact]
    public void Erklaerungen_SindNieLeer()
    {
        Assert.False(string.IsNullOrWhiteSpace(NtfsBesonderheiten.JunctionErklaerung()));
        Assert.False(string.IsNullOrWhiteSpace(NtfsBesonderheiten.HerkunftsmarkierungErklaerung()));
    }
}
