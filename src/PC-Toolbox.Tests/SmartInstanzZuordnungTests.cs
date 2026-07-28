using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class SmartInstanzZuordnungTests
{
    // Echte Werte von Alex' PC (27.07.2026) - der Grund, warum es diese Klasse gibt.
    private const string InstanzAdata = @"SCSI\Disk&Ven_ADATA&Prod_SU650\5&10db0b5b&0&020000_0";
    private const string PnpAdata     = @"SCSI\DISK&VEN_ADATA&PROD_SU650\5&10DB0B5B&0&020000";

    private const string InstanzSeagate = @"SCSI\Disk&Ven_&Prod_ST2000DX002-2DV1\5&10db0b5b&0&030000_0";
    private const string PnpSeagate     = @"SCSI\DISK&VEN_&PROD_ST2000DX002-2DV1\5&10DB0B5B&0&030000";

    // Die NVMe und die USB-Platten melden kein SMART - duerfen also nie zugeordnet werden.
    private const string PnpNvme = @"SCSI\DISK&VEN_NVME&PROD_APS-SE20Q-2T\5&15B399C5&0&000000";
    private const string PnpUsb   = @"USBSTOR\DISK&VEN_EXTERNAL&PROD__USB3.0&REV_0301\0000007788FC&1";

    private static readonly string[] AlleLaufwerke = { PnpNvme, PnpAdata, PnpSeagate, PnpUsb };

    [Fact]
    public void Normalisieren_MachtGrossKleinUndZaehlerVergleichbar()
    {
        Assert.Equal(SmartInstanzZuordnung.Normalisieren(PnpAdata),
                     SmartInstanzZuordnung.Normalisieren(InstanzAdata));
    }

    [Fact]
    public void Normalisieren_SchneidetNurZiffernZaehlerAb()
    {
        Assert.Equal(@"SCSI\DISK&PROD_X\ABC", SmartInstanzZuordnung.Normalisieren(@"SCSI\Disk&Prod_X\abc_0"));
        Assert.Equal(@"SCSI\DISK&PROD_X\ABC", SmartInstanzZuordnung.Normalisieren(@"SCSI\Disk&Prod_X\abc_12"));
        // Kein reiner Ziffern-Anhang -> bleibt vollstaendig erhalten
        Assert.Equal(@"SCSI\DISK\ABC_TEIL2A", SmartInstanzZuordnung.Normalisieren(@"SCSI\Disk\abc_teil2a"));
    }

    [Fact]
    public void Normalisieren_LeerUndNullSindHarmlos()
    {
        Assert.Equal("", SmartInstanzZuordnung.Normalisieren(null));
        Assert.Equal("", SmartInstanzZuordnung.Normalisieren(""));
        Assert.Equal("", SmartInstanzZuordnung.Normalisieren("   "));
    }

    [Fact]
    public void FindePnpId_FindetDasRichtigeLaufwerk()
    {
        Assert.Equal(PnpAdata, SmartInstanzZuordnung.FindePnpId(InstanzAdata, AlleLaufwerke));
        Assert.Equal(PnpSeagate, SmartInstanzZuordnung.FindePnpId(InstanzSeagate, AlleLaufwerke));
    }

    [Fact]
    public void FindePnpId_VerwechseltDieBeidenSataPlattenNicht()
    {
        // Beide haengen am selben Controller (5&10db0b5b) und unterscheiden sich nur
        // in der letzten Zahlengruppe - genau hier wuerde eine schlampige Zuordnung
        // die Ausfallwarnung ans falsche Laufwerk haengen.
        Assert.NotEqual(SmartInstanzZuordnung.FindePnpId(InstanzAdata, AlleLaufwerke),
                        SmartInstanzZuordnung.FindePnpId(InstanzSeagate, AlleLaufwerke));
    }

    [Fact]
    public void FindePnpId_OhneEntsprechungLieberNichts()
    {
        // Ein Instanzname, den es auf diesem PC nicht gibt: es darf NICHT einfach
        // das erste Laufwerk genommen werden (das war der alte Fehler).
        var fremd = @"SCSI\Disk&Ven_FREMD&Prod_GIBTESNICHT\9&99999999&0&990000_0";
        Assert.Null(SmartInstanzZuordnung.FindePnpId(fremd, AlleLaufwerke));
    }

    [Fact]
    public void FindePnpId_LeereEingabeGibtNull()
    {
        Assert.Null(SmartInstanzZuordnung.FindePnpId(null, AlleLaufwerke));
        Assert.Null(SmartInstanzZuordnung.FindePnpId("", AlleLaufwerke));
        Assert.Null(SmartInstanzZuordnung.FindePnpId(InstanzAdata, Array.Empty<string>()));
    }

    [Fact]
    public void FindePnpId_FindetAuchBeiGekuerztemProduktnamen()
    {
        // Manche Treiber kuerzen den Produktnamen. Die geraetespezifische Kennung
        // hinter dem letzten Backslash bleibt aber gleich - darueber wird gefunden.
        var gekuerzt = @"SCSI\Disk&Ven_ADATA&Prod_SU6\5&10db0b5b&0&020000_0";
        Assert.Equal(PnpAdata, SmartInstanzZuordnung.FindePnpId(gekuerzt, AlleLaufwerke));
    }

    [Fact]
    public void FindePnpId_BeiMehrdeutigkeitLieberNichts()
    {
        // Zwei Laufwerke mit identischer Instanz-Kennung (konstruiert): dann ist
        // keine Zuordnung besser als eine geratene.
        var a = @"SCSI\DISK&VEN_A&PROD_X\5&AAAA&0&010000";
        var b = @"SCSI\DISK&VEN_B&PROD_Y\5&AAAA&0&010000";
        var instanz = @"SCSI\Disk&Ven_C&Prod_Z\5&aaaa&0&010000_0";
        Assert.Null(SmartInstanzZuordnung.FindePnpId(instanz, new[] { a, b }));
    }
}
