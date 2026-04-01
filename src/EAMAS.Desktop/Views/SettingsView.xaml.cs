using EAMAS.Desktop.ViewModels;
using System.Windows.Controls;

namespace EAMAS.Desktop.Views
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private readonly SettingsViewModel _vm;

        public SettingsView(SettingsViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
            Loaded += (_, _) => vm.Initialize();
        }

        private void ChangePassword_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.SetPasswordInputs(
                TxtCurrentPwd.Password,
                TxtNewPwd.Password,
                TxtConfirmPwd.Password);
            _vm.ChangePasswordCommand.Execute(null);
            TxtCurrentPwd.Clear();
            TxtNewPwd.Clear();
            TxtConfirmPwd.Clear();
        }
    }
}
