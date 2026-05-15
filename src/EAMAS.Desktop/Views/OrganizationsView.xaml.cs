using EAMAS.Desktop.ViewModels;
using System.Windows;

namespace EAMAS.Desktop.Views
{
    public partial class OrganizationsView : System.Windows.Controls.UserControl
    {
        private readonly OrganizationsViewModel _vm;

        public OrganizationsView(OrganizationsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Loaded += (_, _) => vm.Initialize();
        }

        private void SaveUser_Click(object sender, RoutedEventArgs e)
        {
            _vm.UserEditPassword = TxtUserPwd.Password;
            _vm.SaveUser();
            TxtUserPwd.Clear();
        }
    }
}
