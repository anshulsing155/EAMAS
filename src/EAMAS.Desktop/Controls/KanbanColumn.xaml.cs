using EAMAS.Core.Models;
using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace EAMAS.Desktop.Controls
{
    public partial class KanbanColumn : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(KanbanColumn), new PropertyMetadata("Column"));

        public static readonly DependencyProperty CountProperty =
            DependencyProperty.Register(nameof(Count), typeof(int), typeof(KanbanColumn), new PropertyMetadata(0));

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(KanbanColumn));

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(object), typeof(KanbanColumn));

        public static readonly DependencyProperty ColumnStatusProperty =
            DependencyProperty.Register(nameof(ColumnStatus), typeof(string), typeof(KanbanColumn), new PropertyMetadata("Backlog"));

        public static readonly DependencyProperty IsWarningProperty =
            DependencyProperty.Register(nameof(IsWarning), typeof(bool), typeof(KanbanColumn), new PropertyMetadata(false));

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }
        public IEnumerable Items { get => (IEnumerable)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
        public object ViewModel { get => GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }
        public string ColumnStatus { get => (string)GetValue(ColumnStatusProperty); set => SetValue(ColumnStatusProperty, value); }
        public bool IsWarning { get => (bool)GetValue(IsWarningProperty); set => SetValue(IsWarningProperty, value); }

        public KanbanColumn() => InitializeComponent();
    }
}
