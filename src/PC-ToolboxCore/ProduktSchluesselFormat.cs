namespace PCToolboxCore;

// Reine Formatpruefung fuer einen 25-stelligen Windows-Produktschluessel
// (XXXXX-XXXXX-XXXXX-XXXXX-XXXXX) - keine Dekodierung, nur ob eine gefundene Zeichenkette
// ueberhaupt wie ein Schluessel aussieht, bevor sie angezeigt wird.
public static class ProduktSchluesselFormat
{
    public static bool SiehtGueltigAus(string? schluessel)
    {
        if (string.IsNullOrWhiteSpace(schluessel)) return false;
        var teile = schluessel.Split('-');
        return teile.Length == 5 && teile.All(t => t.Length == 5);
    }
}
