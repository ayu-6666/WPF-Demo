using System.Collections.Generic;
using Elastic.BMS.Core;

namespace Elastic.BMS.ViewModels
{
    /// <summary>系统设置 - 通信设置</summary>
    public class SettingsCommViewModel : ObservableObject
    {
        private string _ip = "172.30.12.5";
        public string Ip
        {
            get => _ip;
            set => SetProperty(ref _ip, value);
        }

        private string _port = "502";
        public string Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public List<string> PortOptions { get; } = new List<string> { "COM1", "COM2", "COM3", "COM4" };

        private string _selectedCommPort = "COM1";
        public string SelectedCommPort
        {
            get => _selectedCommPort;
            set => SetProperty(ref _selectedCommPort, value);
        }

        private string _baudRate = "9600";
        public string BaudRate
        {
            get => _baudRate;
            set => SetProperty(ref _baudRate, value);
        }

        private string _dataBits = "8";
        public string DataBits
        {
            get => _dataBits;
            set => SetProperty(ref _dataBits, value);
        }

        private string _stopBits = "1";
        public string StopBits
        {
            get => _stopBits;
            set => SetProperty(ref _stopBits, value);
        }

        public List<string> ParityOptions { get; } = new List<string> { "None", "Odd", "Even", "Mark", "Space" };

        private string _parity = "None";
        public string Parity
        {
            get => _parity;
            set => SetProperty(ref _parity, value);
        }
    }
}
