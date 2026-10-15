using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfDevDemo.Controls
{
    /// <summary>
    /// 只读代码查看器（带行号与语法高亮），用于 CodeViewer 的 Xaml / CSharp 选项卡。
    /// </summary>
    public class CodeTextEditor : Grid
    {
        private static readonly Color CommentColor = Color.FromRgb(0x00, 0x80, 0x00);
        private static readonly Color StringColor = Color.FromRgb(0xA3, 0x15, 0x15);
        private static readonly Color KeywordColor = Color.FromRgb(0x1E, 0x50, 0xC8);
        private static readonly Color TypeColor = Color.FromRgb(0x2B, 0x91, 0xAF);
        private static readonly Color AttrColor = Color.FromRgb(0xA3, 0x15, 0x15);
        private static readonly Color LineNumberColor = Color.FromRgb(0x80, 0x80, 0x80);

        private const double LineHeight = 17d;

        private static readonly Regex XamlTagRegex = new Regex(@"(</?)([\w:.\-]+)", RegexOptions.Compiled);
        private static readonly Regex XamlAttrRegex = new Regex(@"([\w:.\-]+)(=)(""([^""]*)"")", RegexOptions.Compiled);
        private static readonly Regex XamlCommentRegex = new Regex(@"<!--.*?(-->|$)", RegexOptions.Compiled);
        private static readonly Regex CSharpKeywordRegex =
            new Regex(@"\b(using|namespace|public|private|protected|internal|class|struct|enum|interface|void|partial|static|readonly|const|new|if|else|switch|case|break|continue|return|foreach|for|while|do|try|catch|finally|throw|true|false|null|this|base|get|set|var|object|bool|string|int|double|float|decimal|long|byte|char)\b",
                RegexOptions.Compiled);
        private static readonly Regex CSharpTypeRegex = new Regex(@"\b([A-Z][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
        private static readonly Regex CSharpCommentRegex = new Regex(@"(//.*?$|/\*.*?\*/)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex CSharpStringRegex = new Regex("@?\"((\\\\\"|[^\"])*)\"", RegexOptions.Compiled);

        private readonly TextBlock _lineNumbers = new TextBlock
        {
            Foreground = new SolidColorBrush(LineNumberColor),
            TextAlignment = TextAlignment.Right,
            IsHitTestVisible = false,
        };

        private readonly ScrollViewer _numbersScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };

        private readonly RichTextBox _textBox = new RichTextBox
        {
            IsReadOnly = true,
            IsDocumentEnabled = false,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 10),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
        };

        public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
            nameof(Code), typeof(string), typeof(CodeTextEditor),
            new PropertyMetadata(string.Empty, (d, e) => ((CodeTextEditor)d).Rebuild()));

        public static readonly DependencyProperty CodeLanguageProperty = DependencyProperty.Register(
            nameof(CodeLanguage), typeof(string), typeof(CodeTextEditor),
            new PropertyMetadata("Xaml", (d, e) => ((CodeTextEditor)d).Rebuild()));

        public string Code
        {
            get => (string)GetValue(CodeProperty);
            set => SetValue(CodeProperty, value);
        }

        public string CodeLanguage
        {
            get => (string)GetValue(CodeLanguageProperty);
            set => SetValue(CodeLanguageProperty, value);
        }

        public CodeTextEditor()
        {
            var font = new FontFamily("Consolas, Microsoft YaHei UI");
            _lineNumbers.FontFamily = font;
            _lineNumbers.FontSize = 13;
            _lineNumbers.LineHeight = LineHeight;
            _lineNumbers.Margin = new Thickness(8, 6, 8, 0);

            _textBox.FontFamily = font;
            _textBox.FontSize = 13;
            _textBox.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            _textBox.SetResourceReference(BackgroundProperty, "WD.BackgroundBrush");
            _textBox.PreviewMouseWheel += TextBox_PreviewMouseWheel;
            _textBox.Loaded += TextBox_Loaded;

            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Children.Add(_numbersScroll);
            SetColumn(_numbersScroll, 0);
            _numbersScroll.Content = _lineNumbers;
            Children.Add(_textBox);
            SetColumn(_textBox, 1);
        }

        private void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            // RichTextBox 内部包含 ScrollViewer，找到它并同步行号滚动
            var scrollViewer = FindScrollViewer(_textBox);
            if (scrollViewer != null)
                scrollViewer.ScrollChanged += TextBox_ScrollChanged;
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv)
                return sv;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
                if (result != null)
                    return result;
            }
            return null;
        }

        private void TextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            var offset = _textBox.VerticalOffset - (e.Delta / 3d);
            _textBox.ScrollToVerticalOffset(offset);
        }

        private void TextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _numbersScroll.ScrollToVerticalOffset(e.VerticalOffset);
            _numbersScroll.InvalidateVisual();
        }

        private void Rebuild()
        {
            var code = Code ?? string.Empty;
            var lines = code.Replace("\r\n", "\n").Split('\n');
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                PageWidth = 3000,
                LineHeight = LineHeight,
            };
            foreach (var line in lines)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = LineHeight };
                AppendHighlighted(paragraph, line);
                doc.Blocks.Add(paragraph);
            }
            _textBox.Document = doc;

            var sb = new System.Text.StringBuilder();
            for (var i = 1; i <= lines.Length; i++)
            {
                sb.Append(i);
                if (i < lines.Length)
                    sb.Append('\n');
            }
            _lineNumbers.Text = sb.ToString();
            _lineNumbers.FontSize = 13;
        }

        private void AppendHighlighted(Paragraph paragraph, string line)
        {
            var isXaml = string.Equals(CodeLanguage, "Xaml", StringComparison.OrdinalIgnoreCase);
            var index = 0;
            while (index < line.Length)
            {
                Match m;
                if (isXaml)
                {
                    if ((m = XamlCommentRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Value, CommentColor, false);
                        index = m.Index + m.Length;
                        continue;
                    }
                    if ((m = XamlAttrRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Groups[1].Value, AttrColor, false);
                        AddRun(paragraph, m.Groups[2].Value, KeywordColor, false);
                        AddRun(paragraph, m.Groups[3].Value, StringColor, true);
                        index = m.Index + m.Length;
                        continue;
                    }
                    if ((m = XamlTagRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Groups[1].Value, KeywordColor, false);
                        AddRun(paragraph, m.Groups[2].Value, KeywordColor, false);
                        index = m.Index + m.Length;
                        continue;
                    }
                }
                else
                {
                    if ((m = CSharpCommentRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Value, CommentColor, false);
                        index = m.Index + m.Length;
                        continue;
                    }
                    if ((m = CSharpStringRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Value, StringColor, false);
                        index = m.Index + m.Length;
                        continue;
                    }
                    if ((m = CSharpKeywordRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Value, KeywordColor, false);
                        index = m.Index + m.Length;
                        continue;
                    }
                    if ((m = CSharpTypeRegex.Match(line, index)).Success && m.Index == index)
                    {
                        AddRun(paragraph, m.Value, TypeColor, false);
                        index = m.Index + m.Length;
                        continue;
                    }
                }
                var next = FindNext(line, index, isXaml);
                AddRun(paragraph, line.Substring(index, next - index),
                    (Color?)null, false);
                index = next;
            }
        }

        private static int FindNext(string line, int index, bool isXaml)
        {
            var best = line.Length;
            Match Test(Regex r)
            {
                var m = r.Match(line, index);
                return m.Success && m.Index > index ? m : null;
            }
            var candidates = isXaml
                ? new[] { Test(XamlCommentRegex), Test(XamlAttrRegex), Test(XamlTagRegex) }
                : new[] { Test(CSharpCommentRegex), Test(CSharpStringRegex), Test(CSharpKeywordRegex), Test(CSharpTypeRegex) };
            foreach (var m in candidates)
                if (m != null && m.Index < best)
                    best = m.Index;
            return best;
        }

        private static void AddRun(Paragraph paragraph, string text, Color? color, bool underline)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var run = new Run(text);
            if (color.HasValue)
                run.Foreground = new SolidColorBrush(color.Value);
            if (underline)
                run.TextDecorations = TextDecorations.Underline;
            paragraph.Inlines.Add(run);
        }
    }
}
