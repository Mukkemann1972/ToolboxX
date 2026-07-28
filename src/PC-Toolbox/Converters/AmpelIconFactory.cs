using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PCToolbox.Models;

namespace PCToolbox.Converters;

// Baut die Ampel-Symbole (bisher Emoji ✅⚠️❌❔) als eigene Vektorgrafik statt
// auf die Emoji-Schriftart des jeweiligen Windows angewiesen zu sein (die je nach
// Version/Sprache unterschiedlich aussieht oder fehlt). Ein Design-Raster (24x24),
// per Viewbox auf die gewuenschte Groesse skaliert - bleibt auf jeder Aufloesung scharf.
internal static class AmpelIconFactory
{
    public static UIElement? Build(string emoji, double size = 16)
    {
        var status = emoji.Trim() switch
        {
            "✅" => AmpelStatus.Gruen,
            "⚠️" or "⚠" => AmpelStatus.Gelb,
            "❌" => AmpelStatus.Rot,
            "❔" or "❓" => AmpelStatus.Unbekannt,
            _ => (AmpelStatus?)null
        };
        return status is null ? null : Build(status.Value, size);
    }

    public static UIElement Build(AmpelStatus status, double size = 16)
    {
        var (fill, glyph) = status switch
        {
            AmpelStatus.Gruen => (LookupBrush("OkBrush", Brushes.LimeGreen), Glyph.Check),
            AmpelStatus.Gelb => (LookupBrush("WarnBrush", Brushes.Orange), Glyph.Exclamation),
            AmpelStatus.Rot => (LookupBrush("DangerBrush", Brushes.OrangeRed), Glyph.Cross),
            _ => (LookupBrush("TextMutedBrush", Brushes.Gray), Glyph.Question)
        };

        var canvas = new Canvas { Width = 24, Height = 24 };

        if (status == AmpelStatus.Gelb)
        {
            // Warndreieck statt Kreis - optisch von Gruen/Rot unterscheidbar auch fuer Farbenblinde.
            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection { new(12, 2), new(22.5, 21), new(1.5, 21) },
                Fill = fill,
                StrokeLineJoin = PenLineJoin.Round
            });
        }
        else
        {
            canvas.Children.Add(new Ellipse { Width = 22, Height = 22, Fill = fill, Margin = new Thickness(1) });
        }

        canvas.Children.Add(BuildGlyph(glyph));

        return new Viewbox
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center,
            Child = canvas
        };
    }

    private enum Glyph { Check, Exclamation, Cross, Question }

    private static UIElement BuildGlyph(Glyph glyph)
    {
        switch (glyph)
        {
            case Glyph.Check:
                return new Path
                {
                    Data = Geometry.Parse("M 6,12.5 L 10,17 L 18,7"),
                    Stroke = Brushes.White,
                    StrokeThickness = 2.6,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
            case Glyph.Cross:
                return new Path
                {
                    Data = Geometry.Parse("M 7,7 L 17,17 M 17,7 L 7,17"),
                    Stroke = Brushes.White,
                    StrokeThickness = 2.6,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
            case Glyph.Exclamation:
                var group = new GeometryGroup();
                group.Children.Add(new RectangleGeometry(new Rect(11, 8, 2, 6.5), 1, 1));
                group.Children.Add(new EllipseGeometry(new Point(12, 17.5), 1.15, 1.15));
                return new Path { Data = group, Fill = Brushes.White };
            default:
                return new TextBlock
                {
                    Text = "?",
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = Brushes.White,
                    Width = 24,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                };
        }
    }

    private static Brush LookupBrush(string resourceKey, Brush fallback)
    {
        var found = Application.Current?.TryFindResource(resourceKey) as Brush;
        return found ?? fallback;
    }
}
