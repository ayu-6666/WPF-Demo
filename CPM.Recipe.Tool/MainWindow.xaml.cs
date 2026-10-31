using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CPM.Recipe.Tool.Models;
using CPM.Recipe.Tool.Services;
using Microsoft.Win32;

namespace CPM.Recipe.Tool
{
    public partial class MainWindow : Window
    {
        private const string DefaultSourceRoot = @"D:\CMP\RecipeConfig\UnitRecipe";
        private const string MigratedRoot = @"D:\CMP\RecipeConfig\UnitRecipeMigrate";

        private RecipeModel _currentRecipe;
        private string _currentFile;
        private string _sourceRoot = DefaultSourceRoot;
        private List<string> _allJsonFiles = new List<string>();
        private bool _userChangingType;

        public MainWindow()
        {
            InitializeComponent();
            PathTextBox.Text = DefaultSourceRoot;
            Loaded += (s, e) => InitTypesAndScan();
            StateChanged += OnWindowStateChanged;
        }

        // ============ 标题栏 ============
        private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void OnToggleMax(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (MaxIcon != null) MaxIcon.Text = WindowState == WindowState.Maximized ? "❐" : "▢";
        }

        // ============ 初始化 ============
        /// <summary>ComboBox 条目数据类：Display 显示文本，Type 为枚举值（null = 全部）</summary>
        private class TypeItem
        {
            public string Display { get; set; }
            public RecipeType? Type { get; set; }
        }

        private void InitTypesAndScan()
        {
            try
            {
                // 用 ItemsSource + ItemTemplate（XAML 里显式白字），不再塞 ComboBoxItem 容器对象
                _userChangingType = false;
                var items = new List<TypeItem> { new TypeItem { Display = "(全部)", Type = null } };
                foreach (var type in RecipeMigrationService.SupportedTypes)
                    items.Add(new TypeItem { Display = type.ToString(), Type = type });
                RecipeTypeCombo.ItemsSource = items;
                RecipeTypeCombo.SelectedIndex = 0;

                ScanDirectory();
                Log($"扫描目录: {_sourceRoot}  找到 {_allJsonFiles.Count} 个 json 文件");
            }
            catch (Exception ex)
            {
                Log($"初始化失败: {ex.Message}");
            }
        }

        private void ScanDirectory()
        {
            _allJsonFiles.Clear();
            if (Directory.Exists(_sourceRoot))
                _allJsonFiles.AddRange(Directory.EnumerateFiles(_sourceRoot, "*.json", SearchOption.AllDirectories));
            StatusText.Text = $"共 {_allJsonFiles.Count} 个文件";
        }

        private void Log(string msg)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogList.Items.Add($"[{ts}] {msg}");
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }

