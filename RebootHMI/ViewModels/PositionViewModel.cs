using System.Collections.Generic;
using RebootHMI.Core;
using RebootHMI.Models;

namespace RebootHMI.ViewModels
{
    public class PositionViewModel : ObservableObject
    {
        private int _activeTabIndex;

        public List<TabItem> Tabs { get; }
        public List<AxisData> Axes { get; } = new List<AxisData>
        {
            new AxisData { Name = "X轴" },
            new AxisData { Name = "Y轴" },
            new AxisData { Name = "Z轴" },
            new AxisData { Name = "C轴" },
        };

        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (SetProperty(ref _activeTabIndex, value))
                    SyncSelection();
            }
        }

        public RelayCommand PrevTabCommand { get; }
        public RelayCommand NextTabCommand { get; }
        public RelayCommand ConfirmCommand { get; }

        public PositionViewModel(string tab1, string tab2, string tab3)
        {
            Tabs = new List<TabItem>
            {
                new TabItem { Name = tab1, IsSelected = true },
                new TabItem { Name = tab2 },
                new TabItem { Name = tab3 },
            };
            foreach (var tab in Tabs)
            {
                var t = tab;
                tab.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TabItem.IsSelected) && t.IsSelected)
                        _activeTabIndex = Tabs.IndexOf(t);
                };
            }

            PrevTabCommand = new RelayCommand(() =>
                ActiveTabIndex = (ActiveTabIndex + Tabs.Count - 1) % Tabs.Count);
            NextTabCommand = new RelayCommand(() =>
                ActiveTabIndex = (ActiveTabIndex + 1) % Tabs.Count);
            ConfirmCommand = new RelayCommand(() => { });
        }

        private void SyncSelection()
        {
            for (var i = 0; i < Tabs.Count; i++)
                Tabs[i].IsSelected = i == _activeTabIndex;
        }
    }
}
