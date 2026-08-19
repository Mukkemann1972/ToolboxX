using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using PCToolbox.Services.Pro;
using PCToolboxCore.Pro;
#endif

namespace PCToolbox.Modules;

public partial class DruckerView : UserControl
{
    private readonly DruckerService _dienst = new();

#if PRO_BUILD
    private readonly UsbGeraeteProService _proDienst = new();
    private ListView _geisterListe = null!;
    private TextBlock _geisterStatusText = null!;
    private StackPanel _geisterBereich = null!;

    private readonly DruckertreiberService _treiberDienst = new();
    private TextBlock _verwaisteTreiberStatusText = null!;
    private StackPanel _verwaisteTreiberListe = null!;

    private readonly IppDruckerReparaturService _ippDienst = new();
    private ComboBox _ippNameBox = null!;
    private TextBox _ippHostBox = null!;
    private TextBlock _ippUrlVorschauText = null!;
    private Button _ippReparierenButton = null!;
    private TextBlock _ippStatusText = null!;
#endif

    public DruckerView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Drucker", "Printers");
        QuelleHinweis.ToolTip = Loc.T(
            "WMI-Abfrage (Win32_Printer) - dieselbe Quelle, die auch die Windows-Einstellungen-App unter der Haube nutzt. Rein lesend.",
            "WMI query (Win32_Printer) - the same source the Windows Settings app uses under the hood. Read-only.");
        EinleitungText.Text = Loc.T(
            "Zeigt alle installierten Drucker mit ihrem aktuellen Status - bereit, druckt, oder ein konkreter Fehler wie \"kein Papier\" oder \"Tonerstand niedrig\", statt nur einem kryptischen Symbol im Infobereich. Reine Anzeige, es wird nichts gedruckt oder verändert.",
            "Shows all installed printers with their current status - ready, printing, or a concrete error like \"out of paper\" or \"toner low\", instead of just a cryptic icon in the system tray. Read-only, nothing is printed or changed.");
        NeuLadenButton.Content = Loc.T("Neu einlesen", "Reload");

        WarteschlangeTitel.Text = Loc.T("Drucker reagiert nicht oder druckt nicht?", "Printer not responding or not printing?");
        WarteschlangeText.Text = Loc.T(
            "Löscht nur noch nicht gedruckte Aufträge aus der Warteschlange und startet den Druckerspooler-" +
            "Dienst neu - der Klassiker unter den Fixes, wenn ein hängender Auftrag alles blockiert. Der " +
            "Drucker selbst, seine Treiber und Einstellungen bleiben unverändert.",
            "Deletes only unprinted jobs from the queue and restarts the print spooler service - the " +
            "classic fix when a stuck job blocks everything. The printer itself, its drivers and settings " +
            "stay unchanged.");
        WarteschlangeLeerenButton.Content = Loc.T("Warteschlange leeren & Spooler neu starten", "Clear queue & restart spooler");

#if PRO_BUILD
        BauGeisterBereich();
        BauVerwaisteTreiberBereich();
        BauIppReparaturBereich();
#endif

