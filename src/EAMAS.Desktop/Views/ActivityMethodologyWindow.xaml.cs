using System.Windows;

namespace EAMAS.Desktop.Views
{
    public partial class ActivityMethodologyWindow : Window
    {
        public ActivityMethodologyWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
