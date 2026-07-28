using PCToolboxCore;

namespace PCToolboxCore.Models;

public enum AppTheme
{
    DunkelNeon,
    Hell
}

// Alles, was der Nutzer in den Einstellungen waehlt. Bewusst schlank - nur
// Theme + Akzentfarbe + Sprache, kein wachsender Options-Wust (s. Grundsatz
// "keine komplizierten Settings").
public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.DunkelNeon;

    // Hex-Farbcode "#RRGGBB", Standard = das bisherige Cyan.
    public string AccentColor { get; set; } = "#22D3EE";

    // null = noch nie gespeichert -> beim allerersten Start anhand der
    // Systemsprache erkennen (s. SettingsService.Load), statt immer stur
    // Deutsch anzunehmen. Danach immer der zuletzt gewaehlte Wert.
    public Sprache? Sprache { get; set; }
}
