using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class UsbErklaerungenTests
{
    [Fact]
    public void EnergiesparmodusVerdacht_IstNieLeer()
    {
        Assert.False(string.IsNullOrWhiteSpace(UsbErklaerungen.EnergiesparmodusVerdacht()));
    }
}
