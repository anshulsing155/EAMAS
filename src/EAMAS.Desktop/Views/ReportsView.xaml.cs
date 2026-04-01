using EAMAS.Desktop.ViewModels;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Views
{
    public partial class ReportsView : System.Windows.Controls.UserControl
    {
        public ReportsView(ReportsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Loaded += (_, _) => vm.Initialize();
        }
    }

    public class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int idx && parameter is string p && int.TryParse(p, out int target))
                return idx == target;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true && parameter is string p && int.TryParse(p, out int target))
                return target;
            return System.Windows.Data.Binding.DoNothing;
        }
    }

    public class ScoreTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
                return score >= 70 ? System.Windows.Media.Color.FromRgb(21, 128, 61)
                    : score >= 40 ? System.Windows.Media.Color.FromRgb(180, 83, 9)
                    : System.Windows.Media.Color.FromRgb(185, 28, 28);
            return System.Windows.Media.Color.FromRgb(71, 85, 105);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
