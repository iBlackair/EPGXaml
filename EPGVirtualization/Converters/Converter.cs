﻿using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;

namespace EPGVirtualization.Converters
{
    /// <summary>
    /// Helper converter for program background based on selection
    /// </summary>
    public class ProgramBackgroundConverter : IValueConverter
    {
        // First program color set
        private static readonly SolidColorBrush Color1Selected = new SolidColorBrush(Color.FromRgb(100, 149, 237)); // Cornflower Blue
        private static readonly SolidColorBrush Color1Normal = new SolidColorBrush(Color.FromRgb(202, 225, 255));   // Light blue

        // Second program color set (alternating)
        private static readonly SolidColorBrush Color2Selected = new SolidColorBrush(Color.FromRgb(70, 130, 180));   // Steel Blue
        private static readonly SolidColorBrush Color2Normal = new SolidColorBrush(Color.FromRgb(176, 196, 222));   // Light Steel Blue

        // Dictionary to track program alternation by channel
        private static Dictionary<int, Dictionary<DateTime, bool>> _alternatingState = new Dictionary<int, Dictionary<DateTime, bool>>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSelected = value is bool boolValue && boolValue;

            // Check if we have the alternating flag from the ProgramControl
            if (parameter is bool isAlternating)
            {
                if (isSelected)
                {
                    return isAlternating ? Color2Selected : Color1Selected;
                }
                else
                {
                    return isAlternating ? Color2Normal : Color1Normal;
                }
            }
            // Default fallback if no alternating info
            else if (parameter is ProgramInfo program)
            {
                // Get or create alternating tracking for this channel
                if (!_alternatingState.TryGetValue(program.ChannelIndex, out var channelState))
                {
                    channelState = new Dictionary<DateTime, bool>();
                    _alternatingState[program.ChannelIndex] = channelState;
                }

                // Get or determine alternating state for this program
                if (!channelState.TryGetValue(program.StartTime, out bool isAlt))
                {
                    // Determine based on position in the sequence
                    var programsList = _alternatingState.Keys
                        .Where(k => k == program.ChannelIndex)
                        .SelectMany(k => _alternatingState[k].Keys)
                        .OrderBy(dt => dt)
                        .ToList();

                    isAlt = programsList.Count % 2 == 1;
                    channelState[program.StartTime] = isAlt;
                }

                if (isSelected)
                {
                    return isAlt ? Color2Selected : Color1Selected;
                }
                else
                {
                    return isAlt ? Color2Normal : Color1Normal;
                }
            }

            // Default fallback
            return isSelected ? Color1Selected : Color1Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter to create a Thickness from a TimelineHeight value
    /// </summary>
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
            if (value is Thickness thickness)
            {
                return thickness.Top;
            }
            return 0.0;
        }
    }
}
