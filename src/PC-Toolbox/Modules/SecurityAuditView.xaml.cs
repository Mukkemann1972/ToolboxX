using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

/// <summary>
/// Security-Audit (Free): sechs rein lesende Checks - "Es schaut, nichts wird angefasst"
/// (Alex' Formulierung, 19.08.2026, Konzept/Recherche von Hermes). Kein Admin nötig (bis auf
/// die ehrliche Einschränkung beim Ereignisprotokoll "Sicherheit", s. dort). Keine Cloud-
/// Anfragen, keine automatische Reparatur - reine Bestandsaufnahme mit Erklärung.
/// </summary>
public partial class SecurityAuditView : UserControl
{
    private readonly SecurityAuditService _dienst = new();

    public SecurityAuditView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("🛡️ Security-Audit", "🛡️ Security Audit");
        EinleitungText.Text = Loc.T(
            "Sechs rein lesende Sicherheits-Checks: Virenschutz-Status, auffällige Ereignisse im " +
            "Protokoll, unsignierte Programme, vertrauenswürdige Zertifikate, Browser-Erweiterungen " +
            "und Autostart-Sicherheitshinweise. Es wird nichts verändert, nichts an eine Cloud " +
            "gesendet - nur angezeigt und erklärt.",
            "Six purely read-only security checks: antivirus status, unusual events in the log, " +
            "unsigned programs, trusted certificates, browser extensions, and autostart security " +
            "hints. Nothing is changed, nothing is sent to a cloud - only shown and explained.");

        PruefenButton.Content = Loc.T("Jetzt prüfen", "Check now");
        GesamtbildText.Text = "";

        HinweisText.Text = Loc.T(
            "Ein lokaler Check findet keine gezielten Angriffe - er zeigt Zustände, keine Beweise. " +
            "Wer einen konkreten Verdacht hat, ist bei einem Repair-Café oder einer Fachwerkstatt " +
            "besser aufgehoben als bei einer automatischen Prüfung.",
            "A local check doesn't detect targeted attacks - it shows states, not proof. If you have " +
            "a concrete suspicion, a repair café or a professional shop can help more than an " +
            "automated check.");
    }

    private async void Pruefen_Click(object sender, RoutedEventArgs e)
    {
        PruefenButton.IsEnabled = false;
        FortschrittBalken.Visibility = Visibility.Visible;
        GesamtbildText.Text = "";
        ErgebnisBereich.Children.Clear();

        var befunde = new List<SicherheitsBefund>();

        // 1. Defender
        var (avAktiv, echtzeit, sigAlter, manipSchutz) = await _dienst.DefenderStatusAsync();
        befunde.Add(SicherheitsAuditDefender.Bewerten(avAktiv, echtzeit, sigAlter, manipSchutz));

        // 2. Ereignis-Anomalien
        var ereignisse = await _dienst.EreignisseAsync();
        var zugriffVerweigert = false;
        foreach (var (kategorie, (eintraege, verweigert)) in ereignisse)
        {
            if (verweigert) { zugriffVerweigert = true; continue; }
            befunde.Add(SicherheitsAuditEreignisse.Bewerten(kategorie, eintraege));
        }
        if (zugriffVerweigert)
            befunde.Add(new SicherheitsBefund(
                Loc.T("Sicherheits-Ereignisprotokoll", "Security event log"),
                Loc.T(
                    "Fehlgeschlagene Anmeldungen, neue Konten und gelöschte Protokolle konnten ohne " +
                    "Administratorrechte nicht gelesen werden - Windows schützt dieses Protokoll " +
                    "besonders. Toolbox X als Administrator starten, um diese drei Checks mit " +
                    "einzubeziehen.",
                    "Failed logons, new accounts, and cleared logs could not be read without " +
                    "administrator rights - Windows protects this log especially. Start Toolbox X as " +
                    "administrator to include these three checks."),
                AmpelStatus.Unbekannt));

        // 3. Signaturen
        var signaturen = await _dienst.SignaturenAsync();
        befunde.Add(SicherheitsAuditSignaturen.Auswerten(signaturen));

        // 4. Zertifikate
        var zertifikate = await _dienst.ZertifikateAsync();
        befunde.Add(SicherheitsAuditZertifikate.Zusammenfassen(zertifikate));

        // 5. Browser-Extensions
        var extensions = await _dienst.BrowserExtensionsAsync();
        befunde.Add(SicherheitsAuditExtensions.Zusammenfassen(extensions));

        // 6. Autostart (wiederverwendet)
        var autostartBefunde = await _dienst.AutostartHinweiseAsync();
        befunde.Add(AutostartZusammenfassung(autostartBefunde));

        foreach (var befund in befunde)
            ErgebnisBereich.Children.Add(BefundKarte(befund));

        var unauffaellig = befunde.Count(b => b.Status == AmpelStatus.Gruen);
        GesamtbildText.Text = Loc.T(
            $"{unauffaellig} von {befunde.Count} Bereichen unauffällig.",
            $"{unauffaellig} of {befunde.Count} areas unremarkable.");

        FortschrittBalken.Visibility = Visibility.Collapsed;
        PruefenButton.IsEnabled = true;
    }

    // Check 6 nutzt AutostartService.WeitereOrteAsync() direkt (Winlogon\Shell + IFEO) -
    // fasst die Einzelbefunde zu EINER Karte zusammen (Sprungmarke ins Autostart-Modul für
    // Details, s. Alex' "Spinnennetz"-Idee 19.08.2026) statt sie separat zu duplizieren.
    private static SicherheitsBefund AutostartZusammenfassung(List<SicherheitsBefund> befunde)
    {
        var titel = Loc.T("Autostart-Sicherheitshinweise", "Autostart security hints");
        var auffaellig = befunde.Where(b => b.Status is AmpelStatus.Gelb or AmpelStatus.Rot).ToList();

        if (auffaellig.Count == 0)
            return new(titel, Loc.T(
                "Keine Auffälligkeiten bei Windows-Shell (Winlogon) oder Programmstart-Umleitungen " +
                "(IFEO) - Details im Autostart-Modul unter \"Weitere, seltenere Orte prüfen\".",
                "No irregularities in Windows shell (Winlogon) or program launch redirects (IFEO) - " +
                "details in the Autostart module under \"Check further, rarer locations\"."),
                AmpelStatus.Gruen);

        var schlimmster = auffaellig.Any(b => b.Status == AmpelStatus.Rot) ? AmpelStatus.Rot : AmpelStatus.Gelb;
        return new(titel, Loc.T(
            $"{auffaellig.Count} Auffälligkeit(en) gefunden - Details im Autostart-Modul unter " +
            "\"Weitere, seltenere Orte prüfen\".",
            $"{auffaellig.Count} finding(s) - details in the Autostart module under \"Check further, " +
            "rarer locations\"."), schlimmster);
    }

    private Border BefundKarte(SicherheitsBefund befund)
    {
        var innen = new StackPanel();

        var titelZeile = new TextBlock
        {
            Text = $"{befund.Status.Symbol()} {befund.Titel}",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
        };

        var textBlock = new TextBlock
        {
            Text = befund.Text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 8, 0, 0),
        };

        var expander = new Expander
        {
            Header = titelZeile,
            Content = textBlock,
            Margin = new Thickness(0, 0, 0, 10),
        };

        innen.Children.Add(expander);

        return new Border
        {
            Background = (Brush)FindResource("BgCardBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 6, 14, 0),
            Margin = new Thickness(0, 0, 0, 10),
            Child = innen,
        };
    }
}
