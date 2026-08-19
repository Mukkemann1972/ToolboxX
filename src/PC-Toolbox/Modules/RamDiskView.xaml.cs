using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolboxCore.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using PCToolbox.Services.Pro;
#endif

namespace PCToolbox.Modules;

public partial class RamDiskView : UserControl
{
    private readonly RamDiskService _ramDiskService = new();

#if PRO_BUILD
    private readonly EigeneRamDiskService _eigeneRamDiskService = new();
    private ComboBox _engineAuswahl = null!;
    private StackPanel _dokanInstallierenPanel = null!;
#endif

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

#if PRO_BUILD
        BauEngineBereich();
#endif

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
#if PRO_BUILD
        if (_engineAuswahl.SelectedIndex == 1)
        {
            await EigeneEngineStatusAktualisierenAsync();
            return;
        }
        _dokanInstallierenPanel.Visibility = System.Windows.Visibility.Collapsed;
#endif

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

#if PRO_BUILD
        var (erfolg, meldung) = _engineAuswahl.SelectedIndex == 1
            ? await _eigeneRamDiskService.ErstellenAsync(einstellungen)
            : await _ramDiskService.ErstellenAsync(einstellungen);
#else
        var (erfolg, meldung) = await _ramDiskService.ErstellenAsync(einstellungen);
#endif
        MeldungText.Text = meldung;
        MeldungText.Foreground = erfolg ? Brushes.LimeGreen : Brushes.OrangeRed;

        await StatusAktualisierenAsync();
    }

    private async void Entfernen_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        EntfernenButton.IsEnabled = false;
        MeldungText.Text = Loc.T("Entferne RAM-Disk (UAC-Bestätigung nötig)...", "Removing RAM disk (UAC confirmation required)...");

#if PRO_BUILD
        var (erfolg, meldung) = _engineAuswahl.SelectedIndex == 1
            ? await _eigeneRamDiskService.EntfernenAsync()
            : await _ramDiskService.EntfernenAsync(AusgewaehlterLaufwerksbuchstabe());
#else
        var (erfolg, meldung) = await _ramDiskService.EntfernenAsync(AusgewaehlterLaufwerksbuchstabe());
#endif
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

