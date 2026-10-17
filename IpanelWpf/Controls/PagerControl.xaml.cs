using System.Windows;
using System.Windows.Controls;

namespace IpanelWpf.Controls
{
    public partial class PagerControl : UserControl
    {
        public static readonly DependencyProperty TotalTextProperty =
            DependencyProperty.Register(nameof(TotalText), typeof(string), typeof(PagerControl), new PropertyMetadata("共 0 条"));
        public static readonly DependencyProperty PageSizeIndexProperty =
            DependencyProperty.Register(nameof(PageSizeIndex), typeof(int), typeof(PagerControl), new PropertyMetadata(1));
        public static readonly DependencyProperty PageValueProperty =
            DependencyProperty.Register(nameof(PageValue), typeof(string), typeof(PagerControl), new PropertyMetadata("1"));

        public string TotalText
        {
            get => (string)GetValue(TotalTextProperty);
            set => SetValue(TotalTextProperty, value);
        }

        public int PageSizeIndex
        {
            get => (int)GetValue(PageSizeIndexProperty);
            set => SetValue(PageSizeIndexProperty, value);
        }

        public string PageValue
        {
            get => (string)GetValue(PageValueProperty);
            set => SetValue(PageValueProperty, value);
        }

        public PagerControl()
        {
            InitializeComponent();
        }
    }
}