        // ============ RecipeType 下拉联动 ============
        private void OnRecipeTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_userChangingType) return;
            if (!(RecipeTypeCombo.SelectedItem is TypeItem sel)) return;
            var selectedType = sel.Type; // null 表示 (全部)

            try
            {
                if (selectedType == null)
                {
                    // (全部) → 直接加载根目录第一个 json
                    var first = _allJsonFiles.FirstOrDefault();
                    if (first != null) LoadAndShow(first);
                    return;
                }

                // 按选择的枚举过滤 → 取第一个匹配文件
                var filterName = selectedType.Value.ToString();
                var filtered = _allJsonFiles.Where(f =>
                {
                    try
                    {
                        var m = RecipeMigrationService.Load(f);
                        // 用 ParseRecipeType 解析（兼容别名 RollerRecipe / PenRecipe 等）
                        var parsed = RecipeMigrationService.ParseRecipeType(m.RecipeTypeName);
                        return parsed == selectedType.Value;
                    }
                    catch { return false; }
                }).ToList();

                if (filtered.Count == 0)
                {
                    Log($"未找到 RecipeType={filterName} 的 JSON 文件");
                    return;
                }

                Log($"按 RecipeType={filterName} 加载 {filtered.Count} 个匹配文件，预览第一个");
                LoadAndShow(filtered[0]);

                // 用选中的枚举名对应的模板，强制重新迁移一次（模板刷新）
                if (_currentRecipe != null)
                {
                    _currentRecipe.RecipeTypeName = filterName;
                    RecipeMigrationService.Migrate(_currentRecipe);
                    JsonPreview.Text = RecipeMigrationService.ToJson(_currentRecipe);
                    StatusText.Text = $"已应用模板（{filterName}）  {_currentRecipe.Data?.Count ?? 0} Sections";
                    Log($"  强制应用 {filterName} 模板 → {_currentRecipe.Data?.Count ?? 0} Sections");
                    
                    // 注意：不要覆盖 fileTxt（它显示当前 json 文件名，LoadAndShow 里已设好）
                }
            }
            catch (Exception ex)
            {
                Log($"RecipeType 切换失败: {ex.Message}");
            }
        }

        // ============ 文件操作 ============
        private void OnBrowseFile(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                InitialDirectory = Directory.Exists(_sourceRoot) ? _sourceRoot : DefaultSourceRoot
            };
            if (dlg.ShowDialog(this) == true)
            {
                _sourceRoot = Path.GetDirectoryName(Path.GetDirectoryName(dlg.FileName));
                if (!Directory.Exists(_sourceRoot)) _sourceRoot = Path.GetDirectoryName(dlg.FileName);
                PathTextBox.Text = _sourceRoot;
                ScanDirectory();
                LoadAndShow(dlg.FileName);
                // 同步 ComboBox 到该文件的 RecipeType
                SyncComboToRecipeType(_currentRecipe?.RecipeTypeName);
            }
        }

        private void OnOpenDirectory(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_sourceRoot) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log($"打开目录失败: {ex.Message}");
            }
        }

        private void SyncComboToRecipeType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return;
            var parsed = RecipeMigrationService.ParseRecipeType(typeName);
            _userChangingType = true;
            try
            {
                foreach (var it in RecipeTypeCombo.Items.OfType<TypeItem>())
                {
                    // (全部) 的 Type 是 null
                    if (parsed == null && it.Type == null) { RecipeTypeCombo.SelectedItem = it; return; }
                    if (parsed != null && it.Type == parsed.Value)
                    {
                        RecipeTypeCombo.SelectedItem = it;
                        return;
                    }
                }
            }
            finally
            {
                _userChangingType = false;
            }
        }

        // ============ 迁移 ============
        private void OnMigrateOne(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFile) || _currentRecipe == null)
            {
                MessageBox.Show(this, "请先选择一个 JSON 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Log($"── 单文件迁移: {_currentFile}");
                var before = _currentRecipe.Data?.Count ?? 0;
                RecipeMigrationService.Migrate(_currentRecipe);
                var after = _currentRecipe.Data?.Count ?? 0;
                Log($"  Section: {before} → {after}  (+{after - before})");
                JsonPreview.Text = RecipeMigrationService.ToJson(_currentRecipe);
                StatusText.Text = $"已迁移 (新增 {after - before} Section)";
            }
            catch (Exception ex)
            {
                Log($"  失败: {ex.Message}");
                MessageBox.Show(this, ex.Message, "迁移失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveMigrated(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFile) || _currentRecipe == null)
            {
                MessageBox.Show(this, "没有可保存的内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                RecipeMigrationService.Migrate(_currentRecipe);
                var root = FindRootFor(_currentFile);
                var dest = RecipeMigrationService.SaveMigrated(_currentRecipe, _currentFile, root, MigratedRoot);
                Log($"  已保存: {dest}");
                MessageBox.Show(this, $"已保存到:\n{dest}", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"  保存失败: {ex.Message}");
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnMigrateDirectory(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择要迁移的根目录（UnitRecipe）",
                ShowNewFolderButton = false,
                SelectedPath = Directory.Exists(_sourceRoot) ? _sourceRoot : DefaultSourceRoot
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var source = dlg.SelectedPath;
            _sourceRoot = source;
            PathTextBox.Text = source;
            Log($"══ 开始批量迁移: {source}");
            Log($"══ 目标目录: {MigratedRoot}");

            try
            {
                var results = RecipeMigrationService
                    .MigrateDirectory(source, MigratedRoot, msg => Dispatcher.Invoke(() => Log(msg)))
                    .ToList();

                var ok = results.Count(r => r.Success);
                var fail = results.Count - ok;
                Log($"══ 完成: 成功 {ok}，失败 {fail}");
                StatusText.Text = $"批量迁移完成 {ok}/{results.Count}";

                var firstOk = results.FirstOrDefault(r => r.Success);
                if (firstOk != null) LoadAndShow(firstOk.Source);

                if (fail > 0)
                    MessageBox.Show(this, $"成功 {ok} 个，失败 {fail} 个。详见日志。", "批量迁移", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show(this, $"全部成功！共 {ok} 个。\n输出目录:\n{MigratedRoot}", "批量迁移完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"══ 异常: {ex.Message}");
                MessageBox.Show(this, ex.Message, "批量迁移异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnClearLog(object sender, RoutedEventArgs e) => LogList.Items.Clear();

        // ============ 辅助 ============
        private void LoadAndShow(string path)
        {
            try
            {
                _currentFile = path;
                _currentRecipe = RecipeMigrationService.Load(path);
                RecipeMigrationService.Migrate(_currentRecipe);
                JsonPreview.Text = RecipeMigrationService.ToJson(_currentRecipe);
                StatusText.Text = $"已加载 {Path.GetFileName(path)}  ({_currentRecipe?.Data?.Count ?? 0} Sections)";
                fileTxt.Text = path;
                Log($"加载: {path}  [RecipeType={_currentRecipe?.RecipeTypeName ?? "(null)"}]");
                SyncComboToRecipeType(_currentRecipe?.RecipeTypeName);
            }
            catch (Exception ex)
            {
                Log($"加载失败: {path}  {ex.Message}");
                JsonPreview.Text = "";
            }
        }

        private static string FindRootFor(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                var subDirs = Directory.GetDirectories(dir, "*Recipe*", SearchOption.TopDirectoryOnly);
                if (subDirs.Length > 0) return dir;
                var parent = Path.GetDirectoryName(dir);
                if (parent == null) break;
                dir = parent;
            }
            return DefaultSourceRoot;
        }
    }
}
