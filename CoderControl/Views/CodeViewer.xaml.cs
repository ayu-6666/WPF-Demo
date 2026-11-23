using CoderControl.Models;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CoderControl.Views
{
    /// <summary>
    /// 文档区：标签页 + AvalonEdit 代码视图 + Sample 界面预览。
    /// </summary>
    public partial class CodeViewer : UserControl
    {
        // 依赖属性：接收初始展示的源码集合
        public static readonly DependencyProperty SourceCodesProperty = DependencyProperty.Register(
            "SourceCodes", typeof(ObservableCollection<SourceCodeModel>), typeof(CodeViewer),
            new PropertyMetadata(null, OnSourceCodesChanged));

        private readonly Dictionary<TextEditor, SearchPanel> _searchPanels = new Dictionary<TextEditor, SearchPanel>();
        private double _fontSize = 14;
        private bool _wordWrap = true;
        private bool _showLineNumbers = true;
        private int _newFileCounter = 1;

        /// <summary>当前文档发生变化（切换、打开、关闭）时触发。</summary>
        public event EventHandler? CurrentDocumentChanged;

        public ObservableCollection<DocumentTab> Documents { get; } = new ObservableCollection<DocumentTab>();

        public ObservableCollection<SourceCodeModel> SourceCodes
        {
            get => (ObservableCollection<SourceCodeModel>)GetValue(SourceCodesProperty);
            set => SetValue(SourceCodesProperty, value);
        }

        public DocumentTab? Current => MainTab.SelectedItem as DocumentTab;

        public TextEditor? CurrentEditor => Current?.Editor;

        public string CurrentTitle => Current?.Header ?? string.Empty;

        public string CurrentPath => Current?.FilePath ?? string.Empty;

        static CodeViewer()
        {
            // 支持 GBK 等本地编码，编码菜单可切换重载
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public CodeViewer()
        {
            InitializeComponent();
            SourceCodes = new ObservableCollection<SourceCodeModel>();
            SourceCodes.CollectionChanged += (_, _) =>
                Dispatcher.BeginInvoke(new Action(() => BuildTabs(SourceCodes)));
            ApplyDarkTheme();
            UpdateEmptyState();
        }

        private static void OnSourceCodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeViewer viewer && e.NewValue is IEnumerable<SourceCodeModel> sources)
            {
                viewer.BuildTabs(sources);
            }
        }

        // ================= 标签构建 =================

        private void BuildTabs(IEnumerable<SourceCodeModel> sources)
        {
            Documents.Clear();
            foreach (var source in sources)
            {
                Documents.Add(CreateTab(source));
            }

            if (Documents.Count > 0)
            {
                MainTab.SelectedIndex = 0;
            }
            UpdateEmptyState();
            CurrentDocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        private DocumentTab CreateTab(SourceCodeModel source)
        {
            if (string.Equals(source.CodeType, "Sample", StringComparison.OrdinalIgnoreCase))
            {
                return CreateSampleTab();
            }

            string text = ReadSourceCode(source.CodeSource);
            string language = string.Equals(source.CodeType, "CSharp", StringComparison.OrdinalIgnoreCase) ? "C#" : "XAML";
            var editor = CreateEditor(text, language);
            return new DocumentTab
            {
                Header = source.CodeType,
                Key = $"initial://{source.CodeSource}#{source.CodeType}",
                Language = language,
                Content = editor,
                Editor = editor
            };
        }

        /// <summary>创建（或切换到已存在的）Sample 预览页。</summary>
        public void OpenSample()
        {
            const string key = "sample://breathelight";
            var existing = Documents.FirstOrDefault(d => d.Key == key);
            if (existing != null)
            {
                MainTab.SelectedItem = existing;
                return;
            }

            var tab = CreateSampleTab();
            Documents.Add(tab);
            MainTab.SelectedItem = tab;
            UpdateEmptyState();
        }

        private DocumentTab CreateSampleTab()
        {
            var view = new BreatheLightExample();
            return new DocumentTab
            {
                Header = "Sample",
                Key = "sample://breathelight",
                Language = "Sample",
                Content = view
            };
        }

        /// <summary>打开磁盘文件；已打开时直接切换，不重复创建标签。</summary>
        public void OpenFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            var existing = Documents.FirstOrDefault(
                d => d.FilePath != null && string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                MainTab.SelectedItem = existing;
                return;
            }

            var info = new FileInfo(fullPath);
            if (info.Length > 4 * 1024 * 1024)
            {
                ShowMessage("文件超过 4MB，已跳过预览。", MessageBoxImage.Warning);
                return;
            }

            string text;
            Encoding encoding;
            try
            {
                text = ReadFileText(fullPath, null, out encoding);
            }
            catch (Exception ex)
            {
                ShowMessage($"无法读取文件：\n{ex.Message}", MessageBoxImage.Warning);
                return;
            }

            string language = DetectLanguage(fullPath);
            var editor = CreateEditor(text, language);
            var tab = new DocumentTab
            {
                Header = info.Name,
                FilePath = fullPath,
                Key = fullPath,
                Language = language,
                Encoding = encoding,
                Content = editor,
                Editor = editor
            };

            Documents.Add(tab);
            MainTab.SelectedItem = tab;
            UpdateEmptyState();
        }

        private TextEditor CreateEditor(string text, string language)
        {
            var editor = new TextEditor
            {
                FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                FontSize = _fontSize,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                LineNumbersForeground = new SolidColorBrush(Color.FromRgb(133, 133, 133)),
                ShowLineNumbers = _showLineNumbers,
                WordWrap = _wordWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10, 6, 6, 6),
                SyntaxHighlighting = GetHighlighting(language),
                Text = text
            };

            _searchPanels[editor] = SearchPanel.Install(editor);
            return editor;
        }

        // ================= 新建 / 关闭标签 =================

        /// <summary>新建一个空白 C# 标签。</summary>
        public void NewFile()
        {
            int index = _newFileCounter++;
            string code = "using System;\r\n\r\nnamespace CoderControl\r\n{\r\n" +
                          $"    internal class NewFile{index}\r\n    {{\r\n    }}\r\n}}\r\n";
            var editor = CreateEditor(code, "C#");
            var tab = new DocumentTab
            {
                Header = $"NewFile{index}.cs",
                Key = $"new://{index}",
                Language = "C#",
                Encoding = new UTF8Encoding(false),
                Content = editor,
                Editor = editor
            };
            Documents.Add(tab);
            MainTab.SelectedItem = tab;
            UpdateEmptyState();
            editor.Focus();
        }

        /// <summary>关闭当前标签。</summary>
        public void CloseCurrent()
        {
            if (Current != null)
            {
                CloseTab(Current);
            }
        }

        /// <summary>关闭所有标签，显示空状态。</summary>
        public void CloseAll()
        {
            Documents.Clear();
            UpdateEmptyState();
            CurrentDocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CloseTab(DocumentTab tab)
        {
            int index = Documents.IndexOf(tab);
            if (index < 0)
            {
                return;
            }

            // 关闭后自动切到相邻标签
            DocumentTab? next = Documents.Count > 1
                ? Documents[index + 1 < Documents.Count ? index + 1 : index - 1]
                : null;

            Documents.Remove(tab);
            if (next != null && Documents.Contains(next))
            {
                MainTab.SelectedItem = next;
            }

            UpdateEmptyState();
            CurrentDocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: DocumentTab tab })
            {
                CloseTab(tab);
            }
        }

        private void UpdateEmptyState()
        {
            bool empty = Documents.Count == 0;
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            MainTab.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        // ================= 编辑操作（供菜单调用） =================

        public void Undo() => InvokeEditor(e => e.Undo());

        public void Redo() => InvokeEditor(e => e.Redo());

        public void Cut() => InvokeEditor(e => e.Cut());

        public void Copy() => InvokeEditor(e => e.Copy());

        public void Paste() => InvokeEditor(e => e.Paste());

        public void SelectAll() => InvokeEditor(e => e.SelectAll());

        /// <summary>打开 AvalonEdit 内置查找栏。</summary>
        public void ShowFind()
        {
            var editor = CurrentEditor;
            if (editor == null || !_searchPanels.TryGetValue(editor, out var panel))
            {
                return;
            }
            editor.Focus();
            panel.Open();
        }

        public void FindNext()
        {
            var editor = CurrentEditor;
            if (editor != null && _searchPanels.TryGetValue(editor, out var panel))
            {
                editor.Focus();
                panel.FindNext();
            }
        }

        public void FindPrevious()
        {
            var editor = CurrentEditor;
            if (editor != null && _searchPanels.TryGetValue(editor, out var panel))
            {
                editor.Focus();
                panel.FindPrevious();
            }
        }

        public void SetFontSize(double size)
        {
            _fontSize = size;
            foreach (var editor in getEditors())
            {
                editor.FontSize = size;
            }
        }

        public void SetWordWrap(bool enabled)
        {
            _wordWrap = enabled;
            foreach (var editor in getEditors())
            {
                editor.WordWrap = enabled;
            }
        }

        public void SetShowLineNumbers(bool enabled)
        {
            _showLineNumbers = enabled;
            foreach (var editor in getEditors())
            {
                editor.ShowLineNumbers = enabled;
            }
        }

        /// <summary>切换语法高亮语言；language 为 Auto 时按文件扩展名识别。</summary>
        public void SetLanguage(string language)
        {
            var tab = Current;
            if (tab?.Editor == null)
            {
                return;
            }

            if (string.Equals(language, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                tab.Language = DetectLanguage(tab.FilePath);
            }
            else
            {
                tab.Language = language;
            }
            tab.Editor.SyntaxHighlighting = GetHighlighting(tab.Language);
        }

        /// <summary>以指定代码页重新解码当前文件（65001=UTF-8，936=GBK）。</summary>
        public void ReloadWithEncoding(int codePage)
        {
            var tab = Current;
            if (tab?.FilePath == null || tab.Editor == null)
            {
                return;
            }

            try
            {
                var encoding = Encoding.GetEncoding(codePage);
                tab.Editor.Text = ReadFileText(tab.FilePath, encoding, out _);
                tab.Encoding = encoding;
            }
            catch (Exception ex)
            {
                ShowMessage($"按 {codePage} 编码重新加载失败：\n{ex.Message}", MessageBoxImage.Warning);
            }
        }

        /// <summary>重新从磁盘读取当前文件（自动识别 BOM）。</summary>
        public void ReloadCurrent()
        {
            var tab = Current;
            if (tab?.FilePath == null || tab.Editor == null)
            {
                return;
            }

            try
            {
                tab.Editor.Text = ReadFileText(tab.FilePath, null, out Encoding encoding);
                tab.Encoding = encoding;
            }
            catch (Exception ex)
            {
                ShowMessage($"重新加载失败：\n{ex.Message}", MessageBoxImage.Warning);
            }
        }

        /// <summary>使用 Roslyn 格式化当前 C# 文档。</summary>
        public void FormatCurrent()
        {
            var tab = Current;
            if (tab?.Editor == null)
            {
                return;
            }
            if (!string.Equals(tab.Language, "C#", StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("格式化仅支持 C# 文档。", MessageBoxImage.Information);
                return;
            }

            try
            {
                var root = CSharpSyntaxTree.ParseText(tab.Editor.Text).GetRoot();
                tab.Editor.Text = root.NormalizeWhitespace().ToFullString();
            }
            catch (Exception ex)
            {
                ShowMessage($"格式化失败：\n{ex.Message}", MessageBoxImage.Warning);
            }
        }

        /// <summary>去除当前文档每行行尾空白。</summary>
        public void TrimTrailingWhitespace()
        {
            var editor = CurrentEditor;
            if (editor != null)
            {
                editor.Text = Regex.Replace(editor.Text, @"[ \t]+(?=\r?$)", string.Empty, RegexOptions.Multiline);
            }
        }

        /// <summary>删除当前文档中的所有空行。</summary>
        public void RemoveEmptyLines()
        {
            var editor = CurrentEditor;
            if (editor == null)
            {
                return;
            }
            var lines = editor.Text.Split('\n');
            editor.Text = string.Join("\n", lines.Where(l => l.Trim('\r', ' ', '\t').Length > 0));
        }

        /// <summary>当前文档统计信息。</summary>
        public string GetStatistics()
        {
            var tab = Current;
            if (tab == null)
            {
                return "当前没有打开的文档。";
            }

            string text = tab.Editor?.Text ?? string.Empty;
            int lines = text.Length == 0 ? 0 : text.Split('\n').Length;
            int chars = text.Length;
            long bytes = 0;
            if (tab.FilePath != null && File.Exists(tab.FilePath))
            {
                bytes = new FileInfo(tab.FilePath).Length;
            }

            return $"文件：{tab.FilePath ?? tab.Header}\n行数：{lines}\n字符数：{chars}\n" +
                   (bytes > 0 ? $"磁盘大小：{bytes:N0} 字节\n" : string.Empty) +
                   $"编码：{tab.EncodingName}";
        }

        public void CopyCurrentPath()
        {
            var tab = Current;
            if (tab?.FilePath == null)
            {
                return;
            }
            try
            {
                Clipboard.SetText(tab.FilePath);
            }
            catch
            {
                // 剪贴板被占用时忽略
            }
        }

        private IEnumerable<TextEditor> getEditors()
        {
            return Documents.Where(d => d.Editor != null).Select(d => d.Editor!);
        }

        private void InvokeEditor(Action<TextEditor> action)
        {
            var editor = CurrentEditor;
            if (editor == null)
            {
                return;
            }
            editor.Focus();
            action(editor);
        }

        private void ShowMessage(string message, MessageBoxImage icon)
        {
            var owner = Window.GetWindow(this);
            string title = "CoderControl";
            if (owner != null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, icon);
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            }
        }

        // ================= 文件读取与高亮 =================

        private static string ReadFileText(string path, Encoding? forced, out Encoding actual)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (forced != null)
            {
                actual = forced;
                return DecodeBytes(bytes, forced);
            }

            using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true))
            {
                string text = reader.ReadToEnd();
                actual = reader.CurrentEncoding;
                return text;
            }
        }

        private static string DecodeBytes(byte[] bytes, Encoding encoding)
        {
            int offset = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                offset = 3;
            }
            else if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            {
                offset = 2;
            }
            return encoding.GetString(bytes, offset, bytes.Length - offset);
        }

        private static string DetectLanguage(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Text";
            }

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".cs":
                    return "C#";
                case ".xaml":
                    return "XAML";
                case ".xml":
                case ".csproj":
                case ".config":
                case ".resx":
                case ".props":
                case ".targets":
                case ".manifest":
                case ".sln":
                    return "XML";
                case ".js":
                case ".json":
                case ".ts":
                    return "JavaScript";
                case ".html":
                case ".htm":
                    return "HTML";
                case ".css":
                    return "CSS";
                case ".sql":
                    return "TSQL";
                default:
                    return "Text";
            }
        }

        private static IHighlightingDefinition? GetHighlighting(string language)
        {
            string? name = language switch
            {
                "C#" => "C#",
                "XAML" => "XML",
                "XML" => "XML",
                "JavaScript" => "JavaScript",
                "HTML" => "HTML",
                "CSS" => "CSS",
                "TSQL" => "TSQL",
                _ => null
            };
            return name == null ? null : HighlightingManager.Instance.GetDefinition(name);
        }

        private string ReadSourceCode(string path)
        {
            try
            {
                // "/" 开头为本程序集 EmbeddedResource 原文，打包后依然可用
                if (path.StartsWith("/"))
                {
                    string manifestName = "CoderControl" + path.Replace('/', '.');
                    var manifestStream = typeof(CodeViewer).Assembly.GetManifestResourceStream(manifestName);
                    if (manifestStream != null)
                    {
                        using (var reader = new StreamReader(manifestStream))
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
            catch
            {
                // 忽略读取错误，返回提示
            }
            return "// 无法读取源码";
        }

        // VS 深色主题语法配色
        private void ApplyDarkTheme()
        {
            var csharpHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            if (csharpHighlighting == null)
            {
                return;
            }

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

        private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CurrentDocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
