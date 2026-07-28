using PCToolboxCore;
using PCToolboxCore.Models;
using Xunit;

namespace PCToolboxCore.Tests;

public class ImDiskCommandBuilderTests
{
    [Fact]
    public void BuildErstellenArgumente_NtfsStandard_ErzeugtKorrekteArgumente()
    {
        var einstellungen = new RamDiskSettings { Laufwerksbuchstabe = 'R', GroesseMb = 512, AlsNtfsFormatieren = true };

        var argumente = ImDiskCommandBuilder.BuildErstellenArgumente(einstellungen);

        Assert.Equal("-a -s 512M -o awe -m R: -p \"/fs:ntfs /q /y\"", argumente);
    }

    [Fact]
    public void BuildErstellenArgumente_Fat32UndKleinbuchstabe_WirdGrossgeschriebenUndFat32Genutzt()
    {
        var einstellungen = new RamDiskSettings { Laufwerksbuchstabe = 'q', GroesseMb = 2048, AlsNtfsFormatieren = false };

        var argumente = ImDiskCommandBuilder.BuildErstellenArgumente(einstellungen);

        Assert.Equal("-a -s 2048M -o awe -m Q: -p \"/fs:fat32 /q /y\"", argumente);
    }

    [Fact]
    public void BuildEntfernenArgumente_Kleinbuchstabe_WirdGrossgeschrieben()
    {
        var argumente = ImDiskCommandBuilder.BuildEntfernenArgumente('r');

        Assert.Equal("-D -m R:", argumente);
    }
}
