using System.Windows;
using System.Windows.Media;
namespace HekaSomi.Lab.Desktop.Controls;

public sealed class Waveform : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(nameof(Peaks), typeof(double[]), typeof(Waveform), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public double[]? Peaks
    {
        get => (double[]?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(236, 245, 244)), null, new Rect(0, 0, ActualWidth, ActualHeight), 8, 8);
        var values = Peaks;
        if (values is null || values.Length == 0)
            return;
        var step = ActualWidth / values.Length;
        var brush = new SolidColorBrush(Color.FromRgb(0, 123, 120));
        for (var i = 0; i < values.Length; i++)
        {
            var h = Math.Max(2, Math.Clamp(values[i], 0, 1) * (ActualHeight - 10));
            dc.DrawRoundedRectangle(brush, null, new Rect(i * step, (ActualHeight - h) / 2, Math.Max(1, step - 2), h), 1, 1);
        }
    }
}
