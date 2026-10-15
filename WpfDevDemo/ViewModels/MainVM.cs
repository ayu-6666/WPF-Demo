using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using WpfDevDemo.ExampleViews;
using WpfDevDemo.Helpers;
using WpfDevDemo.Models;

namespace WpfDevDemo.ViewModels
{
    public class MainVM : INotifyPropertyChanged
    {
        /// <summary>Basic 分类包含的示例（顺序即显示顺序）</summary>
        private static readonly string[] BasicMenus =
        {
            "Theme", "Menu", "Window", "BasicLoading", "Button", "RadioButton", "Checkbox",
            "TextBox", "PasswordBox", "ComboBox", "ToggleButton", "DatePicker", "DateTimePicker",
            "Calendar", "Slider", "ProgressBar", "DataGrid", "ListBox", "ListView", "TreeView",
            "Expander", "GroupBox", "TabControl", "TaskbarInfo",
        };

        /// <summary>Custom 分类示例（顺序与官方示例一致）</summary>
        private static readonly string[] CustomMenus =
        {
            "NavMenu3D", "UsageGuide", "UsageColor", "MessageBox", "NotifyIcon", "PanningItems",
            "BreatheLight", "RingLoading", "BallLoading", "StreamerLoading", "WaitLoading",
            "CycleLoading", "RollLoading", "CropAvatar", "AnimationAudio", "AMap",
            "TransformLayout", "VerifyCode", "CircleMenu", "StarrySky", "Shake", "BubbleControl",
            "CanvasHandWriting", "Barrage", "SixGridView", "Magnifier", "Step",
            "MultiSelectComboBox", "Mask", "Loading", "Selector", "MultiSelectSearchComboBox",
            "BreadCrumbBar", "CropImage", "Badge", "Toast", "PathIcon", "NumericBox",
            "ColorPicker", "IPEditBox", "TimePicker", "WaterfallPanel", "ChartLine", "Drap",
            "ChartPie", "Spacing", "Drawer", "DateRangePicker", "Gauge", "Tag",
        };

        private readonly ObservableCollection<NavigateMenuModel> _customMenuModelList;
        private readonly ObservableCollection<NavigateMenuModel> _basicMenuModelList;
        private IList<NavigateMenuModel> _navigateMenuModelList;
        private NavigateMenuModel _navigateMenuItem;
        private object _controlPanel;
        private int _segmentedSelectedIndex;
        private string _searchText = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainVM()
        {
            _customMenuModelList = new ObservableCollection<NavigateMenuModel>();
            _basicMenuModelList = new ObservableCollection<NavigateMenuModel>();
            foreach (var name in CustomMenus)
                _customMenuModelList.Add(new NavigateMenuModel { Name = name });
            foreach (var name in BasicMenus)
                _basicMenuModelList.Add(new NavigateMenuModel { Name = name });
            _customMenuModelList.Add(new NavigateMenuModel { Name = "持续更新中" });
            _basicMenuModelList.Add(new NavigateMenuModel { Name = "持续更新中" });

            NavigateMenuModelList = _customMenuModelList;
            _navigateMenuItem = NavigateMenuModelList.First();
            ControlPanel = new NavMenu3DExample();
        }

        public IList<NavigateMenuModel> NavigateMenuModelList
        {
            get => _navigateMenuModelList;
            set
            {
                _navigateMenuModelList = value;
                NotifyPropertyChange("NavigateMenuModelList");
            }
        }

        /// <summary>当前选中</summary>
        public NavigateMenuModel NavigateMenuItem
        {
            get => _navigateMenuItem;
            set
            {
                _navigateMenuItem = value;
                NotifyPropertyChange("NavigateMenuItem");
            }
        }

        /// <summary>更换右侧面板</summary>
        public object ControlPanel
        {
            get => _controlPanel;
            set
            {
                _controlPanel = value;
                NotifyPropertyChange("ControlPanel");
            }
        }

        public int SegmentedSelectedIndex
        {
            get => _segmentedSelectedIndex;
            set
            {
                _segmentedSelectedIndex = value;
                NotifyPropertyChange("SegmentedSelectedIndex");
                NavigateMenuModelList = value == 1 ? _basicMenuModelList : _customMenuModelList;
                ApplySearchFilter();
                if (NavigateMenuModelList.Count > 0)
                    NavigateMenuItem = NavigateMenuModelList.FirstOrDefault(y => y.IsVisible);
            }
        }

        public RelayCommand MenuSearchTextChanged => new RelayCommand(obj =>
        {
            _searchText = obj?.ToString() ?? string.Empty;
            ApplySearchFilter();
        });

        public RelayCommand MenuSelectionChangedCommand => new RelayCommand(obj =>
        {
            if (obj == null) return;
            var model = obj as NavigateMenuModel;
            if (model == null) return;
            NavigateMenuItem = model;
            MenuItemSelection(model.Name);
        });

        private void ApplySearchFilter()
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                foreach (var item in NavigateMenuModelList)
                    item.IsVisible = true;
            }
            else
            {
                var key = _searchText.ToLower();
                foreach (var item in NavigateMenuModelList)
                    item.IsVisible = item.Name.ToLower().Contains(key);
            }
        }

        private void MenuItemSelection(string menuName)
        {
            ControlPanel = CreateExample(menuName);
        }

        private static object CreateExample(string menuName)
        {
            switch (menuName)
            {
                case "NavMenu3D":
                    return new NavMenu3DExample();
                case "PathIcon":
                    return new PathIconExample();
                case "Drawer":
                    return new DrawerExample();
                default:
                    return new PlaceholderExample();
            }
        }

        private void NotifyPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
