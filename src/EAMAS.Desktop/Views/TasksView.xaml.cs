using EAMAS.Desktop.ViewModels;
using System.Windows.Controls;

namespace EAMAS.Desktop.Views
{
    public partial class TasksView : System.Windows.Controls.UserControl
    {
        public TasksView(TasksViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
