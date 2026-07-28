using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace PCToolbox.Converters;

// Fuer Bindungen, die Ampel-Emoji + Text in EINEM String liefern
// (z.B. DiskCheckView/SuperUninstallerView/SystemSaubermacherView/TemperatureView/
// BiosKompendiumView "Titel" als $"{status.Symbol()} {name}"). Erkennt das fuehrende
// Ampel-Emoji, ersetzt NUR das durch die Vektorgrafik, der Rest bleibt normaler Text.
// Kein bekanntes Ampel-Emoji am Anfang (z.B. Klartext ohne Praefix) -> unveraendert.
public sealed class AmpelPrefixTextConverter : IValueConverter
{
    private static readonly string[] Praefixe = { "✅", "⚠️", "⚠", "❌", "❔", "❓" };

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var size = parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 15;

        foreach (var praefix in Praefixe)
        {
            if (!text.StartsWith(praefix, StringComparison.Ordinal)) continue;

            var icon = AmpelIconFactory.Build(praefix, size);
            if (icon is null) break;

            var rest = text[praefix.Length..].TrimStart();
            var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
            block.Inlines.Add(new InlineUIContainer(icon) { BaselineAlignment = BaselineAlignment.Center });
            block.Inlines.Add(new Run(" " + rest));
            return block;
        }

        return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
