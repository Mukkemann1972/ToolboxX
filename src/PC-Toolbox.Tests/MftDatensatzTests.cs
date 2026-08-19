using System.Text;
using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class MftDatensatzTests
{
    private const int UsaOffset = 0x30;
    private const int AttributOffset = 0x38;

    private static byte[] BauDatensatz(string dateiname, long groesse, long elternRecord, bool inBenutzung, bool istVerzeichnis = false, byte nameSpace = 1, uint eigeneRecordNummer = 100)
    {
        var puffer = new byte[1024];
        var nameBytes = Encoding.Unicode.GetBytes(dateiname);

        "FILE"u8.ToArray().CopyTo(puffer, 0);
        BitConverter.GetBytes((ushort)UsaOffset).CopyTo(puffer, 4);
        BitConverter.GetBytes((ushort)3).CopyTo(puffer, 6); // USN + 2 Sektoren (1024 Byte = 2x512)
        BitConverter.GetBytes(1L).CopyTo(puffer, 8);        // LSN, beliebig
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 16); // Sequenznummer
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 18); // Link-Count
        BitConverter.GetBytes((ushort)AttributOffset).CopyTo(puffer, 20);

        ushort flags = 0;
        if (inBenutzung) flags |= 0x1;
        if (istVerzeichnis) flags |= 0x2;
        BitConverter.GetBytes(flags).CopyTo(puffer, 22);
        BitConverter.GetBytes(1024).CopyTo(puffer, 24);
        BitConverter.GetBytes(1024).CopyTo(puffer, 28);
        BitConverter.GetBytes(eigeneRecordNummer).CopyTo(puffer, 44);

        // $FILE_NAME-Attribut ab AttributOffset
        var inhaltLaenge = 0x42 + nameBytes.Length;
        var attributLaenge = 24 + inhaltLaenge;
        BitConverter.GetBytes((uint)0x30).CopyTo(puffer, AttributOffset + 0);      // Typ
        BitConverter.GetBytes((uint)attributLaenge).CopyTo(puffer, AttributOffset + 4);
        puffer[AttributOffset + 8] = 0;   // resident
        puffer[AttributOffset + 9] = 0;   // Namenslaenge des Attributs selbst (nicht der Dateiname)
        BitConverter.GetBytes((uint)inhaltLaenge).CopyTo(puffer, AttributOffset + 16);
        BitConverter.GetBytes((ushort)24).CopyTo(puffer, AttributOffset + 20);     // Inhalt beginnt nach dem 24-Byte-Header

        var inhaltOffset = AttributOffset + 24;
        BitConverter.GetBytes(elternRecord).CopyTo(puffer, inhaltOffset + 0x00);
        BitConverter.GetBytes(groesse).CopyTo(puffer, inhaltOffset + 0x28);
        BitConverter.GetBytes(groesse).CopyTo(puffer, inhaltOffset + 0x30);
        puffer[inhaltOffset + 0x40] = (byte)dateiname.Length;
        puffer[inhaltOffset + 0x41] = nameSpace;
        nameBytes.CopyTo(puffer, inhaltOffset + 0x42);

        // Ende-Markierung direkt nach dem Attribut
        var endeOffset = AttributOffset + attributLaenge;
        BitConverter.GetBytes(0xFFFFFFFF).CopyTo(puffer, endeOffset);

        return puffer;
    }

    [Fact]
    public void Lesen_AktiveDatei_LiefertNameGroesseUndEltern()
    {
        var datensatz = BauDatensatz("urlaubsfoto.jpg", 54321, elternRecord: 42, inBenutzung: true);

        var eintrag = MftDatensatz.Lesen(datensatz);

        Assert.NotNull(eintrag);
        Assert.Equal("urlaubsfoto.jpg", eintrag!.Dateiname);
        Assert.Equal(54321, eintrag.GroesseBytes);
        Assert.Equal(42, eintrag.ElternRecordNummer);
        Assert.Equal(100, eintrag.RecordNummer);
        Assert.False(eintrag.IstGeloescht);
        Assert.False(eintrag.IstVerzeichnis);
    }

    [Fact]
    public void Lesen_NichtInBenutzung_IstAlsGeloeschtMarkiert()
    {
        var datensatz = BauDatensatz("geloeschte-datei.docx", 100, elternRecord: 5, inBenutzung: false);

        var eintrag = MftDatensatz.Lesen(datensatz);

        Assert.NotNull(eintrag);
        Assert.True(eintrag!.IstGeloescht);
    }

    [Fact]
    public void Lesen_Verzeichnis_IstAlsVerzeichnisMarkiert()
    {
        var datensatz = BauDatensatz("Unterordner", 0, elternRecord: 5, inBenutzung: true, istVerzeichnis: true);

        var eintrag = MftDatensatz.Lesen(datensatz);

        Assert.NotNull(eintrag);
        Assert.True(eintrag!.IstVerzeichnis);
    }

    [Fact]
    public void Lesen_UngueltigeSignatur_GibtNullZurueck()
    {
        var datensatz = new byte[1024];
        "BAAD"u8.ToArray().CopyTo(datensatz, 0);

        Assert.Null(MftDatensatz.Lesen(datensatz));
    }

    [Fact]
    public void Lesen_LeererDatensatz_GibtNullZurueck()
    {
        Assert.Null(MftDatensatz.Lesen(new byte[1024]));
    }

    [Fact]
    public void Lesen_BevorzugtNamensraumUngleichDos()
    {
        // Erst ein DOS-Kurzname (Namespace 2), danach der "richtige" Langname (Namespace 1) -
        // das Ergebnis soll den Langnamen liefern, nicht den kurzen.
        var puffer = new byte[1024];
        "FILE"u8.ToArray().CopyTo(puffer, 0);
        BitConverter.GetBytes((ushort)UsaOffset).CopyTo(puffer, 4);
        BitConverter.GetBytes((ushort)3).CopyTo(puffer, 6);
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 18);
        BitConverter.GetBytes((ushort)AttributOffset).CopyTo(puffer, 20);
        BitConverter.GetBytes((ushort)1).CopyTo(puffer, 22);
        BitConverter.GetBytes((uint)1).CopyTo(puffer, 44);

        var offset = AttributOffset;
        offset = SchreibeFileNameAttribut(puffer, offset, "URLAUB~1.JPG", 2, elternRecord: 5, groesse: 10);
        offset = SchreibeFileNameAttribut(puffer, offset, "urlaubsfoto-lang.jpg", 1, elternRecord: 5, groesse: 10);
        BitConverter.GetBytes(0xFFFFFFFF).CopyTo(puffer, offset);

        var eintrag = MftDatensatz.Lesen(puffer);

        Assert.NotNull(eintrag);
        Assert.Equal("urlaubsfoto-lang.jpg", eintrag!.Dateiname);
    }

    private static int SchreibeFileNameAttribut(byte[] puffer, int offset, string name, byte nameSpace, long elternRecord, long groesse)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name);
        var inhaltLaenge = 0x42 + nameBytes.Length;
        var attributLaenge = 24 + inhaltLaenge;

        BitConverter.GetBytes((uint)0x30).CopyTo(puffer, offset + 0);
        BitConverter.GetBytes((uint)attributLaenge).CopyTo(puffer, offset + 4);
        puffer[offset + 8] = 0;
        BitConverter.GetBytes((uint)inhaltLaenge).CopyTo(puffer, offset + 16);
        BitConverter.GetBytes((ushort)24).CopyTo(puffer, offset + 20);

        var inhaltOffset = offset + 24;
        BitConverter.GetBytes(elternRecord).CopyTo(puffer, inhaltOffset + 0x00);
        BitConverter.GetBytes(groesse).CopyTo(puffer, inhaltOffset + 0x30);
        puffer[inhaltOffset + 0x40] = (byte)name.Length;
        puffer[inhaltOffset + 0x41] = nameSpace;
        nameBytes.CopyTo(puffer, inhaltOffset + 0x42);

        return offset + attributLaenge;
    }

    [Fact]
    public void FixupAnwenden_StelltOriginalSektorEndenWiederHer()
    {
        var original = BauDatensatz("test.txt", 5, elternRecord: 5, inBenutzung: true);
        // Die "echten" Original-Byte an den beiden Sektorenden (512-Byte-Grenzen) markieren.
        original[510] = 0xAA; original[511] = 0xBB;
        original[1022] = 0xCC; original[1023] = 0xDD;

        var aufDerPlatte = (byte[])original.Clone();
        // USA schreiben: Pruefnummer + die beiden echten Original-Werte.
        aufDerPlatte[UsaOffset] = 0xF0; aufDerPlatte[UsaOffset + 1] = 0x0D;
        aufDerPlatte[UsaOffset + 2] = 0xAA; aufDerPlatte[UsaOffset + 3] = 0xBB;
        aufDerPlatte[UsaOffset + 4] = 0xCC; aufDerPlatte[UsaOffset + 5] = 0xDD;
        // Und die Sektorenden durch die Pruefnummer "verunreinigen" (so wie NTFS es tatsaechlich tut).
        aufDerPlatte[510] = 0xF0; aufDerPlatte[511] = 0x0D;
        aufDerPlatte[1022] = 0xF0; aufDerPlatte[1023] = 0x0D;

        var bereinigt = MftDatensatz.FixupAnwenden(aufDerPlatte);

        Assert.NotNull(bereinigt);
        Assert.Equal(0xAA, bereinigt![510]);
        Assert.Equal(0xBB, bereinigt[511]);
        Assert.Equal(0xCC, bereinigt[1022]);
        Assert.Equal(0xDD, bereinigt[1023]);
    }

    [Fact]
    public void FixupAnwenden_UngueltigeSignatur_GibtNullZurueck()
    {
        Assert.Null(MftDatensatz.FixupAnwenden(new byte[1024]));
    }
}
