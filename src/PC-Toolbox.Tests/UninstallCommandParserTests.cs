using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class UninstallCommandParserTests
{
    [Fact]
    public void Zerlegen_QuotedPfadMitLeerzeichenUndArgumenten_TrenntKorrekt()
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen(
            @"""C:\Program Files\Firma Name\uninst.exe"" /S /norestart");

        Assert.Equal(@"C:\Program Files\Firma Name\uninst.exe", datei);
        Assert.Equal("/S /norestart", argumente);
    }

    [Fact]
    public void Zerlegen_QuotedPfadOhneArgumente_ArgumenteLeer()
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen(@"""C:\Tools\uninst.exe""");

        Assert.Equal(@"C:\Tools\uninst.exe", datei);
        Assert.Equal("", argumente);
    }

    [Fact]
    public void Zerlegen_UnquotedPfadMitArgument_TrenntAmErstenLeerzeichen()
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen(@"C:\Tools\uninst.exe /S");

        Assert.Equal(@"C:\Tools\uninst.exe", datei);
        Assert.Equal("/S", argumente);
    }

    [Fact]
    public void Zerlegen_UnquotedPfadOhneArgument_ArgumenteLeer()
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen(@"C:\Tools\uninst.exe");

        Assert.Equal(@"C:\Tools\uninst.exe", datei);
        Assert.Equal("", argumente);
    }

    [Fact]
    public void Zerlegen_MsiExecUninstallString_TrenntKorrekt()
    {
        // Typisches Format von MSI-Installationen aus der Registry.
        var (datei, argumente) = UninstallCommandParser.Zerlegen(
            @"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}");

        Assert.Equal("MsiExec.exe", datei);
        Assert.Equal("/X{12345678-1234-1234-1234-123456789012}", argumente);
    }

    [Fact]
    public void Zerlegen_LeererString_LiefertLeeresPaar()
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen("");

        Assert.Equal("", datei);
        Assert.Equal("", argumente);
    }

    [Fact]
    public void Zerlegen_NurWhitespace_LiefertLeeresPaar()
    {
        var (datei, argumente) = UninstallCommandParser.Zerlegen("   ");

        Assert.Equal("", datei);
        Assert.Equal("", argumente);
    }

    [Fact]
    public void Zerlegen_UnbalancierteAnfuehrungszeichen_FaelltAufUnquotedLogikZurueck()
    {
        // Fehlendes schliessendes Anfuehrungszeichen - darf nicht crashen, soll sinnvoll parsen.
        var (datei, argumente) = UninstallCommandParser.Zerlegen(@"""C:\Tools\uninst.exe /S");

        Assert.Equal(@"""C:\Tools\uninst.exe", datei);
        Assert.Equal("/S", argumente);
    }
}
