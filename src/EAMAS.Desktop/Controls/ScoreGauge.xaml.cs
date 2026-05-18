using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EAMAS.Desktop.Controls
{
    public partial class ScoreGauge : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty ScoreProperty =
            DependencyProperty.Register(nameof(Score), typeof(int), typeof(ScoreGauge),
                new PropertyMetadata(0, OnScoreChanged));

        public static readonly DependencyProperty ScoreColorProperty =
            DependencyProperty.Register(nameof(ScoreColor), typeof(System.Windows.Media.Brush), typeof(ScoreGauge),
                new PropertyMetadata(System.Windows.Media.Brushes.Gray));

        public int Score
        {
            get => (int)GetValue(ScoreProperty);
            set => SetValue(ScoreProperty, value);
        }

        public System.Windows.Media.Brush ScoreColor
        {
            get => (System.Windows.Media.Brush)GetValue(ScoreColorProperty);
            set => SetValue(ScoreColorProperty, value);
        }

        public ScoreGauge() => InitializeComponent();

        private static void OnScoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScoreGauge g)
                g.ScoreColor = (int)e.NewValue >= 80
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94))   // green
                    : (int)e.NewValue >= 50
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8))  // yellow
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // red
        }
    }
}
