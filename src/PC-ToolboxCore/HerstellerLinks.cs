namespace PCToolboxCore;

/// <summary>
/// Ordnet einem Hersteller-Namen aus den Windows-Angaben die offizielle Support-Seite zu.
///
/// Bewusst eine kleine, handverlesene Liste der Hersteller, die einem bei PC-Hardware
/// tatsächlich begegnen - und bewusst nur die **offiziellen** Adressen. Kein Umweg über
/// Suchmaschinen oder Treiber-Portale: Genau dort holt man sich die untergeschobenen
/// "Treiber-Assistenten" statt des echten Treibers.
///
/// Windows liefert Herstellernamen völlig uneinheitlich ("Samsung", "SAMSUNG",
/// "Samsung Electronics", bei RAM sogar als Code wie "802C" für Micron). Deshalb wird
/// auf Teilzeichenfolgen geprüft statt auf Gleichheit.
/// </summary>
public static class HerstellerLinks
{
    private static readonly (string[] Muster, string Anzeige, string Adresse)[] Bekannte =
    [
        (["nvidia"], "NVIDIA", "https://www.nvidia.com/de-de/geforce/drivers/"),
        (["amd", "radeon", "ati "], "AMD", "https://www.amd.com/de/support"),
        (["intel"], "Intel", "https://www.intel.de/content/www/de/de/download-center/home.html"),
        (["samsung"], "Samsung", "https://semiconductor.samsung.com/consumer-storage/support/"),
        (["western digital", "wdc", "sandisk"], "Western Digital / SanDisk", "https://support-de.wd.com/"),
        (["seagate"], "Seagate", "https://www.seagate.com/de/de/support/"),
        (["crucial", "micron", "802c"], "Crucial / Micron", "https://www.crucial.de/support"),
        (["kingston", "0198"], "Kingston", "https://www.kingston.com/de/support"),
        (["corsair", "029e"], "Corsair", "https://help.corsair.com/"),
        (["g.skill", "gskill", "04cd"], "G.Skill", "https://www.gskill.com/support"),
        (["hynix", "hyundai", "ad00"], "SK hynix", "https://www.skhynix.com/support/"),
        (["asus", "asustek"], "ASUS", "https://www.asus.com/de/support/"),
        (["msi", "micro-star"], "MSI", "https://de.msi.com/support"),
        (["gigabyte"], "Gigabyte", "https://www.gigabyte.com/de/Support"),
        (["asrock"], "ASRock", "https://www.asrock.com/support/"),
        (["realtek"], "Realtek", "https://www.realtek.com/downloads/"),
        (["logitech", "logitech"], "Logitech", "https://support.logi.com/hc/de"),
        (["hewlett", "hp inc", "hewlett-packard"], "HP", "https://support.hp.com/de-de/"),
        (["dell"], "Dell", "https://www.dell.com/support/home/de-de"),
        (["lenovo"], "Lenovo", "https://support.lenovo.com/de/de"),
        (["acer"], "Acer", "https://www.acer.com/de-de/support"),
        (["toshiba", "kioxia"], "Toshiba / Kioxia", "https://europe.kioxia.com/de-de/personal/support.html"),
        (["pioneer"], "Pioneer", "https://www.pioneer-mea.com/de/support/"),
        (["lg electronics", "hl-dt-st"], "LG", "https://www.lg.com/de/support"),
        (["asus drw", "asus bw"], "ASUS", "https://www.asus.com/de/support/")
    ];

    /// <summary>
    /// Support-Adresse zum Hersteller - oder null, wenn wir ihn nicht sicher zuordnen
    /// können. Dann wird bewusst KEIN Link angezeigt: Ein falscher Link ist schlimmer
    /// als gar keiner.
    /// </summary>
    public static (string Anzeige, string Adresse)? Finde(string? herstellerOderModell)
    {
        if (string.IsNullOrWhiteSpace(herstellerOderModell)) return null;

        var text = herstellerOderModell.ToLowerInvariant();

        foreach (var (muster, anzeige, adresse) in Bekannte)
            if (muster.Any(m => text.Contains(m, StringComparison.Ordinal)))
                return (anzeige, adresse);

        return null;
    }
}
