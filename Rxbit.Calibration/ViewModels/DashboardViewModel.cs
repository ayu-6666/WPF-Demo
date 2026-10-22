using Rxbit.Calibration.Core;
using Rxbit.Calibration.Models;

namespace Rxbit.Calibration.ViewModels
{
    /// <summary>主页</summary>
    public class DashboardViewModel : ObservableObject
    {
        private readonly MainViewModel _main;

        public string WorldTimeText { get; } = "12:17";
        public string WorldLabel { get; } = "世界时间";
        public string BeijingTimeText { get; } = "20:17";
        public string BeijingLabel { get; } = "北京时间";
        public string TimeDate { get; } = "2023-12-27";

        private bool _inputIsOn = true;
        public bool InputIsOn
        {
            get => _inputIsOn;
            set => SetProperty(ref _inputIsOn, value);
        }

        private bool _outputIsOn = false;
        public bool OutputIsOn
        {
            get => _outputIsOn;
            set => SetProperty(ref _outputIsOn, value);
        }

        public string AlarmCount { get; } = "2";
        public string AlarmCode { get; } = "9502";

        public string MonitorTitle { get; } = "监视界面";

        public DashboardViewModel(MainViewModel main)
        {
            _main = main;
        }

        public RelayCommand RunModeCommand { get; } = new RelayCommand(() => { });
    }
}
