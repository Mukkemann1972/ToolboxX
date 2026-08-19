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

        TitelAkku.Text = Loc.T("Akku-Gesundheit", "Battery Health");
        BeschrAkku.Text = Loc.T("Kapazität vs. Werkszustand", "Capacity vs. factory state");

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

        TitelDatenrettung.Text = Loc.T("Datenrettung", "Data Recovery");
        BeschrDatenrettung.Text = Loc.T("Gelöschte Dateien finden", "Find deleted files");

        TitelBios.Text = Loc.T("BIOS-Kompendium", "BIOS Guide");
        BeschrBios.Text = Loc.T("Einstellungen erklärt", "Settings explained");

        TitelSaubermacher.Text = Loc.T("Saubermacher", "Cleanup");
        BeschrSaubermacher.Text = Loc.T("Speicherplatz freigeben", "Free up disk space");

        TitelWindowsReparatur.Text = Loc.T("Windows reparieren", "Repair Windows");
        BeschrWindowsReparatur.Text = Loc.T("Systemdateien prüfen & heilen", "Check & heal system files");

        TitelRegistry.Text = Loc.T("Registry", "Registry");
        BeschrRegistry.Text = Loc.T("Wichtige Stellen erklärt", "Key locations explained");

        TitelLegacyWerkzeuge.Text = Loc.T("Windows-Bordmittel", "Windows Built-in Tools");
        BeschrLegacyWerkzeuge.Text = Loc.T("Versteckte Systemwerkzeuge direkt öffnen", "Open hidden system tools directly");

        TitelDrucker.Text = Loc.T("Drucker", "Printers");
        BeschrDrucker.Text = Loc.T("Status statt kryptischem Symbol", "Status instead of a cryptic icon");

        TitelUsb.Text = Loc.T("USB-Geräte", "USB Devices");
#if PRO_BUILD
        TitelUsb.Text += " " + Loc.T("(Pro)", "(Pro)");
        BeschrUsb.Text = Loc.T("Angeschlossen, Status & Geister-Geräte", "Connected, status & ghost devices");
#else
        BeschrUsb.Text = Loc.T("Angeschlossen & Status", "Connected & status");
