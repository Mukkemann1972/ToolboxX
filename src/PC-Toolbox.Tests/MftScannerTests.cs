using System.Text;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class MftScannerTests
{
    // Bewusst OHNE Fixup (usaLaengeWorte = 1, also 0 geschuetzte Sektoren) - die Fixup-Mechanik
    // selbst ist schon in MftDatensatzTests abgedeckt, hier geht es nur um "findet der Scanner
    // mehrere Datensaetze an verschiedenen Stellen im Datenstrom, auch ueber Blockgrenzen hinweg".
    private static byte[] BauDatensatz(string dateiname, uint eigeneRecordNummer, long elternRecord)
    {
        const int attributOffset = 0x38;
        var puffer = new byte[1024];
        var nameBytes = Encoding.Unicode.GetBytes(dateiname);

        "FILE"u8.ToArray().CopyTo(puffer, 0);
        BitConverter.GetBytes((ushort)0x30).CopyTo(puffer, 4); // USA-Offset (Inhalt irrelevant ohne Fixup)
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 6);    // usaLaengeWorte = 1 -> 0 geschuetzte Sektoren
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 18);
        BitConverter.GetBytes((ushort)attributOffset).CopyTo(puffer, 20);
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 22); // in Benutzung
        BitConverter.GetBytes(eigeneRecordNummer).CopyTo(puffer, 44);

        var inhaltLaenge = 0x42 + nameBytes.Length;
        var attributLaenge = 24 + inhaltLaenge;
        BitConverter.GetBytes((uint)0x30).CopyTo(puffer, attributOffset + 0);
        BitConverter.GetBytes((uint)attributLaenge).CopyTo(puffer, attributOffset + 4);
        BitConverter.GetBytes((uint)inhaltLaenge).CopyTo(puffer, attributOffset + 16);
        BitConverter.GetBytes((ushort)24).CopyTo(puffer, attributOffset + 20);

        var inhaltOffset = attributOffset + 24;
        BitConverter.GetBytes(elternRecord).CopyTo(puffer, inhaltOffset + 0x00);
        BitConverter.GetBytes((long)nameBytes.Length).CopyTo(puffer, inhaltOffset + 0x30);
        puffer[inhaltOffset + 0x40] = (byte)dateiname.Length;
        puffer[inhaltOffset + 0x41] = 1;
        nameBytes.CopyTo(puffer, inhaltOffset + 0x42);

        BitConverter.GetBytes(0xFFFFFFFF).CopyTo(puffer, attributOffset + attributLaenge);

        return puffer;
    }

    [Fact]
    public void NaechsterBlock_ZweiDatensaetzeHintereinander_BeideGefunden()
    {
        var scanner = new MftScanner();
        var block = BauDatensatz("eins.txt", 10, 5).Concat(BauDatensatz("zwei.txt", 11, 5)).ToArray();

        var funde = scanner.NaechsterBlock(block);

        Assert.Equal(2, funde.Count);
        Assert.Contains(funde, f => f.Dateiname == "eins.txt");
        Assert.Contains(funde, f => f.Dateiname == "zwei.txt");
    }

    [Fact]
    public void NaechsterBlock_DatensatzUeberBlockgrenzeHinweg_WirdImNaechstenBlockGefunden()
    {
        var scanner = new MftScanner();
        var datensatz = BauDatensatz("verteilt.txt", 20, 5);
        var teilungspunkt = 100;
        var ersterBlock = datensatz[..teilungspunkt];
        var zweiterBlock = datensatz[teilungspunkt..];

        var funde1 = scanner.NaechsterBlock(ersterBlock);
        Assert.Empty(funde1);

        var funde2 = scanner.NaechsterBlock(zweiterBlock);
        Assert.Single(funde2);
        Assert.Equal("verteilt.txt", funde2[0].Dateiname);
    }

    [Fact]
    public void NaechsterBlock_ZufaelligeBytesOhneSignatur_FindetNichts()
    {
        var scanner = new MftScanner();
        var funde = scanner.NaechsterBlock(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });

        Assert.Empty(funde);
    }
}

public class MftPfadAufloeserTests
{
    [Fact]
    public void PfadAufloesen_VollstaendigeKette_BautGanzenPfad()
    {
        var wurzel = new MftGefundeneDatei(5, 5, "Wurzel", 0, true, false);
        var ordner = new MftGefundeneDatei(6, 5, "Unterordner", 0, true, false);
        var datei = new MftGefundeneDatei(7, 6, "foto.jpg", 100, false, true);
        var alle = new Dictionary<long, MftGefundeneDatei> { [5] = wurzel, [6] = ordner, [7] = datei };

        var pfad = MftPfadAufloeser.PfadAufloesen(datei, alle);

        Assert.Equal(@"Unterordner\foto.jpg", pfad);
    }

    [Fact]
    public void PfadAufloesen_FehlenderElternDatensatz_LiefertNurDateiname()
    {
        var datei = new MftGefundeneDatei(7, 999, "verwaist.jpg", 100, false, true);
        var alle = new Dictionary<long, MftGefundeneDatei> { [7] = datei };

        var pfad = MftPfadAufloeser.PfadAufloesen(datei, alle);

        Assert.Equal("verwaist.jpg", pfad);
    }
}
