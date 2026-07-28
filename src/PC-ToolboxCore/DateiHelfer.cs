using System.Globalization;

namespace PCToolboxCore;

/// <summary>
/// Kleine, gut testbare Bausteine für den Dateimanager. Bewusst alle ohne echten
/// Dateizugriff: Wo etwas über das Dateisystem wissen muss, wird die Prüfung als
/// Funktion hereingereicht. So läuft das hier auch auf dem Pi als echter Test.
/// </summary>
public static class DateiHelfer
{
    private static readonly string[] Einheiten = ["Byte", "KB", "MB", "GB", "TB"];

    /// <summary>Größe in einer Form, die man beim Draufschauen sofort einordnen kann.</summary>
    public static string GroesseFormatieren(long bytes)
    {
        if (bytes < 0) return "";
        if (bytes < 1024) return $"{bytes} Byte";

        double wert = bytes;
        var stufe = 0;

        while (wert >= 1024 && stufe < Einheiten.Length - 1)
        {
            wert /= 1024;
            stufe++;
        }

        // Ab GB eine Nachkommastelle mehr - dort macht der Unterschied noch etwas aus.
        var nachkomma = stufe >= 3 ? 2 : 1;
        return wert.ToString($"N{nachkomma}", CultureInfo.CurrentCulture) + " " + Einheiten[stufe];
    }

    /// <summary>
    /// Der Ordner eine Ebene höher. Null, wenn man schon ganz oben ist (Laufwerkswurzel
    /// oder Netzwerkfreigabe) - dann gibt es kein "höher" mehr und der Knopf gehört
    /// abgeschaltet.
    ///
    /// Bewusst reine Textarbeit statt Directory.GetParent: Die .NET-Methode richtet sich
    /// nach dem Betriebssystem, unter dem sie läuft. Auf dem Pi (Linux) hält sie
    /// "C:\Users\Alex" für einen relativen Pfad und hängt ihn ans Arbeitsverzeichnis -
    /// der Test dazu ist genau darüber gestolpert. Windows-Pfade müssen hier also
    /// unabhängig vom laufenden System behandelt werden.
    /// </summary>
    public static string? EineEbeneHoeher(string? pfad)
    {
        var text = Normalisieren(pfad);
        if (text.Length == 0) return null;

        // Netzwerkfreigabe \\Server\Freigabe ist selbst schon die oberste Ebene.
        if (text.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var teile = text[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (teile.Length <= 2) return null;
        }

        // "C:" (aus "C:\") - oberste Ebene eines Laufwerks.
        if (text.EndsWith(':')) return null;

        var letzterTrenner = text.LastIndexOf('\\');
        if (letzterTrenner < 0) return null;

        var eltern = text[..letzterTrenner];

        // Aus "C:\Users" wird "C:" - für Windows muss daraus wieder "C:\" werden.
        return eltern.EndsWith(':') ? eltern + '\\' : eltern;
    }

    /// <summary>
    /// Findet einen freien Namen, wenn am Ziel schon etwas gleichnamiges liegt:
    /// aus "Urlaub.jpg" wird "Urlaub (2).jpg", dann "Urlaub (3).jpg" und so weiter.
    ///
    /// Bewusst so und nicht "überschreiben?" fragen: Beim Kopieren vieler Dateien will
    /// niemand 40 Rückfragen beantworten - und überschriebene Dateien sind endgültig weg,
    /// das widerspricht dem Sicherheitsprinzip der Toolbox.
    /// </summary>
    public static string FreierZielName(string zielPfad, Func<string, bool> existiert)
    {
        if (!existiert(zielPfad)) return zielPfad;

        var ordner = Path.GetDirectoryName(zielPfad) ?? "";
        var name = Path.GetFileNameWithoutExtension(zielPfad);
        var endung = Path.GetExtension(zielPfad);

        for (var nummer = 2; nummer < 10000; nummer++)
        {
            var kandidat = Path.Combine(ordner, $"{name} ({nummer}){endung}");
            if (!existiert(kandidat)) return kandidat;
        }

        // Praktisch unerreichbar - aber lieber ein eindeutiger Name als eine Endlosschleife.
        return Path.Combine(ordner, $"{name} ({Guid.NewGuid():N}){endung}");
    }

    /// <summary>
    /// Verhindert den Klassiker: einen Ordner in sich selbst (oder in einen eigenen
    /// Unterordner) kopieren/verschieben. Das erzeugt sonst eine Endlosschleife, die
    /// erst aufhört, wenn die Platte voll ist.
    /// </summary>
    public static bool ZielLiegtInQuelle(string quellOrdner, string zielOrdner)
    {
        var quelle = Normalisieren(quellOrdner);
        var ziel = Normalisieren(zielOrdner);

        if (quelle.Length == 0) return false;

        return ziel.Equals(quelle, StringComparison.OrdinalIgnoreCase) ||
               ziel.StartsWith(quelle + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalisieren(string? pfad) =>
        (pfad ?? "").Trim().Replace('/', '\\').TrimEnd('\\');
}
