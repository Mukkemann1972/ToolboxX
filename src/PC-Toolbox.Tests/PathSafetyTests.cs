using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class PathSafetyTests
{
    private static readonly string[] GeschuetzteWurzeln =
    {
        @"C:\Windows",
        @"C:\Program Files\Common Files",
    };

    private static readonly string[] AllgemeineWurzeln =
    {
        @"C:\Program Files",
        @"C:\ProgramData",
    };

    [Theory]
    [InlineData(@"C:\Windows\System32", true)]
    [InlineData(@"C:\Windows", true)]
    [InlineData(@"C:\Program Files\Common Files\Microsoft Shared", true)]
    [InlineData(@"C:\Program Files\MeinTool", false)]
    [InlineData(@"C:\Users\Alex\AppData\Local\MeinTool", false)]
    public void IstGeschuetzterPfad_ErkenntGeschuetzteWurzelnKorrekt(string pfad, bool erwartet)
    {
        Assert.Equal(erwartet, PathSafety.IstGeschuetzterPfad(pfad, GeschuetzteWurzeln));
    }

    [Theory]
    [InlineData(@"C:\Program Files", true)]
    [InlineData(@"C:\Program Files\", true)] // Trailing Backslash darf keinen Unterschied machen
    [InlineData(@"C:\ProgramData", true)]
    [InlineData(@"C:", true)]
    [InlineData(@"C:\", true)]
    [InlineData(@"C:\Program Files\MeinTool", false)]
    public void IstZuAllgemeinerPfad_ErkenntWurzelnUndLaufwerksWurzelnKorrekt(string pfad, bool erwartet)
    {
        Assert.Equal(erwartet, PathSafety.IstZuAllgemeinerPfad(pfad, AllgemeineWurzeln));
    }

    [Theory]
    [InlineData(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)]
    [InlineData(@"HKLM\SOFTWARE\Microsoft", true)]
    [InlineData(@"HKCU\SOFTWARE\MeineFirma\MeinTool", false)]
    public void IstGeschuetzterRegistryPfad_ErkenntMicrosoftWindowsPfade(string pfad, bool erwartet)
    {
        Assert.Equal(erwartet, PathSafety.IstGeschuetzterRegistryPfad(pfad));
    }
}
