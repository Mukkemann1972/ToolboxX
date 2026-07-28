using System.Globalization;

namespace PCToolboxCore;

public enum Sprache { Deutsch, Englisch }

/// <summary>
/// Zweisprachigkeit (Deutsch/Englisch) an EINER Stelle - nach dem Vorbild von
/// RetroTrax (github.com/Mukkemann1972/retrotrax, src/Localisation.h): jeder
/// sichtbare Text wird als <c>Loc.T("deutsch", "english")</c> geschrieben, so
/// steht jede Übersetzung direkt am Verwendungsort und kann nicht vergessen
/// werden. Ändert sich später ein deutscher Text (z.B. durch Testerfeedback),
/// trifft die Änderung sofort dieselbe Zeile - keine getrennte
/// Übersetzungsrunde nötig.
///
/// Ein Sprachwechsel braucht - wie der Theme-Wechsel in den Einstellungen -
/// einen Neustart. WPF-Views müssten sonst ihre komplette Text-Bindung live
/// neu aufbauen; für eine Diagnose-Toolbox ist der Neustart der einfachere,
/// robustere Weg und folgt derselben Konvention wie SettingsView.Apply_Click.
/// </summary>
public static class Loc
{
    public static Sprache Current { get; private set; } = Sprache.Deutsch;

    /// <summary>Beim Programmstart einmal mit der gespeicherten/erkannten Sprache aufrufen.</summary>
    public static void Load(Sprache sprache) => Current = sprache;

    /// <summary>
    /// Sprache für einen frischen ersten Start ohne gespeicherte Einstellung:
    /// richtet sich nach der Systemsprache, wie bei RetroTrax. Reine Logik
    /// ohne Registry-/WMI-Zugriff, deshalb auch auf dem Pi testbar.
    /// </summary>
    public static Sprache ErkenneAusSystemsprache(string kulturKuerzel) =>
        kulturKuerzel.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? Sprache.Deutsch : Sprache.Englisch;

    public static Sprache ErkenneAusSystemsprache() =>
        ErkenneAusSystemsprache(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static string T(string de, string en) => Current == Sprache.Deutsch ? de : en;
}