#endif

        TitelMaus.Text = Loc.T("Maus-Diagnose", "Mouse Diagnostics");
        BeschrMaus.Text = Loc.T("Status & Brücke zu MausWerk", "Status & bridge to MausWerk");

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
        KachelnPanel.Children.Add(ProKachel("📊", Loc.T("Ordnergröße", "Folder Size"),
            Loc.T("Wer frisst den Speicherplatz?", "Who's eating your disk space?"), "OrdnerGroesse"));

        KachelnPanel.Children.Add(ProKachel("📄", Loc.T("Testbericht", "Test report"),
            Loc.T("Laufwerke, Temperatur & Treiber als PDF", "Drives, temperature & drivers as PDF"), "Testbericht"));

        KachelnPanel.Children.Add(ProKachel("🔄", Loc.T("Backup & Sync", "Backup & Sync"),
            Loc.T("Ordner abgleichen mit Vorschau", "Sync folders with preview"), "BackupSync"));

        KachelnPanel.Children.Add(ProKachel("💽", Loc.T("Festplatten-Klonen", "Disk Cloning"),
            Loc.T("Ganze Platte 1:1 kopieren", "Copy an entire disk 1:1"), "FestplattenKlon"));

        KachelnPanel.Children.Add(ProKachel("🔥", Loc.T("Datenvernichter", "Data Wipe"),
            Loc.T("Platte revisionssicher löschen", "Securely wipe a disk"), "Datenvernichter"));

        KachelnPanel.Children.Add(ProKachel("🩹", Loc.T("Partition-Reparatur", "Partition Repair"),
            Loc.T("Bootsektor aus Sicherungskopie reparieren", "Repair boot sector from backup copy"), "PartitionReparatur"));

        KachelnPanel.Children.Add(ProKachel("🖼️", Loc.T("Image-Deploy-Tool", "Image Deploy Tool"),
            Loc.T("Windows-Abbild erfassen & auf SSDs verteilen", "Capture a Windows image & deploy to SSDs"), "ImageDeploy"));

        KachelnPanel.Children.Add(ProKachel("💿", Loc.T("Installations-Assistent", "Setup Assistant"),
            Loc.T("Antwortdatei für unbeaufsichtigte Windows-Installation", "Answer file for unattended Windows setup"), "InstallationsAssistent"));

        KachelnPanel.Children.Add(ProKachel("🧩", Loc.T("Boardview-Betrachter", "Boardview Viewer"),
            Loc.T("Netz/Bauteil auf der Platine finden", "Find a net/part on the board"), "Boardview"));

        KachelnPanel.Children.Add(ProKachel("🧰", Loc.T("Treiber-Backup", "Driver Backup"),
            Loc.T("Treiber sichern & nach Neuinstallation wiederherstellen", "Back up drivers & restore after a reinstall"), "TreiberBackup"));

        KachelnPanel.Children.Add(ProKachel("🧹", Loc.T("Datenreste-Scanner", "Leftover Scanner"),
            Loc.T("Reste alter Deinstallationen finden", "Find leftovers from old uninstalls"), "Datenreste"));

        KachelnPanel.Children.Add(ProKachel("📋", Loc.T("Install-Tracker", "Install Tracker"),
            Loc.T("Vorher/Nachher-Vergleich einer Installation", "Before/after comparison of an install"), "InstallTracker"));

        KachelnPanel.Children.Add(ProKachel("🩹", Loc.T("App-Sicherheits-Check", "App Security Check"),
            Loc.T("Installierte Programme gegen bekannte Schwachstellen prüfen", "Check installed programs against known vulnerabilities"), "AppVulnerability"));

        KachelnPanel.Children.Add(ProKachel("🩺", Loc.T("HDD-Health-Monitor", "HDD Health Monitor"),
            Loc.T("SMART-Trend über Zeit - bevor es knallt", "SMART trend over time - before it fails"), "HddHealthMonitor"));

        KachelnPanel.Children.Add(ProKachel("🧬", Loc.T("Duplikat-Finder", "Duplicate Finder"),
            Loc.T("Inhaltsgleiche Dateien finden und aufräumen", "Find and clean up files with identical content"), "DuplikatFinder"));

        KachelnPanel.Children.Add(ProKachel("📦", Loc.T("Software-Depot", "Software Depot"),
            Loc.T("Standardsoftware gesammelt installieren", "Install standard software in one batch"), "SoftwareDepot"));

        KachelnPanel.Children.Add(ProKachel("🛡️", Loc.T("Update-Profil", "Update Profile"),
            Loc.T("Updates abgestuft verzögern, nie deaktivieren", "Defer updates in stages, never disable"), "WindowsUpdateProfil"));
    }

    private Button ProKachel(string symbol, string titel, string beschreibung, string modulTag)
    {
        var kachel = new Button
        {
            Style = (Style)FindResource("TileCardStyle"),
            Width = 190,
            Height = 120,
            Margin = new Thickness(0, 0, 14, 14),
            Tag = modulTag
        };
        kachel.Click += Kachel_Click;

        var inhalt = new StackPanel();
        inhalt.Children.Add(new TextBlock { Text = symbol, FontSize = 26 });
        inhalt.Children.Add(new TextBlock
        {
            Text = titel,
            FontSize = 16, FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)FindResource("BodyFontFamily"), Margin = new Thickness(0, 8, 0, 2)
        });
        inhalt.Children.Add(new TextBlock
        {
            Text = beschreibung,
            FontSize = 11, Foreground = (Brush)FindResource("TextMutedBrush"), TextWrapping = TextWrapping.Wrap
        });
        kachel.Content = inhalt;

        return kachel;
    }
#endif
}
