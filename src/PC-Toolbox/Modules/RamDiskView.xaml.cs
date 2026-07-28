using System.Windows.Controls;
using System.Windows.Media;
using PCToolboxCore.Models;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox.Modules;

public partial class RamDiskView : UserControl
{
    private readonly RamDiskService _ramDiskService = new();

    public RamDiskView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("RAM-Disk", "RAM disk");
        QuelleHinweis.ToolTip = Loc.T(
            "Motor: ImDisk (freier, offener Treiber von LTRData) - die Toolbox richtet nur die Laufwerksverwaltung ein, kein eigener Treiber-Code.",
            "Engine: ImDisk (free, open driver by LTRData) - the toolbox only sets up drive management, no custom driver code.");
        EinleitungText.Text = Loc.T(
            "Legt ein zusätzliches Laufwerk direkt im Arbeitsspeicher an - deutlich schneller als jede SSD, ideal für Temp-Dateien oder Programme, die viel schreiben. Motor: ImDisk (freie, offene Treiber-Software von LTRData).",
            "Creates an additional drive directly in RAM - significantly faster than any SSD, ideal for temp files or programs that write heavily. Engine: ImDisk (free, open driver software by LTRData).");
        WichtigTitel.Text = Loc.T("⚠️ Wichtig, bevor du sie nutzt", "⚠️ Important before you use it");
        WichtigText.Text = Loc.T(
            "Der Inhalt ist FLÜCHTIG: alles darauf ist beim Herunterfahren, Neustart oder Entfernen der RAM-Disk unwiderruflich weg. Nur für Temp-Dateien, Downloads-Zwischenlager o.ä. nutzen - niemals für Dateien, die du behalten willst.",
            "The content is VOLATILE: everything on it is irrevocably gone when you shut down, restart, or remove the RAM disk. Only use it for temp files, a downloads staging area, or similar - never for files you want to keep.");
        InstallierenErklaerung.Text = Loc.T(
            "Der offizielle ImDisk-Installer liegt bereits bei - startet einen ganz normalen Installationsvorgang mit eigener Bestätigung, nichts läuft im Hintergrund.",
            "The official ImDisk installer is already included - it starts a completely normal installation with its own confirmation, nothing runs in the background.");
        ImDiskInstallierenButton.Content = Loc.T("ImDisk installieren", "Install ImDisk");
        GroesseLabel.Text = Loc.T("GRÖSSE", "SIZE");
        LaufwerksbuchstabeLabel.Text = Loc.T("LAUFWERKSBUCHSTABE", "DRIVE LETTER");
        ErstellenButton.Content = Loc.T("RAM-Disk anlegen", "Create RAM disk");
        EntfernenButton.Content = Loc.T("RAM-Disk entfernen", "Remove RAM disk");

        // A ist unter Windows fast immer das Diskettenlaufwerk-Erbe, D/C oft belegt -
        // R ("RAM") als sinnvoller Standard, danach der Rest des Alphabets als Ausweiche.
        var buchstaben = new[] { 'R' }.Concat("EFGHIJKLMNOPQSTUVWXYZ".ToCharArray());
        foreach (var b in buchstaben)
            LaufwerkBox.Items.Add(new ComboBoxItem { Content = $"{b}:" });
        LaufwerkBox.SelectedIndex = 0;

