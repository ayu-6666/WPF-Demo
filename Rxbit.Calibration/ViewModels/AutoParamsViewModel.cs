using System.Collections.Generic;
using Rxbit.Calibration.Models;

namespace Rxbit.Calibration.ViewModels
{
    /// <summary>自动参数</summary>
    public class AutoParamsViewModel
    {
        public List<AxisAuto> Axes { get; } = new List<AxisAuto>();

        public AutoParamsViewModel()
        {
            for (int i = 0; i < 6; i++)
                Axes.Add(new AxisAuto { AxisName = "X Axis" });
        }
    }
}
