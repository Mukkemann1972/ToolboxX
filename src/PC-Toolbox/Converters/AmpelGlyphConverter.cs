using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PCToolbox.Converters;

// Fuer Bindungen, die NUR das Ampel-Emoji als eigenen String liefern
// (z.B. AutostartView/DiensteView/NetzwerkView/TreiberView "Symbol").
// Unbekannte Emoji (z.B. Pause ⏸️, Schloss 🔒 - keine Ampel-Zustaende)
// werden bewusst unveraendert als Text durchgereicht statt zu verschwinden.
public sealed class AmpelGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var size = parameter is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 16;
        return AmpelIconFactory.Build(text, size) ?? (object)TextBlockFallback(text);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static UIElement TextBlockFallback(string text) => new System.Windows.Controls.TextBlock { Text = text };
}
