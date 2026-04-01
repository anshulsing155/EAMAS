using EAMAS.Core.Models;
using EAMAS.Desktop.Converters;
using EAMAS.Desktop.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Views
{
    public partial class EmployeesView : System.Windows.Controls.UserControl
    {
        private readonly EmployeesViewModel _vm;

        public EmployeesView(EmployeesViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            Loaded += (_, _) => vm.Load();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _vm.EditPassword = TxtEditPwd.Password;
            _vm.SaveCommand.Execute(null);
            TxtEditPwd.Clear();
        }
    }

    public class BoolToLabelConverter : IValueConverter
    {
        public string TrueLabel { get; set; } = string.Empty;
        public string FalseLabel { get; set; } = string.Empty;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? TrueLabel : FalseLabel;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class RoleToBrushConverter : IValueConverter
    {
        public bool IsBackground { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role)
            {
                if (IsBackground)
                    return new SolidColorBrush(role switch
                    {
                        UserRole.Admin => System.Windows.Media.Color.FromRgb(254, 226, 226),
                        UserRole.Manager => System.Windows.Media.Color.FromRgb(254, 249, 195),
                        _ => System.Windows.Media.Color.FromRgb(219, 234, 254)
                    });
                else
                    return new SolidColorBrush(role switch
                    {
                        UserRole.Admin => System.Windows.Media.Color.FromRgb(185, 28, 28),
                        UserRole.Manager => System.Windows.Media.Color.FromRgb(113, 63, 18),
                        _ => System.Windows.Media.Color.FromRgb(29, 78, 216)
                    });
            }
            return new SolidColorBrush(Colors.Gray);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class ActiveToBrushConverter : IValueConverter
    {
        public bool IsBackground { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var active = value is bool b && b;
            if (IsBackground)
                return new SolidColorBrush(active ? System.Windows.Media.Color.FromRgb(220, 252, 231) : System.Windows.Media.Color.FromRgb(254, 226, 226));
            return new SolidColorBrush(active ? System.Windows.Media.Color.FromRgb(21, 128, 61) : System.Windows.Media.Color.FromRgb(185, 28, 28));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class ActiveToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? "Active" : "Inactive";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
