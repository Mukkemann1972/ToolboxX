using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCToolbox.Modules;
using PCToolbox.Services;
using PCToolboxCore;

namespace PCToolbox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Sprungmarken zwischen Modulen (s. ModulNavigation) - einmalig abonniert, gilt fuer die
        // gesamte Laufzeit des Fensters.
        ModulNavigation.SprungAngefordert += modul => NavigiereZu(modul);

        // Eigener Fensterrahmen (WindowStyle="None" + WindowChrome) hat einen bekannten
        // Nebeneffekt: WPF maximiert dann standardmaessig auf die vollen Monitor-Masse
        // statt auf den sichtbaren Arbeitsbereich - das Fenster ragt dabei ueber die
        // Taskleiste und teils auch ueber den Bildschirmrand hinaus (deshalb konnte am
        // linken Rand der Statuszeile ein Buchstabe abgeschnitten wirken). WmGetMinMaxInfo
        // unten korrigiert das, inklusive Mehrschirm-Betrieb (jeder Monitor kann eine
        // andere Aufloesung/Taskleisten-Position haben).
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WmGetMinMaxInfo);
        };

#if PRO_BUILD
        // Monitor-Auto-Schwaerzung (s. Services/Pro/MonitorAutoSchwaerzungWaechter) - einmalig
        // am Hauptfenster registriert, laeuft dann unabhaengig davon weiter, welche Ansicht
        // gerade offen ist. Laedt dabei auch eine evtl. zuvor gespeicherte Einstellung.
        SourceInitialized += (_, _) => PCToolbox.Services.Pro.MonitorAutoSchwaerzungWaechter.Instanz.AmHauptfensterRegistrieren(this);
#endif

        // Sidebar-Beschriftungen bewusst im Code gesetzt statt als XAML-Text:
        // die englischen Gegenstuecke stehen so direkt daneben (Loc.T), ohne dass
        // laengere Texte mit Sonderzeichen in XAML-Attributen escaped werden
        // muessten (siehe PCToolboxCore/Loc.cs).
        NavUebersichtText.Text = Loc.T("🏠  Übersicht", "🏠  Overview");

        NavAbschnittSystem.Text = Loc.T("SYSTEM", "SYSTEM");
        NavSystemText.Text = Loc.T("🖥️  System", "🖥️  System");
        NavTemperaturText.Text = Loc.T("🌡️  Temperatur", "🌡️  Temperature");
        NavGpuDiagnoseText.Text = Loc.T("🎮  GPU-Diagnose", "🎮  GPU Diagnostics");
        NavRamDiskText.Text = Loc.T("💽  RAM-Disk", "💽  RAM Disk");
        NavAutostartText.Text = Loc.T("🚀  Autostart", "🚀  Startup Apps");
        NavDiensteText.Text = Loc.T("⚙️  Dienste", "⚙️  Services");
        NavAkkuText.Text = Loc.T("🔋  Akku-Gesundheit", "🔋  Battery Health");

        NavAbschnittLaufwerke.Text = Loc.T("LAUFWERKE & DATEN", "DRIVES & DATA");
        NavLaufwerkeText.Text = Loc.T("💾  Laufwerke", "💾  Drives");
        NavDatentraegerText.Text = Loc.T("🗄️  Datenträger", "🗄️  Disk Manager");
        NavCommanderText.Text = Loc.T("🗂️  Commander", "🗂️  Commander");

        NavAbschnittNetzwerk.Text = Loc.T("NETZWERK & GERÄTE", "NETWORK & DEVICES");
        NavNetzwerkText.Text = Loc.T("🌐  Netzwerk", "🌐  Network");
        NavTreiberText.Text = Loc.T("🔌  Treiber", "🔌  Drivers");
        NavDruckerText.Text = Loc.T("🖨️  Drucker", "🖨️  Printers");
        NavUsbText.Text = Loc.T("🔌  USB-Geräte", "🔌  USB Devices");
