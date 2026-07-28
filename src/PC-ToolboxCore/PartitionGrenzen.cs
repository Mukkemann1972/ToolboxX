using System.Text.Json;

namespace PCToolboxCore;

/// <summary>
/// Liest die Antwort von Get-PartitionSupportedSize (JSON) aus: Wie klein und wie groß
/// darf diese Partition werden? Diese Grenzen kommen von Windows selbst - sie
/// berücksichtigen, wo die Daten physisch liegen und was dahinter noch frei ist.
///
/// Ohne diese Werte müsste man raten und den Fehlschlag abwarten; mit ihnen kann die
/// Oberfläche von vornherein nur Sinnvolles anbieten.
/// </summary>
public static class PartitionGrenzen
{
    public static (long Min, long Max)? Lesen(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var dokument = JsonDocument.Parse(json);
            var wurzel = dokument.RootElement;

            // Bei mehreren Partitionen liefert PowerShell ein Feld statt eines Objekts.
            if (wurzel.ValueKind == JsonValueKind.Array)
            {
                if (wurzel.GetArrayLength() == 0) return null;
                wurzel = wurzel[0];
            }

            if (wurzel.ValueKind != JsonValueKind.Object) return null;

            var min = ZahlVon(wurzel, "SizeMin");
            var max = ZahlVon(wurzel, "SizeMax");

            return min is null || max is null ? null : (min.Value, max.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? ZahlVon(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var wert)) return null;

        return wert.ValueKind switch
        {
            JsonValueKind.Number when wert.TryGetInt64(out var zahl) => zahl,
            // Sehr große Werte kommen von PowerShell gelegentlich als Text.
            JsonValueKind.String when long.TryParse(wert.GetString(), out var ausText) => ausText,
            _ => null
        };
    }
}
