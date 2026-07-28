using System.Text;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class MinidumpModuleListParserTests
{
    // Baut eine minimale, aber format-korrekte MINIDUMP-Datei von Hand (Header + Stream-
    // Verzeichnis mit einem ModuleListStream + zwei Modul-Eintraegen samt Namens-Strings) -
    // damit die Parser-Logik ohne einen echten Windows-Absturz getestet werden kann.
    private static byte[] BaueTestDump((ulong Basis, uint Groesse, string Name)[] module)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // --- Header (32 Byte) ---
        w.Write(0x504D444Du);      // Signatur "MDMP"
        w.Write(0u);               // Version (irrelevant fuer den Parser)
        w.Write(1u);               // NumberOfStreams
        w.Write(32u);              // StreamDirectoryRva - direkt nach dem Header
        w.Write(0u);               // CheckSum
        w.Write(0u);               // TimeDateStamp
        w.Write(0UL);              // Flags

        // --- Stream-Verzeichnis (1 Eintrag, 12 Byte) ---
        var modulListeRva = 32u + 12u; // direkt nach dem einen Verzeichnis-Eintrag
        w.Write(4u);               // StreamType = ModuleListStream
        w.Write(0u);               // DataSize (Parser nutzt das nicht)
        w.Write(modulListeRva);    // Rva

        // --- ModuleListStream: NumberOfModules + Modul-Eintraege (108 Byte je Eintrag) ---
        w.Write((uint)module.Length);

        // Namens-Strings kommen NACH allen Modul-Eintraegen; Positionen vorab berechnen.
        var stringBasis = modulListeRva + 4 + (uint)(module.Length * 108);
        var stringOffsets = new uint[module.Length];
        uint laufend = stringBasis;
        for (int i = 0; i < module.Length; i++)
        {
            stringOffsets[i] = laufend;
            laufend += 4 + (uint)(module[i].Name.Length * 2); // MINIDUMP_STRING: Laenge(4) + UTF16
        }

        for (int i = 0; i < module.Length; i++)
        {
            w.Write(module[i].Basis);      // BaseOfImage
            w.Write(module[i].Groesse);    // SizeOfImage
            w.Write(0u);                   // CheckSum
            w.Write(0u);                   // TimeDateStamp
            w.Write(stringOffsets[i]);     // ModuleNameRva
            w.Write(new byte[108 - 24]);   // Rest des 108-Byte-Eintrags (VersionInfo/CvRecord/MiscRecord/Reserved)
        }

        for (int i = 0; i < module.Length; i++)
        {
            var bytes = Encoding.Unicode.GetBytes(module[i].Name);
            w.Write((uint)bytes.Length);
            w.Write(bytes);
        }

        w.Flush();
        return ms.ToArray();
    }

    [Fact]
    public void Parse_LiestModuleMitBasisAdresseGroesseUndNamen()
    {
        var dump = BaueTestDump(
        [
            (0x1000_0000, 0x2000, @"C:\Windows\System32\drivers\testdriver.sys"),
            (0x2000_0000, 0x1000, "other.sys"),
        ]);

        var module = MinidumpModuleListParser.Parse(dump);

        Assert.Equal(2, module.Count);
        Assert.Equal("testdriver.sys", module[0].Name); // nur der Dateiname, ohne Pfad
        Assert.Equal(0x1000_0000UL, module[0].BasisAdresse);
        Assert.Equal(0x2000u, module[0].Groesse);
        Assert.Equal("other.sys", module[1].Name);
    }

    [Fact]
    public void Parse_LiefertLeereListeBeiFalscherSignatur()
    {
        var kaputterDump = new byte[64]; // alles Nullen, keine "MDMP"-Signatur

        var module = MinidumpModuleListParser.Parse(kaputterDump);

        Assert.Empty(module);
    }

    [Fact]
    public void Parse_LiefertLeereListeBeiZuKurzerDatei()
    {
        var module = MinidumpModuleListParser.Parse(new byte[10]);

        Assert.Empty(module);
    }

    [Fact]
    public void Enthaelt_ErkenntAdresseInnerhalbUndAusserhalbDesModulBereichs()
    {
        var modul = new MinidumpModul(0x1000, 0x100, "test.sys");

        Assert.True(modul.Enthaelt(0x1000));       // Untergrenze
        Assert.True(modul.Enthaelt(0x10FF));       // innerhalb
        Assert.False(modul.Enthaelt(0x1100));      // Obergrenze (exklusiv)
        Assert.False(modul.Enthaelt(0xFFF));       // davor
    }

    [Fact]
    public void FindeWahrscheinlichenVerursacher_NimmtErstenTreffer()
    {
        var module = new List<MinidumpModul>
        {
            new(0x1000, 0x100, "erstes.sys"),
            new(0x5000, 0x100, "zweites.sys"),
        };

        // Erster Kandidat (0) faellt in keinen Bereich, zweiter (0x5050) trifft "zweites.sys".
        var treffer = MinidumpModuleListParser.FindeWahrscheinlichenVerursacher(module, [0UL, 0x5050UL, 0x1050UL]);

        Assert.Equal("zweites.sys", treffer);
    }

    [Fact]
    public void FindeWahrscheinlichenVerursacher_GibtNullZurueckWennNichtsPasst()
    {
        var module = new List<MinidumpModul> { new(0x1000, 0x100, "test.sys") };

        var treffer = MinidumpModuleListParser.FindeWahrscheinlichenVerursacher(module, [0xDEAD_BEEFUL]);

        Assert.Null(treffer);
    }
}