#if PRO_BUILD
        NavUsbText.Text += " " + Loc.T("(Pro)", "(Pro)");
#endif
        NavMausText.Text = Loc.T("🖱️  Maus-Diagnose", "🖱️  Mouse Diagnostics");

        NavAbschnittWartung.Text = Loc.T("WARTUNG & INFO", "MAINTENANCE & INFO");
        NavSecurityAuditText.Text = Loc.T("🛡️  Security-Audit", "🛡️  Security Audit");
        NavBluescreenText.Text = Loc.T("⚡  Bluescreen", "⚡  Crash Log");
        NavUninstallerText.Text = Loc.T("🧹  Uninstaller", "🧹  Uninstaller");
        NavDatenrettungText.Text = Loc.T("🩺  Datenrettung", "🩺  Data Recovery");
        NavBiosText.Text = Loc.T("📖  BIOS", "📖  BIOS Guide");
        NavSaubermacherText.Text = Loc.T("🧽  Saubermacher", "🧽  Cleanup");
        NavWindowsReparaturText.Text = Loc.T("🩹  Windows reparieren", "🩹  Repair Windows");
        NavRegistryText.Text = Loc.T("🗝️  Registry", "🗝️  Registry");
        NavLegacyWerkzeugeText.Text = Loc.T("🧰  Windows-Bordmittel", "🧰  Windows Built-in Tools");
        NavEntstehungText.Text = Loc.T("🤝  Entstehung", "🤝  About");

        NavEinstellungenText.Text = Loc.T("⚙️  Einstellungen", "⚙️  Settings");

        // Erst NACH InitializeComponent() auswaehlen: InhaltBereich existiert
        // dann sicher. IsChecked=true loest ueber Checked=ZeigeUebersicht_Click
        // die erste Navigation aus (nicht direkt ZeigeUebersicht() rufen - sonst
        // entsteht zusaetzlich die Race Condition, die den Absturz verursacht hat).
        NavUebersicht.IsChecked = true;

#if PRO_BUILD
        ProSidebarAufbauen();
        ProTitelZeichenSetzen();
#endif

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
#if PRO_BUILD
        var titelZusatz = " Pro";
        var fusszeile = Loc.T("Für Werkstatt & IT-Profis · Erschaffen statt konsumieren",
                               "For workshops & IT professionals · Create instead of consume");
#else
        var titelZusatz = "";
        var fusszeile = Loc.T("Kostenlos · Open Source · Erschaffen statt konsumieren",
                               "Free · Open Source · Create instead of consume");
