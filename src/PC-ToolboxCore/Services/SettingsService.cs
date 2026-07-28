using System.Text.Json;
using PCToolboxCore;
using PCToolboxCore.Models;

namespace PCToolboxCore.Services;

// Laedt/speichert AppSettings als JSON. Pfad ist optional uebergebbar (fuer
// Tests mit einer temporaeren Datei) - ohne Angabe landet's unter
// %AppData%\ToolboxX\settings.json.
public static class SettingsService
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ToolboxX", "settings.json");

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            return new AppSettings { Sprache = Loc.ErkenneAusSystemsprache() };

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            // Aeltere settings.json-Dateien (vor der Sprachumschaltung) kennen das Feld
            // noch nicht - dann ebenfalls anhand der Systemsprache erkennen statt stur
            // Deutsch anzunehmen, sonst bekaeme jeder Nutzer mit englischem Windows nach
            // einem Update ungefragt eine deutsche Toolbox.
            settings.Sprache ??= Loc.ErkenneAusSystemsprache();
            return settings;
        }
        catch
        {
            // Kaputte/fremd editierte Datei soll den Start nicht verhindern -
            // dann eben mit Standardwerten weiterlaufen.
            return new AppSettings { Sprache = Loc.ErkenneAusSystemsprache() };
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= DefaultPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    // Dunklere Variante einer Akzentfarbe fuers zweite Palette-Feld
    // (AccentDim, z.B. fuer Hover-Kontraste) - automatisch berechnet, damit
    // der Nutzer nur EINE Farbe waehlen muss statt zwei abzustimmen. Reiner
    // String-/Zahlen-Code (kein System.Drawing) - bleibt so auf jedem
    // Betriebssystem lauffaehig, auch im Pi-Test.
    public static string Darken(string hexColor, double factor = 0.55)
    {
        var hex = hexColor.TrimStart('#');
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        r = (byte) (r * factor);
        g = (byte) (g * factor);
        b = (byte) (b * factor);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
