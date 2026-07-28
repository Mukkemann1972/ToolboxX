using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCToolboxCore;

namespace PCToolbox;

public partial class OverviewView : UserControl
{
    public event EventHandler<string>? KachelAusgewaehlt;

    public OverviewView()
    {
        InitializeComponent();

        // Texte bewusst im Code gesetzt statt als XAML-Attribut: Deutsch/Englisch
        // stehen so direkt nebeneinander (Loc.T), siehe PCToolboxCore/Loc.cs.
        TaglineZeile1.Text = Loc.T("ECHTE ERFAHRUNG TRIFFT KI.", "REAL EXPERIENCE MEETS AI.");
        TaglineZeile2.Text = Loc.T("WERKZEUGE, DIE MITWACHSEN.", "TOOLS THAT GROW WITH YOU.");

        TitelSystem.Text = Loc.T("System", "System");
        BeschrSystem.Text = Loc.T("Übersicht & Zustand", "Overview & health");

        TitelLaufwerke.Text = Loc.T("Laufwerke", "Drives");
        BeschrLaufwerke.Text = Loc.T("SMART & Speicherplatz", "SMART & disk space");

        TitelTemperatur.Text = Loc.T("Temperatur", "Temperature");
        BeschrTemperatur.Text = Loc.T("CPU/GPU-Werte im Blick", "CPU/GPU values at a glance");

        TitelGpuDiagnose.Text = Loc.T("GPU-Diagnose", "GPU Diagnostics");
        BeschrGpuDiagnose.Text = Loc.T("VRAM-Test, alle Hersteller", "VRAM test, any vendor");

        TitelRamDisk.Text = Loc.T("RAM-Disk", "RAM Disk");
        BeschrRamDisk.Text = Loc.T("SSD schonen, RAM nutzen", "Spare your SSD, use RAM");

        TitelAutostart.Text = Loc.T("Autostart", "Startup Apps");
        BeschrAutostart.Text = Loc.T("Schneller hochfahren", "Boot up faster");

        TitelDienste.Text = Loc.T("Dienste", "Services");
        BeschrDienste.Text = Loc.T("Hintergrund entlasten", "Lighten the background load");

        TitelDatentraeger.Text = Loc.T("Datenträger", "Disk Manager");
        BeschrDatentraeger.Text = Loc.T("Formatieren & Größe ändern", "Format & resize");

        TitelNetzwerk.Text = Loc.T("Netzwerk", "Network");
        BeschrNetzwerk.Text = Loc.T("Wo hakt das Internet?", "Where's the internet stuck?");

        TitelTreiber.Text = Loc.T("Treiber-Check", "Driver Check");
        BeschrTreiber.Text = Loc.T("Gerätefehler im Klartext", "Device errors in plain language");

        TitelCommander.Text = Loc.T("Commander", "Commander");
        BeschrCommander.Text = Loc.T("Zwei Ordner nebeneinander", "Two folders side by side");

        TitelBluescreen.Text = Loc.T("Bluescreen", "Crash Log");
        BeschrBluescreen.Text = Loc.T("Absturz-Analyse", "Crash analysis");

        TitelUninstaller.Text = Loc.T("Uninstaller", "Uninstaller");
        BeschrUninstaller.Text = Loc.T("Restlos deinstallieren", "Uninstall completely");

        TitelBios.Text = Loc.T("BIOS-Kompendium", "BIOS Guide");
        BeschrBios.Text = Loc.T("Einstellungen erklärt", "Settings explained");

        TitelSaubermacher.Text = Loc.T("Saubermacher", "Cleanup");
        BeschrSaubermacher.Text = Loc.T("Speicherplatz freigeben", "Free up disk space");

        TitelWindowsReparatur.Text = Loc.T("Windows reparieren", "Repair Windows");
        BeschrWindowsReparatur.Text = Loc.T("Systemdateien prüfen & heilen", "Check & heal system files");

        TitelRegistry.Text = Loc.T("Registry", "Registry");
        BeschrRegistry.Text = Loc.T("Wichtige Stellen erklärt", "Key locations explained");

        TitelDrucker.Text = Loc.T("Drucker", "Printers");
        BeschrDrucker.Text = Loc.T("Status statt kryptischem Symbol", "Status instead of a cryptic icon");

        TitelUsb.Text = Loc.T("USB-Geräte", "USB Devices");
#if PRO_BUILD
        TitelUsb.Text += " " + Loc.T("(Pro)", "(Pro)");
        BeschrUsb.Text = Loc.T("Angeschlossen, Status & Geister-Geräte", "Connected, status & ghost devices");
#else
        BeschrUsb.Text = Loc.T("Angeschlossen & Status", "Connected & status");
#endif

#if PRO_BUILD
        FusszeileText.Text = Loc.T("Erschaffen statt konsumieren · Toolbox X Pro",
                                    "Create instead of consume · Toolbox X Pro");
        HeroLogoBild.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Pro/ToolboxX-Pro-Logo.png"));
        ProKachelnAufbauen();
#else
        FusszeileText.Text = Loc.T("Erschaffen statt konsumieren · Kostenlos · Open Source",
                                    "Create instead of consume · Free · Open Source");
#endif
    }

    private void Kachel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string modul })
            KachelAusgewaehlt?.Invoke(this, modul);
    }

#if PRO_BUILD
    /// <summary>Wie MainWindow.ProSidebarAufbauen: haengt Pro-Kacheln zur Laufzeit an, damit
    /// die XAML-Datei fuer Free/Pro identisch bleibt.</summary>
    private void ProKachelnAufbauen()
    {
        var ordnerGroesseKachel = new Button
        {
            Style = (Style)FindResource("TileCardStyle"),
            Width = 190,
            Height = 120,
            Margin = new Thickness(0, 0, 14, 14),
            Tag = "OrdnerGroesse"
        };
        ordnerGroesseKachel.Click += Kachel_Click;

        var inhalt = new StackPanel();
        inhalt.Children.Add(new TextBlock { Text = "📊", FontSize = 26 });
        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T("Ordnergröße", "Folder Size"),
            FontSize = 16, FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)FindResource("BodyFontFamily"), Margin = new Thickness(0, 8, 0, 2)
        });
        inhalt.Children.Add(new TextBlock
        {
            Text = Loc.T("Wer frisst den Speicherplatz?", "Who's eating your disk space?"),
            FontSize = 11, Foreground = (Brush)FindResource("TextMutedBrush"), TextWrapping = TextWrapping.Wrap
        });
        ordnerGroesseKachel.Content = inhalt;

        KachelnPanel.Children.Add(ordnerGroesseKachel);
    }
#endif
}
