using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class BefehlszeileZerlegerTests
{
    // Tut so, als gaebe es genau diese Dateien auf dem PC.
    private static Func<string, bool> Vorhanden(params string[] dateien) =>
        pfad => dateien.Contains(pfad, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void MitAnfuehrungszeichen_LiefertNurDenPfad()
    {
        var pfad = BefehlszeileZerleger.ProgrammPfadErmitteln(
            "\"C:\\Program Files\\Firma\\tool.exe\" -minimized", Vorhanden());

        Assert.Equal(@"C:\Program Files\Firma\tool.exe", pfad);
    }

    [Fact]
    public void OhneAnfuehrungszeichenMitLeerzeichenImPfad_FindetTrotzdemDieRichtigeDatei()
    {
        // Der eigentliche Knackpunkt: naives Trennen am ersten Leerzeichen ergaebe
        // "C:\Program" - und damit einen falschen "Datei fehlt"-Alarm.
        var pfad = BefehlszeileZerleger.ProgrammPfadErmitteln(
            @"C:\Program Files\Firma\tool.exe -minimized",
            Vorhanden(@"C:\Program Files\Firma\tool.exe"));

        Assert.Equal(@"C:\Program Files\Firma\tool.exe", pfad);
    }

    [Fact]
    public void OhneEndung_ErgaenztExe()
    {
        var pfad = BefehlszeileZerleger.ProgrammPfadErmitteln(
            @"C:\Tools\start /schnell",
            Vorhanden(@"C:\Tools\start.exe"));

        Assert.Equal(@"C:\Tools\start.exe", pfad);
    }

    [Fact]
    public void OhneParameter_LiefertDenGanzenPfad()
    {
        var pfad = BefehlszeileZerleger.ProgrammPfadErmitteln(
            @"C:\Program Files\Firma\tool.exe",
            Vorhanden(@"C:\Program Files\Firma\tool.exe"));

        Assert.Equal(@"C:\Program Files\Firma\tool.exe", pfad);
    }

    [Fact]
    public void NichtsGefunden_LiefertDenTeilVorDemErstenLeerzeichen()
    {
        var pfad = BefehlszeileZerleger.ProgrammPfadErmitteln(
            @"C:\Weg\altesprogramm.exe /still", Vorhanden());

        Assert.Equal(@"C:\Weg\altesprogramm.exe", pfad);
    }

    [Fact]
    public void LeerOderNull_GibtLeerenTextStattAbsturz()
    {
        Assert.Equal("", BefehlszeileZerleger.ProgrammPfadErmitteln(null, Vorhanden()));
        Assert.Equal("", BefehlszeileZerleger.ProgrammPfadErmitteln("   ", Vorhanden()));
    }
}
