using System;
using System.Globalization;
using System.Windows.Data;

namespace GitVisualizer.UI.Converters;

/// <summary>
/// 列挙値が ConverterParameter(列挙値の名前)と等しいかを bool に変換します。RadioButton.IsChecked のバインドに使う。
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // チェックが外れたときは何もしない(別の RadioButton がチェックされた側で値が設定される)
        return value is true && parameter is string name
            ? Enum.Parse(targetType, name)
            : Binding.DoNothing;
    }
}
