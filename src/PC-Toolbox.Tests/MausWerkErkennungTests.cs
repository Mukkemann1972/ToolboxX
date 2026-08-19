using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class MausWerkErkennungTests
{
    [Theory]
    [InlineData(@"HID\VID_248A&PID_8366&COL01\7&13E27575&0&0000")]
    [InlineData(@"USB\VID_248A&PID_8366\6&D2C5702&0&22")]
    [InlineData(@"hid\vid_248a&pid_8366&col01\7&13e27575&0&0000")]
    public void ErkenntKompatibleIsyMaus(string geraeteId)
    {
        Assert.True(MausWerkErkennung.IstKompatibel(geraeteId));
    }

    [Theory]
    [InlineData(@"HID\VID_04D9&PID_8008\7&13E27575&0&0000")]
    [InlineData(@"USB\VID_046A&PID_0113&MI_00\8&19A20CD7&0&0000")]
    [InlineData("")]
    [InlineData(null)]
    public void ErkenntFremdeMausNicht(string? geraeteId)
    {
        Assert.False(MausWerkErkennung.IstKompatibel(geraeteId));
    }
}
