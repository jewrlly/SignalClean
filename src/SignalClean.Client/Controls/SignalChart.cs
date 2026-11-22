using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SignalClean.Client.Controls;

/// <summary>
/// Легкий графік без сторонніх бібліотек: сирий сигнал (сірий), очищений (синій),
/// точки, які було виправлено або відкинуто (червоні).
/// Масштаб по осі Y береться за очищеними даними, тому викиди не «сплющують» графік.
/// </summary>
public sealed class SignalChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double?>?> RawValuesProperty =
        AvaloniaProperty.Register<SignalChart, IReadOnlyList<double?>?>(nameof(RawValues));

    public static readonly StyledProperty<IReadOnlyList<double?>?> CleanValuesProperty =
        AvaloniaProperty.Register<SignalChart, IReadOnlyList<double?>?>(nameof(CleanValues));

    public static readonly StyledProperty<IReadOnlyList<bool>?> FlaggedProperty =
        AvaloniaProperty.Register<SignalChart, IReadOnlyList<bool>?>(nameof(Flagged));

    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#FAFAFA"));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#E0E0E0")), 1);
    private static readonly IPen RawPen = new Pen(new SolidColorBrush(Color.Parse("#9E9E9E")), 1);
    private static readonly IPen CleanPen = new Pen(new SolidColorBrush(Color.Parse("#1565C0")), 2);
    private static readonly IBrush FlagBrush = new SolidColorBrush(Color.Parse("#D32F2F"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#616161"));

    static SignalChart()
    {
        AffectsRender<SignalChart>(RawValuesProperty, CleanValuesProperty, FlaggedProperty);
        ClipToBoundsProperty.OverrideDefaultValue<SignalChart>(true);
    }

    public IReadOnlyList<double?>? RawValues
    {
        get => GetValue(RawValuesProperty);
        set => SetValue(RawValuesProperty, value);
    }

    public IReadOnlyList<double?>? CleanValues
    {
        get => GetValue(CleanValuesProperty);
        set => SetValue(CleanValuesProperty, value);
    }

    public IReadOnlyList<bool>? Flagged
    {
        get => GetValue(FlaggedProperty);
        set => SetValue(FlaggedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(BackgroundBrush, bounds);

        var clean = CleanValues ?? [];
        var raw = RawValues ?? [];
        int count = Math.Max(clean.Count, raw.Count);
        var finite = clean.Where(v => v is double d && double.IsFinite(d)).Select(v => v!.Value).ToList();

        if (count < 2 || finite.Count == 0)
        {
            DrawLabel(context, "Немає даних для графіка", new Point(12, 12));
            return;
        }

        const double padLeft = 56;
        const double padRight = 12;
        const double padTop = 12;
        const double padBottom = 12;
        var plot = new Rect(padLeft, padTop, Math.Max(1, bounds.Width - padLeft - padRight), Math.Max(1, bounds.Height - padTop - padBottom));

        double min = finite.Min();
        double max = finite.Max();
        double span = max - min;
        if (span < 1e-9)
        {
            span = Math.Max(1, Math.Abs(max) * 0.1);
        }

        // 15 % запасу зверху і знизу
        min -= span * 0.15;
        max += span * 0.15;

        double X(int i) => plot.Left + (plot.Width * i / (count - 1));
        double Y(double v) => plot.Bottom - (plot.Height * (Math.Clamp(v, min, max) - min) / (max - min));

        // Сітка та підписи осі Y
        for (int k = 0; k <= 4; k++)
        {
            double value = min + ((max - min) * k / 4);
            double y = Y(value);
            context.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawLabel(context, value.ToString("G5", CultureInfo.InvariantCulture), new Point(4, y - 8));
        }

        DrawSeries(context, raw, RawPen, X, Y);
        DrawSeries(context, clean, CleanPen, X, Y);

        var flagged = Flagged ?? [];
        for (int i = 0; i < flagged.Count && i < count; i++)
        {
            if (!flagged[i])
            {
                continue;
            }

            double? v = i < raw.Count ? raw[i] : null;
            double y = v is double d && double.IsFinite(d) ? Y(d) : plot.Bottom;
            context.DrawEllipse(FlagBrush, null, new Point(X(i), y), 3.5, 3.5);
        }
    }

    private static void DrawSeries(DrawingContext context, IReadOnlyList<double?> values, IPen pen, Func<int, double> x, Func<double, double> y)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            bool open = false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not double v || !double.IsFinite(v))
                {
                    // Пропуск у даних — розрив лінії.
                    if (open)
                    {
                        g.EndFigure(false);
                        open = false;
                    }

                    continue;
                }

                var point = new Point(x(i), y(v));
                if (!open)
                {
                    g.BeginFigure(point, false);
                    open = true;
                }
                else
                {
                    g.LineTo(point);
                }
            }

            if (open)
            {
                g.EndFigure(false);
            }
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawLabel(DrawingContext context, string text, Point origin)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, LabelBrush);
        context.DrawText(formatted, origin);
    }
}
