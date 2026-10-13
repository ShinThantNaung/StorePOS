using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StorePOS.Infrastructure;

public sealed class BoolToVisibleConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = value switch
        {
            bool b => b,
            null => false,
            string s => s.Length > 0,
            int i => i != 0,
            _ => true,
        };
        return on != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>For toggle groups: IsChecked="{Binding Mode, Converter={StaticResource Eq}, ConverterParameter=...}".</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter == null) return Binding.DoNothing;
        if (targetType.IsEnum) return Enum.Parse(targetType, parameter.ToString()!);
        return parameter;
    }
}

public sealed class BoolToSemiBoldConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToStrikeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TextDecorations.Strikethrough : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Voided and reversed rows use the secondary text colour, never red.</summary>
public sealed class BoolToSecondaryBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.FindResource(value is true ? "TextSecondary" : "Text");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Weekly chart bar: [fraction of the busiest day, width of the bar area] → bar width.
/// At most 170 (spec), less when the area is narrow, always leaving room for the amount after it.
/// </summary>
public sealed class BarWidthConverter : IMultiValueConverter
{
    public double Max { get; set; } = 170;
    public double AmountRoom { get; set; } = 110;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double fraction || values[1] is not double area || fraction <= 0) return 0.0;
        var width = fraction * Math.Max(0, Math.Min(Max, area - AmountRoom));
        return Math.Max(2, width);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class Ui
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(Ui), new FrameworkPropertyMetadata(""));

    public static string GetPlaceholder(DependencyObject o) => (string)o.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject o, string v) => o.SetValue(PlaceholderProperty, v);

    public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.RegisterAttached(
        "HoverBackground", typeof(Brush), typeof(Ui), new FrameworkPropertyMetadata(null));

    public static Brush? GetHoverBackground(DependencyObject o) => (Brush?)o.GetValue(HoverBackgroundProperty);
    public static void SetHoverBackground(DependencyObject o, Brush? v) => o.SetValue(HoverBackgroundProperty, v);

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(Ui), new FrameworkPropertyMetadata(new CornerRadius(2)));

    public static CornerRadius GetCornerRadius(DependencyObject o) => (CornerRadius)o.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject o, CornerRadius v) => o.SetValue(CornerRadiusProperty, v);
}
