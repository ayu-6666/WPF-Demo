using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CoderControl.Models
{
    /// <summary>
    /// 解决方案资源管理器节点：项目、文件夹或文件。
    /// </summary>
    public class FileNode : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public string Name { get; set; } = string.Empty;

        /// <summary>磁盘完整路径；解决方案根节点为空。</summary>
        public string FullPath { get; set; } = string.Empty;

        public bool IsFolder { get; set; }

        /// <summary>是否为解决方案根节点（用于加粗展示）。</summary>
        public bool IsRoot { get; set; }

        /// <summary>子节点是否已从磁盘加载（文件夹按需加载）。</summary>
        public bool ChildrenLoaded { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileNode> Children { get; } = new ObservableCollection<FileNode>();

        public string IconGlyph { get; set; } = "\uE8A5";

        public Brush IconBrush { get; set; } = new SolidColorBrush(Color.FromRgb(200, 200, 200));

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Name;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
