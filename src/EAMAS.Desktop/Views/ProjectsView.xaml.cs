using EAMAS.Desktop.ViewModels;
using System.Windows.Controls;

namespace EAMAS.Desktop.Views
{
    public partial class ProjectsView : System.Windows.Controls.UserControl
    {
        private ProjectsViewModel? _vm;

        public ProjectsView(ProjectsViewModel vm)
        {
            InitializeComponent();
            DataContext = _vm = vm;
        }

        private void GhTokenBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm != null && sender is PasswordBox pb)
                _vm.GhToken = pb.Password;
        }

        private void AiKeyBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm != null && sender is PasswordBox pb)
                _vm.AiApiKey = pb.Password;
        }
    }
}
