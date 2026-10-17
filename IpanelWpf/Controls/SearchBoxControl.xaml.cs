using System.Windows;
using System.Windows.Controls;

namespace IpanelWpf.Controls
{
    public partial class SearchBoxControl : UserControl
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SearchBoxControl), new PropertyMetadata("搜索"));
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(SearchBoxControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty BoxWidthProperty =
            DependencyProperty.Register(nameof(BoxWidth), typeof(double), typeof(SearchBoxControl), new PropertyMetadata(200.0));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double BoxWidth
        {
            get => (double)GetValue(BoxWidthProperty);
            set => SetValue(BoxWidthProperty, value);
        }

        public SearchBoxControl()
        {
            InitializeComponent();
        }
    }
}
