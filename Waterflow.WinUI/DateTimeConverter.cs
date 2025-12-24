using System;
using Microsoft.UI.Xaml.Data;

namespace Waterflow.WinUI;

public sealed class DateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            DateTimeOffset dto => dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

