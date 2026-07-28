using PCToolboxCore;

namespace PCToolbox.Models;

/// <summary>Ein Speicherriegel, so wie er im Steckplatz sitzt.</summary>
public class SpeicherRiegel
{
    public string Steckplatz { get; set; } = "";
    public double GroesseGb { get; set; }
    public string Typ { get; set; } = "";
    public string Bauform { get; set; } = "";
    public int? TaktMhz { get; set; }
    public string? Hersteller { get; set; }
    public string? Teilenummer { get; set; }

    public string Ueberschrift => $"{GroesseGb:0.#} GB {Typ}".Trim();

    public string Zeile2
    {
        get
        {
            var teile = new List<string> { Loc.T($"Steckplatz {Steckplatz}", $"Slot {Steckplatz}") };

            if (!string.IsNullOrWhiteSpace(Bauform)) teile.Add(Bauform);
            if (TaktMhz is > 0) teile.Add($"{TaktMhz} MHz");
            if (!string.IsNullOrWhiteSpace(Hersteller)) teile.Add(Hersteller!);
            if (!string.IsNullOrWhiteSpace(Teilenummer)) teile.Add(Teilenummer!);

            return string.Join("   ·   ", teile);
        }
    }

    public (string Anzeige, string Adresse)? Link => HerstellerLinks.Finde(Hersteller ?? Teilenummer);
}

/// <summary>Ein Laufwerk - Festplatte, SSD oder auch ein DVD-/Blu-ray-Brenner.</summary>
public class LaufwerkInfo
{
    public string Modell { get; set; } = "";
    public string Art { get; set; } = "";
    public double? GroesseGb { get; set; }
    public string? Schnittstelle { get; set; }
    public bool IstOptisch { get; set; }
    public string? Laufwerksbuchstabe { get; set; }

    public string Ueberschrift => Modell;

    public string Zeile2
    {
        get
        {
            var teile = new List<string> { Art };

            if (GroesseGb is > 0) teile.Add($"{GroesseGb:0.#} GB");
            if (!string.IsNullOrWhiteSpace(Laufwerksbuchstabe)) teile.Add(Loc.T($"Laufwerk {Laufwerksbuchstabe}", $"Drive {Laufwerksbuchstabe}"));
            if (!string.IsNullOrWhiteSpace(Schnittstelle)) teile.Add(Loc.T($"Anschluss: {Schnittstelle}", $"Interface: {Schnittstelle}"));

            return string.Join("   ·   ", teile);
        }
    }

    public (string Anzeige, string Adresse)? Link => HerstellerLinks.Finde(Modell);
}
