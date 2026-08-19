using System.Windows;
using System.Windows.Controls;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class BiosKompendiumView : UserControl
{
    private readonly BiosInfoService _biosInfo = new();
    private readonly BiosKompendiumService _kompendium = new();

    // ToString() bewusst ueberschrieben: die geschlossene ComboBox-Box faellt in unserem
    // eigenen ComboBox-Template (ToolboxXTheme.xaml) auf ToString() zurueck statt zuverlaessig
    // DisplayMemberPath zu respektieren (WPF-Eigenheit bei benutzerdefinierten Templates) -
    // ohne diese Ueberschreibung stand hier der rohe Record-Text
    // "KategorieAuswahl { Anzeige = Alle, Wert = <null> }" statt "Alle".
    private record KategorieAuswahl(string Anzeige, BiosKategorie? Wert)
    {
        public override string ToString() => Anzeige;
    }

    public BiosKompendiumView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("BIOS-Kompendium", "BIOS compendium");
        QuelleHinweis.ToolTip = Loc.T(
            "'Dein System' oben: WMI Win32_BIOS (Hersteller/Version/Datum). Die Einträge selbst sind ein festes, handgeschriebenes Nachschlagewerk - Windows kann echte BIOS-Einstellungen nicht auslesen.",
            "'Your system' above: WMI Win32_BIOS (manufacturer/version/date). The entries themselves are a fixed, hand-written reference - Windows cannot read real BIOS settings.");
        KategorieLabel.Text = Loc.T("Kategorie:", "Category:");
        SucheLabel.Text = Loc.T("Suche:", "Search:");

        var auswahl = new List<KategorieAuswahl> { new(Loc.T("Alle", "All"), null) };
        auswahl.AddRange(Enum.GetValues<BiosKategorie>().Select(k => new KategorieAuswahl(BiosBegriff.KategorieName(k), k)));
        KategorieCombo.ItemsSource = auswahl;
        KategorieCombo.DisplayMemberPath = nameof(KategorieAuswahl.Anzeige);
        KategorieCombo.SelectedIndex = 0;

        Loaded += async (_, _) =>
        {
            try
            {
                await LadeSystemInfoAsync();
                ListeAktualisieren();
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(BiosKompendiumView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "BiosKompendiumView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async Task LadeSystemInfoAsync()
    {
        var info = await _biosInfo.GetBiosInfoAsync();
        DeinSystemZeile.Text = info.Fehler is not null
            ? $"❔ {info.Fehler}"
            : Loc.T(
                  $"Dein BIOS: {info.Hersteller}, Version {info.Version}" +
                  (string.IsNullOrEmpty(info.ReleaseDatum) ? "" : $" vom {info.ReleaseDatum}"),
                  $"Your BIOS: {info.Hersteller}, version {info.Version}" +
                  (string.IsNullOrEmpty(info.ReleaseDatum) ? "" : $" from {info.ReleaseDatum}"));

        // Einziger echter Live-Wert in diesem sonst statischen Nachschlagewerk (s.
        // BiosInfoService-Kommentar) - Windows kann keine BIOS-EINSTELLUNGEN auslesen, aber
        // diesen einen Grundmodus zuverlaessig ueber eine native API (GetFirmwareType).
        var firmwareTyp = FirmwareTypService.Ermitteln();
        FirmwareTypZeile.Text = firmwareTyp switch
        {
            FirmwareTyp.Uefi => Loc.T("Startmodus: UEFI", "Boot mode: UEFI"),
            FirmwareTyp.LegacyBios => Loc.T("Startmodus: Legacy-BIOS (kein UEFI)", "Boot mode: Legacy BIOS (no UEFI)"),
            _ => Loc.T("Startmodus: konnte nicht ermittelt werden", "Boot mode: could not be determined")
        };
        FirmwareRufusHinweis.Text = FirmwareTypErklaerung.RufusHinweis(firmwareTyp);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => ListeAktualisieren();

    private void ListeAktualisieren()
    {
        var kategorie = (KategorieCombo.SelectedItem as KategorieAuswahl)?.Wert;
        var suchtext = SucheBox.Text;

        Liste.ItemsSource = _kompendium.Suchen(suchtext, kategorie).Select(ZuAnzeigeZeile).ToList();
    }

    private static BegriffZeile ZuAnzeigeZeile(BiosBegriff b) => new()
    {
        Titel = $"{b.Risiko.Symbol()} {b.Name}",
        KategorieText = BiosBegriff.KategorieName(b.Kategorie),
        WasEsIst = b.WasEsIst,
        WasPassiertBeiAenderung = b.WasPassiertBeiAenderung,
        WannAendern = b.WannAendern,
        FallsProblem = b.FallsProblem,
        WasEsIstLabel = Loc.T("Was es ist", "What it is"),
        WasPassiertBeiAenderungLabel = Loc.T("Was passiert bei einer Änderung", "What happens if you change it"),
        WannAendernLabel = Loc.T("Wann ändern", "When to change it"),
        FallsProblemLabel = Loc.T("Falls ein Problem auftritt", "If a problem occurs"),
    };

    private class BegriffZeile
    {
        public string Titel { get; set; } = "";
        public string KategorieText { get; set; } = "";
        public string WasEsIst { get; set; } = "";
        public string WasPassiertBeiAenderung { get; set; } = "";
        public string WannAendern { get; set; } = "";
        public string FallsProblem { get; set; } = "";
        public string WasEsIstLabel { get; set; } = "";
        public string WasPassiertBeiAenderungLabel { get; set; } = "";
        public string WannAendernLabel { get; set; } = "";
        public string FallsProblemLabel { get; set; } = "";
    }
}
