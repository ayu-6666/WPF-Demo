using ICSharpCode.AvalonEdit;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace CoderControl.Models
{
    /// <summary>
    /// 右侧文档区的一个标签：一个文件或一个示例页面。
    /// </summary>
    public class DocumentTab : INotifyPropertyChanged
    {
        private string _header = string.Empty;
        private Encoding _encoding = new UTF8Encoding(false);
        private string _language = "Text";

        public string Header
        {
            get => _header;
            set { _header = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToolTipText)); }
        }

        /// <summary>磁盘路径；示例页为空。</summary>
        public string? FilePath { get; set; }

        /// <summary>用于去重的唯一键（文件路径或 sample://xxx）。</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>标签内容：AvalonEdit 编辑器或示例页面控件。</summary>
        public object Content { get; set; } = null!;

        public TextEditor? Editor { get; set; }

        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(); }
        }

        public Encoding Encoding
        {
            get => _encoding;
            set
            {
                _encoding = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EncodingName));
                OnPropertyChanged(nameof(ToolTipText));
            }
        }

        public string EncodingName => Encoding.WebName.ToUpperInvariant();

        public string ToolTipText => !string.IsNullOrEmpty(FilePath)
            ? $"{FilePath}\n编码: {EncodingName}"
            : Key.StartsWith("sample://", System.StringComparison.Ordinal)
                ? $"{Header}（示例页面）"
                : $"{Header}（未保存的新文件）";

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Header;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
