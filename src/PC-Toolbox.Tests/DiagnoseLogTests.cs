using PCToolboxCore;
using Xunit;

namespace PCToolboxCore.Tests;

public class DiagnoseLogTests
{
    private static string NeuerTestPfad() =>
        Path.Combine(Path.GetTempPath(), $"toolboxx-log-test-{Guid.NewGuid()}.log");

    [Fact]
    public void Schreiben_LegtDateiUndVerzeichnisAnFallsNichtVorhanden()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"toolboxx-log-test-{Guid.NewGuid()}", "unterordner", "log.txt");
        try
        {
            DiagnoseLog.Schreiben("TestQuelle", "Testnachricht", pfad);

            Assert.True(File.Exists(pfad));
        }
        finally
        {
            var wurzel = Path.GetDirectoryName(Path.GetDirectoryName(pfad));
            if (wurzel is not null && Directory.Exists(wurzel)) Directory.Delete(wurzel, recursive: true);
        }
    }

    [Fact]
    public void Schreiben_EnthaeltQuelleUndNachricht()
    {
        var pfad = NeuerTestPfad();
        try
        {
            // Expliziter Pfad-Parameter, damit der Test isoliert bleibt statt im
            // echten %AppData% des Entwickler-PCs zu schreiben.
            DiagnoseLog.Schreiben("DashboardView", "Etwas ist schiefgelaufen", pfad);
            var inhalt = File.ReadAllText(pfad);

            Assert.Contains("DashboardView", inhalt);
            Assert.Contains("Etwas ist schiefgelaufen", inhalt);
        }
        finally
        {
            if (File.Exists(pfad)) File.Delete(pfad);
        }
    }

    [Fact]
    public void Schreiben_HaengtMehrereEintraegeAn()
    {
        var pfad = NeuerTestPfad();
        try
        {
            DiagnoseLog.Schreiben("Quelle1", "Erste Nachricht", pfad);
            DiagnoseLog.Schreiben("Quelle2", "Zweite Nachricht", pfad);
            var inhalt = File.ReadAllText(pfad);

            Assert.Contains("Erste Nachricht", inhalt);
            Assert.Contains("Zweite Nachricht", inhalt);
        }
        finally
        {
            if (File.Exists(pfad)) File.Delete(pfad);
        }
    }

    [Fact]
    public void Schreiben_KuerztDateiWennSieZuGrossWird()
    {
        var pfad = NeuerTestPfad();
        try
        {
            // Eine kuenstlich grosse Datei anlegen (deutlich ueber der 512-KB-Grenze),
            // damit der Kuerz-Pfad beim naechsten Schreiben wirklich greift.
            var grosserInhalt = new string('X', 600 * 1024);
            File.WriteAllText(pfad, grosserInhalt);

            DiagnoseLog.Schreiben("Quelle", "Neuer Eintrag nach dem Kuerzen", pfad);

            var inhalt = File.ReadAllText(pfad);
            Assert.True(inhalt.Length < grosserInhalt.Length, "Die Datei sollte nach dem Kuerzen kleiner sein.");
            Assert.Contains("Neuer Eintrag nach dem Kuerzen", inhalt);
        }
        finally
        {
            if (File.Exists(pfad)) File.Delete(pfad);
        }
    }

    [Fact]
    public void Schreiben_WirftNieEineException()
    {
        // Ein ungueltiger Pfad (z.B. verbotene Zeichen) darf die Aufrufer-App
        // nicht zusaetzlich zum Absturz bringen - Schreiben() muss das schlucken.
        var ungueltigerPfad = Path.Combine(Path.GetTempPath(), "toolboxx-log-test", "\0ungueltig.log");

        var exception = Record.Exception(() => DiagnoseLog.Schreiben("Quelle", "Nachricht", ungueltigerPfad));

        Assert.Null(exception);
    }
}
