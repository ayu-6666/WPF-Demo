using Elastic.BMS.Core;

namespace Elastic.BMS.ViewModels
{
    /// <summary>系统设置（含"基本设置/通信设置"两个子页）</summary>
    public class SettingsViewModel : ObservableObject
    {
        private bool _isBasicSelected = true;

        public bool IsBasicSelected
        {
            get => _isBasicSelected;
            set
            {
                if (!SetProperty(ref _isBasicSelected, value)) return;
                OnPropertyChanged(nameof(IsCommSelected));
                OnPropertyChanged(nameof(CurrentContent));
            }
        }

        public bool IsCommSelected
        {
            get => !_isBasicSelected;
            set => IsBasicSelected = !value;
        }

        public object CurrentContent => _isBasicSelected
            ? (object)Basic
            : Comm;

        public SettingsBasicViewModel Basic { get; } = new SettingsBasicViewModel();
        public SettingsCommViewModel Comm { get; } = new SettingsCommViewModel();

        public RelayCommand SelectBasicCommand { get; }
        public RelayCommand SelectCommCommand { get; }

        public SettingsViewModel()
        {
            SelectBasicCommand = new RelayCommand(() => IsBasicSelected = true);
            SelectCommCommand = new RelayCommand(() => IsBasicSelected = false);
        }
    }
}
