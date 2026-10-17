using IpanelWpf.Core;

namespace IpanelWpf.ViewModels
{
    public class PlaceholderViewModel : ObservableObject
    {
        public string Title { get; }

        public PlaceholderViewModel(string title)
        {
            Title = title;
        }
    }
}