        Loaded += async (_, _) =>
        {
            try
            {
                await LadenAsync();
#if PRO_BUILD
                await GeisterLadenAsync();
                await VerwaisteTreiberLadenAsync();
#endif
            }
            catch (Exception ex)
            {
                DiagnoseLog.Schreiben(nameof(DruckerView), ex.ToString());
                MessageBox.Show(
                    Loc.T($"Fehler beim Laden dieses Moduls:\n\n{ex}", $"Error loading this module:\n\n{ex}"),
                    "DruckerView – Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    private async void NeuLaden_Click(object sender, RoutedEventArgs e)
    {
        await LadenAsync();
#if PRO_BUILD
        await GeisterLadenAsync();
        await VerwaisteTreiberLadenAsync();
#endif
    }

    private async void WarteschlangeLeeren_Click(object sender, RoutedEventArgs e)
    {
        WarteschlangeLeerenButton.IsEnabled = false;
        WarteschlangeStatusText.Text = Loc.T("Leere Warteschlange …", "Clearing queue…");
        WarteschlangeStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var (erfolg, meldung) = await _dienst.WarteschlangeLeerenAsync();

        WarteschlangeStatusText.Text = (erfolg ? "✔ " : "✘ ") + meldung;
        WarteschlangeStatusText.Foreground = (Brush)FindResource(erfolg ? "OkBrush" : "DangerBrush");
        WarteschlangeLeerenButton.IsEnabled = true;

        if (erfolg) await LadenAsync();
    }

    private async Task LadenAsync()
    {
        NeuLadenButton.IsEnabled = false;

        var drucker = await _dienst.GetDruckerAsync();

#if PRO_BUILD
        var vorherAusgewaehlt = _ippNameBox.Text;
        _ippNameBox.ItemsSource = drucker.Select(d => d.Name).ToList();
        _ippNameBox.Text = vorherAusgewaehlt;
#endif

        Liste.ItemsSource = drucker.Count == 0
            ? new[] { new DruckerAnzeigeZeile
              {
                  Name = Loc.T("Keine Drucker gefunden", "No printers found"),
                  BefundText = Loc.T("Entweder ist kein Drucker installiert, oder der Druckerspooler-Dienst läuft nicht.",
                                     "Either no printer is installed, or the print spooler service isn't running."),
                  Symbol = AmpelStatus.Unbekannt.Symbol(),
                  AmpelFarbe = FarbeFuer(AmpelStatus.Unbekannt),
                  DetailZeile = "",
                  StandardSichtbar = Visibility.Collapsed
              } }
            : drucker.Select(ZuAnzeigeZeile).ToList();

        NeuLadenButton.IsEnabled = true;
    }

    private DruckerAnzeigeZeile ZuAnzeigeZeile(DruckerInfo d) => new()
    {
        Name = d.Name,
        BefundText = d.BefundText,
        Symbol = d.Ampel.Symbol(),
        AmpelFarbe = FarbeFuer(d.Ampel),
        DetailZeile = Loc.T(
            $"{(d.IstNetzwerkdrucker ? "Netzwerkdrucker" : "Lokal angeschlossen")} · Anschluss: {d.AnschlussName} · Treiber: {d.TreiberName}",
            $"{(d.IstNetzwerkdrucker ? "Network printer" : "Locally connected")} · Port: {d.AnschlussName} · Driver: {d.TreiberName}"),
        StandardSichtbar = d.IstStandard ? Visibility.Visible : Visibility.Collapsed
    };

    private Brush FarbeFuer(AmpelStatus ampel) => ampel switch
    {
        AmpelStatus.Gruen => (Brush)FindResource("OkBrush"),
        AmpelStatus.Gelb => (Brush)FindResource("WarnBrush"),
        AmpelStatus.Rot => (Brush)FindResource("DangerBrush"),
        _ => (Brush)FindResource("TextMutedBrush")
    };

    private class DruckerAnzeigeZeile
    {
        public string Name { get; set; } = "";
        public string BefundText { get; set; } = "";
        public string Symbol { get; set; } = "";
        public Brush AmpelFarbe { get; set; } = Brushes.Gray;
        public string DetailZeile { get; set; } = "";
        public Visibility StandardSichtbar { get; set; }
    }

#if PRO_BUILD
    /// <summary>
    /// Häufigster Praxisfall: ein USB-Drucker wurde an einem anderen PC benutzt (z.B. verliehen)
    /// und wird am eigenen PC nicht mehr erkannt. Windows merkt sich unter der Haube trotzdem
    /// jedes je angeschlossene USB-Gerät (s. UsbGeraeteProService/GeisterUsbGeraete) - hier
    /// dieselbe Technik, aber auf die Geräteklasse "Printer" eingegrenzt, damit man dafür nicht
    /// erst wissen muss, dass das eigentliche Werkzeug im "USB-Geräte"-Modul steckt.
    /// </summary>
    private void BauGeisterBereich()
    {
        _geisterBereich = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        _geisterBereich.Children.Add(new TextBlock
        {
            Text = Loc.T("Drucker wird nicht erkannt?", "Printer not recognized?"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });
        _geisterBereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Wurde der Drucker an einem anderen PC benutzt (z.B. verliehen) und meldet sich hier nicht mehr, " +
                "hilft oft: den alten, verwaisten Geräte-Eintrag unten entfernen und den Drucker neu anstecken - " +
                "Windows installiert ihn dann wie ein brandneues Gerät neu. Nur der Windows-interne Eintrag wird " +
                "gelöscht, der Drucker selbst ist davon nicht betroffen.",
                "If the printer was used on another PC (e.g. lent out) and isn't recognized here anymore, it " +
                "often helps to remove the old, orphaned device entry below and plug the printer back in - " +
                "Windows will then install it fresh, like a brand-new device. Only the internal Windows entry " +
                "is deleted, the printer itself is unaffected."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        _geisterStatusText = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };
        _geisterBereich.Children.Add(_geisterStatusText);

        _geisterListe = new ListView { Background = (Brush)FindResource("BgCardBrush"), BorderThickness = new Thickness(0) };
        var gridView = new GridView();

        gridView.Columns.Add(new GridViewColumn { Width = 280, Header = Loc.T("Name", "Name"), DisplayMemberBinding = new Binding("Name") });
        gridView.Columns.Add(new GridViewColumn { Width = 180, Header = Loc.T("Zuletzt gesehen", "Last seen"), DisplayMemberBinding = new Binding("LetzteAktivitaetAnzeige") });

        var entfernenButtonFactory = new FrameworkElementFactory(typeof(Button));
        entfernenButtonFactory.SetBinding(ContentControl.ContentProperty, new Binding("EntfernenText"));
        entfernenButtonFactory.SetValue(Button.PaddingProperty, new Thickness(10, 4, 10, 4));
        entfernenButtonFactory.SetBinding(FrameworkElement.TagProperty, new Binding("PnpDeviceId"));
        entfernenButtonFactory.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)GeraetEntfernen_Click);
        gridView.Columns.Add(new GridViewColumn { Width = 140, CellTemplate = new DataTemplate { VisualTree = entfernenButtonFactory } });

        _geisterListe.View = gridView;
        _geisterListe.PreviewMouseWheel += (_, e) =>
        {
            HauptScroll.ScrollToVerticalOffset(HauptScroll.VerticalOffset - e.Delta);
            e.Handled = true;
        };
        _geisterBereich.Children.Add(_geisterListe);

        ProZusatzBereich.Children.Add(_geisterBereich);
    }

    private async Task GeisterLadenAsync()
    {
        _geisterStatusText.Text = Loc.T("Lese Geräteverlauf ein …", "Reading device history…");

        var geister = await _proDienst.LadeGeisterDruckerAsync();

        _geisterListe.ItemsSource = geister.Select(g => new GeisterZeile
        {
            PnpDeviceId = g.PnpDeviceId,
            Name = g.Name,
            LetzteAktivitaetAnzeige = g.LetzteAktivitaet is { } zeit ? zeit.ToString("dd.MM.yyyy HH:mm") : Loc.T("unbekannt", "unknown"),
            EntfernenText = Loc.T("Entfernen", "Remove")
        }).ToList();

        _geisterStatusText.Text = geister.Count == 0
            ? Loc.T("Keine früheren USB-Drucker gefunden - entweder noch angeschlossen oder nie einer registriert.",
                    "No earlier USB printers found - either still connected, or none was ever registered.")
            : Loc.T($"{geister.Count} frühere USB-Drucker gefunden.", $"{geister.Count} earlier USB printer(s) found.");
    }

    private async void GeraetEntfernen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string pnpDeviceId } button) return;

        var name = (_geisterListe.ItemsSource as IEnumerable<GeisterZeile>)?.FirstOrDefault(z => z.PnpDeviceId == pnpDeviceId)?.Name ?? pnpDeviceId;

        var antwort = MessageBox.Show(
            Loc.T(
                $"„{name}\" wirklich entfernen?\n\nDas löscht nur den Windows-internen Eintrag für dieses Gerät. " +
                "Wird der Drucker nochmal angeschlossen, meldet er sich ganz normal neu an - nichts geht dabei verloren.\n\n" +
                "Windows fragt im nächsten Schritt nach einer Administratorbestätigung.",
                $"Really remove \"{name}\"?\n\nThis only deletes the internal Windows entry for this device. " +
                "If the printer is connected again, it will register normally - nothing is lost.\n\n" +
                "Windows will ask for an administrator confirmation in the next step."),
            Loc.T("Gerät entfernen", "Remove device"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        button.IsEnabled = false;
        var erfolg = await Task.Run(() => _proDienst.GeraetEntfernen(pnpDeviceId));

        _geisterStatusText.Text = erfolg
            ? Loc.T($"„{name}\" wurde entfernt.", $"\"{name}\" was removed.")
            : Loc.T($"„{name}\" konnte nicht entfernt werden - entweder wurde die Bestätigung abgelehnt, oder Windows kennt diesen Eintrag so nicht (mehr).",
                    $"\"{name}\" could not be removed - either the confirmation was declined, or Windows no longer recognizes this entry.");

        await GeisterLadenAsync();
    }

    /// <summary>
    /// Anderer Fall als die Geister-GERÄTE-Sektion oben: hier geht es um Treiber-SOFTWARE-Pakete
    /// (Win32_PrinterDriver), die bleiben, nachdem der zugehörige Drucker längst deinstalliert
    /// wurde - unabhängig davon, ob der je per USB lief. Windows hält einige eigene Klassen-/
    /// Kompatibilitätstreiber absichtlich dauerhaft vor (s. GeisterDruckertreiber-Schutzliste im
    /// Kern) - die tauchen hier bewusst nie auf.
    /// </summary>
    private void BauVerwaisteTreiberBereich()
    {
        var bereich = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        bereich.Children.Add(new TextBlock
        {
            Text = Loc.T("Verwaiste Druckertreiber (Pro)", "Orphaned printer drivers (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });
        bereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Treibersoftware, die von keinem installierten Drucker mehr benutzt wird - meist, weil der " +
                "Drucker längst entfernt wurde, das Treiberpaket aber liegen blieb. Windows-eigene Klassen- und " +
                "Kompatibilitätstreiber werden hier nie vorgeschlagen, auch wenn sie gerade kein Drucker " +
                "referenziert - die gehören zur Windows-Druckinfrastruktur, nicht zu einem einzelnen Gerät.",
                "Driver software no longer used by any installed printer - usually because the printer was " +
                "removed long ago but the driver package stayed behind. Windows' own class and compatibility " +
                "drivers are never suggested here, even if no printer currently references them - they belong " +
                "to Windows' printing infrastructure, not to a single device."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        _verwaisteTreiberStatusText = new TextBlock { FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
        bereich.Children.Add(_verwaisteTreiberStatusText);

        _verwaisteTreiberListe = new StackPanel();
        bereich.Children.Add(_verwaisteTreiberListe);

        ProZusatzBereich.Children.Add(bereich);
    }

    private async Task VerwaisteTreiberLadenAsync()
    {
        _verwaisteTreiberStatusText.Text = Loc.T("Suche verwaiste Treiber …", "Searching for orphaned drivers…");
        _verwaisteTreiberListe.Children.Clear();

        var treiber = await _treiberDienst.GeisterErmittelnAsync();

        _verwaisteTreiberStatusText.Text = treiber.Count == 0
            ? Loc.T("Keine verwaisten Treiber gefunden.", "No orphaned drivers found.")
            : Loc.T($"{treiber.Count} verwaiste(r) Treiber gefunden.", $"{treiber.Count} orphaned driver(s) found.");

        foreach (var name in treiber)
        {
            var zeile = new Border
            {
                Background = (Brush)FindResource("BgCardBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var innen = new DockPanel();

            var entfernenButton = new Button
            {
                Content = Loc.T("Entfernen", "Remove"),
                Padding = new Thickness(10, 4, 10, 4)
            };
            entfernenButton.Click += async (_, _) => await TreiberEntfernenAsync(name, entfernenButton);
            DockPanel.SetDock(entfernenButton, Dock.Right);
            innen.Children.Add(entfernenButton);

            innen.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            zeile.Child = innen;
            _verwaisteTreiberListe.Children.Add(zeile);
        }
    }

    private async Task TreiberEntfernenAsync(string treiberName, Button auslöser)
    {
        var antwort = MessageBox.Show(
            Loc.T(
                $"Treiberpaket „{treiberName}\" wirklich entfernen?\n\nDas löscht nur die Treibersoftware, " +
                "kein aktueller Drucker benutzt sie. Wird künftig ein Drucker angeschlossen, der diesen " +
                "Treiber braucht, installiert Windows ihn bei Bedarf neu.\n\n" +
                "Windows fragt im nächsten Schritt nach einer Administratorbestätigung.",
                $"Really remove the driver package \"{treiberName}\"?\n\nThis only deletes the driver " +
                "software - no current printer uses it. If a printer needing this driver is connected in " +
                "the future, Windows will reinstall it as needed.\n\n" +
                "Windows will ask for an administrator confirmation in the next step."),
            Loc.T("Treiber entfernen", "Remove driver"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        auslöser.IsEnabled = false;
        var (erfolg, meldung) = await _treiberDienst.TreiberEntfernenAsync(treiberName);

        _verwaisteTreiberStatusText.Text = (erfolg ? "✔ " : "✘ ") + meldung;
        _verwaisteTreiberStatusText.Foreground = (Brush)FindResource(erfolg ? "OkBrush" : "DangerBrush");

        if (erfolg) await VerwaisteTreiberLadenAsync();
        else auslöser.IsEnabled = true;
    }

    /// <summary>
    /// Alex' eigene Werkstatt-Erkenntnis (HP Smart Tank 655): statt des oft launischen
    /// OEM-Treibers den Drucker über Windows' eingebauten, treiberlosen "Microsoft IPP Class
    /// Driver" neu einrichten. Entfernt die alte Warteschlange und legt sie über die
    /// IPP-Adresse neu an (s. PCToolboxCore.Pro.IppReparaturHelfer für die Details/Quelle).
    /// Das alte Treiberpaket bleibt dabei liegen - dafür ist der Bereich "Verwaiste
    /// Druckertreiber" direkt darüber da, keine Dopplung.
    /// </summary>
    private void BauIppReparaturBereich()
    {
        var bereich = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };

        bereich.Children.Add(new TextBlock
        {
            Text = Loc.T("IPP-Reparatur (Pro)", "IPP repair (Pro)"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 4)
        });
        bereich.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Drucker macht mit seinem eigenen (OEM-)Treiber Probleme? Entfernt die Warteschlange und " +
                "richtet sie über Windows' eingebauten, treiberlosen IPP-Standard neu ein - kein Hersteller-" +
                "Treiber nötig, funktioniert bei den meisten Netzwerkdruckern der letzten Jahre (\"IPP " +
                "Everywhere\"). Die IP-Adresse steht meist im Netzwerk-Menü am Drucker selbst oder im Router.",
                "Printer causing trouble with its own (OEM) driver? Removes the queue and sets it up again via " +
                "Windows' built-in, driverless IPP standard - no manufacturer driver needed, works with most " +
                "network printers from the last few years (\"IPP Everywhere\"). The IP address is usually shown " +
                "in the printer's own network menu or in the router."),
            Foreground = (Brush)FindResource("TextMutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        });

        bereich.Children.Add(new TextBlock { Text = Loc.T("Druckername", "Printer name"), FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        _ippNameBox = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 8) };
        // ComboBox selbst hat kein TextChanged - bei IsEditable=true steckt intern eine
        // TextBox, deren geroutetes Event hier abgefangen wird (Standard-WPF-Muster).
        _ippNameBox.AddHandler(TextBoxBase.TextChangedEvent, (TextChangedEventHandler)((_, _) => IppZustandAktualisieren()));
        bereich.Children.Add(_ippNameBox);

        bereich.Children.Add(new TextBlock { Text = Loc.T("IP-Adresse oder Hostname des Druckers", "Printer's IP address or hostname"), FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        _ippHostBox = new TextBox { Margin = new Thickness(0, 0, 0, 4) };
        _ippHostBox.TextChanged += (_, _) => IppZustandAktualisieren();
        bereich.Children.Add(_ippHostBox);

        _ippUrlVorschauText = new TextBlock { FontSize = 11, Foreground = (Brush)FindResource("TextMutedBrush"), Margin = new Thickness(0, 0, 0, 10) };
        bereich.Children.Add(_ippUrlVorschauText);

        _ippReparierenButton = new Button
        {
            Content = Loc.T("Jetzt über IPP reparieren", "Repair via IPP now"),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8),
            IsEnabled = false
        };
        _ippReparierenButton.Click += IppReparieren_Click;
        bereich.Children.Add(_ippReparierenButton);

        _ippStatusText = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        bereich.Children.Add(_ippStatusText);

        ProZusatzBereich.Children.Add(bereich);
        IppZustandAktualisieren();
    }

    private void IppZustandAktualisieren()
    {
        var name = _ippNameBox.Text?.Trim() ?? "";
        var host = _ippHostBox.Text?.Trim() ?? "";

        if (host.Length == 0)
        {
            _ippUrlVorschauText.Text = Loc.T("IP-Adresse eingeben, um die IPP-Adresse zu sehen.", "Enter an IP address to see the IPP address.");
            _ippReparierenButton.IsEnabled = false;
            return;
        }

        var url = IppReparaturHelfer.IppUrlBauen(host);
        _ippUrlVorschauText.Text = Loc.T($"IPP-Adresse: {url}", $"IPP address: {url}");
        _ippReparierenButton.IsEnabled = name.Length > 0 && IppReparaturHelfer.IstGueltigeUrl(url);
    }

    private async void IppReparieren_Click(object sender, RoutedEventArgs e)
    {
        var name = _ippNameBox.Text.Trim();
        var url = IppReparaturHelfer.IppUrlBauen(_ippHostBox.Text.Trim());

        var antwort = MessageBox.Show(
            Loc.T(
                $"Drucker „{name}\" wirklich über IPP neu einrichten?\n\nDie bestehende Druckerwarteschlange " +
                $"„{name}\" wird entfernt und unter IPP-Adresse {url} neu angelegt. Das alte Treiberpaket " +
                "bleibt unangetastet liegen (kann danach oben im Bereich \"Verwaiste Druckertreiber\" entfernt " +
                "werden, falls gewünscht).\n\nWindows fragt im nächsten Schritt nach einer Administratorbestätigung.",
                $"Really set up the printer \"{name}\" via IPP?\n\nThe existing print queue \"{name}\" will be " +
                $"removed and recreated at IPP address {url}. The old driver package stays untouched (can be " +
                "removed afterward in the \"Orphaned printer drivers\" section above, if desired).\n\nWindows " +
                "will ask for an administrator confirmation in the next step."),
            Loc.T("Über IPP reparieren", "Repair via IPP"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

        if (antwort != MessageBoxResult.Yes) return;

        _ippReparierenButton.IsEnabled = false;
        _ippStatusText.Text = Loc.T("Richte über IPP ein …", "Setting up via IPP…");
        _ippStatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var (erfolg, meldung) = await _ippDienst.ReparierenAsync(name, url);

        _ippStatusText.Text = (erfolg ? "✔ " : "✘ ") + meldung;
        _ippStatusText.Foreground = (Brush)FindResource(erfolg ? "OkBrush" : "DangerBrush");
        _ippReparierenButton.IsEnabled = true;

        if (erfolg)
        {
            await LadenAsync();
            await VerwaisteTreiberLadenAsync();
        }
    }
#endif
}