#endif
        StatusZeile.Text = $"Toolbox X{titelZusatz} · v{version?.Major}.{version?.Minor}.{version?.Build} · {fusszeile}";
    }

    private void ZeigeUebersicht()
    {
        var uebersicht = new OverviewView();
        uebersicht.KachelAusgewaehlt += (_, modul) => NavigiereZu(modul);
        InhaltBereich.Content = uebersicht;
    }

    private void NavigiereZu(string modul)
    {
        InhaltBereich.Content = modul switch
        {
            "System" => new DashboardView(),
            "Laufwerke" => new DiskCheckView(),
            "Temperatur" => new TemperatureView(),
            "GpuDiagnose" => new GpuDiagnoseView(),
            "RamDisk" => new RamDiskView(),
            "Autostart" => new AutostartView(),
            "Dienste" => new DiensteView(),
            "AkkuGesundheit" => new AkkuGesundheitView(),
            "Datentraeger" => new DatentraegerView(),
            "Netzwerk" => new NetzwerkView(),
            "Treiber" => new TreiberView(),
            "Commander" => new CommanderView(),
            "SecurityAudit" => new SecurityAuditView(),
            "Bluescreen" => new BluescreenView(),
            "Uninstaller" => new SuperUninstallerView(),
            "Datenrettung" => new DatenrettungView(),
            "Bios" => new BiosKompendiumView(),
            "Saubermacher" => new SystemSaubermacherView(),
            "WindowsReparatur" => new WindowsReparaturView(),
            "Registry" => new RegistryKompendiumView(),
            "Drucker" => new DruckerView(),
            "UsbGeraete" => new UsbGeraeteView(),
            "MausDiagnose" => new MausDiagnoseView(),
            "LegacyWerkzeuge" => new LegacyWerkzeugeView(),
#if PRO_BUILD
            "OrdnerGroesse" => new PCToolbox.Modules.Pro.OrdnerGroesseView(),
            "Testbericht" => new PCToolbox.Modules.Pro.TestberichtView(),
            "BackupSync" => new PCToolbox.Modules.Pro.BackupSyncView(),
            "FestplattenKlon" => new PCToolbox.Modules.Pro.FestplattenKlonView(),
            "Datenvernichter" => new PCToolbox.Modules.Pro.DatenvernichterView(),
            "PartitionReparatur" => new PCToolbox.Modules.Pro.PartitionReparaturView(),
            "ImageDeploy" => new PCToolbox.Modules.Pro.ImageDeployView(),
            "InstallationsAssistent" => new PCToolbox.Modules.Pro.InstallationsAssistentView(),
            "Boardview" => new PCToolbox.Modules.Pro.BoardviewView(),
            "TreiberBackup" => new PCToolbox.Modules.Pro.TreiberBackupView(),
            "Datenreste" => new PCToolbox.Modules.Pro.DatenresteView(),
            "InstallTracker" => new PCToolbox.Modules.Pro.InstallTrackerView(),
            "AppVulnerability" => new PCToolbox.Modules.Pro.AppVulnerabilityView(),
            "HddHealthMonitor" => new PCToolbox.Modules.Pro.HddHealthMonitorView(),
            "DuplikatFinder" => new PCToolbox.Modules.Pro.DuplikatFinderView(),
            "SoftwareDepot" => new PCToolbox.Modules.Pro.SoftwareDepotView(),
            "WindowsUpdateProfil" => new PCToolbox.Modules.Pro.WindowsUpdateProfilView(),
#endif
            _ => InhaltBereich.Content
        };
    }

