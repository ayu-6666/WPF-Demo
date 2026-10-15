using System.ComponentModel;

namespace WpfDevDemo.Models
{
    public class NavigateMenuModel : INotifyPropertyChanged
    {
        private bool _isVisible = true;

        public string Name { get; set; }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                NotifyPropertyChange("IsVisible");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
