using CoderControl.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoderControl.Views
{
    /// <summary>
    /// 解决方案资源管理器：读取 IpanelWpf.sln 项目列表，按需加载文件夹内容。
    /// </summary>
    public partial class SolutionExplorer : UserControl
    {
        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs", ".screenshots", "packages", "publish",
            "node_modules", "testresults", ".tmp_ref", ".trae"
        };

        // 只展示可预览的文本类文件，避免把二进制产物铺满文件树
        private static readonly HashSet<string> VisibleExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".xaml", ".csproj", ".sln", ".json", ".xml", ".config", ".resx",
            ".md", ".txt", ".bat", ".cmd", ".ps1", ".sh", ".yml", ".yaml", ".props",
            ".targets", ".manifest", ".html", ".htm", ".css", ".js", ".ts", ".sql",
            ".csv", ".ini", ".editorconfig", ".gitignore"
        };

        private static readonly Regex ProjectRegex = new Regex(
            "^Project\\(\"\\{[^}]+\\}\"\\)\\s*=\\s*\"([^\"]+)\",\\s*\"([^\"]+)\"",
            RegexOptions.Compiled);

        private readonly FileNode _rootNode = new FileNode();
        private bool _suppressOpen;
        private bool _openedOnThisClick;
        private string _solutionDir = string.Empty;

        /// <summary>用户点击文件节点时触发，参数为文件完整路径。</summary>
        public event EventHandler<string>? FileOpenRequested;

        public SolutionExplorer()
        {
            InitializeComponent();
            FileTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeItemExpanded));
            Loaded += (_, _) => BuildSolution();
        }

        /// <summary>重新扫描解决方案。</summary>
        public void RefreshSolution()
        {
            BuildSolution();
        }

        /// <summary>折叠所有项目与文件夹。</summary>
        public void CollapseAll()
        {
            foreach (var project in _rootNode.Children)
            {
                CollapseNode(project);
            }
        }

        /// <summary>展开所有项目与文件夹（按需加载后展开）。</summary>
        public void ExpandAll()
        {
            foreach (var project in _rootNode.Children)
            {
                ExpandNode(project);
            }
        }

        private void ExpandNode(FileNode node)
        {
            if (!node.IsFolder)
            {
                return;
            }
            LoadChildren(node);
            node.IsExpanded = true;
            foreach (var child in node.Children)
            {
                ExpandNode(child);
            }
        }

        private void CollapseNode(FileNode node)
        {
            node.IsExpanded = false;
            foreach (var child in node.Children)
            {
                CollapseNode(child);
            }
        }

        private void BuildSolution()
        {
            _suppressOpen = true;
            try
            {
                string? slnPath = FindSolutionFile();
                _solutionDir = slnPath == null ? string.Empty : (Path.GetDirectoryName(slnPath) ?? string.Empty);

                _rootNode.Name = slnPath == null
                    ? "解决方案（未找到 .sln）"
                    : $"解决方案 '{Path.GetFileNameWithoutExtension(slnPath)}'";
                _rootNode.IsFolder = true;
                _rootNode.IsRoot = true;
                _rootNode.FullPath = _solutionDir;
                _rootNode.IconGlyph = "\uE8B7";
                _rootNode.IconBrush = CreateBrush(0x4E, 0xC9, 0xB0);
                _rootNode.Children.Clear();
                _rootNode.ChildrenLoaded = true;

                if (slnPath != null)
                {
                    foreach (var (name, projectFile) in ReadProjects(slnPath))
                    {
                        string projectDir = Path.GetDirectoryName(Path.Combine(_solutionDir, projectFile)) ?? _solutionDir;
                        if (!Directory.Exists(projectDir))
                        {
                            continue;
                        }

                        var projectNode = new FileNode
                        {
                            Name = name,
                            FullPath = projectDir,
                            IsFolder = true,
                            IconGlyph = "\uE8B7",
                            IconBrush = CreateBrush(0xDC, 0xB6, 0x7A)
                        };
                        _rootNode.Children.Add(projectNode);
                    }

                    _rootNode.Name += $" ({_rootNode.Children.Count} 个项目)";
                }

                FileTree.ItemsSource = new[] { _rootNode };
                _rootNode.IsExpanded = true;

                // 默认展开 CoderControl 及其 Models/Views，和参考图保持一致
                var coderControl = _rootNode.Children.FirstOrDefault(
                    n => string.Equals(n.Name, "CoderControl", StringComparison.OrdinalIgnoreCase));
                if (coderControl != null)
                {
                    LoadChildren(coderControl);
                    coderControl.IsExpanded = true;
                    foreach (var folderName in new[] { "Models", "Views" })
                    {
                        var folder = coderControl.Children.FirstOrDefault(
                            n => n.IsFolder && string.Equals(n.Name, folderName, StringComparison.OrdinalIgnoreCase));
                        if (folder != null)
                        {
                            LoadChildren(folder);
                            folder.IsExpanded = true;
                        }
                    }
                }
            }
            catch
            {
                // 扫描失败时保留空树，不影响主界面使用
            }
            finally
            {
                _suppressOpen = false;
            }
        }

        private static IEnumerable<(string Name, string ProjectFile)> ReadProjects(string slnPath)
        {
            var result = new List<(string, string)>();
            foreach (string line in File.ReadAllLines(slnPath))
            {
                Match match = ProjectRegex.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                string name = match.Groups[1].Value;
                string path = match.Groups[2].Value;
                // 跳过解决方案项（解决方案文件夹，不含工程文件）
                if (!path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                result.Add((name, path));
            }
            return result;
        }

        private static string? FindSolutionFile()
        {
            foreach (string start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                var dir = new DirectoryInfo(start);
                for (int i = 0; i < 8 && dir != null; i++)
                {
                    string? sln = Directory.EnumerateFiles(dir.FullName, "*.sln").FirstOrDefault();
                    if (sln != null)
                    {
                        return sln;
                    }
                    dir = dir.Parent;
                }
            }
            return null;
        }

        private void LoadChildren(FileNode node)
        {
            if (!node.IsFolder || node.ChildrenLoaded)
            {
                return;
            }

            node.ChildrenLoaded = true;
            try
            {
                var directory = new DirectoryInfo(node.FullPath);
                if (!directory.Exists)
                {
                    return;
                }

                var folders = directory.EnumerateDirectories()
                    .Where(d => !ExcludedDirectories.Contains(d.Name))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var folder in folders)
                {
                    node.Children.Add(new FileNode
                    {
                        Name = folder.Name,
                        FullPath = folder.FullName,
                        IsFolder = true,
                        IconGlyph = "\uE8B7",
                        IconBrush = CreateBrush(0xDC, 0xB6, 0x7A)
                    });
                }

                var files = directory.EnumerateFiles()
                    .Where(IsVisibleFile)
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    var (glyph, color) = GetFileIcon(file.Extension);
                    node.Children.Add(new FileNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsFolder = false,
                        IconGlyph = glyph,
                        IconBrush = color
                    });
                }
            }
            catch
            {
                // 无权限的目录直接跳过
            }
        }

        private static bool IsVisibleFile(FileInfo file)
        {
            if (VisibleExtensions.Contains(file.Extension))
            {
                return true;
            }
            // 无扩展名的配置类文件（如 .gitignore）也展示
            return file.Name.StartsWith(".", StringComparison.Ordinal) && file.Extension.Length == 0;
        }

        private static (string Glyph, Brush Brush) GetFileIcon(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".cs":
                    return ("\uE943", CreateBrush(0xC5, 0x86, 0xC0));
                case ".xaml":
                    return ("\uE7C3", CreateBrush(0x5F, 0xA8, 0xFF));
                case ".csproj":
                case ".sln":
                case ".props":
                case ".targets":
                    return ("\uE8B7", CreateBrush(0x8D, 0xBE, 0x6B));
                case ".json":
                case ".xml":
                case ".config":
                case ".resx":
                case ".manifest":
                case ".yml":
                case ".yaml":
                    return ("\uE8A5", CreateBrush(0xD7, 0xBA, 0x7D));
                default:
                    return ("\uE8A5", CreateBrush(0xC8, 0xC8, 0xC8));
            }
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private void OnTreeItemExpanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.DataContext is FileNode node)
            {
                LoadChildren(node);
            }
        }

        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suppressOpen || e.NewValue is not FileNode node || node.IsFolder)
            {
                return;
            }
            _openedOnThisClick = true;
            FileOpenRequested?.Invoke(this, node.FullPath);
        }

        private void FileTree_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _openedOnThisClick = false;
        }

        // 已选中的节点再次点击时不会触发 SelectedItemChanged，这里补充处理
        private void FileTree_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_openedOnThisClick || e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var item = ItemsControl.ContainerFromElement(FileTree, source) as TreeViewItem;
            if (item?.DataContext is FileNode node && !node.IsFolder)
            {
                FileOpenRequested?.Invoke(this, node.FullPath);
            }
        }

        private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
        {
            CollapseAll();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSolution();
        }
    }
}
