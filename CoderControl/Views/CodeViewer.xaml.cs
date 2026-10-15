using CoderControl.Models;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CoderControl.Views
{
    /// <summary>
    /// CodeViewer.xaml 的交互逻辑
    /// </summary>
    public partial class CodeViewer : UserControl
    {
        // 依赖属性：接收图4中的 SourceCodes 集合
        // 必须用具体集合类型注册，XAML 才能直接以子元素形式添加 SourceCodeModel（IEnumerable 会报 MC3089）
        public static readonly DependencyProperty SourceCodesProperty = DependencyProperty.Register(
            "SourceCodes", typeof(ObservableCollection<SourceCodeModel>), typeof(CodeViewer),
            new PropertyMetadata(null, OnSourceCodesChanged));

        public ObservableCollection<SourceCodeModel> SourceCodes
        {
            get => (ObservableCollection<SourceCodeModel>)GetValue(SourceCodesProperty);
            set => SetValue(SourceCodesProperty, value);
        }

        public CodeViewer()
        {
            InitializeComponent();
            // 应用 VS 深色主题配色 (C#)
            ApplyDarkTheme();
        }

        private static void OnSourceCodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeViewer viewer && e.NewValue is IEnumerable<SourceCodeModel> sources)
            {
                viewer.BuildTabs(sources);
            }
        }

        // 动态构建 Tab 项
        private void BuildTabs(IEnumerable<SourceCodeModel> sources)
        {
            MainTab.Items.Clear();
            foreach (var source in sources)
            {
                var tabItem = new TabItem { Header = source.CodeType };

                // 读取源码
                string codeText = ReadSourceCode(source.CodeSource);

                if (source.CodeType.ToLower() == "sample")
                {
                    // 如果是 Sample，加载自定义视图 (如 BreatheLightExample)
                    // 这里可以通过反射或工厂根据路径动态创建实例
                    var sampleView = new Views.BreatheLightExample();
                    tabItem.Content = new ContentControl { Content = sampleView };
                }
                else
                {
                    // 如果是 Xaml 或 CSharp，创建 AvalonEdit 实例
                    var editor = new ICSharpCode.AvalonEdit.TextEditor
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 14,
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                        ShowLineNumbers = true,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Text = codeText
                    };

                    // 应用语法高亮
                    if (source.CodeType.ToLower() == "csharp")
                        editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
                    else if (source.CodeType.ToLower() == "xaml")
                        editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");

                    tabItem.Content = editor;
                }

                MainTab.Items.Add(tabItem);
            }

            if (MainTab.Items.Count > 0) MainTab.SelectedIndex = 0; // 默认选中第一个
        }

        // 读取源码文件 (支持 pack:// 或物理路径)
        private string ReadSourceCode(string path)
        {
            try
            {
                // 如果是以 "/" 开头的 pack URI，需要特殊处理或改用 Application.GetResourceStream
                if (path.StartsWith("/"))
                {
                    var uri = new Uri("pack://application:,,," + path.Replace(";component", "").Replace(";", ""));
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo != null)
                    {
                        using (var reader = new StreamReader(streamInfo.Stream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                else if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch { /* 忽略读取错误，返回默认提示 */ }
            return "// 无法读取源码";
        }

        // 应用 VS 深色主题配色
        private void ApplyDarkTheme()
        {
            var csharpHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            if (csharpHighlighting != null)
            {
                // 先通过 GetNamedColor 获取颜色对象，可能为 null，建议检查
                var commentColor = csharpHighlighting.GetNamedColor("Comment");
                if (commentColor != null)
                    commentColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(87, 166, 74));

                var stringColor = csharpHighlighting.GetNamedColor("String");
                if (stringColor != null)
                    stringColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(214, 157, 133));

                var keywordColor = csharpHighlighting.GetNamedColor("Keyword");
                if (keywordColor != null)
                    keywordColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(86, 156, 214));

                var classColor = csharpHighlighting.GetNamedColor("Class");
                if (classColor != null)
                    classColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(78, 201, 176));
            }
        }

        // Tab 切换时自动格式化代码
        private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTab.SelectedItem is TabItem tabItem && tabItem.Content is ICSharpCode.AvalonEdit.TextEditor editor)
            {
                // 如果是 C# 代码，使用 Roslyn 格式化
                if (string.Equals(tabItem.Header?.ToString(), "csharp", StringComparison.OrdinalIgnoreCase))
                {
                    FormatCSharpCode(editor);
                }
            }
        }

        // 使用 Roslyn 格式化 C# 代码
        private void FormatCSharpCode(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(editor.Text);
                var root = tree.GetRoot();
                var formattedRoot = root.NormalizeWhitespace();
                string formattedCode = formattedRoot.ToFullString();

                // 避免频繁刷新造成光标跳动，如果需要保留光标位置可以进一步处理
                if (editor.Text != formattedCode)
                {
                    editor.Text = formattedCode;
                }
            }
            catch { /* 格式化失败保持原样 */ }
        }

        // 右下角悬浮按钮点击事件 (添加新 Tab)
        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            // 动态添加一个 C# Tab 示范
            var newTab = new TabItem { Header = $"NewFile{MainTab.Items.Count + 1}" };
            var editor = new ICSharpCode.AvalonEdit.TextEditor
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                ShowLineNumbers = true,
                Text = "// 新文件\nusing System;\n\nnamespace Test\n{\n    class Program\n    {\n        static void Main()\n        {\n            Console.WriteLine(\"Hello\");\n        }\n    }\n}"
            };
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            newTab.Content = editor;
            MainTab.Items.Add(newTab);
            MainTab.SelectedIndex = MainTab.Items.Count - 1; // 切换到新 Tab
        }
    }
}
