using System.Collections.Generic;
using RebootHMI.Core;
using RebootHMI.Models;

namespace RebootHMI.ViewModels
{
    public class AutoModeViewModel : ObservableObject
    {
        public List<ToggleItem> GripperToggles { get; } = new List<ToggleItem>
        {
            new ToggleItem("转角缸", "松开", "夹紧", false),
            new ToggleItem("防坠落", "松开", "夹紧", false),
            new ToggleItem("插销缸", "松开", "夹紧", true),
        };

        public List<ToggleItem> TrayToggles { get; } = new List<ToggleItem>
        {
            new ToggleItem("上提", "缩回", "伸出", true),
            new ToggleItem("下提", "缩回", "伸出", true),
            new ToggleItem("托举", "缩回", "伸出", true),
            new ToggleItem("挡停", "缩回", "伸出", true),
        };

        public List<DropdownItem> TrackRows { get; } = new List<DropdownItem>
        {
            new DropdownItem { Label = "1#料道", Value = "放料确认" },
            new DropdownItem { Label = "瑕疵检测", Value = "屏蔽" },
            new DropdownItem { Label = "瑕疵检测", Value = "门关闭" },
            new DropdownItem { Label = "进料扫码", Value = "未完成", IsRed = true },
            new DropdownItem { Label = "2#料道", Value = "放行确认" },
            new DropdownItem { Label = "2#料道", Value = "拍照合格" },
            new DropdownItem { Label = "2#料道", Value = "拍照不合格" },
        };

        public List<string> TrackOptions { get; } = new List<string>
        {
            "放料确认", "屏蔽", "门关闭", "未完成", "放行确认", "拍照合格", "拍照不合格"
        };

        public RelayCommand NgActionCommand { get; }

        public AutoModeViewModel()
        {
            NgActionCommand = new RelayCommand(o => { });
        }
    }
}
