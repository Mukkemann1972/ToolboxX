using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class TreiberView : UserControl
{
    private readonly TreiberService _dienst = new();
    private List<TreiberZeile> _alle = [];

    public TreiberView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Treiber-Check", "Driver check");
        QuelleHinweis.ToolTip = Loc.T(
            "Geräte-Manager-Fehlercodes über WMI Win32_PnPEntity (ConfigManagerErrorCode) - dieselbe Information wie im Windows-Geräte-Manager, nur im Klartext erklärt.",
            "Device Manager error codes via WMI Win32_PnPEntity (ConfigManagerErrorCode) - the same information as in the Windows Device Manager, just explained in plain language.");
        EinleitungText.Text = Loc.T(
            "Sucht Geräte, bei denen Windows ein Problem meldet - und erklärt zu jeder Fehlernummer, was sie bedeutet und was du dagegen tun kannst. Es wird nur gelesen, nichts installiert.",
            "Looks for devices where Windows reports a problem - and explains for each error number what it means and what you can do about it. Only reading happens, nothing is installed.");
        NichtsAutomatischTitel.Text = Loc.T("ℹ️ Warum hier nichts automatisch aktualisiert wird", "ℹ️ Why nothing is updated automatically here");
        NichtsAutomatischText.Text = Loc.T(
            "Programme, die „alle Treiber auf einen Klick“ erneuern, sind die häufigste Ursache für kaputte PCs, die wir kennen - und ein beliebtes Geschäftsmodell. Ein alter Treiber, der funktioniert, ist kein Problem. Deshalb zeigt dieses Modul, WAS klemmt und WO der richtige Treiber herkommt - installiert wird bewusst nichts von allein.",
            "Programs that update \"all drivers with one click\" are the most common cause of broken PCs that we know of - and a popular business model. An old driver that works is not a problem. That's why this module shows WHAT is stuck and WHERE the right driver comes from - nothing is deliberately installed on its own.");
        PruefenButton.Content = Loc.T("Erneut prüfen", "Check again");
        AlleAnzeigenBox.Content = Loc.T("Auch fehlerfreie Geräte anzeigen", "Also show error-free devices");

        Loaded += async (_, _) => await PruefenAsync();
    }

    private async Task PruefenAsync()
    {
        PruefenButton.IsEnabled = false;
        ZusammenfassungText.Text = Loc.T("Lese Geräte ein …", "Loading devices…");
        HinweisText.Text = "";

        var geraete = await _dienst.LadeGeraeteAsync();
        var heute = DateTime.Today;

        _alle = geraete
            .Select(g => new TreiberZeile(g, TreiberBewerter.Bewerten(g, heute), FarbeFuer))
            .OrderBy(z => Reihenfolge(z.Ampel))
            .ThenBy(z => z.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var mitProblem = _alle.Count(z => z.Ampel == AmpelStatus.Rot);
        var hinweise = _alle.Count(z => z.Ampel == AmpelStatus.Gelb);

        ZusammenfassungText.Text = TreiberBewerter.Zusammenfassung(_alle.Count, mitProblem, hinweise);
        HinweisText.Text = mitProblem == 0 && hinweise == 0
            ? Loc.T("Es gibt nichts zu tun. Fehlerfreie Geräte werden ausgeblendet - über das Kästchen unten kannst du sie einblenden.",
                    "There's nothing to do. Error-free devices are hidden - you can show them via the checkbox below.")
            : Loc.T("Fehlerfreie Geräte sind ausgeblendet, damit die Liste übersichtlich bleibt.",
                    "Error-free devices are hidden so the list stays manageable.");

        AnzeigeAktualisieren();
        PruefenButton.IsEnabled = true;
    }

    private void AnzeigeAktualisieren()
    {
        var sichtbar = AlleAnzeigenBox.IsChecked == true
            ? _alle
            : _alle.Where(z => z.Ampel != AmpelStatus.Gruen).ToList();

        // Gruppiert nach Kategorie statt einer langen flachen Liste - die Reihenfolge der
        // Zeilen innerhalb einer Gruppe (Rot vor Gelb vor Gruen) bleibt dabei erhalten,
        // die Gruppen selbst stehen alphabetisch nach Kategorie-Klartext.
        Liste.ItemsSource = sichtbar
            .GroupBy(z => GeraeteKategorien.Klartext(z.Klasse))
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new TreiberGruppe { Kategorie = g.Key, Zeilen = g.ToList() })
            .ToList();
    }

    private static int Reihenfolge(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Rot => 0,
        AmpelStatus.Gelb => 1,
        _ => 2
    };

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private async void Pruefen_Click(object sender, RoutedEventArgs e) => await PruefenAsync();

    private void AlleAnzeigen_Click(object sender, RoutedEventArgs e) => AnzeigeAktualisieren();
}

/// <summary>Eine Kategorie-Gruppe fuer die gruppierte Anzeige im Treiber-Check.</summary>
public class TreiberGruppe
{
    public string Kategorie { get; set; } = "";
    public List<TreiberZeile> Zeilen { get; set; } = [];
}

public class TreiberZeile
{
    private readonly GeraetBefund _geraet;

    public TreiberZeile(GeraetBefund geraet, TreiberBewertung bewertung, Func<AmpelStatus, Brush> farbe)
    {
        _geraet = geraet;
        Ampel = bewertung.Ampel;
        Kurzform = bewertung.Kurzform;
        WasEsIst = bewertung.WasEsIst;
        Empfehlung = bewertung.Empfehlung;
        Symbol = bewertung.Ampel.Symbol();
        AmpelFarbe = farbe(bewertung.Ampel);
    }

    public AmpelStatus Ampel { get; }
    public string Name => _geraet.Name;
    public string? Klasse => _geraet.Klasse;
    public string Kurzform { get; }
    public string WasEsIst { get; }
    public string Empfehlung { get; }
    public string Symbol { get; }
    public Brush AmpelFarbe { get; }

    public string Herkunft
    {
        get
        {
            var teile = new List<string>();

            if (!string.IsNullOrWhiteSpace(_geraet.Hersteller)) teile.Add(_geraet.Hersteller!);
            if (!string.IsNullOrWhiteSpace(_geraet.Klasse)) teile.Add(Loc.T($"Kategorie: {_geraet.Klasse}", $"Category: {_geraet.Klasse}"));
            if (!string.IsNullOrWhiteSpace(_geraet.TreiberVersion)) teile.Add(Loc.T($"Treiber {_geraet.TreiberVersion}", $"Driver {_geraet.TreiberVersion}"));
            if (_geraet.TreiberDatum is { } datum) teile.Add(Loc.T($"vom {datum:dd.MM.yyyy}", $"from {datum:yyyy-MM-dd}"));
            if (_geraet.FehlerCode != 0) teile.Add(Loc.T($"Windows-Fehlernummer {_geraet.FehlerCode}", $"Windows error number {_geraet.FehlerCode}"));

            return string.Join("   ·   ", teile);
        }
    }
}