        Loaded += async (_, _) => await StatusAktualisierenAsync();
    }

    private char AusgewaehlterLaufwerksbuchstabe() =>
        ((string)((ComboBoxItem)LaufwerkBox.SelectedItem).Content)[0];

    private int AusgewaehlteGroesseMb() =>
        int.Parse((string)((ComboBoxItem)GroesseBox.SelectedItem).Tag);

    private async Task StatusAktualisierenAsync()
    {
        if (!_ramDiskService.IstImDiskInstalliert())
        {
            StatusText.Text = Loc.T("ImDisk ist nicht installiert.", "ImDisk is not installed.");
            StatusText.Foreground = Brushes.OrangeRed;
            ErstellenButton.IsEnabled = false;
            InstallierenPanel.Visibility = _ramDiskService.InstallerVorhanden()
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            MeldungText.Text = _ramDiskService.InstallerVorhanden()
                ? ""
                : Loc.T("Kein Installer gefunden - bitte manuell laden: github.com/LTRData/ImDisk",
                        "No installer found - please download manually: github.com/LTRData/ImDisk");
            return;
        }

        InstallierenPanel.Visibility = System.Windows.Visibility.Collapsed;
        var status = await _ramDiskService.GetStatusAsync(AusgewaehlterLaufwerksbuchstabe());

        if (status.IstAktiv)
        {
            StatusText.Text = Loc.T(
                $"Aktiv: Laufwerk {status.Laufwerksbuchstabe}: ({status.GroesseGb:0.0} GB, {status.FreiGb:0.0} GB frei)",
                $"Active: Drive {status.Laufwerksbuchstabe}: ({status.GroesseGb:0.0} GB, {status.FreiGb:0.0} GB free)");
            StatusText.Foreground = Brushes.LimeGreen;
            ErstellenButton.IsEnabled = false;
            EntfernenButton.IsEnabled = true;
        }
        else
        {
            StatusText.Text = Loc.T($"Laufwerk {status.Laufwerksbuchstabe}: ist aktuell keine RAM-Disk.",
                                     $"Drive {status.Laufwerksbuchstabe}: is currently not a RAM disk.");
            StatusText.Foreground = (Brush)FindResource("TextMutedBrush");
            ErstellenButton.IsEnabled = true;
            EntfernenButton.IsEnabled = false;
        }
    }

    private async void Erstellen_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ErstellenButton.IsEnabled = false;
        MeldungText.Text = Loc.T("Lege RAM-Disk an (UAC-Bestätigung nötig)...", "Creating RAM disk (UAC confirmation required)...");

        var einstellungen = new RamDiskSettings
        {
            Laufwerksbuchstabe = AusgewaehlterLaufwerksbuchstabe(),
            GroesseMb = AusgewaehlteGroesseMb()
        };

        var (erfolg, meldung) = await _ramDiskService.ErstellenAsync(einstellungen);
        MeldungText.Text = meldung;
        MeldungText.Foreground = erfolg ? Brushes.LimeGreen : Brushes.OrangeRed;

        await StatusAktualisierenAsync();
    }

    private async void Entfernen_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        EntfernenButton.IsEnabled = false;
        MeldungText.Text = Loc.T("Entferne RAM-Disk (UAC-Bestätigung nötig)...", "Removing RAM disk (UAC confirmation required)...");

        var (erfolg, meldung) = await _ramDiskService.EntfernenAsync(AusgewaehlterLaufwerksbuchstabe());
        MeldungText.Text = meldung;
        MeldungText.Foreground = erfolg ? Brushes.LimeGreen : Brushes.OrangeRed;

        await StatusAktualisierenAsync();
    }

    private void ImDiskInstallieren_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ImDiskInstallierenButton.IsEnabled = false;
        var gestartet = _ramDiskService.InstallerStarten();
        MeldungText.Text = gestartet
            ? Loc.T("Installer gestartet - folge dem Installationsdialog. Wenn er fertig ist, hier 'Aktualisieren' " +
                    "bzw. das Modul neu öffnen, damit ImDisk erkannt wird.",
                    "Installer started - follow the installation dialog. When it's done, 'Refresh' here " +
                    "or reopen the module so ImDisk is detected.")
            : Loc.T("Installer konnte nicht gestartet werden (z.B. UAC abgelehnt).",
                    "Installer could not be started (e.g. UAC declined).");
        MeldungText.Foreground = gestartet ? Brushes.LimeGreen : Brushes.OrangeRed;
        ImDiskInstallierenButton.IsEnabled = true;
    }
}
