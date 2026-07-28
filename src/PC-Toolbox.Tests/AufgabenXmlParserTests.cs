using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class AufgabenXmlParserTests
{
    // Aufbau wie ihn Windows in C:\Windows\System32\Tasks ablegt (gekuerzt).
    //
    // Zeilenenden bewusst per ReplaceLineEndings auf \n normalisiert: die Tests unten
    // tauschen MEHRZEILIGE XML-Ausschnitte per Replace aus. Ohne Normalisierung haengt
    // das davon ab, ob die Datei mit LF (Pi/Linux) oder CRLF (Windows) ausgecheckt ist -
    // bei CRLF trifft ein Suchmuster mit \n nicht zu, der Austausch passiert
    // stillschweigend NICHT und der Test prueft am Ende das unveraenderte XML.
    // Genau daran sind zwei Tests auf Windows fehlgeschlagen, waehrend sie auf dem Pi
    // gruen waren. Fuer den Parser selbst sind Zeilenenden irrelevant (XML).
    private static readonly string AnmeldeAufgabe = """
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Author>Beispiel GmbH</Author>
            <Description>Hält Beispiel aktuell.</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>
            </LogonTrigger>
          </Triggers>
          <Settings>
            <Enabled>true</Enabled>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>C:\Program Files\Beispiel\update.exe</Command>
              <Arguments>/silent</Arguments>
            </Exec>
          </Actions>
        </Task>
        """.ReplaceLineEndings("\n");

    private static readonly string TaeglicheAufgabe = """
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <Triggers>
            <CalendarTrigger>
              <StartBoundary>2026-01-01T09:00:00</StartBoundary>
              <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>
            </CalendarTrigger>
          </Triggers>
          <Actions><Exec><Command>C:\Tools\taeglich.exe</Command></Exec></Actions>
        </Task>
        """.ReplaceLineEndings("\n");

    [Fact]
    public void Auswerten_AnmeldeAufgabe_WirdAlsAutostartErkannt()
    {
        var info = AufgabenXmlParser.Auswerten(AnmeldeAufgabe);

        Assert.NotNull(info);
        Assert.True(info!.StartetAutomatischMit);
        Assert.True(info.IstAktiviert);
        Assert.Equal(@"C:\Program Files\Beispiel\update.exe", info.Befehl);
        Assert.Equal("/silent", info.Argumente);
        Assert.Equal("Beispiel GmbH", info.Autor);
    }

    [Fact]
    public void Auswerten_TaeglicheAufgabe_IstKeinAutostart()
    {
        // Eine Aufgabe, die taeglich um 9 Uhr laeuft, gehoert nicht in ein Autostart-Modul -
        // sonst steht die Liste voll mit Dingen, die den Start gar nicht bremsen.
        var info = AufgabenXmlParser.Auswerten(TaeglicheAufgabe);

        Assert.NotNull(info);
        Assert.False(info!.StartetAutomatischMit);
    }

    [Fact]
    public void Auswerten_AbgeschalteterAusloeser_ZaehltNichtAlsAutostart()
    {
        var xml = AnmeldeAufgabe.Replace(
            "<LogonTrigger>\n      <Enabled>true</Enabled>",
            "<LogonTrigger>\n      <Enabled>false</Enabled>");

        var info = AufgabenXmlParser.Auswerten(xml);

        Assert.NotNull(info);
        Assert.False(info!.StartetAutomatischMit);
    }

    [Fact]
    public void Auswerten_AufgabeIstDeaktiviert_WirdErkannt()
    {
        var xml = AnmeldeAufgabe.Replace("<Settings>\n    <Enabled>true</Enabled>",
                                          "<Settings>\n    <Enabled>false</Enabled>");

        var info = AufgabenXmlParser.Auswerten(xml);

        Assert.NotNull(info);
        Assert.True(info!.StartetAutomatischMit);
        Assert.False(info.IstAktiviert);
    }

    [Fact]
    public void Auswerten_OhneNamensraum_FunktioniertTrotzdem()
    {
        // Aeltere Windows-Versionen schreiben die Datei ohne xmlns-Angabe.
        var xml = AnmeldeAufgabe.Replace(" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"", "");

        var info = AufgabenXmlParser.Auswerten(xml);

        Assert.NotNull(info);
        Assert.True(info!.StartetAutomatischMit);
    }

    [Fact]
    public void Auswerten_KaputteDatei_GibtNullStattAbsturz()
    {
        Assert.Null(AufgabenXmlParser.Auswerten("das ist kein XML"));
        Assert.Null(AufgabenXmlParser.Auswerten(""));
        Assert.Null(AufgabenXmlParser.Auswerten("<Etwas><Anderes /></Etwas>"));
    }
}
