using EPGVirtualization.Controls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EPGVirtualization.Converters
{
    public class DateToFormattedStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime date)
            {
                string format = parameter as string ?? "dddd, MMMM d, yyyy";
                return date.ToString(format, culture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimelineHeightToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double height)
            {
                return new Thickness(0, height, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgramBackgroundConverter : IValueConverter
    {
        private static readonly SolidColorBrush Color1Selected = new(Color.FromRgb(18, 18, 20));
        private static readonly SolidColorBrush Color1Normal = new(Color.FromRgb(38, 38, 38));
        private static readonly SolidColorBrush Color2Selected = new(Color.FromRgb(63, 63, 65));
        private static readonly SolidColorBrush Color2Normal = new(Color.FromRgb(85, 85, 85));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && parameter is ProgramInfo program)
            {
                bool isAlternate = program.ChannelIndex % 2 == 0;

                if (isSelected)
                {
                    return isAlternate ? Color2Selected : Color1Selected;
                }
                else
                {
                    return isAlternate ? Color2Normal : Color1Normal;
                }
            }

            return Color1Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}