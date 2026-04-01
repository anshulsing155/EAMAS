using EAMAS.Desktop.Controls;
using EAMAS.Desktop.ViewModels;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace EAMAS.Desktop.Views
{
    public partial class DashboardView : System.Windows.Controls.UserControl
    {
        public DashboardView(DashboardViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            Loaded += (_, _) => vm.Load();
        }
    }
}