#if PRO_BUILD
    /// <summary>
    /// Engine-Auswahl zwischen ImDisk (extern, dieselbe Engine wie Free) und der komplett
    /// selbst geschriebenen RAM-Disk (s. EigeneRamDiskService, PCToolboxCore.Pro.EigeneRamDisk).
    /// Standardmaessig ist die eigene Engine vorausgewaehlt - dasselbe Muster wie beim eigenen
    /// Vulkan-VRAM-Test (GpuDiagnoseView): das Pro-Alleinstellungsmerkmal soll nicht in einem
    /// zweiten Klick versteckt sein.
    /// </summary>
    private void BauEngineBereich()
    {
        var label = new TextBlock
        {
            Text = Loc.T("Engine (Pro):", "Engine (Pro):"),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        _engineAuswahl = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        _engineAuswahl.Items.Add(new ComboBoxItem { Content = Loc.T("ImDisk (extern, wie Free)", "ImDisk (external, same as Free)") });
        _engineAuswahl.Items.Add(new ComboBoxItem { Content = Loc.T("Eigene RAM-Disk (selbst geschrieben)", "Built-in RAM disk (written from scratch)") });
        _engineAuswahl.SelectedIndex = 1;
        _engineAuswahl.SelectionChanged += async (_, _) => await StatusAktualisierenAsync();

        var hinweis = new TextBlock
        {
            Text = Loc.T(
                "Die eigene RAM-Disk läuft komplett innerhalb der Toolbox (eigene Speicherlogik, kein " +
                "ImDisk-Code) - andockt über Dokan (freier, offener Treiber, github.com/dokan-dev/dokany). " +
                "Wichtiger Unterschied zu ImDisk: sie existiert nur, solange Toolbox X geöffnet ist - beim " +
                "Beenden der App ist sie weg (ImDisk-Disks überleben dagegen einen App-Neustart, bis zum " +
                "nächsten Neustart des PCs oder explizitem Entfernen).",
                "The built-in RAM disk runs entirely inside the Toolbox (own storage logic, no ImDisk " +
                "code) - docked in via Dokan (free, open driver, github.com/dokan-dev/dokany). Important " +
                "difference from ImDisk: it only exists while Toolbox X is open - it's gone once the app " +
                "closes (ImDisk disks, by contrast, survive an app restart until the next PC reboot or " +
                "explicit removal)."),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 4, 0, 10)
        };

        _dokanInstallierenPanel = new StackPanel { Visibility = Visibility.Collapsed };
        var dokanErklaerung = new TextBlock
        {
            Text = Loc.T(
                "Der offizielle Dokan-Installer liegt bereits bei - startet einen ganz normalen " +
                "Installationsvorgang mit eigener Bestätigung, nichts läuft im Hintergrund.",
                "The official Dokan installer is already included - it starts a completely normal " +
                "installation with its own confirmation, nothing runs in the background."),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("TextMutedBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var dokanButton = new Button
        {
            Content = Loc.T("Dokan installieren", "Install Dokan"),
            Padding = new Thickness(18, 10, 18, 10),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        dokanButton.Click += DokanInstallieren_Click;
        _dokanInstallierenPanel.Children.Add(dokanErklaerung);
        _dokanInstallierenPanel.Children.Add(dokanButton);

        ProZusatzBereich.Children.Add(label);
        ProZusatzBereich.Children.Add(_engineAuswahl);
        ProZusatzBereich.Children.Add(hinweis);
        ProZusatzBereich.Children.Add(_dokanInstallierenPanel);
    }

    private Task EigeneEngineStatusAktualisierenAsync()
    {
        InstallierenPanel.Visibility = Visibility.Collapsed;

        if (!_eigeneRamDiskService.IstDokanInstalliert())
        {
            StatusText.Text = Loc.T("Dokan ist nicht installiert.", "Dokan is not installed.");
            StatusText.Foreground = Brushes.OrangeRed;
            ErstellenButton.IsEnabled = false;
            EntfernenButton.IsEnabled = false;
            _dokanInstallierenPanel.Visibility = _eigeneRamDiskService.InstallerVorhanden()
                ? Visibility.Visible
                : Visibility.Collapsed;
            MeldungText.Text = _eigeneRamDiskService.InstallerVorhanden()
                ? ""
                : Loc.T("Kein Installer gefunden - bitte manuell laden: github.com/dokan-dev/dokany",
                        "No installer found - please download manually: github.com/dokan-dev/dokany");
            return Task.CompletedTask;
        }

        _dokanInstallierenPanel.Visibility = Visibility.Collapsed;

        if (_eigeneRamDiskService.IstAktiv)
        {
            var buchstabe = _eigeneRamDiskService.AktiverLaufwerksbuchstabe!.Value;
            var laufwerk = System.IO.DriveInfo.GetDrives()
                .FirstOrDefault(d => d.Name.StartsWith($"{buchstabe}:", StringComparison.OrdinalIgnoreCase) && d.IsReady);
            var groesseGb = laufwerk is null ? 0 : Math.Round(laufwerk.TotalSize / 1024.0 / 1024 / 1024, 2);
            var freiGb = laufwerk is null ? 0 : Math.Round(laufwerk.AvailableFreeSpace / 1024.0 / 1024 / 1024, 2);

            StatusText.Text = Loc.T(
                $"Aktiv: Laufwerk {buchstabe}: ({groesseGb:0.0} GB, {freiGb:0.0} GB frei) - läuft nur, solange Toolbox X geöffnet ist.",
                $"Active: Drive {buchstabe}: ({groesseGb:0.0} GB, {freiGb:0.0} GB free) - runs only while Toolbox X is open.");
            StatusText.Foreground = Brushes.LimeGreen;
            ErstellenButton.IsEnabled = false;
            EntfernenButton.IsEnabled = true;
        }
        else
        {
            StatusText.Text = Loc.T(
                $"Laufwerk {AusgewaehlterLaufwerksbuchstabe()}: ist aktuell keine eigene RAM-Disk.",
                $"Drive {AusgewaehlterLaufwerksbuchstabe()}: is not currently a built-in RAM disk.");
            StatusText.Foreground = (Brush)FindResource("TextMutedBrush");
            ErstellenButton.IsEnabled = true;
            EntfernenButton.IsEnabled = false;
        }

        return Task.CompletedTask;
    }

    private void DokanInstallieren_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        var gestartet = _eigeneRamDiskService.InstallerStarten();
        MeldungText.Text = gestartet
            ? Loc.T("Installer gestartet - folge dem Installationsdialog. Wenn er fertig ist, hier 'Aktualisieren' " +
                    "bzw. das Modul neu öffnen, damit Dokan erkannt wird.",
                    "Installer started - follow the installation dialog. When it's done, 'Refresh' here " +
                    "or reopen the module so Dokan is detected.")
            : Loc.T("Installer konnte nicht gestartet werden (z.B. UAC abgelehnt).",
                    "Installer could not be started (e.g. UAC declined).");
        MeldungText.Foreground = gestartet ? Brushes.LimeGreen : Brushes.OrangeRed;
        button.IsEnabled = true;
    }
#endif
}
