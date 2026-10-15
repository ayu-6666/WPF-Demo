using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoderControl.Models
{
    // SourceCodeModel.cs
    public class SourceCodeModel
    {
        // 源码路径 (相对路径或绝对路径，如 "/WPFDevelopers.SamplesCode;component/ExampleViews/ScrollViewerAnimationExample.xaml")
        public string CodeSource { get; set; } = string.Empty;

        // 代码类型 (Sample, Xaml, CSharp)
        public string CodeType { get; set; } = string.Empty;
    }

    // BreatheLightModel.cs (用于 Sample 页面的数据绑定)
    public class BreatheLightModel
    {
        public string Title { get; set; } = string.Empty;
        public string ImageSource { get; set; } = string.Empty;
        public string Status { get; set; } = "正在直播";
    }
}
