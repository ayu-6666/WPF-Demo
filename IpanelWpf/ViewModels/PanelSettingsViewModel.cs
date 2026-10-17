using IpanelWpf.Core;

namespace IpanelWpf.ViewModels
{
    public class PanelSettingsViewModel : ObservableObject
    {
        public string PageName => "面板设置";

        private int _tabIndex;
        public int TabIndex
        {
            get => _tabIndex;
            set => SetProperty(ref _tabIndex, value);
        }

        // 面板 tab
        private int _themeIndex = 1;   // 0 亮色 / 1 暗色 / 2 跟随系统
        public int ThemeIndex
        {
            get => _themeIndex;
            set => SetProperty(ref _themeIndex, value);
        }

        private bool _menuFixed = true;
        public bool MenuFixed
        {
            get => _menuFixed;
            set => SetProperty(ref _menuFixed, value);
        }

        public string PanelAlias { get; set; } = "1Panel";
        public string RestartCountdown { get; set; } = "86400";
        public string DefaultAddress { get; set; } = "117.72.44.246";

        private bool _hideExperience;
        public bool HideExperience
        {
            get => _hideExperience;
            set => SetProperty(ref _hideExperience, value);
        }

        // 安全 tab
        public string PanelPort { get; set; } = "80";
        public string ListenAddress { get; set; } = "0.0.0.0";
        public string SecurityEntrance { get; set; } = "********";
        public string UnauthPage { get; set; } = "205-预览页面";

        private bool _sslEnabled;
        public bool SslEnabled
        {
            get => _sslEnabled;
            set => SetProperty(ref _sslEnabled, value);
        }

        public string PasswordExpire { get; set; } = "0";

        private bool _pwdComplexity = true;
        public bool PwdComplexity
        {
            get => _pwdComplexity;
            set => SetProperty(ref _pwdComplexity, value);
        }
    }
}
