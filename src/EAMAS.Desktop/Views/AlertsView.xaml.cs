using EAMAS.Desktop.ViewModels;
using System.Windows.Controls;

namespace EAMAS.Desktop.Views
{
    public partial class AlertsView : System.Windows.Controls.UserControl
    {
        private readonly AlertsViewModel _vm;

        public AlertsView(AlertsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Loaded += (_, _) => vm.Initialize();
            Unloaded += (_, _) => vm.Cleanup();
        }
    }
}
