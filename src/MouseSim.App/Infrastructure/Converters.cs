using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace MouseSim.App.Infrastructure;

/// <summary>bool 取反。</summary>
public class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>枚举值与 ConverterParameter（枚举名）比较，用于 RadioButtons 绑定枚举。</summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter != null && Enum.TryParse(targetType, parameter.ToString(), out var result)
            ? result
            : Binding.DoNothing;
}

/// <summary>WPF Key 枚举 → 友好显示名。</summary>
public class KeyDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Key key ? WpfKeys.Display(key) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
