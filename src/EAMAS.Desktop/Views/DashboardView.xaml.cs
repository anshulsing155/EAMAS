using EAMAS.Desktop.ViewModels;

namespace EAMAS.Desktop.Views
{
    public partial class DashboardView : System.Windows.Controls.UserControl
    {
        private readonly DashboardViewModel _vm;

        public DashboardView(DashboardViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Loaded += (_, _) => { vm.Load(); vm.StartAutoRefresh(); };
            Unloaded += (_, _) => vm.StopAutoRefresh();
        }
    }
}
