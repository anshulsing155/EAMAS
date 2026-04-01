using EAMAS.Desktop.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Controls
{
    public partial class SimpleBarChart : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(List<BarChartItem>),
                typeof(SimpleBarChart), new PropertyMetadata(null));

        public static readonly DependencyProperty MaxHeightBarProperty =
            DependencyProperty.Register(nameof(MaxHeightBar), typeof(double),
                typeof(SimpleBarChart), new PropertyMetadata(120.0));

        public List<BarChartItem> Items
        {
            get => (List<BarChartItem>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public double MaxHeightBar
        {
            get => (double)GetValue(MaxHeightBarProperty);
            set => SetValue(MaxHeightBarProperty, value);
        }

        public SimpleBarChart() => InitializeComponent();
    }

    public class BarHeightMultiConverter : IMultiValueConverter
    {
        public static readonly BarHeightMultiConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is double pct && values[1] is double max)
                return Math.Max(pct / 100.0 * max, 3.0);
            if (values[0] is double p)
                return Math.Max(p / 100.0 * 120.0, 3.0);
            return 3.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => new object[] { System.Windows.Data.Binding.DoNothing, System.Windows.Data.Binding.DoNothing };
    }

    public class HexToColorConverter : IValueConverter
    {
        public static readonly HexToColorConverter Instance = new();

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
