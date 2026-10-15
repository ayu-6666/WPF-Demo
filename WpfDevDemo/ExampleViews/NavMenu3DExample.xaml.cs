using System.Diagnostics;
using System.Windows.Navigation;
using WPFDevelopers.Controls;

namespace WpfDevDemo.ExampleViews
{
    public partial class NavMenu3DExample
    {
        public NavMenu3DExample()
        {
            InitializeComponent();
        }

        private void NavMenu3D_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var item = e.AddedItems[0] as WPFDevelopers.Controls.NavMenu3DItem;
            if (item?.Tag is string tag)
                Toast.Push(tag, ToastImage.Info, true);
        }

        private void GithubHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void GiteeHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
