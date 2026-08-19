using PCToolboxCore;
using PCToolboxCore.Models;
using Xunit;

namespace PCToolboxCore.Tests;

public class DatentraegerTests
{
    private const long Gb = DateisystemRegeln.Gigabyte;

    // ================= Dateisystem-Regeln =================

    [Fact]
    public void Fat32_UeberDreissigZweiGb_WirdAbgelehntMitBegruendung()
    {
        var pruefung = DateisystemRegeln.Pruefen("FAT32", 64 * Gb);

        Assert.False(pruefung.Moeglich);
        Assert.Contains("32 GB", pruefung.Hinweis);
        Assert.Contains("exFAT", pruefung.Hinweis);
    }

    [Fact]
    public void Fat32_KleinerStick_GehtUndWarntVorDerVierGbGrenze()
    {
        var pruefung = DateisystemRegeln.Pruefen("FAT32", 16 * Gb);

        Assert.True(pruefung.Moeglich);
        // Genau die Grenze, ueber die man sonst erst Monate spaeter stolpert.
        Assert.Contains("4 GB", pruefung.Hinweis);
    }

    [Fact]
    public void Fat_UeberVierGb_WirdAbgelehnt()
    {
        Assert.False(DateisystemRegeln.Pruefen("FAT", 8 * Gb).Moeglich);
        Assert.True(DateisystemRegeln.Pruefen("FAT", 1440 * 1024).Moeglich); // Diskette
    }

    [Theory]
    [InlineData("NTFS")]
    [InlineData("exFAT")]
    public void GrosseFormate_GehenAuchBeiGrossenDatentraegern(string dateisystem)
    {
        Assert.True(DateisystemRegeln.Pruefen(dateisystem, 4000 * Gb).Moeglich);
    }

    [Fact]
    public void UnbekanntesFormat_WirdEhrlichAbgelehnt()
    {
        Assert.False(DateisystemRegeln.Pruefen("ext4", 100 * Gb).Moeglich);
    }

    [Theory]
    [InlineData(500, false, "NTFS")]   // eingebaute Platte
    [InlineData(64, true, "exFAT")]    // grosser Stick
    [InlineData(8, true, "FAT32")]     // kleiner Stick
    public void Empfehlung_PasstZumEinsatzzweck(int gb, bool wechsel, string erwartet)
    {
        Assert.Equal(erwartet, DateisystemRegeln.Empfehlung(gb * Gb, wechsel));
    }

    // ================= Schutzregeln =================

    [Fact]
    public void Systemlaufwerk_LaesstSichWederFormatierenNochInDerGroesseAendern()
    {
        var system = new DatentraegerVolume
        {
            Buchstabe = 'C', IstSystemlaufwerk = true, Dateisystem = "NTFS", GroesseBytes = 500 * Gb
        };

        Assert.False(DatentraegerSchutz.DarfFormatieren(system).Erlaubt);
        Assert.False(DatentraegerSchutz.DarfGroesseAendern(system).Erlaubt);
    }

    [Fact]
    public void LaufwerkMitDaten_DarfFormatiertWerden_AberMitDeutlicherWarnung()
    {
        var volume = new DatentraegerVolume
        {
            Buchstabe = 'E', GroesseBytes = 100 * Gb, FreiBytes = 40 * Gb, Dateisystem = "NTFS"
        };

        var urteil = DatentraegerSchutz.DarfFormatieren(volume);

        Assert.True(urteil.Erlaubt);
        Assert.Contains("unwiderruflich", urteil.Begruendung);
    }

    [Fact]
    public void LeeresLaufwerk_WirdAlsGefahrlosGekennzeichnet()
    {
        var volume = new DatentraegerVolume
        {
            Buchstabe = 'E', GroesseBytes = 100 * Gb, FreiBytes = 100 * Gb, Dateisystem = "NTFS"
        };

        Assert.Contains("gefahrlos", DatentraegerSchutz.DarfFormatieren(volume).Begruendung);
    }

    [Fact]
    public void GroesseAendern_NurBeiNtfs()
    {
        var exfat = new DatentraegerVolume { Buchstabe = 'E', Dateisystem = "exFAT", GroesseBytes = 64 * Gb };

        var urteil = DatentraegerSchutz.DarfGroesseAendern(exfat);

        Assert.False(urteil.Erlaubt);
        Assert.Contains("exFAT", urteil.Begruendung);
    }

    [Fact]
    public void GroessePruefen_AusserhalbDerGrenzen_WirdMitKlartextAbgelehnt()
    {
        Assert.False(DatentraegerSchutz.GroessePruefen(10 * Gb, 50 * Gb, 200 * Gb).Erlaubt);
        Assert.False(DatentraegerSchutz.GroessePruefen(500 * Gb, 50 * Gb, 200 * Gb).Erlaubt);
        Assert.True(DatentraegerSchutz.GroessePruefen(100 * Gb, 50 * Gb, 200 * Gb).Erlaubt);
    }

    // ================= Befehle =================

    [Fact]
    public void Formatieren_ErzeugtDenErwartetenBefehl()
    {
        var befehl = DatentraegerBefehle.Formatieren('e', "exFAT", "Sicherung", schnell: true);

        Assert.Equal("Format-Volume -DriveLetter E -FileSystem exFAT -NewFileSystemLabel 'Sicherung' " +
                     "-Force -Confirm:$false", befehl);
    }

    [Fact]
    public void Formatieren_VollstaendigeFormatierung_HaengtFullAn()
    {
        var befehl = DatentraegerBefehle.Formatieren('E', "NTFS", "Daten", schnell: false);

        Assert.Contains(" -Full ", befehl);
    }

    [Fact]
    public void Bezeichnung_MitAnfuehrungszeichen_WirdSicherEntwertet()
    {
        // "Alex' Platte" wuerde den Befehl sonst mittendrin aufbrechen.
        var befehl = DatentraegerBefehle.BezeichnungAendern('E', "Alex' Platte");

        Assert.Contains("'Alex'' Platte'", befehl);
    }

    [Fact]
    public void GroesseAendern_UebergibtBytesGenau()
    {
        Assert.Equal("Resize-Partition -DriveLetter D -Size 107374182400",
            DatentraegerBefehle.GroesseAendern('d', 100 * Gb));
    }

    [Fact]
    public void BuchstabeAendern_MachtBeideGross()
    {
        Assert.Equal("Set-Partition -DriveLetter E -NewDriveLetter X",
            DatentraegerBefehle.BuchstabeAendern('e', 'x'));
    }

    [Fact]
    public void AlsArgumente_LaeuftOhneProfilUndOhneRueckfragen()
    {
        var argumente = DatentraegerBefehle.AlsArgumente("Get-Volume");

        Assert.Contains("-NoProfile", argumente);
        Assert.Contains("-NonInteractive", argumente);
        Assert.Contains("Get-Volume", argumente);
    }

    [Fact]
    public void AlsArgumente_FehlerInDerPipelineFuehrtZuExit1()
    {
        // Kein Stilmittel: PowerShell.exe gibt bei einem Cmdlet-Fehler in einer Pipeline
        // sonst trotzdem Exit-Code 0 zurueck (s. Kommentar an AlsArgumente) - das try/catch
        // mit explizitem "exit 1" ist die Korrektur dafuer.
        var argumente = DatentraegerBefehle.AlsArgumente("Get-Volume");

        Assert.Contains("$ErrorActionPreference = 'Stop'", argumente);
        Assert.Contains("try {", argumente);
        Assert.Contains("exit 1", argumente);
    }
}
