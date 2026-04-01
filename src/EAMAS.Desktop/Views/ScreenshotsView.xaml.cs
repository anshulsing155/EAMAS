using EAMAS.Desktop.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Views
{
    public partial class ScreenshotsView : System.Windows.Controls.UserControl
    {
        public ScreenshotsView(ScreenshotsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Loaded += (_, _) => vm.Initialize();
        }
    }

    public class SelectedBorderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var currentItem = values.Length > 0 ? values[0] : null;
            var selectedItem = values.Length > 1 ? values[1] : null;

            return Equals(currentItem, selectedItem) && currentItem != null
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235))
                : new SolidColorBrush(System.Windows.Media.Colors.Transparent);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => new object[] { System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing };
    }
}
