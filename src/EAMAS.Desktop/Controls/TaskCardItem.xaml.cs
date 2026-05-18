using EAMAS.Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace EAMAS.Desktop.Controls
{
    public partial class TaskCardItem : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty TaskProperty =
            DependencyProperty.Register(nameof(Task), typeof(ProjectTask), typeof(TaskCardItem));

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(object), typeof(TaskCardItem));

        public ProjectTask? Task
        {
            get => (ProjectTask?)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public object ViewModel
        {
            get => GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public TaskCardItem() => InitializeComponent();
    }
}
