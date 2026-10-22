using System.Collections.Generic;
using Rxbit.Calibration.Core;
using Rxbit.Calibration.Models;

namespace Rxbit.Calibration.ViewModels
{
    /// <summary>输出界面</summary>
    public class OutputViewModel : ObservableObject
    {
        public List<IoRow> LeftRows { get; } = new List<IoRow>();
        public List<IoRow> RightRows { get; } = new List<IoRow>();

        public OutputViewModel()
        {
            for (var i = 0; i <= 8; i++)
            {
                var addr = $"Y{i:D2}";
                var comment = "通用输出";
                LeftRows.Add(new IoRow(addr, comment, i == 0, i == 0));
                RightRows.Add(new IoRow(addr, comment, i == 0, i == 0));
            }
        }
    }
}
