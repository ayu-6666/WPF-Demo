using System.Collections.Generic;
using Rxbit.Calibration.Core;
using Rxbit.Calibration.Models;

namespace Rxbit.Calibration.ViewModels
{
    /// <summary>手动参数</summary>
    public class ManualParamsViewModel : ObservableObject
    {
        public List<AxisManual> Axes { get; } = new List<AxisManual>();
        public List<ManualCtrlAxis> CtrlAxes { get; } = new List<ManualCtrlAxis> {
            new ManualCtrlAxis("X Axis", 0, 1),
            new ManualCtrlAxis("Y Axis", 1, 1),
            new ManualCtrlAxis("Z Axis", 0, 0),
            new ManualCtrlAxis("A Axis", 0, 0),
        };

        private string _selectedAxis = "A";
        public string SelectedAxis
        {
            get => _selectedAxis;
            set => SetProperty(ref _selectedAxis, value);
        }

        public List<string> AxisOptions { get; } = new List<string> { "A", "B", "C", "X", "Y", "Z" };

        public RelayCommand DecreaseCmd { get; }
        public RelayCommand IncreaseCmd { get; }

        public ManualParamsViewModel()
        {
            string[] names = { "X Axis", "Y Axis", "Z Axis", "A Axis", "B Axis", "C Axis" };
            foreach (var n in names)
                Axes.Add(new AxisManual { AxisName = n });

            DecreaseCmd = new RelayCommand(o => {
                if (o is ManualCtrlAxis a) a.Value -= a.Step;
            });
            IncreaseCmd = new RelayCommand(o => {
                if (o is ManualCtrlAxis a) a.Value += a.Step;
            });
        }
    }
}
