namespace PCToolbox.Services;

// Erlaubt es einem Modul, eine Navigation zu einem anderen Modul auszuloesen (Alex' "Sprungmarken"-
// Idee, 19.08.2026: ein Hinweistext wie "erst den Stick als FAT32 formatieren" verlinkt direkt ins
// Datentraeger-Modul statt dass man selbst suchen muss) - ohne dass Module sich gegenseitig
// referenzieren muessen. MainWindow abonniert einmalig und leitet an die bestehende
// NavigiereZu(string)-Logik weiter - derselbe Mechanismus, den die Uebersicht-Kacheln schon nutzen,
// nur jetzt auch von INNERHALB eines Moduls heraus auslösbar.
//
// V1 bewusst nur Inhalts-Navigation, wie bei den Uebersicht-Kacheln - die Sidebar-Markierung wird
// (noch) nicht mitgezogen, das war vorher schon so und wird hier nicht extra nachgezogen.
public static class ModulNavigation
{
    public static event Action<string>? SprungAngefordert;

    public static void SpringeZu(string modul) => SprungAngefordert?.Invoke(modul);
}
