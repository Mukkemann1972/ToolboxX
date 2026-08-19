using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class KarteileichenErkennungTests
{
    [Fact]
    public void IstMsiBasiert_MsiExecWirdErkannt()
    {
        Assert.True(KarteileichenErkennung.IstMsiBasiert(@"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}"));
    }

    [Fact]
    public void IstMsiBasiert_GrossKleinschreibungEgal()
    {
        Assert.True(KarteileichenErkennung.IstMsiBasiert(@"msiexec /I{abc}"));
    }

    [Fact]
    public void IstMsiBasiert_NormalerDeinstallerIstFalse()
    {
        Assert.False(KarteileichenErkennung.IstMsiBasiert(@"""C:\Program Files\App\uninstall.exe"" /S"));
    }

    [Fact]
    public void IstMsiBasiert_LeererStringIstFalse()
    {
        Assert.False(KarteileichenErkennung.IstMsiBasiert(""));
    }
}
