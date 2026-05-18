using EAMAS.Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace EAMAS.Desktop.Controls
{
    public partial class TaskCard : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty TaskProperty =
            DependencyProperty.Register(nameof(Task), typeof(ProjectTask), typeof(TaskCard));

        public ProjectTask? Task
        {
            get => (ProjectTask?)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public TaskCard() => InitializeComponent();
    }
}
