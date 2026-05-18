using EAMAS.Desktop.ViewModels;
using System.Windows.Controls;

namespace EAMAS.Desktop.Views
{
    public partial class SprintPlannerView : System.Windows.Controls.UserControl
    {
        public SprintPlannerView(SprintPlannerViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
