using EAMAS.Desktop.ViewModels;
using System.Windows;

namespace EAMAS.Desktop.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        public LoginWindow(LoginViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.Login(TxtPassword.Password);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                _vm.Login(TxtPassword.Password);
            base.OnKeyDown(e);
        }
    }
}
