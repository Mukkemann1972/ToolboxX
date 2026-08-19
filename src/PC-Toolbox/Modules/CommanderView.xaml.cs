using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCToolbox.Models;
using PCToolbox.Services;
using PCToolboxCore;
#if PRO_BUILD
using PCToolbox.Services.Pro;
using PCToolboxCore.Pro;
#endif

namespace PCToolbox.Modules;

public partial class CommanderView : UserControl
{
    private readonly DateiManagerService _dienst = new();
#if PRO_BUILD
    private readonly DateiSperreService _dateiSperreService = new();
#endif

    private Seite _links = null!;
    private Seite _rechts = null!;
    private Seite _aktiv = null!;

    /// <summary>Was der "OK"-Knopf der Eingabezeile gerade bewirken soll.</summary>
    private enum Eingabezweck { Keiner, Umbenennen, NeuerOrdner, AlsZipErstellen }

    private Eingabezweck _zweck = Eingabezweck.Keiner;

    public CommanderView()
    {
        InitializeComponent();

        TitelText.Text = Loc.T("Commander", "Commander");
        EinleitungText.Text = Loc.T(
            "Zwei Ordner nebeneinander - links und rechts. Du wählst auf einer Seite aus und schickst es mit einem Knopf auf die andere. Gelöscht wird immer nur in den Papierkorb, und nichts wird jemals überschrieben.",
            "Two folders side by side - left and right. You select on one side and send it to the other with a button. Deleting always only moves to the recycle bin, and nothing is ever overwritten.");
        UmbenennenButton.Content = Loc.T("Umbenennen", "Rename");
        NeuerOrdnerButton.Content = Loc.T("Neuer Ordner", "New folder");
        AlsZipButton.Content = Loc.T("Als ZIP erstellen", "Create as ZIP");
        AlsZipButton.ToolTip = Loc.T(
            "Packt die Auswahl als ZIP-Archiv in den Ordner auf der jeweils anderen Seite. ZIP-Dateien lassen sich außerdem per Doppelklick wie ein Ordner durchsuchen.",
            "Packs the selection as a ZIP archive into the folder on the other side. ZIP files can also be double-clicked to browse them like a folder.");
        LoeschenButton.Content = Loc.T("In den Papierkorb (F8)", "To recycle bin (F8)");
        EingabeOkButton.Content = Loc.T("OK", "OK");
        EingabeAbbrechenButton.Content = Loc.T("Abbrechen", "Cancel");
        LinksHochButton.ToolTip = Loc.T("Eine Ebene höher", "One level up");
        RechtsHochButton.ToolTip = Loc.T("Eine Ebene höher", "One level up");
        LinksNameSpalte.Header = Loc.T("Name", "Name");
        LinksGroesseSpalte.Header = Loc.T("Größe", "Size");
        LinksGeaendertSpalte.Header = Loc.T("Geändert", "Modified");
        RechtsNameSpalte.Header = Loc.T("Name", "Name");
        RechtsGroesseSpalte.Header = Loc.T("Größe", "Size");
        RechtsGeaendertSpalte.Header = Loc.T("Geändert", "Modified");
        LinksFilter.ToolTip = RechtsFilter.ToolTip = Loc.T("Liste nach Name filtern", "Filter list by name");

        _links = new Seite(LinksPfad, LinksListe, LinksLaufwerk, LinksTitel, LinksFilter);
        _rechts = new Seite(RechtsPfad, RechtsListe, RechtsLaufwerk, RechtsTitel, RechtsFilter);
        _aktiv = _links;

        Loaded += async (_, _) => await StartenAsync();
    }

