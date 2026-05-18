using EAMAS.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace EAMAS.Desktop.Converters
{
    public class PriorityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskPriority p)
                return p switch
                {
                    TaskPriority.Critical => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                    TaskPriority.High     => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 115, 22)),
                    TaskPriority.Medium   => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8)),
                    TaskPriority.Low      => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
                    _                    => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                };
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class PriorityToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskPriority p)
                return p switch
                {
                    TaskPriority.Critical => "CRIT",
                    TaskPriority.High     => "HIGH",
                    TaskPriority.Medium   => "MED",
                    TaskPriority.Low      => "LOW",
                    _                    => "?"
                };
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class SprintStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SprintStatus s)
                return s switch
                {
                    SprintStatus.Planning  => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
                    SprintStatus.Active    => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),
                    SprintStatus.Completed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
                    _                     => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                };
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class CodeReviewStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CodeReviewStatus s)
                return s switch
                {
                    CodeReviewStatus.Passed            => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),
                    CodeReviewStatus.Failed            => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                    CodeReviewStatus.InProgress        => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8)),
                    CodeReviewStatus.NeedsHumanReview  => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 115, 22)),
                    _                                  => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                };
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
                return s.ToLowerInvariant() switch
                {
                    "error"   => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                    "warning" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8)),
                    _         => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139))
                };
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