#if PRO_BUILD
    /// <summary>
    /// Haengt die Pro-Eintraege zur Laufzeit an die Sidebar an, statt sie fest in
    /// MainWindow.xaml zu verdrahten - so bleibt die XAML-Datei fuer Free/Pro identisch
    /// und die eigentliche Trennung passiert allein ueber den Compile-Ausschluss in
    /// PC-Toolbox.csproj (Modules/Pro, Services/Pro fehlen im Free-Build komplett).
    /// </summary>
    private void ProSidebarAufbauen()
    {
        // Jede Gruppe hat ihren eigenen Anker (ProAnkerLaufwerke/ProAnkerWartung, s. MainWindow.xaml) -
        // so landen die Pro-Eintraege thematisch bei den Free-Eintraegen, zu denen sie gehoeren,
        // statt alle zusammen an einer Stelle zu klumpen.
        var laufwerkeIndex = SidebarPanel.Children.IndexOf(ProAnkerLaufwerke);

        var ordnerGroesseButton = NeuerSidebarKnopf("📊  Ordnergröße", "📊  Folder Size", () => new PCToolbox.Modules.Pro.OrdnerGroesseView());
        SidebarPanel.Children.Insert(laufwerkeIndex, ordnerGroesseButton);

        var backupSyncButton = NeuerSidebarKnopf("🔄  Backup & Sync", "🔄  Backup & Sync", () => new PCToolbox.Modules.Pro.BackupSyncView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 1, backupSyncButton);

        var festplattenKlonButton = NeuerSidebarKnopf("💽  Festplatten-Klonen", "💽  Disk Cloning", () => new PCToolbox.Modules.Pro.FestplattenKlonView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 2, festplattenKlonButton);

        var datenvernichterButton = NeuerSidebarKnopf("🔥  Datenvernichter", "🔥  Data Wipe", () => new PCToolbox.Modules.Pro.DatenvernichterView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 3, datenvernichterButton);

        var partitionReparaturButton = NeuerSidebarKnopf("🩹  Partition-Reparatur", "🩹  Partition Repair", () => new PCToolbox.Modules.Pro.PartitionReparaturView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 4, partitionReparaturButton);

        var imageDeployButton = NeuerSidebarKnopf("🖼️  Image-Deploy-Tool", "🖼️  Image Deploy Tool", () => new PCToolbox.Modules.Pro.ImageDeployView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 5, imageDeployButton);

        var installationsAssistentButton = NeuerSidebarKnopf("💿  Installations-Assistent", "💿  Setup Assistant", () => new PCToolbox.Modules.Pro.InstallationsAssistentView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 6, installationsAssistentButton);

        var boardviewButton = NeuerSidebarKnopf("🧩  Boardview-Betrachter", "🧩  Boardview Viewer", () => new PCToolbox.Modules.Pro.BoardviewView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 7, boardviewButton);

        var treiberBackupButton = NeuerSidebarKnopf("🧰  Treiber-Backup", "🧰  Driver Backup", () => new PCToolbox.Modules.Pro.TreiberBackupView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 8, treiberBackupButton);

        var hddHealthButton = NeuerSidebarKnopf("🩺  HDD-Health-Monitor", "🩺  HDD Health Monitor", () => new PCToolbox.Modules.Pro.HddHealthMonitorView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 9, hddHealthButton);

        var duplikatFinderButton = NeuerSidebarKnopf("🧬  Duplikat-Finder", "🧬  Duplicate Finder", () => new PCToolbox.Modules.Pro.DuplikatFinderView());
        SidebarPanel.Children.Insert(laufwerkeIndex + 10, duplikatFinderButton);

        var wartungIndex = SidebarPanel.Children.IndexOf(ProAnkerWartung);
        var testberichtButton = NeuerSidebarKnopf("📄  Testbericht", "📄  Test report", () => new PCToolbox.Modules.Pro.TestberichtView());
        SidebarPanel.Children.Insert(wartungIndex, testberichtButton);

        var datenresteButton = NeuerSidebarKnopf("🧹  Datenreste-Scanner", "🧹  Leftover Scanner", () => new PCToolbox.Modules.Pro.DatenresteView());
        SidebarPanel.Children.Insert(wartungIndex + 1, datenresteButton);

        var installTrackerButton = NeuerSidebarKnopf("📋  Install-Tracker", "📋  Install Tracker", () => new PCToolbox.Modules.Pro.InstallTrackerView());
        SidebarPanel.Children.Insert(wartungIndex + 2, installTrackerButton);

        var appVulnButton = NeuerSidebarKnopf("🩹  App-Sicherheits-Check", "🩹  App Security Check", () => new PCToolbox.Modules.Pro.AppVulnerabilityView());
        SidebarPanel.Children.Insert(wartungIndex + 3, appVulnButton);

        var softwareDepotButton = NeuerSidebarKnopf("📦  Software-Depot", "📦  Software Depot", () => new PCToolbox.Modules.Pro.SoftwareDepotView());
        SidebarPanel.Children.Insert(wartungIndex + 4, softwareDepotButton);

        var updateProfilButton = NeuerSidebarKnopf("🛡️  Update-Profil", "🛡️  Update Profile", () => new PCToolbox.Modules.Pro.WindowsUpdateProfilView());
        SidebarPanel.Children.Insert(wartungIndex + 5, updateProfilButton);
    }

    private RadioButton NeuerSidebarKnopf(string deutsch, string englisch, Func<UserControl> ansichtErstellen)
    {
        var knopf = new RadioButton
        {
            GroupName = "Nav",
            Style = (Style)FindResource("SidebarItemStyle"),
            Content = new TextBlock { Text = Loc.T(deutsch, englisch) }
        };
        knopf.Checked += (_, _) => InhaltBereich.Content = ansichtErstellen();
        return knopf;
    }

    /// <summary>Ersetzt das kleine X-Zeichen oben links durch die Pro-Variante und haengt "PRO" an den Titeltext an.</summary>
    private void ProTitelZeichenSetzen()
    {
        TitelZeichenBild.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Pro/ToolboxX-Pro-Zeichen.png"));
        TitelTextBlock.Inlines.Add(new Run(" PRO") { Foreground = (Brush)FindResource("AccentBrush"), FontWeight = FontWeights.Bold });
    }