    private async Task StartenAsync()
    {
        var laufwerke = DateiManagerService.Laufwerke();

        foreach (var seite in new[] { _links, _rechts })
        {
            seite.LaufwerkFeld.ItemsSource = laufwerke;
            seite.LaufwerkFeld.SelectedItem = laufwerke.FirstOrDefault();
        }

        // Links der eigene Benutzerordner, rechts die Dokumente: der Weg, den man in
        // neun von zehn Fällen sowieso als Erstes einschlägt.
        await WechselnAsync(_links, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        await WechselnAsync(_rechts, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        MarkierungAktualisieren();
    }

    private async Task WechselnAsync(Seite seite, string? ordner)
    {
        if (string.IsNullOrWhiteSpace(ordner)) return;

        if (!Directory.Exists(ordner))
        {
            Melden(Loc.T($"Den Ordner „{ordner}\" gibt es nicht (mehr).", $"The folder \"{ordner}\" no longer exists."), erfolg: false);
            return;
        }

        // Diese Methode navigiert ausschliesslich zu echten Ordnern - eine evtl. laufende
        // ZIP-Ansicht dieser Seite ist damit vorbei.
        seite.ZipArchivPfad = null;
        seite.ZipInnererPfad = "";

        seite.Pfad = ordner;
        seite.PfadFeld.Text = ordner;
        seite.AlleEintraege = await _dienst.AuflistenAsync(ordner);
        seite.FilterFeld.Text = "";

        FilterAnwenden(seite);

        // Laufwerksauswahl mitziehen, ohne dabei erneut einen Wechsel auszulösen.
        var laufwerk = Path.GetPathRoot(ordner);
        if (laufwerk is not null && !Equals(seite.LaufwerkFeld.SelectedItem, laufwerk))
        {
            seite.LaufwerkWechselIgnorieren = true;
            seite.LaufwerkFeld.SelectedItem = laufwerk;
            seite.LaufwerkWechselIgnorieren = false;
        }
    }

    private Seite SeiteVon(object? absender) =>
        absender is FrameworkElement { Tag: "rechts" } ? _rechts : _links;

    /// <summary>
    /// Filtert die bereits geladene Liste einer Seite nach Namen - rein clientseitig,
    /// ohne den Ordner erneut einzulesen. "..“ bleibt immer sichtbar, damit man auch
    /// während des Filterns noch eine Ebene hoch navigieren kann.
    /// </summary>
    private void FilterAnwenden(Seite seite)
    {
        var suchtext = seite.FilterFeld.Text.Trim();

        var sichtbar = string.IsNullOrEmpty(suchtext)
            ? seite.AlleEintraege
            : seite.AlleEintraege.Where(e => e.IstNachOben || e.Name.Contains(suchtext, StringComparison.CurrentCultureIgnoreCase)).ToList();

        seite.Liste.ItemsSource = sichtbar;

        var anzahlGesamt = seite.AlleEintraege.Count(e => !e.IstNachOben);
        var anzahlSichtbar = sichtbar.Count(e => !e.IstNachOben);

        seite.Titel.Text = anzahlSichtbar == anzahlGesamt
            ? Loc.T($"{anzahlGesamt} Einträge", $"{anzahlGesamt} entries")
            : Loc.T($"{anzahlSichtbar} von {anzahlGesamt} Einträgen", $"{anzahlSichtbar} of {anzahlGesamt} entries");
    }

    private void Filter_TextChanged(object sender, TextChangedEventArgs e) => FilterAnwenden(SeiteVon(sender));

    private Seite Gegenueber => _aktiv == _links ? _rechts : _links;

    private void MarkierungAktualisieren()
    {
        // Die aktive Seite bekommt einen farbigen Rahmen - sonst weiss man beim
        // Kopieren nicht, in welche Richtung es geht.
        foreach (var seite in new[] { _links, _rechts })
        {
            var istAktiv = seite == _aktiv;
            seite.Liste.BorderThickness = new Thickness(istAktiv ? 2 : 0);
            seite.Liste.BorderBrush = istAktiv ? (Brush)FindResource("AccentBrush") : Brushes.Transparent;
        }

        var richtung = Loc.T(_aktiv == _links ? "nach rechts" : "nach links", _aktiv == _links ? "to the right" : "to the left");
        KopierenButton.Content = Loc.T($"Kopieren {richtung} ➜ (F5)", $"Copy {richtung} ➜ (F5)");
        VerschiebenButton.Content = Loc.T($"Verschieben {richtung} ➜ (F6)", $"Move {richtung} ➜ (F6)");
    }

    private void Melden(string text, bool erfolg)
    {
        StatusText.Text = text;
        StatusText.Foreground = erfolg ? (Brush)FindResource("OkBrush") : (Brush)FindResource("DangerBrush");
    }

    private List<DateiEintrag> Auswahl() =>
        _aktiv.Liste.SelectedItems.Cast<DateiEintrag>().Where(e => !e.IstNachOben).ToList();

    // ================= Navigation =================

    private void Liste_Fokus(object sender, RoutedEventArgs e)
    {
        _aktiv = SeiteVon(sender);
        MarkierungAktualisieren();
    }

    private async void Liste_DoppelKlick(object sender, MouseButtonEventArgs e)
    {
        var seite = SeiteVon(sender);
        if (seite.Liste.SelectedItem is not DateiEintrag eintrag) return;

        if (seite.ZipArchivPfad is not null)
        {
            if (eintrag.IstNachOben)
            {
                await ZipHochAsync(seite);
            }
            else if (eintrag.IstOrdner)
            {
                seite.ZipInnererPfad = seite.ZipInnererPfad.Length == 0 ? eintrag.Name : $"{seite.ZipInnererPfad}/{eintrag.Name}";
                await ZipListeAktualisierenAsync(seite);
            }
            // Eine Datei innerhalb des ZIPs: bewusst nichts - Entpacken ist (noch) nicht Teil
            // dieses Moduls, siehe IDEEN_BACKLOG.md.
            return;
        }

        if (eintrag.IstOrdner)
        {
            await WechselnAsync(seite, eintrag.VollerPfad);
            return;
        }

        if (IstZipDatei(eintrag))
        {
            await ZipOeffnenAsync(seite, eintrag.VollerPfad);
            return;
        }

        DateiOeffnen(eintrag);
    }

    private static bool IstZipDatei(DateiEintrag eintrag) =>
        !eintrag.IstOrdner && Path.GetExtension(eintrag.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>Öffnet ein ZIP-Archiv "wie einen Ordner" - rein lesend, siehe Klassendoku oben.</summary>
    private async Task ZipOeffnenAsync(Seite seite, string zipDateiPfad)
    {
        seite.ZipArchivPfad = zipDateiPfad;
        seite.ZipInnererPfad = "";
        await ZipListeAktualisierenAsync(seite);
    }

    /// <summary>Eine Ebene höher innerhalb eines ZIPs - oder, wenn schon ganz oben, zurück in den echten Ordner.</summary>
    private async Task ZipHochAsync(Seite seite)
    {
        if (seite.ZipInnererPfad.Length == 0)
        {
            var enthaltenderOrdner = Path.GetDirectoryName(seite.ZipArchivPfad!) ?? seite.Pfad;
            await WechselnAsync(seite, enthaltenderOrdner);
            return;
        }

        var letzterTrenner = seite.ZipInnererPfad.LastIndexOf('/');
        seite.ZipInnererPfad = letzterTrenner < 0 ? "" : seite.ZipInnererPfad[..letzterTrenner];
        await ZipListeAktualisierenAsync(seite);
    }

    private async Task ZipListeAktualisierenAsync(Seite seite)
    {
        List<ZipVerzeichnisListe.ZipQuellEintrag> alle;
        try
        {
            alle = await _dienst.ZipEintraegeAsync(seite.ZipArchivPfad!);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Melden(Loc.T($"Das ZIP-Archiv konnte nicht gelesen werden: {e.Message}", $"The ZIP archive could not be read: {e.Message}"), false);
            var enthaltenderOrdner = Path.GetDirectoryName(seite.ZipArchivPfad!) ?? seite.Pfad;
            await WechselnAsync(seite, enthaltenderOrdner);
            return;
        }

        var kinder = ZipVerzeichnisListe.KindEintraege(alle, seite.ZipInnererPfad);

        var liste = new List<DateiEintrag> { new() { Name = "..", IstOrdner = true, IstNachOben = true } };
        liste.AddRange(kinder.Select(k => new DateiEintrag { Name = k.Name, IstOrdner = k.IstOrdner, GroesseBytes = k.GroesseBytes }));

        seite.AlleEintraege = liste;
        seite.FilterFeld.Text = "";
        FilterAnwenden(seite);

        seite.PfadFeld.Text = seite.ZipInnererPfad.Length == 0
            ? seite.ZipArchivPfad!
            : $"{seite.ZipArchivPfad}\\{seite.ZipInnererPfad.Replace('/', '\\')}";
    }

    /// <summary>
    /// Öffnet eine Datei mit dem Programm, das Windows dafür vorgesehen hat. Bei
    /// ausführbaren Dateien wird vorher gefragt - genau da passieren die Unfälle,
    /// wenn jemand etwas Fremdes doppelt anklickt.
    /// </summary>
    private void DateiOeffnen(DateiEintrag eintrag)
    {
        var endung = Path.GetExtension(eintrag.Name).ToLowerInvariant();
        string[] heikel = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr"];

        if (heikel.Contains(endung))
        {
            var antwort = MessageBox.Show(
                Loc.T(
                    $"„{eintrag.Name}\" ist ein Programm bzw. eine Befehlsdatei. Beim Öffnen wird sie " +
                    "wirklich ausgeführt.\n\nNur fortfahren, wenn du weisst, woher diese Datei stammt.\n\n" +
                    "Jetzt ausführen?",
                    $"\"{eintrag.Name}\" is a program or command file. Opening it will " +
                    "really execute it.\n\nOnly continue if you know where this file came from.\n\n" +
                    "Run it now?"),
                Loc.T("Sicherheitsabfrage", "Security confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (antwort != MessageBoxResult.Yes) return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(eintrag.VollerPfad) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Melden(Loc.T($"„{eintrag.Name}\" konnte nicht geöffnet werden - vermutlich ist kein Programm dafür eingerichtet.",
                         $"\"{eintrag.Name}\" could not be opened - probably no program is set up for it."), false);
        }
    }

    private async void Hoch_Click(object sender, RoutedEventArgs e)
    {
        var seite = SeiteVon(sender);

        if (seite.ZipArchivPfad is not null)
        {
            await ZipHochAsync(seite);
            return;
        }

        if (DateiHelfer.EineEbeneHoeher(seite.Pfad) is { } hoeher)
            await WechselnAsync(seite, hoeher);
        else
            Melden(Loc.T("Hier ist schon die oberste Ebene des Laufwerks erreicht.", "This is already the topmost level of the drive."), erfolg: false);
    }

    private async void Pfad_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var seite = SeiteVon(sender);
        await WechselnAsync(seite, seite.PfadFeld.Text.Trim());
    }

    private async void Laufwerk_Gewechselt(object sender, SelectionChangedEventArgs e)
    {
        var seite = SeiteVon(sender);
        if (seite.LaufwerkWechselIgnorieren) return;

        if (seite.LaufwerkFeld.SelectedItem is string laufwerk)
            await WechselnAsync(seite, laufwerk);
    }

    /// <summary>
    /// Tastaturkürzel wie beim Total Commander üblich: F5 kopieren, F6 verschieben,
    /// F8 in den Papierkorb. Während die Eingabezeile offen ist (Umbenennen/Neuer
    /// Ordner), bleiben die Kürzel bewusst aus - da haben Enter/Escape Vorrang und
    /// ein versehentliches F8 soll nicht mitten in einer Texteingabe löschen.
    /// </summary>
    private async void Commander_KeyDown(object sender, KeyEventArgs e)
    {
        if (_zweck != Eingabezweck.Keiner) return;

        switch (e.Key)
        {
            case Key.F5:
                e.Handled = true;
                await UebertragenAsync(verschieben: false);
                break;
            case Key.F6:
                e.Handled = true;
                await UebertragenAsync(verschieben: true);
                break;
            case Key.F8:
                e.Handled = true;
                await LoeschenAsync();
                break;
        }
    }

    // ================= Aktionen =================

    private async void Kopieren_Click(object sender, RoutedEventArgs e) => await UebertragenAsync(verschieben: false);

    private async void Verschieben_Click(object sender, RoutedEventArgs e) => await UebertragenAsync(verschieben: true);

    private async Task UebertragenAsync(bool verschieben)
    {
        if (_aktiv.ZipArchivPfad is not null || Gegenueber.ZipArchivPfad is not null)
        {
            Melden(Loc.T("Im ZIP-Modus nicht möglich - bitte erst einen echten Ordner öffnen.",
                         "Not possible in ZIP mode - please open a real folder first."), false);
            return;
        }

        var auswahl = Auswahl();
        if (auswahl.Count == 0)
        {
            Melden(Loc.T("Nichts ausgewählt. Markiere links oder rechts, was übertragen werden soll.",
                         "Nothing selected. Select on the left or right what should be transferred."), false);
            return;
        }

        var ziel = Gegenueber.Pfad;
        var quellen = auswahl.Select(a => a.VollerPfad).ToList();

        StatusText.Text = verschieben ? Loc.T("Verschiebe …", "Moving…") : Loc.T("Kopiere …", "Copying…");
        StatusText.Foreground = (Brush)FindResource("TextMutedBrush");

        var ergebnis = verschieben
            ? await _dienst.VerschiebenAsync(quellen, ziel)
            : await _dienst.KopierenAsync(quellen, ziel);

        var wort = Loc.T(verschieben ? "verschoben" : "kopiert", verschieben ? "moved" : "copied");

        Melden(ergebnis.Erfolgreich
                ? Loc.T(
                      $"{ergebnis.Erledigt} Einträge {wort}. Gleichnamiges am Ziel wurde nicht überschrieben, " +
                      "sondern als „Name (2)\" abgelegt.",
                      $"{ergebnis.Erledigt} entries {wort}. Items with the same name at the destination were not overwritten, " +
                      "but saved as \"Name (2)\" instead.")
                : Loc.T($"{ergebnis.Erledigt} {wort}, {ergebnis.Fehler.Count} nicht: {string.Join(" | ", ergebnis.Fehler.Take(3))}",
                        $"{ergebnis.Erledigt} {wort}, {ergebnis.Fehler.Count} not: {string.Join(" | ", ergebnis.Fehler.Take(3))}"),
            ergebnis.Erfolgreich);

#if PRO_BUILD
        if (!ergebnis.Erfolgreich)
            await GesperrteDateienEntsperrenUndErneutVersuchenAsync(ergebnis.Fehler, ziel, verschieben);
#endif

        await WechselnAsync(_links, _links.Pfad);
        await WechselnAsync(_rechts, _rechts.Pfad);
    }

#if PRO_BUILD
    /// <summary>
    /// Pro-only: fragt für jeden Fehleintrag, der laut HResult eine Sharing-Violation war
    /// (Free-Anwender sehen dieselbe rohe Fehlermeldung, aber keine Möglichkeit, den
    /// blockierenden Prozess zu schließen - genau die "Pro darf mehr" Grenze), welche Prozesse
    /// die jeweilige Datei blockieren, und bietet an, sie zu schließen und die EINE betroffene
    /// Datei erneut zu übertragen. Kritische Systemprozesse (DateiSperreInfo.IstKritisch) werden
    /// nie zum Schließen angeboten, auch in Pro nicht.
    /// </summary>
    private async Task GesperrteDateienEntsperrenUndErneutVersuchenAsync(
        IReadOnlyList<DateiAktionFehler> fehlerListe, string zielOrdner, bool verschieben)
    {
        foreach (var fehler in fehlerListe)
        {
            if (!SharingViolationErkennung.IstSharingViolation(fehler.HResult)) continue;

            var blockierer = _dateiSperreService.BlockierendeProzesseErmitteln(fehler.Pfad);
            if (blockierer.Count == 0) continue; // Sperre ist inzwischen von selbst weg

            var schliessbar = blockierer.Where(b => !b.IstKritisch).ToList();
            var name = Path.GetFileName(fehler.Pfad.TrimEnd('\\'));

            if (schliessbar.Count == 0)
            {
                MessageBox.Show(
                    Loc.T(
                        $"„{name}\" wird von einer kritischen Systemkomponente blockiert " +
                        $"({string.Join(", ", blockierer.Select(b => b.ProzessName))}) - die kann aus " +
                        "Sicherheitsgründen nicht geschlossen werden, auch nicht in Pro.",
                        $"\"{name}\" is blocked by a critical system component " +
                        $"({string.Join(", ", blockierer.Select(b => b.ProzessName))}) - it cannot be closed " +
                        "for safety reasons, not even in Pro."),
                    Loc.T("Datei entsperren (Pro)", "Unlock file (Pro)"), MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }

            var programmListe = string.Join("\n", schliessbar.Select(b => $"  • {b.ProzessName} (PID {b.ProzessId})"));
            var antwort = MessageBox.Show(
                Loc.T(
                    $"„{name}\" ist gesperrt, weil folgendes Programm sie gerade offen hält:\n\n{programmListe}\n\n" +
                    "Jetzt schließen und die Übertragung erneut versuchen? Ungespeicherte Änderungen in " +
                    "diesem Programm gehen dabei verloren.",
                    $"\"{name}\" is locked because the following program currently has it open:\n\n{programmListe}\n\n" +
                    "Close it now and retry the transfer? Unsaved changes in that program will be lost."),
                Loc.T("Datei entsperren (Pro)", "Unlock file (Pro)"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

            if (antwort != MessageBoxResult.Yes) continue;

            var (_, schliessenFehler) = _dateiSperreService.ProzesseSchliessen(schliessbar.Select(b => b.ProzessId));
            if (schliessenFehler.Count > 0)
            {
                Melden(string.Join(" | ", schliessenFehler), false);
                continue;
            }

            var wiederholungsErgebnis = verschieben
                ? await _dienst.VerschiebenAsync([fehler.Pfad], zielOrdner)
                : await _dienst.KopierenAsync([fehler.Pfad], zielOrdner);

            Melden(wiederholungsErgebnis.Erfolgreich
                    ? Loc.T($"„{name}\" nach Entsperren erfolgreich übertragen.", $"\"{name}\" transferred successfully after unlocking.")
                    : Loc.T($"„{name}\" auch nach Entsperren nicht übertragbar: {wiederholungsErgebnis.Fehler.FirstOrDefault()}",
                            $"\"{name}\" still could not be transferred after unlocking: {wiederholungsErgebnis.Fehler.FirstOrDefault()}"),
                wiederholungsErgebnis.Erfolgreich);
        }
    }
#endif

    private async void Loeschen_Click(object sender, RoutedEventArgs e) => await LoeschenAsync();

    private async Task LoeschenAsync()
    {
        if (_aktiv.ZipArchivPfad is not null)
        {
            Melden(Loc.T("Im ZIP-Modus nicht möglich.", "Not possible in ZIP mode."), false);
            return;
        }

        var auswahl = Auswahl();
        if (auswahl.Count == 0)
        {
            Melden(Loc.T("Nichts ausgewählt.", "Nothing selected."), false);
            return;
        }

        var antwort = MessageBox.Show(
            Loc.T(
                $"{auswahl.Count} Eintrag/Einträge in den Papierkorb verschieben?\n\n" +
                "Nichts wird endgültig gelöscht - du kannst alles im Windows-Papierkorb wiederherstellen.",
                $"Move {auswahl.Count} entry/entries to the recycle bin?\n\n" +
                "Nothing is permanently deleted - you can restore everything from the Windows recycle bin."),
            Loc.T("In den Papierkorb", "To recycle bin"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);

        if (antwort != MessageBoxResult.Yes) return;

        var ergebnis = await _dienst.InDenPapierkorbAsync(auswahl.Select(a => a.VollerPfad));

        Melden(ergebnis.Erfolgreich
                ? Loc.T($"{ergebnis.Erledigt} Einträge liegen jetzt im Papierkorb und sind von dort wiederherstellbar.",
                        $"{ergebnis.Erledigt} entries are now in the recycle bin and can be restored from there.")
                : Loc.T($"{ergebnis.Erledigt} verschoben, {ergebnis.Fehler.Count} nicht: {string.Join(" | ", ergebnis.Fehler.Take(3))}",
                        $"{ergebnis.Erledigt} moved, {ergebnis.Fehler.Count} not: {string.Join(" | ", ergebnis.Fehler.Take(3))}"),
            ergebnis.Erfolgreich);

        await WechselnAsync(_aktiv, _aktiv.Pfad);
    }

    // ================= Eingabezeile =================

    private void Umbenennen_Click(object sender, RoutedEventArgs e)
    {
        if (_aktiv.ZipArchivPfad is not null)
        {
            Melden(Loc.T("Im ZIP-Modus nicht möglich.", "Not possible in ZIP mode."), false);
            return;
        }

        var auswahl = Auswahl();
        if (auswahl.Count != 1)
        {
            Melden(Loc.T("Zum Umbenennen bitte genau einen Eintrag auswählen.", "Please select exactly one entry to rename."), false);
            return;
        }

        EingabeZeigen(Eingabezweck.Umbenennen, Loc.T("Neuer Name:", "New name:"), auswahl[0].Name);
    }

    private void NeuerOrdner_Click(object sender, RoutedEventArgs e)
    {
        if (_aktiv.ZipArchivPfad is not null)
        {
            Melden(Loc.T("Im ZIP-Modus nicht möglich.", "Not possible in ZIP mode."), false);
            return;
        }

        EingabeZeigen(Eingabezweck.NeuerOrdner, Loc.T($"Neuer Ordner in {_aktiv.Pfad}:", $"New folder in {_aktiv.Pfad}:"), Loc.T("Neuer Ordner", "New folder"));
    }

    private void AlsZip_Click(object sender, RoutedEventArgs e)
    {
        if (_aktiv.ZipArchivPfad is not null)
        {
            Melden(Loc.T("Im ZIP-Modus nicht möglich - bitte erst einen echten Ordner öffnen.",
                         "Not possible in ZIP mode - please open a real folder first."), false);
            return;
        }

        if (Gegenueber.ZipArchivPfad is not null)
        {
            Melden(Loc.T("Ziel ist eine ZIP-Ansicht, kein echter Ordner.", "Target is a ZIP view, not a real folder."), false);
            return;
        }

        var auswahl = Auswahl();
        if (auswahl.Count == 0)
        {
            Melden(Loc.T("Nichts ausgewählt.", "Nothing selected."), false);
            return;
        }

        var vorschlag = (auswahl.Count == 1 ? Path.GetFileNameWithoutExtension(auswahl[0].Name) : "Archiv") + ".zip";
        EingabeZeigen(Eingabezweck.AlsZipErstellen,
            Loc.T($"Name des ZIP-Archivs (wird in {Gegenueber.Pfad} angelegt):", $"Name of the ZIP archive (will be created in {Gegenueber.Pfad}):"),
            vorschlag);
    }

    private void EingabeZeigen(Eingabezweck zweck, string beschriftung, string vorgabe)
    {
        _zweck = zweck;
        EingabeBeschriftung.Text = beschriftung;
        EingabeFeld.Text = vorgabe;
        EingabePanel.Visibility = Visibility.Visible;
        EingabeFeld.Focus();
        EingabeFeld.SelectAll();
    }

    private void EingabeVerstecken()
    {
        _zweck = Eingabezweck.Keiner;
        EingabePanel.Visibility = Visibility.Collapsed;
    }

    private void EingabeFeld_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) EingabeOk_Click(sender, e);
        else if (e.Key == Key.Escape) EingabeVerstecken();
    }

    private async void EingabeOk_Click(object sender, RoutedEventArgs e)
    {
        var eingabe = EingabeFeld.Text.Trim();
        var zweck = _zweck;

        bool erfolg;
        string meldung;

        if (zweck == Eingabezweck.AlsZipErstellen)
        {
            var auswahl = Auswahl();
            (erfolg, meldung) = auswahl.Count > 0
                ? await _dienst.AlsZipErstellenAsync(auswahl.Select(a => a.VollerPfad).ToList(), Gegenueber.Pfad, eingabe)
                : (false, Loc.T("Die Auswahl hat sich zwischenzeitlich geändert - bitte noch einmal.", "The selection changed in the meantime - please try again."));
        }
        else
        {
            (erfolg, meldung) = zweck switch
            {
                Eingabezweck.Umbenennen when Auswahl().Count == 1 =>
                    _dienst.Umbenennen(Auswahl()[0].VollerPfad, eingabe),
                Eingabezweck.NeuerOrdner =>
                    _dienst.NeuerOrdner(_aktiv.Pfad, eingabe),
                _ => (false, Loc.T("Die Auswahl hat sich zwischenzeitlich geändert - bitte noch einmal.", "The selection changed in the meantime - please try again."))
            };
        }

        Melden(meldung, erfolg);

        if (erfolg)
        {
            EingabeVerstecken();

            if (zweck == Eingabezweck.AlsZipErstellen)
            {
                await WechselnAsync(_links, _links.Pfad);
                await WechselnAsync(_rechts, _rechts.Pfad);
            }
            else
            {
                await WechselnAsync(_aktiv, _aktiv.Pfad);
            }
        }
    }

    private void EingabeAbbrechen_Click(object sender, RoutedEventArgs e) => EingabeVerstecken();

    /// <summary>Bündelt die Bedienelemente einer der beiden Seiten.</summary>
    private class Seite(TextBox pfadFeld, ListView liste, ComboBox laufwerkFeld, TextBlock titel, TextBox filterFeld)
    {
        public TextBox PfadFeld { get; } = pfadFeld;
        public ListView Liste { get; } = liste;
        public ComboBox LaufwerkFeld { get; } = laufwerkFeld;
        public TextBlock Titel { get; } = titel;
        public TextBox FilterFeld { get; } = filterFeld;

        public string Pfad { get; set; } = "";

        /// <summary>Die zuletzt eingelesene, ungefilterte Liste - die Grundlage fürs clientseitige Filtern.</summary>
        public List<DateiEintrag> AlleEintraege { get; set; } = [];

        /// <summary>Verhindert eine Rückkopplung, wenn wir die Auswahl selbst setzen.</summary>
        public bool LaufwerkWechselIgnorieren { get; set; }

        /// <summary>Ungleich null, solange diese Seite gerade ein ZIP-Archiv statt eines echten Ordners zeigt.</summary>
        public string? ZipArchivPfad { get; set; }

        /// <summary>Pfad innerhalb des ZIPs ("" = Wurzel), Schrägstrich-getrennt wie in ZIP-Einträgen üblich.</summary>
        public string ZipInnererPfad { get; set; } = "";
    }
}
