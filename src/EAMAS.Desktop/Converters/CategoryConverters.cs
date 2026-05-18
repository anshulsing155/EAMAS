using EAMAS.Core.Enums;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Converters
{
    public class CategoryToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActivityCategory cat)
            {
                return cat switch
                {
                    ActivityCategory.Productive => new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74)),
                    ActivityCategory.Distracting => new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                    ActivityCategory.Neutral => new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139))
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class CategoryToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActivityCategory cat)
                return cat.ToString();
            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value switch
            {
                bool bv => bv,
                string s => !string.IsNullOrEmpty(s),
                int i => i > 0,
                null => false,
                _ => true
            };
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    /// <summary>Shows Collapsed when value is NOT null; Visible when null.</summary>
    public class NullToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    /// <summary>Returns a semi-transparent highlight Brush when bool is true.</summary>
    public class BoolToHighlightBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var active = value is bool b && b;
            return active
                ? new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(20, 37, 99, 235))
                : System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class ScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
            {
                return score >= 70
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74))
                    : score >= 40
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6))
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class UserRoleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Core.Models.UserRole role)
                return role.ToString();
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    /// <summary>Maps a status message string to a WPF Brush for the status bar background.</summary>
    public class StatusMessageColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessBrush = new(System.Windows.Media.Color.FromRgb(20, 83, 45));
        private static readonly SolidColorBrush ErrorBrush   = new(System.Windows.Media.Color.FromRgb(127, 29, 29));
        private static readonly SolidColorBrush WarnBrush    = new(System.Windows.Media.Color.FromRgb(120, 53, 15));
        private static readonly SolidColorBrush InfoBrush    = new(System.Windows.Media.Color.FromRgb(30, 41, 59));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string msg || string.IsNullOrEmpty(msg)) return InfoBrush;
            var lower = msg.ToLowerInvariant();
            if (lower.Contains("fail") || lower.Contains("error") || lower.Contains("invalid") || lower.Contains("not found"))
                return ErrorBrush;
            if (lower.Contains("warn") || lower.Contains("caution") || lower.Contains("incomplete"))
                return WarnBrush;
            if (lower.Contains("success") || lower.Contains("created") || lower.Contains("saved") ||
                lower.Contains("generated") || lower.Contains("complete") || lower.Contains("done") ||
                lower.Contains("activated") || lower.Contains("added") || lower.Contains("indexed"))
                return SuccessBrush;
            return InfoBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

}
