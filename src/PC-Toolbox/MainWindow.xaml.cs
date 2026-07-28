using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCToolbox.Modules;
using PCToolboxCore;

namespace PCToolbox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

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

        // Sidebar-Beschriftungen bewusst im Code gesetzt statt als XAML-Text:
        // die englischen Gegenstuecke stehen so direkt daneben (Loc.T), ohne dass
        // laengere Texte mit Sonderzeichen in XAML-Attributen escaped werden
        // muessten (siehe PCToolboxCore/Loc.cs).
        NavUebersichtText.Text = Loc.T("🏠  Übersicht", "🏠  Overview");
        NavSystemText.Text = Loc.T("🖥️  System", "🖥️  System");
        NavLaufwerkeText.Text = Loc.T("💾  Laufwerke", "💾  Drives");
        NavTemperaturText.Text = Loc.T("🌡️  Temperatur", "🌡️  Temperature");
        NavGpuDiagnoseText.Text = Loc.T("🎮  GPU-Diagnose", "🎮  GPU Diagnostics");
        NavRamDiskText.Text = Loc.T("💽  RAM-Disk", "💽  RAM Disk");
        NavAutostartText.Text = Loc.T("🚀  Autostart", "🚀  Startup Apps");
        NavDiensteText.Text = Loc.T("⚙️  Dienste", "⚙️  Services");
        NavDatentraegerText.Text = Loc.T("🗄️  Datenträger", "🗄️  Disk Manager");
        NavNetzwerkText.Text = Loc.T("🌐  Netzwerk", "🌐  Network");
        NavTreiberText.Text = Loc.T("🔌  Treiber", "🔌  Drivers");
        NavCommanderText.Text = Loc.T("🗂️  Commander", "🗂️  Commander");
        NavBluescreenText.Text = Loc.T("⚡  Bluescreen", "⚡  Crash Log");
        NavUninstallerText.Text = Loc.T("🧹  Uninstaller", "🧹  Uninstaller");
        NavBiosText.Text = Loc.T("📖  BIOS", "📖  BIOS Guide");
        NavSaubermacherText.Text = Loc.T("🧽  Saubermacher", "🧽  Cleanup");
        NavWindowsReparaturText.Text = Loc.T("🩹  Windows reparieren", "🩹  Repair Windows");
        NavRegistryText.Text = Loc.T("🗝️  Registry", "🗝️  Registry");
        NavDruckerText.Text = Loc.T("🖨️  Drucker", "🖨️  Printers");
        NavUsbText.Text = Loc.T("🔌  USB-Geräte", "🔌  USB Devices");
#if PRO_BUILD
        NavUsbText.Text += " " + Loc.T("(Pro)", "(Pro)");
#endif
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
            "Datentraeger" => new DatentraegerView(),
            "Netzwerk" => new NetzwerkView(),
            "Treiber" => new TreiberView(),
            "Commander" => new CommanderView(),
            "Bluescreen" => new BluescreenView(),
            "Uninstaller" => new SuperUninstallerView(),
            "Bios" => new BiosKompendiumView(),
            "Saubermacher" => new SystemSaubermacherView(),
            "WindowsReparatur" => new WindowsReparaturView(),
            "Registry" => new RegistryKompendiumView(),
            "Drucker" => new DruckerView(),
            "UsbGeraete" => new UsbGeraeteView(),
#if PRO_BUILD
            "OrdnerGroesse" => new PCToolbox.Modules.Pro.OrdnerGroesseView(),
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
        var einstellungenButton = (RadioButton)NavEinstellungenText.Parent!;
        var index = SidebarPanel.Children.IndexOf(einstellungenButton);

        SidebarPanel.Children.Insert(index, new Separator { Margin = new Thickness(10, 8, 10, 8), Opacity = 0.3 });

        var ordnerGroesseButton = new RadioButton
        {
            GroupName = "Nav",
            Style = (Style)FindResource("SidebarItemStyle"),
            Content = new TextBlock { Text = Loc.T("📊  Ordnergröße", "📊  Folder Size") }
        };
        ordnerGroesseButton.Checked += (_, _) => InhaltBereich.Content = new PCToolbox.Modules.Pro.OrdnerGroesseView();
        SidebarPanel.Children.Insert(index + 1, ordnerGroesseButton);
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
    private void ZeigeDatentraeger_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DatentraegerView();
    private void ZeigeNetzwerk_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new NetzwerkView();
    private void ZeigeTreiber_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new TreiberView();
    private void ZeigeCommander_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new CommanderView();
    private void ZeigeBluescreen_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new BluescreenView();
    private void ZeigeSuperUninstaller_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new SuperUninstallerView();
    private void ZeigeBiosKompendium_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new BiosKompendiumView();
    private void ZeigeSaubermacher_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new SystemSaubermacherView();
    private void ZeigeWindowsReparatur_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new WindowsReparaturView();
    private void ZeigeRegistryKompendium_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new RegistryKompendiumView();
    private void ZeigeDrucker_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new DruckerView();
    private void ZeigeUsbGeraete_Click(object sender, RoutedEventArgs e) => InhaltBereich.Content = new UsbGeraeteView();
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
