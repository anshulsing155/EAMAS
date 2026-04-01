using EAMAS.Core.Enums;
using EAMAS.Desktop.ViewModels;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Views
{
    public partial class ActivityLogsView : System.Windows.Controls.UserControl
    {
        public ActivityLogsView(ActivityLogsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Loaded += (_, _) => vm.Initialize();
        }
    }

    public class CategoryBgColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActivityCategory cat)
                return cat switch
                {
                    ActivityCategory.Productive => System.Windows.Media.Color.FromRgb(220, 252, 231),
                    ActivityCategory.Distracting => System.Windows.Media.Color.FromRgb(254, 226, 226),
                    ActivityCategory.Neutral => System.Windows.Media.Color.FromRgb(219, 234, 254),
                    _ => System.Windows.Media.Color.FromRgb(241, 245, 249)
                };
            return System.Windows.Media.Color.FromRgb(241, 245, 249);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class CategoryTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ActivityCategory cat)
                return cat switch
                {
                    ActivityCategory.Productive => System.Windows.Media.Color.FromRgb(21, 128, 61),
                    ActivityCategory.Distracting => System.Windows.Media.Color.FromRgb(185, 28, 28),
                    ActivityCategory.Neutral => System.Windows.Media.Color.FromRgb(29, 78, 216),
                    _ => System.Windows.Media.Color.FromRgb(71, 85, 105)
                };
            return System.Windows.Media.Color.FromRgb(71, 85, 105);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex)
            {
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
                catch { }
            }
            return System.Windows.Media.Colors.Blue;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