#endif

    private void ZeigeUebersicht_Click(object sender, RoutedEventArgs e) => ZeigeUebersicht();
    private void ZeigeDashboard_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DashboardView();
    private void ZeigeDiskCheck_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DiskCheckView();
    private void ZeigeTemperature_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new TemperatureView();
    private void ZeigeGpuDiagnose_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new GpuDiagnoseView();
    private void ZeigeRamDisk_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new RamDiskView();
    private void ZeigeAutostart_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new AutostartView();
    private void ZeigeDienste_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DiensteView();
    private void ZeigeAkkuGesundheit_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new AkkuGesundheitView();
    private void ZeigeDatentraeger_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DatentraegerView();
    private void ZeigeNetzwerk_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new NetzwerkView();
    private void ZeigeTreiber_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new TreiberView();
    private void ZeigeCommander_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new CommanderView();
    private void ZeigeSecurityAudit_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new SecurityAuditView();
    private void ZeigeBluescreen_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new BluescreenView();
    private void ZeigeSuperUninstaller_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new SuperUninstallerView();
    private void ZeigeDatenrettung_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DatenrettungView();
    private void ZeigeBiosKompendium_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new BiosKompendiumView();
    private void ZeigeSaubermacher_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new SystemSaubermacherView();
    private void ZeigeWindowsReparatur_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new WindowsReparaturView();
    private void ZeigeRegistryKompendium_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new RegistryKompendiumView();
    private void ZeigeLegacyWerkzeuge_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new LegacyWerkzeugeView();
    private void ZeigeDrucker_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DruckerView();
    private void ZeigeUsbGeraete_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new UsbGeraeteView();
    private void ZeigeMausDiagnose_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new MausDiagnoseView();
    private void ZeigeEntstehung_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new EntstehungView();
    private void ZeigeEinstellungen_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new SettingsView();

    private void Minimieren_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximieren_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Schliessen_Click(object sender, RoutedEventArgs e) => Close();

    // ================= Maximieren ohne Ueberlappung =================

    /// <summary>
    /// Faengt WM_GETMINMAXINFO ab und setzt die maximierte Fenstergroesse auf den
    /// tatsaechlichen Arbeitsbereich des Monitors, auf dem das Fenster gerade liegt
    /// (statt der vollen Monitor-Aufloesung, die Windows bei eigenem Fensterrahmen
    /// sonst verwendet). Ohne das wuerde "Maximieren" die Taskleiste verdecken und
    /// das Fenster leicht ueber den sichtbaren Bildschirmrand hinausschieben.
    /// </summary>
    private nint WmGetMinMaxInfo(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg != WM_GETMINMAXINFO) return 0;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != 0)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);

            var arbeitsbereich = monitorInfo.rcWork;
            var monitorFlaeche = monitorInfo.rcMonitor;

            var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            minMaxInfo.ptMaxPosition.x = arbeitsbereich.left - monitorFlaeche.left;
            minMaxInfo.ptMaxPosition.y = arbeitsbereich.top - monitorFlaeche.top;
            minMaxInfo.ptMaxSize.x = arbeitsbereich.right - arbeitsbereich.left;
            minMaxInfo.ptMaxSize.y = arbeitsbereich.bottom - arbeitsbereich.top;
            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        handled = true;
        return 0;
    }

    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, int dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }
}
