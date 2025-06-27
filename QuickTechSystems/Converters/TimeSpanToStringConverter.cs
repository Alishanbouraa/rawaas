using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                return timeSpan.ToString(@"hh\:mm");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string timeString && !string.IsNullOrWhiteSpace(timeString))
            {
                if (TimeSpan.TryParseExact(timeString, @"hh\:mm", null, out TimeSpan result))
                {
                    return result;
                }
                if (TimeSpan.TryParseExact(timeString, @"h\:mm", null, out TimeSpan result2))
                {
                    return result2;
                }
                if (TimeSpan.TryParse(timeString, out TimeSpan result3))
                {
                    return result3;
                }
            }
            return new TimeSpan(8, 0, 0);
        }
    }
}