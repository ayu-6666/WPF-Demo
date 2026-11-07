using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CPM.Recipe.Tool.Models;
using CPM.Recipe.Tool.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CPM.Recipe.Tool
{
    public partial class MainWindow : Window
    {
        private const string DefaultSourceRoot = @"D:\CMP\RecipeConfig\UnitRecipe";
        private const string DefaultMigratedRoot = @"D:\CMP\RecipeConfig\UnitRecipeMigrate";
        private const string BackupFolderName = "_backup";

        private readonly AppSettings _settings = SettingsService.Load();

        private RecipeModel _currentRecipe;
        private string _currentFile;
        private string _sourceRoot = ResolveInitialRoot();

        /// <summary>
        /// 输出目录：优先使用用户上次通过“选择目录”记住的路径；
        /// 用 --root 指定副本根时忽略该偏好，输出放到同级 “&lt;root&gt;_Migrate”，保证验证不会写生产输出目录。
        /// </summary>
        private string MigratedRoot;

        private static string ResolveMigratedRoot(string sourceRoot)
        {
            var full = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(full, Path.GetFullPath(DefaultSourceRoot).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return DefaultMigratedRoot;
            return full + "_Migrate";
        }

        private static string ResolveInitialOutputRoot(AppSettings settings)
        {
            if (!HasRootArgument() && settings != null && !string.IsNullOrWhiteSpace(settings.OutputRoot)
                && Directory.Exists(settings.OutputRoot))
                return Path.GetFullPath(settings.OutputRoot);
            return ResolveMigratedRoot(ResolveInitialRoot());
        }

        private static bool HasRootArgument()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 1; i < args.Length - 1; i++)
                if (string.Equals(args[i], "--root", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>支持 --root &lt;目录&gt; 指定本次会话的扫描根目录（便于在副本上验证，避免误写生产目录）。</summary>
        private static string ResolveInitialRoot()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 1; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--root", StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(args[i + 1]))
                        return Path.GetFullPath(args[i + 1]);
                }
                if (args.Length > 1 && !args[1].StartsWith("-") && Directory.Exists(args[1]))
                    return Path.GetFullPath(args[1]);
            }
            catch { }
            return DefaultSourceRoot;
        }

        /// <summary>
        /// EC Configuration 目录：支持 --chamber-root &lt;目录&gt;（便于在副本上联调），
        /// 其次使用上次选择的目录，最后回退到默认 ChamberConfig。
        /// </summary>
        private static string ResolveInitialChamberRoot(AppSettings settings)
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (var i = 1; i < args.Length - 1; i++)
                {
                    if (string.Equals(args[i], "--chamber-root", StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(args[i + 1]))
                        return Path.GetFullPath(args[i + 1]);
                }
            }
            catch { }

            if (settings != null && !string.IsNullOrWhiteSpace(settings.ChamberRoot)
                && Directory.Exists(settings.ChamberRoot))
                return Path.GetFullPath(settings.ChamberRoot);

            return DefaultChamberRoot;
        }

        private List<string> _allJsonFiles = new List<string>();
        private bool _userChangingType;
        private string _typeFolder;   // 当前 Recipe 类型目录名（null = (全部)）

        // ===== 新增：JSON 数据视图状态 =====
        private JObject _currentJson;
        private JsonFileFormat _currentFormat;
        private string _currentRawText = "";
        private string _dataArrayProperty = "Data";
        private DataTable _rowTable;

        // ===== 新增：文件下拉 + 未保存修改 =====
        private readonly List<FileItem> _fileItems = new List<FileItem>();
        private readonly List<JObject> _pendingEdits = new List<JObject>();
        private bool _suppressComboEvents;

        private FileSystemWatcher _watcher;
        private DispatcherTimer _watcherTimer;

        // ===== EC Configuration 页签状态 =====
        private const string DefaultChamberRoot = @"D:\CMP\ChamberConfig";
        private string _chamberRoot;
        private readonly List<EcCategory> _ecCategories = new List<EcCategory>();
        private EcFileData _ecFileData;
        private List<EcEntry> _ecViewEntries = new List<EcEntry>();
        private string _ecSheetName;
        private bool _ecLoading;

        /// <summary>重排/导出流程进行中，避免保存动作再次触发自动导出（防止递归）。</summary>
        private bool _autoExporting;

        /// <summary>窗口正在关闭，关闭流程内的保存不触发自动导出。</summary>
        private bool _closing;

        public MainWindow()
        {
            InitializeComponent();
            MigratedRoot = ResolveInitialOutputRoot(_settings);
            _chamberRoot = ResolveInitialChamberRoot(_settings);
            PathTextBox.Text = _sourceRoot;
            OutputPathTextBox.Text = MigratedRoot;
            if (EcRootTextBox != null) EcRootTextBox.Text = _chamberRoot;
            Loaded += (s, e) => { InitTypesAndScan(); InitEcTab(); };
            StateChanged += OnWindowStateChanged;
            Closing += OnWindowClosing;
            PreviewTabs.SelectedIndex = 0;

            ApplyLanguage();
            UpdateThemeIcon();
        }

        // ================= 标题栏 =================
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

        // ================= 主题 / 语言 / 帮助 =================
        private void OnToggleTheme(object sender, RoutedEventArgs e)
        {
            var next = ThemeManager.IsLight ? ThemeManager.Dark : ThemeManager.Light;
            ThemeManager.Apply(next);
            _settings.Theme = next;
            SettingsService.Save(_settings);
            UpdateThemeIcon();
            StatusText.Text = next == ThemeManager.Light ? L.T("status.themeLight") : L.T("status.themeDark");
        }

        private void UpdateThemeIcon()
        {
            if (ThemeIcon == null) return;
            ThemeIcon.Text = ThemeManager.IsLight ? "☀" : "☾";
            ThemeBtn.ToolTip = ThemeManager.IsLight ? L.T("tip.theme.light") : L.T("tip.theme.dark");
        }

        private void OnToggleLanguage(object sender, RoutedEventArgs e)
        {
            L.SetLanguage(L.IsEnglish ? L.Zh : L.En);
            _settings.Language = L.Language;
            SettingsService.Save(_settings);
            ApplyLanguage();
            StatusText.Text = L.T("status.ready");
            Log(L.IsEnglish ? "Language switched to English (data and current file kept)."
                            : "界面语言已切换为中文（已加载数据与当前文件保持不变）。");
        }

        /// <summary>按当前语言刷新全部静态文案；不重新加载任何数据，保证已加载数据/当前文件不丢。</summary>
        private void ApplyLanguage()
        {
            Title = L.T("app.windowTitle");
            TitleBarTitle.Text = L.T("app.title");
            FooterLeft.Text = L.T("footer.left");
            FooterRight.Text = L.T("footer.right");

            PathLabel.Text = L.T("label.path");
            BrowseBtn.Content = L.T("btn.browse");
            OutputLabel.Text = L.T("label.output");
            OpenOutputBtn.Content = L.T("btn.openOutput");
            OpenOutputBtn.ToolTip = L.T("btn.openOutputTip");
            BrowseOutputBtn.Content = L.T("btn.browseOutput");
            BrowseOutputBtn.ToolTip = L.T("btn.browseOutputTip");

            AddBtnBar.Content = L.T("btn.add");
            AddBtnBar.ToolTip = L.T("btn.addTip");
            DeleteBtnBar.Content = L.T("btn.delete");
            DeleteBtnBar.ToolTip = L.T("btn.deleteTip");
            EditBtnBar.Content = L.T("btn.edit");
            EditBtnBar.ToolTip = L.T("btn.editTip");
            SaveAllBtnBar.Content = L.T("btn.saveAll");
            SaveAllBtnBar.ToolTip = L.T("btn.saveAllTip");
            MigrateOutputBtn.Content = L.T("btn.migrateOutput");
            MigrateOutputBtn.ToolTip = L.T("btn.migrateOutputTip");

            TypeLabel.Text = L.T("label.recipeType");
            FileLabel.Text = L.T("label.recipeFile");
            RefreshBtn.ToolTip = L.T("tip.refresh");

            PreviewTitle.Text = L.T("label.preview");

            DataTab.Header = L.T("tab.data");
            RawTab.Header = L.T("tab.raw");
            FilesTab.Header = L.T("tab.files");

            AddBtn.Content = L.T("btn.add");
            AddBtn.ToolTip = L.T("btn.addTip");
            DeleteBtn.Content = L.T("btn.delete");
            DeleteBtn.ToolTip = L.T("btn.deleteTip");
            DiscardBtn.Content = L.T("btn.discard");
            DiscardBtn.ToolTip = L.T("btn.discardTip");
            SaveBtn.Content = L.T("btn.save");
            DoubleClickHint.Text = L.T("data.doubleClickHint");
            MenuEdit.Header = L.T("menu.edit");
            MenuCopyJson.Header = L.T("menu.copyJson");
            MenuCopyId.Header = L.T("menu.copyId");

            RawHint.Text = L.T("raw.hint");
            FileListHelp.Text = L.T("files.hint");
            LogTitle.Text = L.T("log.title");
            ClearLogBtn.Content = L.T("btn.clear");

            FileNameCol.Header = L.T("col.fileName");
            SizeCol.Header = L.T("col.size");
            ModifiedCol.Header = L.T("col.modified");
            RelPathCol.Header = L.T("col.relativePath");

            ThemeBtn.Width = 42;
            HelpBtn.ToolTip = L.T("tip.help");
            LangBtn.ToolTip = L.T("tip.language");
            LangIcon.Text = L.IsEnglish ? "中" : "EN";

            UpdateThemeIcon();

            // 类型下拉的“(全部)”项随语言变化（其余为目录名，不翻译）
            var typeItems = (RecipeTypeCombo.ItemsSource as IEnumerable<TypeItem>)?.ToList() ?? new List<TypeItem>();
            foreach (var it in typeItems)
                if (string.IsNullOrEmpty(it.FolderName)) it.Display = L.T("combo.allTypes");
            if (typeItems.Count > 0) RecipeTypeCombo.Items.Refresh();

            FileHintText.Text = Directory.Exists(_sourceRoot) ? L.T("status.fileHint", _fileItems.Count) : L.T("status.dirMissing");
            FileListHint.Text = L.T("files.count", _fileItems.Count);
            FileListEmpty.Text = L.T("files.empty");
            DataGridEmpty.Text = L.T("data.empty");

            // EC Configuration 页签（Tab 标题按需求固定为英文）
            EcPathLabel.Text = L.T("ec.label.root");
            EcBrowseBtn.Content = L.T("btn.browse");
            EcRefreshBtn.ToolTip = L.T("tip.refresh");
            EcTypeTitle.Text = L.T("ec.label.type");
            EcFileTitle.Text = L.T("ec.label.file");
            EcSheetTitle.Text = L.T("ec.label.sheet");
            EcAddBtn.Content = L.T("ec.btn.add");
            EcAddBtn.ToolTip = L.T("ec.btn.addTip");
            EcEditBtn.Content = L.T("ec.btn.batchEdit");
            EcEditBtn.ToolTip = L.T("ec.btn.batchEditTip");
            EcDeleteBtn.Content = L.T("ec.btn.delete");
            EcDeleteBtn.ToolTip = L.T("ec.btn.deleteTip");
            EcMenuAdd.Header = L.T("ec.menu.add");
            EcMenuEdit.Header = L.T("ec.menu.edit");
            EcMenuDelete.Header = L.T("ec.menu.deleteSelected");
            EcGridEmpty.Text = L.T("ec.grid.empty");
            UpdateEcUiState();

            UpdateDirtyUi();
            if (_currentJson != null && _rowTable != null) RefreshDataTab();
            RefreshRawTab();
        }

        private void OnOpenHelp(object sender, RoutedEventArgs e)
        {
            var helpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help");
            var candidates = L.IsEnglish
                ? new[] { Path.Combine(helpDir, "help-en.pdf"), Path.Combine(helpDir, "help-zh.pdf") }
                : new[] { Path.Combine(helpDir, "help-zh.pdf") };

            var path = candidates.FirstOrDefault(File.Exists);
            if (path == null)
            {
                StatusText.Text = L.T("msg.title.helpMissing");
                MessageDialog.Warning(this, L.T("msg.helpMissing", candidates[0]), L.T("msg.title.helpMissing"));
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                StatusText.Text = L.T("status.helpOpened");
            }
            catch (Exception ex)
            {
                StatusText.Text = L.T("msg.title.error");
                MessageDialog.Error(this, L.T("msg.openHelpFailed", ex.Message), L.T("msg.title.error"));
            }
        }

        // ================= 选择目录 / 输出目录 =================
        /// <summary>只允许选择文件夹：选中后把它作为新的 recipe 根，并重建类型列表与文件列表。</summary>
        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            if (!ConfirmLeaveCurrentFile()) return;

            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = L.T("dlg.migrateRoot"),
                ShowNewFolderButton = false,
                SelectedPath = Directory.Exists(_sourceRoot) ? _sourceRoot : DefaultSourceRoot
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var selected = dlg.SelectedPath;
            if (string.IsNullOrEmpty(selected)) return;
            if (!Directory.Exists(selected))
            {
                MessageDialog.Warning(this, L.T("msg.dirMissing", selected), L.T("msg.title.warning"));
                return;
            }

            _sourceRoot = Path.GetFullPath(selected);
            PathTextBox.Text = _sourceRoot;
            if (string.IsNullOrWhiteSpace(_settings.OutputRoot))
                MigratedRoot = ResolveMigratedRoot(_sourceRoot);
            UpdateOutputPath();

            // 新根目录与旧文件无关：清空当前视图后重建类型/文件列表（类型下拉重建会联动加载第一个文件）
            _currentFile = null;
            _typeFolder = null;
            _currentJson = null;
            _currentRawText = "";
            _pendingEdits.Clear();
            UpdateDirtyUi();
            RefreshDataTab();
            RefreshRawTab();

            InitTypesAndScan();
            Log($"已切换根目录: {_sourceRoot}（类型 {RecipeTypeCombo.Items.Count - 1} 个，文件 {_fileItems.Count} 个）");
            StatusText.Text = L.T("status.files", _allJsonFiles.Count);
        }

        private void OnOpenOutputDirectory(object sender, RoutedEventArgs e) => OpenOutputDirectory();

        /// <summary>打开输出目录；不存在时先创建再打开。</summary>
        private void OpenOutputDirectory()
        {
            try
            {
                if (!Directory.Exists(MigratedRoot)) Directory.CreateDirectory(MigratedRoot);
                Process.Start(new ProcessStartInfo(MigratedRoot) { UseShellExecute = true });
                StatusText.Text = L.T("status.outputOpened", MigratedRoot);
                Log($"已打开输出目录: {MigratedRoot}");
            }
            catch (Exception ex)
            {
                Log($"打开输出目录失败: {ex.Message}");
                MessageDialog.Error(this, ex.Message, L.T("msg.title.error"));
            }
        }

        private void UpdateOutputPath()
        {
            if (OutputPathTextBox != null) OutputPathTextBox.Text = MigratedRoot;
        }

        // ================= 输出目录选择 =================
        /// <summary>只允许选择文件夹；选中后生效并持久化，供“打开输出”和迁移导出共同使用。</summary>
        private void OnBrowseOutputFolder(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = L.T("dlg.outputFolder"),
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(MigratedRoot) ? MigratedRoot : _sourceRoot
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var selected = dlg.SelectedPath ?? "";
            if (!ValidateOutputDirectory(selected, true)) return;

            MigratedRoot = Path.GetFullPath(selected);
            OutputPathTextBox.Text = MigratedRoot;
            _settings.OutputRoot = MigratedRoot;
            SettingsService.Save(_settings);

            Log($"输出目录已更新: {MigratedRoot}");
            StatusText.Text = L.T("status.outputChanged", MigratedRoot);
        }

        /// <summary>
        /// 校验输出目录：空路径 / 指向文件 / 无写权限都会提示并返回 false；
        /// 目录不存在时询问是否创建（策略：默认创建后再使用）。
        /// </summary>
        private bool ValidateOutputDirectory(string path, bool allowCreate)
        {
            path = (path ?? "").Trim();
            if (path.Length == 0)
            {
                MessageDialog.Warning(this, L.T("msg.outputEmpty"), L.T("msg.title.warning"));
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                MessageDialog.Warning(this, L.T("msg.outputInvalid", ex.Message), L.T("msg.title.warning"));
                return false;
            }

            if (File.Exists(full))
            {
                MessageDialog.Warning(this, L.T("msg.outputIsFile", full), L.T("msg.title.warning"));
                return false;
            }

            if (!Directory.Exists(full))
            {
                if (!allowCreate)
                {
                    MessageDialog.Warning(this, L.T("msg.outputMissing", full), L.T("msg.title.warning"));
                    return false;
                }

                if (!MessageDialog.Confirm(this, L.T("msg.outputCreateConfirm", full), L.T("msg.title.confirmCreate")))
                    return false;

                try
                {
                    Directory.CreateDirectory(full);
                    Log($"已创建输出目录: {full}");
                }
                catch (Exception ex)
                {
                    MessageDialog.Error(this, L.T("msg.outputCreateFailed", full, ex.Message), L.T("msg.title.error"));
                    return false;
                }
            }

            // 写权限探测：写入并删除一个临时文件
            try
            {
                var probe = Path.Combine(full, ".write_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                MessageDialog.Warning(this, L.T("msg.outputNoAccess", full, ex.Message), L.T("msg.title.warning"));
                return false;
            }
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _closing = true;
            StopWatcher();

            if (_pendingEdits.Count > 0)
            {
                var r = MessageDialog.Ask(this, L.T("msg.closingUnsaved", _pendingEdits.Count),
                    L.T("msg.title.unsaved"));
                if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                if (r == MessageBoxResult.Yes && !SavePendingEdits(false)) e.Cancel = true;
            }
        }

        // ================= 初始化 =================
        /// <summary>ComboBox 条目数据类：Display 显示文本，Type 为枚举值（null = 全部）</summary>
        private class TypeItem
        {
            public string Display { get; set; }
            public RecipeType? Type { get; set; }
            public string FolderName { get; set; }   // Recipe 类型目录名，null = (全部)
        }

        /// <summary>Recipe 文件下拉条目：Display 仅显示文件名；RelativePath 用于提示与“相对路径”列。</summary>
        private sealed class FileItem
        {
            public string FullPath { get; set; }
            public string Display { get; set; }
            public string RelativePath { get; set; }
            public string Name { get; set; }
            public string SizeText { get; set; }
            public string ModifiedText { get; set; }
        }

        private void InitTypesAndScan()
        {
            try
            {
                _userChangingType = false;

                // Recipe 类型列表只来自 UnitRecipe 下“确实存在 JSON 文件”的子目录
                // （第一项为 (全部)）；没有 JSON 的目录不会出现在文件列表里，因此也不作为可选类型/写操作范围
                var items = new List<TypeItem> { new TypeItem { Display = L.T("combo.allTypes"), FolderName = null } };
                if (Directory.Exists(_sourceRoot))
                {
                    foreach (var dir in Directory.GetDirectories(_sourceRoot).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        var name = Path.GetFileName(dir);
                        if (string.Equals(name, BackupFolderName, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!ContainsJsonFiles(dir)) continue;
                        items.Add(new TypeItem
                        {
                            Display = name,
                            FolderName = name,
                            Type = RecipeMigrationService.ParseRecipeType(name)
                        });
                    }
                }

                StartWatcher();

                RecipeTypeCombo.ItemsSource = null;
                RecipeTypeCombo.ItemsSource = items;
                RecipeTypeCombo.SelectedIndex = 0;   // 默认 (全部)；触发 OnRecipeTypeChanged → 加载第一个文件

                Log($"扫描目录: {_sourceRoot}  Recipe 类型: {items.Count - 1} 个  json 文件: {_allJsonFiles.Count} 个");
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
                _allJsonFiles.AddRange(Directory.EnumerateFiles(_sourceRoot, "*.json", SearchOption.AllDirectories)
                    .Where(f => !IsUnderBackupFolder(f)));
            StatusText.Text = L.T("status.files", _allJsonFiles.Count);
        }

        private static bool IsUnderBackupFolder(string file)
        {
            var sep = Path.DirectorySeparatorChar;
            return file.IndexOf(sep + BackupFolderName + sep, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>指定目录（含子目录，排除 _backup）内是否存在 JSON；没有文件的目录不进入类型下拉/写操作范围。</summary>
        private static bool ContainsJsonFiles(string dir)
        {
            try
            {
                return Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                    .Any(f => !IsUnderBackupFolder(f));
            }
            catch { return false; }
        }

        /// <summary>当前 Recipe 类型目录下的文件；未选类型时返回全部类型目录内的文件（不含根目录散落的 JSON）。</summary>
        private List<string> GetScopedFiles()
        {
            if (string.IsNullOrEmpty(_typeFolder))
            {
                // 仅保留位于某个 Recipe 类型目录内的条目，根目录散落的 JSON 不属于任何文件列表类型
                return _allJsonFiles
                    .Where(f => GetRelativePath(f).IndexOf(Path.DirectorySeparatorChar) > 0)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var dir = Path.Combine(_sourceRoot, _typeFolder);
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories)
                .Where(f => !IsUnderBackupFolder(f))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 新增/修改/删除的作用目录：优先当前文件所在 Recipe 类型目录，其次当前下拉选中的类型。
        /// 这样跨类型不再联动，所有写回只影响一个类型目录。
        /// </summary>
        private string GetOperationFolder()
        {
            return GetFolderOfCurrentFile() ?? _typeFolder;
        }

        /// <summary>取指定 Recipe 类型目录下的 JSON；folder 为 null 时仅取根目录。scopeName 用于界面/日志展示。</summary>
        private List<string> GetFilesInFolder(string folder, out string scopeName)
        {
            scopeName = string.IsNullOrEmpty(folder) ? L.T("scope.root") : folder;
            if (string.IsNullOrEmpty(_sourceRoot) || !Directory.Exists(_sourceRoot)) return new List<string>();

            var dir = string.IsNullOrEmpty(folder) ? _sourceRoot : Path.Combine(_sourceRoot, folder);
            if (!Directory.Exists(dir)) return new List<string>();

            var option = string.IsNullOrEmpty(folder) ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            return Directory.EnumerateFiles(dir, "*.json", option)
                .Where(f => !IsUnderBackupFolder(f))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>当前文件必须来自『文件列表』中的条目；列表外的来源不允许增删改。</summary>
        private bool EnsureCurrentFileInList()
        {
            if (!string.IsNullOrEmpty(_currentFile)
                && _fileItems.Any(x => string.Equals(x.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase)))
                return true;

            MessageDialog.Warning(this, L.T("msg.scopeTypeRequired"), L.T("msg.title.warning"));
            return false;
        }

        /// <summary>写操作范围：必须是文件列表中的具体 Recipe 类型目录，且其中确有 JSON 文件。</summary>
        private bool TryGetScopeFiles(out List<string> files, out string scopeName)
        {
            files = new List<string>();
            scopeName = "";
            var folder = GetOperationFolder();
            if (string.IsNullOrEmpty(folder))
            {
                MessageDialog.Warning(this, L.T("msg.scopeTypeRequired"), L.T("msg.title.warning"));
                return false;
            }

            files = GetFilesInFolder(folder, out scopeName);
            if (files.Count == 0)
            {
                MessageDialog.Warning(this, L.T("msg.scopeEmpty", folder), L.T("msg.title.warning"));
                return false;
            }
            return true;
        }

        /// <summary>重新扫描目录并刷新文件下拉列表与『文件列表』Tab（保持当前选中项）。</summary>
        private void RefreshFileList()
        {
            ScanDirectory();

            _fileItems.Clear();
            foreach (var f in GetScopedFiles())
            {
                var info = (FileInfo)null;
                try { info = new FileInfo(f); } catch { }
                _fileItems.Add(new FileItem
                {
                    FullPath = f,
                    Name = Path.GetFileName(f),
                    RelativePath = GetRelativePath(f),
                    Display = Path.GetFileName(f),
                    SizeText = info != null && info.Exists ? FormatSize(info.Length) : "",
                    ModifiedText = info != null && info.Exists ? info.LastWriteTime.ToString("yyyy-MM-dd HH:mm") : ""
                });
            }

            _suppressComboEvents = true;
            try
            {
                RecipeFileCombo.ItemsSource = null;
                RecipeFileCombo.ItemsSource = _fileItems;

                var sel = _fileItems.FirstOrDefault(x =>
                    string.Equals(x.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase));
                if (sel != null) RecipeFileCombo.SelectedItem = sel;
                else RecipeFileCombo.SelectedIndex = -1;

                FileListGrid.ItemsSource = null;
                FileListGrid.ItemsSource = _fileItems;
                FileListGrid.SelectedItem = sel;
            }
            finally
            {
                _suppressComboEvents = false;
            }

            FileHintText.Text = Directory.Exists(_sourceRoot)
                ? L.T("status.fileHint", _fileItems.Count)
                : L.T("status.dirMissing");
            FileListHint.Text = L.T("files.count", _fileItems.Count);
            FileListEmpty.Visibility = _fileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }

        private void OnFileListDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = FileListGrid.SelectedItem as FileItem;
            if (item == null) return;
            if (string.Equals(item.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase)) return;
            if (!ConfirmLeaveCurrentFile()) return;

            LoadAndShow(item.FullPath);
            PreviewTabs.SelectedIndex = 0;
        }

        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return "";
            try
            {
                var root = Path.GetFullPath(_sourceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(fullPath);
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(root.Length);
            }
            catch { }
            return fullPath;
        }

        private void StartWatcher()
        {
            StopWatcher();
            if (!Directory.Exists(_sourceRoot)) return;

            try
            {
                _watcher = new FileSystemWatcher(_sourceRoot, "*.json")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };
                _watcher.Created += OnWatcherChanged;
                _watcher.Deleted += OnWatcherChanged;
                _watcher.Renamed += OnWatcherChanged;
            }
            catch (Exception ex)
            {
                Log($"文件目录监视不可用（可点『⟳』手动刷新）: {ex.Message}");
            }
        }

        private void StopWatcher()
        {
            try
            {
                if (_watcher != null) { _watcher.EnableRaisingEvents = false; _watcher.Dispose(); _watcher = null; }
                if (_watcherTimer != null) { _watcherTimer.Stop(); _watcherTimer = null; }
            }
            catch { }
        }

        private void OnWatcherChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (_watcherTimer == null)
                {
                    _watcherTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                    _watcherTimer.Tick += (s, a) =>
                    {
                        _watcherTimer.Stop();
                        RefreshFileList();
                        Log($"目录内容变化，文件列表已刷新（{_fileItems.Count} 个）");
                    };
                }
                _watcherTimer.Stop();
                _watcherTimer.Start();
            }));
        }

        private void OnRefreshFileList(object sender, RoutedEventArgs e)
        {
            RefreshFileList();
            Log($"手动刷新文件列表: {_fileItems.Count} 个文件");
            StatusText.Text = L.T("status.refreshed", _fileItems.Count);
        }

        private void Log(string msg)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogList.Items.Add($"[{ts}] {msg}");
            LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }

        // ================= RecipeType 下拉联动 =================
        private void OnRecipeTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_userChangingType) return;
            if (!(RecipeTypeCombo.SelectedItem is TypeItem sel)) return;

            // 切换类型会重建内存数据，先处理未保存的修改
            if (_pendingEdits.Count > 0)
            {
                if (!ConfirmLeaveCurrentFile())
                {
                    SyncComboToRecipeType(_currentRecipe?.RecipeTypeName);
                    return;
                }
            }

            try
            {
                // 切换类型时无条件重新枚举该类型目录（也用于首次填充文件列表）
                _typeFolder = sel.FolderName;
                RefreshFileList();

                var scopeText = string.IsNullOrEmpty(sel.FolderName) ? L.T("combo.allTypes") : sel.FolderName;
                if (_fileItems.Count == 0)
                {
                    // 不清空当前内容，避免切到空目录时丢失已加载文件
                    Log($"已切换到 Recipe 类型 {scopeText}：该目录下没有 JSON 文件，保持当前内容不变");
                    StatusText.Text = L.T("status.typeNoFiles", scopeText);
                    return;
                }

                // 默认选中策略：当前文件仍在该类型目录下则保留，否则加载该目录第一个文件
                var keep = _fileItems.FirstOrDefault(x =>
                    string.Equals(x.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase));
                if (keep != null)
                {
                    Log($"已切换到 Recipe 类型 {scopeText}：{_fileItems.Count} 个文件，保留当前文件");
                    return;
                }

                Log($"已切换到 Recipe 类型 {scopeText}：{_fileItems.Count} 个文件，默认加载第一个（{_fileItems[0].Display}）");
                LoadAndShow(_fileItems[0].FullPath);
            }
            catch (Exception ex)
            {
                Log($"RecipeType 切换失败: {ex.Message}");
            }
        }

        /// <summary>当前文件所在的一级子目录名（相对 _sourceRoot），无则返回 null。</summary>
        private string GetFolderOfCurrentFile()
        {
            if (string.IsNullOrEmpty(_currentFile)) return null;
            var rel = GetRelativePath(_currentFile);
            var parts = rel.Split(new[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[0] : null;
        }

        // ================= 文件操作 =================
        private void OnRecipeFileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressComboEvents) return;

            var item = RecipeFileCombo.SelectedItem as FileItem;
            if (item == null) return;
            if (string.Equals(item.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase)) return;

            if (!ConfirmLeaveCurrentFile())
            {
                // 恢复下拉框到当前文件
                _suppressComboEvents = true;
                try
                {
                    var cur = _fileItems.FirstOrDefault(x =>
                        string.Equals(x.FullPath, _currentFile, StringComparison.OrdinalIgnoreCase));
                    if (cur != null) RecipeFileCombo.SelectedItem = cur;
                }
                finally
                {
                    _suppressComboEvents = false;
                }
                return;
            }

            LoadAndShow(item.FullPath);
        }

        /// <summary>切换文件/重建数据前，处理未保存修改；返回 false 表示用户取消操作。</summary>
        private bool ConfirmLeaveCurrentFile()
        {
            if (_pendingEdits.Count == 0) return true;

            var r = MessageDialog.Ask(this, L.T("msg.pendingUnsaved", _pendingEdits.Count),
                L.T("msg.title.unsaved"));

            if (r == MessageBoxResult.Cancel) return false;

            if (r == MessageBoxResult.Yes)
                return SavePendingEdits(false);

            _pendingEdits.Clear();
            UpdateDirtyUi();
            return true;
        }

        private void SyncComboToRecipeType(string typeName)
        {
            _userChangingType = true;
            try
            {
                var folder = GetFolderOfCurrentFile();
                if (folder == null && !string.IsNullOrEmpty(typeName))
                {
                    var parsed = RecipeMigrationService.ParseRecipeType(typeName);
                    if (parsed != null && Directory.Exists(_sourceRoot))
                    {
                        foreach (var dir in Directory.GetDirectories(_sourceRoot))
                        {
                            var name = Path.GetFileName(dir);
                            if (RecipeMigrationService.ParseRecipeType(name) == parsed) { folder = name; break; }
                        }
                    }
                }

                var match = RecipeTypeCombo.Items.OfType<TypeItem>().FirstOrDefault(it =>
                    string.Equals(it.FolderName ?? "", folder ?? "", StringComparison.OrdinalIgnoreCase));
                if (match == null) return;

                RecipeTypeCombo.SelectedItem = match;
                if (!string.Equals(_typeFolder ?? "", match.FolderName ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    _typeFolder = match.FolderName;
                    RefreshFileList();
                }
            }
            finally
            {
                _userChangingType = false;
            }
        }

        // ================= 迁移并导出（含 Id 重排预览） =================
        private void OnMigrateAndExport(object sender, RoutedEventArgs e) => RunResequenceAndExport();

        /// <summary>单个文件的重排预览：数组顺序 + 每条的旧 Id → 新 Id。</summary>
        private sealed class ResequenceFilePreview
        {
            public string RelativePath;
            public int Count;
            public bool Changed;
            public List<string> Lines = new List<string>();
        }

        /// <summary>
        /// 重排预览 → 迁移到输出目录 → 输出副本按数组顺序重排 Id → 打开输出目录。
        /// 预览可取消（取消时不写任何文件）；源文件是否同步重排由预览中的选项决定（默认关闭）。
        /// </summary>
        private void RunResequenceAndExport()
        {
            if (_autoExporting) return;

            if (!Directory.Exists(_sourceRoot))
            {
                MessageDialog.Warning(this, L.T("msg.dirMissing", _sourceRoot), L.T("msg.title.warning"));
                return;
            }

            // 输出目录非法（指向文件 / 无权限）时在预览前就拦住
            if (!ValidateOutputDirectory(MigratedRoot, true)) return;

            // 先处理未保存修改；临时屏蔽嵌套导出，避免“保存 → 导出 → 再次导出”弹两次预览
            var prevAutoExporting = _autoExporting;
            _autoExporting = true;
            bool canContinue;
            try
            {
                canContinue = ConfirmLeaveCurrentFile();
            }
            finally
            {
                _autoExporting = prevAutoExporting;
            }
            if (!canContinue) return;

            ScanDirectory();
            var preview = BuildResequencePreview();
            if (preview.Count == 0)
            {
                MessageDialog.Info(this, L.T("msg.reseqNone"), L.T("msg.title.info"));
                return;
            }

            var conflicts = CountIdNameConflicts();
            bool applyToSource;
            if (!ShowResequencePreview(preview, conflicts, out applyToSource))
            {
                Log("Id 重排预览已取消：未改动任何文件、未执行导出");
                StatusText.Text = L.T("status.ready");
                return;
            }

            ExecuteResequenceAndExport(applyToSource);
        }

        /// <summary>读取每个 JSON 的主数据数组，生成按数组顺序的 Id 映射（1..N）。</summary>
        private List<ResequenceFilePreview> BuildResequencePreview()
        {
            var list = new List<ResequenceFilePreview>();
            foreach (var file in _allJsonFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList())
            {
                try
                {
                    var doc = JsonDocumentService.Load(file);
                    string prop;
                    var arr = JsonDocumentService.GetPrimaryArray(doc.Root, out prop);
                    if (arr == null || arr.Count == 0) continue;

                    var item = new ResequenceFilePreview
                    {
                        RelativePath = GetRelativePath(file),
                        Count = arr.Count
                    };

                    for (var i = 0; i < arr.Count; i++)
                    {
                        if (!(arr[i] is JObject obj) || obj["Id"] == null) continue;
                        long cur;
                        var ok = JsonDocumentService.TryGetId(obj, out cur);
                        if (!ok || cur != i + 1) item.Changed = true;
                        item.Lines.Add($"  #{i + 1}: {(ok ? cur.ToString(CultureInfo.InvariantCulture) : "?")} → {i + 1}");
                    }

                    list.Add(item);
                }
                catch (Exception ex)
                {
                    Log($"  预览读取失败: {GetRelativePath(file)} → {ex.Message}");
                }
            }
            return list;
        }

        /// <summary>语义检查：同一 Id 在不同文件中对应不同 Name 的个数（重排后这些 Id 会变化）。</summary>
        private int CountIdNameConflicts()
        {
            var map = new Dictionary<long, HashSet<string>>();
            foreach (var file in _allJsonFiles.ToList())
            {
                try
                {
                    var doc = JsonDocumentService.Load(file);
                    string prop;
                    var arr = JsonDocumentService.GetPrimaryArray(doc.Root, out prop);
                    if (arr == null) continue;

                    foreach (var o in arr.OfType<JObject>())
                    {
                        long id;
                        if (!JsonDocumentService.TryGetId(o, out id)) continue;
                        var name = o["Name"]?.ToString() ?? "";
                        if (!map.TryGetValue(id, out var set))
                        {
                            set = new HashSet<string>();
                            map[id] = set;
                        }
                        if (name.Length > 0) set.Add(name);
                    }
                }
                catch { /* 预览阶段忽略单文件读取失败 */ }
            }
            return map.Count(kv => kv.Value.Count > 1);
        }

        /// <summary>重排预览窗口（代码构建，避免新增 XAML 页面）。返回 false 表示用户取消。</summary>
        private bool ShowResequencePreview(List<ResequenceFilePreview> preview, int conflicts, out bool applyToSource)
        {
            applyToSource = false;

            var win = new Window
            {
                Title = L.T("reseq.title"),
                Owner = this,
                Width = 760,
                Height = 600,
                MinWidth = 620,
                MinHeight = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = new FontFamily("Microsoft YaHei"),
                Background = BrushOf("BgBrush"),
                Foreground = BrushOf("WindowText")
            };

            var root = new DockPanel { Margin = new Thickness(16) };

            var header = new TextBlock
            {
                Text = L.T("reseq.body", preview.Count, preview.Count(p => p.Changed)),
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var hint = new TextBlock
            {
                Text = L.T("reseq.hint"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushOf("TextSub"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(hint, Dock.Top);
            root.Children.Add(hint);

            if (conflicts > 0)
            {
                var warn = new TextBlock
                {
                    Text = L.T("reseq.conflict", conflicts),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = BrushOf("WarningText"),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                DockPanel.SetDock(warn, Dock.Top);
                root.Children.Add(warn);
            }

            var check = new CheckBox
            {
                Content = L.T("reseq.applySource"),
                IsChecked = false,
                Margin = new Thickness(0, 10, 0, 10)
            };
            DockPanel.SetDock(check, Dock.Bottom);
            root.Children.Add(check);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            DockPanel.SetDock(buttons, Dock.Bottom);

            // 使用项目内主题按钮样式，避免默认 Button 在深色主题下出现“浅底浅字”不可读
            var cancel = new Button
            {
                Content = L.T("reseq.cancel"),
                Width = 110,
                Height = 34,
                Margin = new Thickness(0, 0, 10, 0),
                Style = (Style)Application.Current.TryFindResource("OutlineBtn"),
                Padding = new Thickness(8, 0, 8, 0)
            };
            var ok = new Button
            {
                Content = L.T("reseq.ok"),
                Width = 160,
                Height = 34,
                Style = (Style)Application.Current.TryFindResource("GradientBtn"),
                Padding = new Thickness(10, 0, 10, 0)
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            var body = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                Text = BuildPreviewText(preview)
            };
            root.Children.Add(body);

            win.Content = root;

            var confirmed = false;
            var applySourceChecked = false;
            ok.Click += (s, e) =>
            {
                confirmed = true;
                applySourceChecked = check.IsChecked == true;
                win.Close();
            };
            cancel.Click += (s, e) => win.Close();

            win.ShowDialog();

            applyToSource = applySourceChecked;
            return confirmed;
        }

        private string BuildPreviewText(List<ResequenceFilePreview> preview)
        {
            var sb = new StringBuilder();
            foreach (var p in preview)
            {
                sb.AppendLine($"{p.RelativePath}  ({p.Count} 条, {(p.Changed ? "需重排" : "无需变化")})");
                if (p.Changed) foreach (var line in p.Lines) sb.AppendLine(line);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>执行：可选重排源文件 → 迁移到输出目录 → 输出副本重排 → 刷新界面 → 打开输出目录。</summary>
        private void ExecuteResequenceAndExport(bool applyToSource)
        {
            _autoExporting = true;
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var sourceBackupRoot = Path.Combine(_sourceRoot, BackupFolderName);

                var sourceReseq = 0;
                if (applyToSource)
                {
                    Log("══ 重排源文件（备份 + 原子写）");
                    foreach (var file in _allJsonFiles.ToList())
                    {
                        var res = JsonDocumentService.ResequenceIds(file, f => BuildBackupPath(sourceBackupRoot, f, timestamp));
                        if (!res.Success && !res.Skipped)
                        {
                            Log($"  源文件重排失败: {GetRelativePath(file)} → {res.Error}");
                        }
                        else if (res.Success && !res.Skipped)
                        {
                            sourceReseq++;
                            Log($"  源文件重排: {GetRelativePath(file)}  {res.TotalHits} 条");
                        }
                    }
                }

                Log($"══ 开始迁移: {_sourceRoot} → {MigratedRoot}");
                var results = RecipeMigrationService
                    .MigrateDirectory(_sourceRoot, MigratedRoot, msg => Dispatcher.Invoke(() => Log(msg)))
                    .ToList();

                var ok = results.Count(r => r.Success);
                var fail = results.Count - ok;

                // 默认路径：只重排导出副本，源文件保持原 Id
                var outputReseq = 0;
                foreach (var file in Directory.EnumerateFiles(MigratedRoot, "*.json", SearchOption.AllDirectories)
                             .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList())
                {
                    var res = JsonDocumentService.ResequenceIds(file, null);
                    if (!res.Success && !res.Skipped) Log($"  导出副本重排失败: {RelPath(MigratedRoot, file)} → {res.Error}");
                    else if (res.Success && !res.Skipped) outputReseq++;
                }

                Log($"══ 完成: 迁移成功 {ok}/{results.Count}，导出副本重排 {outputReseq}，源文件重排 {sourceReseq}");
                StatusText.Text = L.T("status.resequenced", outputReseq + sourceReseq);

                RefreshFileList();
                if (!string.IsNullOrEmpty(_currentFile) && File.Exists(_currentFile)) ReloadCurrentFileFromDisk();

                if (fail > 0)
                {
                    var detail = string.Join("\n", results.Where(r => !r.Success).Take(6)
                        .Select(r => $"  {GetRelativePath(r.Source)}：{r.Message}"));
                    MessageDialog.Warning(this, L.T("msg.reseqFailed", ok, results.Count, fail, detail),
                        L.T("msg.title.export"));
                }
                else
                {
                    MessageDialog.Info(this, L.T("msg.reseqDone", ok, outputReseq, sourceReseq, MigratedRoot),
                        L.T("msg.title.export"));
                }

                OpenOutputDirectory();
            }
            catch (Exception ex)
            {
                Log($"══ 导出异常: {ex.Message}");
                MessageDialog.Error(this, ex.Message, L.T("msg.title.batchFailed"));
            }
            finally
            {
                _autoExporting = false;
            }
        }

        private static string RelPath(string root, string fullPath)
        {
            try
            {
                var b = new Uri(Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar);
                var f = new Uri(Path.GetFullPath(fullPath));
                return Uri.UnescapeDataString(b.MakeRelativeUri(f).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch { return fullPath; }
        }

        private static Brush BrushOf(string key)
        {
            var res = Application.Current.TryFindResource(key) as Brush;
            return res ?? Brushes.Transparent;
        }

        private void OnClearLog(object sender, RoutedEventArgs e) => LogList.Items.Clear();

        // ================= 加载与视图刷新 =================
        private void LoadAndShow(string path)
        {
            try
            {
                var doc = JsonDocumentService.Load(path);
                if (!(doc.Root is JObject root))
                    throw new InvalidDataException("根节点不是 JSON 对象");

                _currentFile = path;
                _currentFormat = doc.Format;
                _currentRawText = doc.RawText;
                _currentJson = root;

                _currentRecipe = RecipeMigrationService.Load(path);
                if (_currentRecipe != null) RecipeMigrationService.Migrate(_currentRecipe);

                _pendingEdits.Clear();
                UpdateDirtyUi();

                SyncComboToRecipeType(_currentRecipe?.RecipeTypeName);

                RefreshDataTab();
                RefreshRawTab();
                SyncFileComboSelection(path);

                var count = 0;
                string prop;
                var arr = JsonDocumentService.GetPrimaryArray(_currentJson, out prop);
                if (arr != null) count = arr.Count;
                StatusText.Text = L.T("status.loaded", Path.GetFileName(path), count);
                Log($"加载: {path}  [RecipeType={_currentRecipe?.RecipeTypeName ?? "(null)"}]  {_currentFormat.Describe()}");
            }
            catch (Exception ex)
            {
                Log($"加载失败: {path}  {ex.Message}");
                _currentJson = null;
                _currentRawText = "";
                RefreshDataTab();
                RefreshRawTab();
                MessageDialog.Error(this, L.T("msg.loadFailed", ex.Message), L.T("msg.title.loadFailed"));
            }
        }

        /// <summary>把 RecipeModel（迁移后的模板数据）同步到 JSON 视图，保持原有"迁移后预览"的行为。</summary>
        private void ApplyRecipeModelToJsonView()
        {
            try
            {
                var json = RecipeMigrationService.ToJson(_currentRecipe);
                _currentJson = string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
                _pendingEdits.Clear();
                UpdateDirtyUi();
                RefreshDataTab();
                _currentRawText = _currentRawText ?? "";
                RefreshRawTab();
            }
            catch (Exception ex)
            {
                Log($"同步数据视图失败: {ex.Message}");
            }
        }

        /// <summary>JSON 视图变更后回写 RecipeModel，保证既有"保存到迁移目录"等功能使用同一份数据。</summary>
        private void SyncRecipeModelFromJsonView()
        {
            try
            {
                _currentRecipe = _currentJson == null
                    ? null
                    : JsonConvert.DeserializeObject<RecipeModel>(_currentJson.ToString());
            }
            catch (Exception ex)
            {
                Log($"同步 RecipeModel 失败: {ex.Message}");
            }
        }

        private void SyncFileComboSelection(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            _suppressComboEvents = true;
            try
            {
                var item = _fileItems.FirstOrDefault(x =>
                    string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    RefreshFileList();
                    item = _fileItems.FirstOrDefault(x =>
                        string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
                }
                if (item != null) RecipeFileCombo.SelectedItem = item;
            }
            finally
            {
                _suppressComboEvents = false;
            }
        }

        private JArray GetCurrentArray()
        {
            if (_currentJson == null) return null;
            string prop;
            return JsonDocumentService.GetPrimaryArray(_currentJson, out prop);
        }

        // ================= 数据列表 Tab =================
        private void RefreshDataTab()
        {
            _rowTable = null;
            DataGrid1.ItemsSource = null;

            string prop = _dataArrayProperty;
            var arr = _currentJson == null ? null : JsonDocumentService.GetPrimaryArray(_currentJson, out prop);

            if (arr == null)
            {
                _dataArrayProperty = "Data";
                DataGridEmpty.Text = L.T("data.empty");
                DataGridEmpty.Visibility = Visibility.Visible;
                DataListHint.Text = L.T("data.rows", 0);
                return;
            }

            _dataArrayProperty = prop;
            var table = BuildRowTable(arr);
            if (table == null || table.Rows.Count == 0)
            {
                DataGridEmpty.Text = L.T("data.emptyCollection");
                DataGridEmpty.Visibility = Visibility.Visible;
                DataListHint.Text = L.T("data.rows", 0);
                return;
            }

            DataGridEmpty.Visibility = Visibility.Collapsed;
            _rowTable = table;
            DataGrid1.ItemsSource = table.DefaultView;
            DataListHint.Text = L.T("data.rowsWithCollection", table.Rows.Count,
                string.IsNullOrEmpty(_dataArrayProperty) ? L.T("data.rootArray") : _dataArrayProperty);
        }

        private DataTable BuildRowTable(JArray arr)
        {
            var objects = arr.OfType<JObject>().ToList();
            if (objects.Count == 0) return null;

            var names = new List<string>();
            foreach (var o in objects)
            {
                foreach (var p in o.Properties())
                {
                    var n = p.Name;
                    if (string.IsNullOrEmpty(n) || n == "__idx") continue;
                    if (!names.Contains(n)) names.Add(n);
                }
            }

            // Id 优先，主要字段次之，数组/对象汇总列放最后
            names = names
                .Select((n, i) => new { n, i })
                .OrderBy(x => ColumnRank(x.n))
                .ThenBy(x => x.i)
                .Select(x => x.n)
                .ToList();

            var table = new DataTable();
            table.Columns.Add("__idx", typeof(int));
            foreach (var n in names)
                table.Columns.Add(n, DetectColumnType(objects, n));

            for (var i = 0; i < objects.Count; i++)
            {
                var o = objects[i];
                var row = table.NewRow();
                row["__idx"] = i;
                foreach (var n in names)
                    row[n] = CellValue(o[n], table.Columns[n].DataType);
                table.Rows.Add(row);
            }
            return table;
        }

        private static int ColumnRank(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "id": return 0;
                case "name": return 1;
                case "type": return 2;
                case "unit": return 3;
                case "min": return 4;
                case "max": return 5;
                case "default": return 6;
                case "steps": return 90;
                case "options": return 91;
                default: return 50;
            }
        }

        private static Type DetectColumnType(List<JObject> objects, string name)
        {
            var kinds = new HashSet<JTokenType>();
            foreach (var o in objects)
            {
                var t = o[name];
                if (t == null || t.Type == JTokenType.Null) continue;
                kinds.Add(t.Type);
            }

            if (kinds.Any(k => k == JTokenType.Object || k == JTokenType.Array)) return typeof(string);
            if (kinds.Count == 0) return typeof(string);
            if (kinds.All(k => k == JTokenType.Boolean)) return typeof(bool);
            if (kinds.All(k => k == JTokenType.Integer)) return typeof(long);
            if (kinds.All(k => k == JTokenType.Integer || k == JTokenType.Float)) return typeof(double);
            return typeof(string);
        }

        private static object CellValue(JToken token, Type columnType)
        {
            if (token == null || token.Type == JTokenType.Null) return DBNull.Value;

            if (token is JArray jarr)
                return L.T("data.arrayItem", jarr.Count);
            if (token is JObject)
                return L.T("data.object");

            if (columnType == typeof(bool) && token.Type == JTokenType.Boolean) return token.Value<bool>();
            if (columnType == typeof(long) && token.Type == JTokenType.Integer) return token.Value<long>();
            if (columnType == typeof(double))
                return token.Value<double>();

            return token.ToString();
        }

        private void OnGridAutoGenerating(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "__idx")
            {
                e.Cancel = true;
                return;
            }

            double width;
            switch (e.PropertyName.ToLowerInvariant())
            {
                case "id": width = 64; break;
                case "name": width = 240; break;
                case "type": width = 90; break;
                case "unit": width = 76; break;
                case "min": width = 70; break;
                case "max": width = 70; break;
                case "default": width = 150; break;
                case "steps": width = 76; break;
                case "options": width = 80; break;
                default: width = 120; break;
            }

            e.Column.Width = new DataGridLength(width);
            e.Column.IsReadOnly = true;
            if (e.Column is DataGridTextColumn textColumn)
                textColumn.FontSize = 12.5;
        }

        /// <summary>右键时先选中光标所在行，保证上下文菜单作用于该行。</summary>
        private void OnGridRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is DataGridRow))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row)
            {
                DataGrid1.SelectedItem = row.Item;
                row.IsSelected = true;
            }
        }

        private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is DataGridRow))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow) EditSelectedRow();
        }

        private void OnEditSelectedRow(object sender, RoutedEventArgs e) => EditSelectedRow();

        private void OnDataSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateEditButtonState();

        private void UpdateEditButtonState()
        {
            if (EditBtnBar != null) EditBtnBar.IsEnabled = GetSelectedObject() != null;
        }

        /// <summary>
        /// 工具栏“修改”：作用于数据列表当前选中行；
        /// 弹框确认后暂存为待保存修改，再沿用既有策略按 Id 跨文件整对象替换（备份 + 原子写 + 逐文件命中计数）。
        /// </summary>
        private void OnEditItem(object sender, RoutedEventArgs e)
        {
            if (GetSelectedObject() == null)
            {
                MessageDialog.Info(this, L.T("msg.selectRow"), L.T("msg.title.info"));
                return;
            }

            // 仅允许操作文件列表中的条目，且写回范围限定为当前 Recipe 类型目录
            if (!EnsureCurrentFileInList()) return;
            if (!TryGetScopeFiles(out _, out _)) return;

            if (!EditSelectedRow()) return;

            // 所有 Tab/列表已刷新；此处立即写盘并给出跨文件命中与备份提示
            SavePendingEdits(true);
        }

        private JObject GetSelectedObject()
        {
            var drv = DataGrid1.SelectedItem as DataRowView;
            if (drv == null || _rowTable == null) return null;

            var idx = Convert.ToInt32(drv.Row["__idx"], CultureInfo.InvariantCulture);
            var arr = GetCurrentArray();
            if (arr == null || idx < 0 || idx >= arr.Count) return null;
            return arr[idx] as JObject;
        }

        private bool EditSelectedRow()
        {
            var obj = GetSelectedObject();
            if (obj == null)
            {
                MessageDialog.Info(this, L.T("msg.selectRow"), L.T("msg.title.info"));
                return false;
            }

            var dlg = new EditItemDialog(obj, Path.GetFileName(_currentFile));
            dlg.Owner = this;
            if (dlg.ShowDialog() != true || dlg.Result == null) return false;

            var newObj = dlg.Result;
            long id;
            if (!JsonDocumentService.TryGetId(newObj, out id))
            {
                MessageDialog.Warning(this, L.T("msg.noId"), L.T("msg.title.warning"));
                return false;
            }

            var arr = GetCurrentArray();
            if (arr == null) return false;

            var applied = 0;
            for (var i = 0; i < arr.Count; i++)
            {
                if (!(arr[i] is JObject existing)) continue;
                long existId;
                if (JsonDocumentService.TryGetId(existing, out existId) && existId == id)
                {
                    arr[i] = newObj.DeepClone();
                    applied++;
                }
            }

            // 同一 ID 只保留最后一次修改
            for (var i = _pendingEdits.Count - 1; i >= 0; i--)
            {
                long pid;
                if (JsonDocumentService.TryGetId(_pendingEdits[i], out pid) && pid == id)
                    _pendingEdits.RemoveAt(i);
            }
            _pendingEdits.Add(newObj.DeepClone() as JObject);

            SyncRecipeModelFromJsonView();
            RefreshDataTab();
            SelectRowById(id);
            UpdateDirtyUi();

            StatusText.Text = L.T("status.modified", id);
            Log($"修改: ID={id}  Name={newObj["Name"]}  当前文件命中 {applied} 处（待保存，尚未写盘）");
            return true;
        }

        private void SelectRowById(long id)
        {
            if (_rowTable == null || !_rowTable.Columns.Contains("Id")) return;

            foreach (var item in DataGrid1.Items)
            {
                var drv = item as DataRowView;
                if (drv == null || drv.Row["Id"] == DBNull.Value) continue;
                if (Convert.ToInt64(drv.Row["Id"], CultureInfo.InvariantCulture) == id)
                {
                    DataGrid1.SelectedItem = item;
                    DataGrid1.ScrollIntoView(item);
                    break;
                }
            }
        }

        private void OnCopySelectedRow(object sender, RoutedEventArgs e)
        {
            var obj = GetSelectedObject();
            if (obj == null) return;
            try
            {
                Clipboard.SetText(obj.ToString(Formatting.Indented));
                StatusText.Text = L.T("status.copiedJson");
            }
            catch (Exception ex)
            {
                Log($"复制失败: {ex.Message}");
            }
        }

        private void OnCopySelectedId(object sender, RoutedEventArgs e)
        {
            var obj = GetSelectedObject();
            long id;
            if (obj == null || !JsonDocumentService.TryGetId(obj, out id)) return;
            try
            {
                Clipboard.SetText(id.ToString(CultureInfo.InvariantCulture));
                StatusText.Text = L.T("status.copiedId", id);
            }
            catch (Exception ex)
            {
                Log($"复制失败: {ex.Message}");
            }
        }

        // ================= 新增 / 删除（直接写盘：备份 + 原子写） =================
        private void OnAddItem(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFile) || _currentJson == null)
            {
                MessageDialog.Warning(this, L.T("msg.noFile"), L.T("msg.title.warning"));
                return;
            }

            // 先处理未保存修改，避免后续写盘覆盖内存中的编辑
            if (!ConfirmLeaveCurrentFile()) return;

            // 仅允许操作文件列表中的条目，且写回范围限定为当前 Recipe 类型目录
            if (!EnsureCurrentFileInList()) return;
            if (!TryGetScopeFiles(out var scopeFiles, out var scopeName)) return;

            var arr = GetCurrentArray();
            if (arr == null)
            {
                MessageDialog.Warning(this, L.T("msg.noDataArray"), L.T("msg.title.warning"));
                return;
            }

            // 复用现有动态表单：以当前集合第一个对象为字段模板（Id 由程序自动分配，不显示）
            var proto = arr.OfType<JObject>().FirstOrDefault()?.DeepClone() as JObject ?? new JObject();
            var maxId = 0L;
            foreach (var o in arr.OfType<JObject>())
            {
                long oid;
                if (JsonDocumentService.TryGetId(o, out oid) && oid > maxId) maxId = oid;
            }
            var dlg = new EditItemDialog(proto, Path.GetFileName(_currentFile), true, arr.Count, maxId + 1) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            var newObj = dlg.Result;
            long id;
            if (!JsonDocumentService.TryGetId(newObj, out id))
            {
                MessageDialog.Warning(this, L.T("msg.addNoId"), L.T("msg.title.warning"));
                return;
            }

            var duplicate = arr.OfType<JObject>().Any(o =>
                JsonDocumentService.TryGetId(o, out var existId) && existId == id);
            if (duplicate)
            {
                if (!MessageDialog.Confirm(this, L.T("msg.addDuplicate", id), L.T("msg.title.warning"))) return;
            }

            var index = dlg.InsertIndex < 0 ? arr.Count : dlg.InsertIndex;
            if (index < 0 || index > arr.Count)
            {
                StatusText.Text = L.T("msg.addInvalidIndex", arr.Count);
                MessageDialog.Warning(this, L.T("msg.addInvalidIndex", arr.Count), L.T("msg.title.warning"));
                return;
            }

            arr.Insert(index, newObj);
            var total = arr.Count;

            string backup;
            string error;
            if (!WriteCurrentFile(out backup, out error))
            {
                arr.RemoveAt(index);
                StatusText.Text = L.T("msg.title.saveFailed");
                Log($"新增失败: {error}");
                MessageDialog.Error(this, L.T("msg.loadFailed", error), L.T("msg.title.saveFailed"));
                return;
            }

            ReloadCurrentFileFromDisk();
            RefreshFileList();
            SelectRowByIndex(index);

            StatusText.Text = L.T("status.inserted", index, total);
            Log(L.T("scope.line", scopeName, scopeFiles.Count));
            Log($"新增: 索引 {index}  ID={id}  Name={newObj["Name"]}  → {_currentFile}  备份 {GetRelativePath(backup)}");
            MessageDialog.Info(this, L.T("msg.addDone", index, total, GetRelativePath(backup)),
                L.T("msg.title.added"));

            // 新增后：按 Id 重排并迁移导出到输出目录
            if (!_autoExporting && !_closing) RunResequenceAndExport();
        }

        private void OnDeleteItem(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(_sourceRoot))
            {
                MessageDialog.Warning(this, L.T("msg.dirMissing", _sourceRoot), L.T("msg.title.warning"));
                return;
            }

            if (!ConfirmLeaveCurrentFile()) return;

            // 仅允许操作文件列表中的条目，且删除范围限定为当前 Recipe 类型目录
            if (!EnsureCurrentFileInList()) return;
            if (!TryGetScopeFiles(out var deleteFiles, out var scopeName)) return;

            // 建议 ID：优先当前选中行，其次当前文件里出现的所有 ID
            long? preselected = null;
            var selected = GetSelectedObject();
            long selectedId;
            if (selected != null && JsonDocumentService.TryGetId(selected, out selectedId)) preselected = selectedId;

            var knownIds = new List<long>();
            var arr = GetCurrentArray();
            if (arr != null)
            {
                foreach (var o in arr.OfType<JObject>())
                {
                    long oid;
                    if (JsonDocumentService.TryGetId(o, out oid) && !knownIds.Contains(oid)) knownIds.Add(oid);
                }
            }

            var dlg = new DeleteByIdDialog(knownIds, preselected) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            var id = dlg.Id;

            if (!MessageDialog.Confirm(this, L.T("msg.deleteConfirm", deleteFiles.Count, id, scopeName),
                    L.T("msg.title.confirmWrite"))) return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupRoot = Path.Combine(_sourceRoot, BackupFolderName);

            Log(L.T("scope.line", scopeName, deleteFiles.Count));
            var results = new List<FileEditResult>();
            foreach (var file in deleteFiles)
            {
                try
                {
                    var res = JsonDocumentService.RemoveByIds(file, new[] { id },
                        f => BuildBackupPath(backupRoot, f, timestamp));
                    if (res.Skipped) continue;
                    results.Add(res);
                }
                catch (Exception ex)
                {
                    results.Add(new FileEditResult { FilePath = file, Success = false, Error = ex.Message });
                }
            }

            var okFiles = results.Where(x => x.Success && x.TotalHits > 0).ToList();
            var failed = results.Where(x => !x.Success).ToList();
            var total = okFiles.Sum(x => x.TotalHits);

            foreach (var res in results)
            {
                var rel = GetRelativePath(res.FilePath);
                if (!res.Success) Log($"  删除失败: {rel}  →  {res.Error}");
                else if (res.TotalHits > 0) Log($"  {rel}: 删除 ID={id} × {res.TotalHits}    备份 → {GetRelativePath(res.BackupPath)}");
            }

            if (total == 0 && failed.Count == 0)
            {
                StatusText.Text = L.T("msg.deleteNotFound", id, scopeName);
                MessageDialog.Warning(this, L.T("msg.deleteNotFound", id, scopeName), L.T("msg.title.warning"));
                return;
            }

            StatusText.Text = L.T("status.deleted", id, okFiles.Count, total);
            Log($"删除: ID={id}  命中 {okFiles.Count} 个文件，共 {total} 处；失败 {failed.Count} 个文件");

            if (failed.Count > 0)
            {
                MessageDialog.Error(this, L.T("msg.deletePartial", total, failed.Count,
                    string.Join("\n", failed.Take(6).Select(f => $"  {GetRelativePath(f.FilePath)}：{f.Error}"))),
                    L.T("msg.title.partial"));
            }
            else
            {
                MessageDialog.Info(this, L.T("msg.deleteDone", id, okFiles.Count, total),
                    L.T("msg.title.deleted"));
            }

            if (okFiles.Any(x => string.Equals(x.FilePath, _currentFile, StringComparison.OrdinalIgnoreCase)))
                ReloadCurrentFileFromDisk();
            RefreshFileList();

            // 删除后：按 Id 重排并迁移导出到输出目录
            if (!_autoExporting && !_closing) RunResequenceAndExport();
        }

        /// <summary>把当前内存 JSON 写回当前文件：沿用原编码/缩进，先备份再原子替换。</summary>
        private bool WriteCurrentFile(out string backupPath, out string error)
        {
            backupPath = null;
            error = null;
            try
            {
                if (_currentJson == null || string.IsNullOrEmpty(_currentFile))
                {
                    error = L.T("msg.noFile");
                    return false;
                }
                if (!File.Exists(_currentFile))
                {
                    error = L.T("msg.dirMissing", _currentFile);
                    return false;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupRoot = Path.Combine(_sourceRoot, BackupFolderName);
                var backup = BuildBackupPath(backupRoot, _currentFile, timestamp);
                var text = JsonDocumentService.Serialize(_currentJson, _currentFormat);
                JsonDocumentService.SaveAtomic(_currentFile, text, _currentFormat, backup);
                backupPath = backup;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>从磁盘回读当前文件并刷新两个 Tab（用于新增/删除后的自愈）。</summary>
        private bool ReloadCurrentFileFromDisk()
        {
            if (string.IsNullOrEmpty(_currentFile) || !File.Exists(_currentFile)) return false;
            try
            {
                var doc = JsonDocumentService.Load(_currentFile);
                if (!(doc.Root is JObject root)) return false;

                _currentJson = root;
                _currentRawText = doc.RawText;
                _currentFormat = doc.Format;
                _pendingEdits.Clear();
                UpdateDirtyUi();
                SyncRecipeModelFromJsonView();
                RefreshDataTab();
                RefreshRawTab();
                return true;
            }
            catch (Exception ex)
            {
                Log($"回读当前文件失败: {ex.Message}");
                StatusText.Text = L.T("msg.loadFailed", ex.Message);
                return false;
            }
        }

        private void SelectRowByIndex(int index)
        {
            if (_rowTable == null) return;
            foreach (var item in DataGrid1.Items)
            {
                var drv = item as DataRowView;
                if (drv == null) continue;
                if (Convert.ToInt32(drv.Row["__idx"], CultureInfo.InvariantCulture) == index)
                {
                    DataGrid1.SelectedItem = item;
                    DataGrid1.ScrollIntoView(item);
                    break;
                }
            }
        }

        // ================= 保存（按 ID 跨文件替换） =================
        private void OnSaveEdits(object sender, RoutedEventArgs e) => SavePendingEdits(true);

        private bool SavePendingEdits(bool showConfirm)
        {
            if (_pendingEdits.Count == 0)
            {
                if (showConfirm)
                    MessageDialog.Info(this, L.T("msg.saveNone"), L.T("msg.title.info"));
                return true;
            }

            // 作用域：仅文件列表中当前 Recipe 类型目录，目录外的来源一律不写
            if (!EnsureCurrentFileInList()) return false;
            if (!TryGetScopeFiles(out var scopeFiles, out var scopeName)) return false;

            var ids = new List<string>();
            foreach (var edit in _pendingEdits)
            {
                long id;
                if (JsonDocumentService.TryGetId(edit, out id)) ids.Add(id.ToString(CultureInfo.InvariantCulture));
            }

            if (showConfirm)
            {
                var scopeText = L.T("scope.short", scopeName, scopeFiles.Count);
                if (!MessageDialog.Confirm(this,
                        L.T("msg.saveConfirm", string.Join(", ", ids), scopeText,
                            string.IsNullOrEmpty(_dataArrayProperty) ? L.T("data.rootArray") : _dataArrayProperty, BackupFolderName),
                        L.T("msg.title.confirmWrite")))
                    return false;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupRoot = Path.Combine(_sourceRoot, BackupFolderName);

            Log(L.T("scope.line", scopeName, scopeFiles.Count));
            var results = new List<FileEditResult>();
            foreach (var file in scopeFiles)
            {
                var res = JsonDocumentService.ApplyEdits(file, _dataArrayProperty, _pendingEdits,
                    f => BuildBackupPath(backupRoot, f, timestamp));
                if (res.Skipped) continue;
                results.Add(res);
            }

            var okFiles = results.Where(x => x.Success && x.TotalHits > 0).ToList();
            var failFiles = results.Where(x => !x.Success).ToList();
            var totalHits = okFiles.Sum(x => x.TotalHits);

            var duplicateWarnings = new List<string>();
            foreach (var res in results)
            {
                var rel = GetRelativePath(res.FilePath);
                if (!res.Success)
                {
                    Log($"  写回失败: {rel}  →  {res.Error}");
                    continue;
                }
                if (res.TotalHits == 0) continue;

                var detail = string.Join(", ", res.HitsById.Select(kv => $"ID={kv.Key} × {kv.Value}"));
                Log($"  {rel}: {detail}    备份 → {GetRelativePath(res.BackupPath)}");

                foreach (var kv in res.HitsById.Where(kv => kv.Value > 1))
                    duplicateWarnings.Add($"{rel}：ID={kv.Key} 出现 {kv.Value} 次（已全部替换为同一对象）");
            }

            Log($"保存: 已更新 {okFiles.Count} 个文件，共替换 {totalHits} 条对象；失败 {failFiles.Count} 个");
            StatusText.Text = L.T("status.savedScope", okFiles.Count, totalHits, scopeName);

            if (failFiles.Count > 0)
            {
                var msg = L.T("msg.savePartial", okFiles.Count, failFiles.Count,
                    string.Join("\n", failFiles.Take(6).Select(f => $"  {GetRelativePath(f.FilePath)}：{f.Error}")));
                MessageDialog.Error(this, msg, L.T("msg.title.partial"));
            }
            else
            {
                var head = L.T("msg.saveHead", okFiles.Count, totalHits);
                var lines = okFiles.Take(12).Select(f => $"  {GetRelativePath(f.FilePath)}：{f.TotalHits}").ToList();
                var more = okFiles.Count > 12 ? $"\n  … {okFiles.Count - 12}" : "";
                var dup = duplicateWarnings.Count > 0
                    ? "\n\n" + L.T("msg.saveDupNote") + "\n" + string.Join("\n", duplicateWarnings.Take(6))
                    : "";

                MessageDialog.Info(this, head + "\n" + string.Join("\n", lines) + more + dup,
                    L.T("msg.title.saveDone"));
            }

            if (failFiles.Count > 0) return false;

            _pendingEdits.Clear();
            UpdateDirtyUi();

            // 从磁盘回读当前文件，保证两个 Tab 与磁盘一致
            if (!string.IsNullOrEmpty(_currentFile) && File.Exists(_currentFile))
            {
                try
                {
                    var doc = JsonDocumentService.Load(_currentFile);
                    if (doc.Root is JObject root)
                    {
                        _currentJson = root;
                        _currentRawText = doc.RawText;
                        _currentFormat = doc.Format;
                    }
                }
                catch (Exception ex)
                {
                    Log($"回读当前文件失败: {ex.Message}");
                }
            }

            RefreshDataTab();
            RefreshRawTab();
            SyncRecipeModelFromJsonView();

            // 修改（保存）完成后：按 Id 重排并迁移导出到输出目录
            if (!_autoExporting && !_closing) RunResequenceAndExport();
            return true;
        }

        private string BuildBackupPath(string backupRoot, string filePath, string timestamp)
        {
            var rel = GetRelativePath(filePath);
            try
            {
                if (Path.IsPathRooted(rel))
                {
                    // 文件不在当前扫描根下：备份到 _backup 根
                    return Path.Combine(backupRoot, Path.GetFileName(rel) + "." + timestamp + ".bak");
                }
                var dir = Path.Combine(backupRoot, Path.GetDirectoryName(rel) ?? "");
                return Path.Combine(dir, Path.GetFileName(rel) + "." + timestamp + ".bak");
            }
            catch
            {
                return Path.Combine(backupRoot, Path.GetFileName(filePath) + "." + timestamp + ".bak");
            }
        }

        // ================= 放弃修改 =================
        private void OnDiscardEdits(object sender, RoutedEventArgs e)
        {
            if (_pendingEdits.Count == 0)
            {
                MessageDialog.Info(this, L.T("msg.discardNone"), L.T("msg.title.info"));
                return;
            }

            if (!MessageDialog.Confirm(this, L.T("msg.discardConfirm"), L.T("msg.title.discard"))) return;

            _pendingEdits.Clear();
            UpdateDirtyUi();
            LoadAndShow(_currentFile);
        }

        private void UpdateDirtyUi()
        {
            var dirty = _pendingEdits.Count > 0;
            SaveBtn.IsEnabled = dirty;
            DirtyHint.Text = dirty ? L.T("data.dirty", _pendingEdits.Count) : "";
        }

        // ================= 原文 Tab =================
        private void RefreshRawTab()
        {
            var text = _currentRawText ?? "";
            RawText.Text = text;
            RawLineNo.Text = BuildLineNumberText(text);

            RawInfoText.Text = string.IsNullOrEmpty(_currentFile)
                ? L.T("raw.noFile")
                : L.T("raw.info", Path.GetFileName(_currentFile), _currentFormat == null ? "" : _currentFormat.Describe(), text.Length);

            RawScroll.ScrollToHome();
        }

        private static string BuildLineNumberText(string text)
        {
            var lines = 1;
            foreach (var ch in text ?? "")
                if (ch == '\n') lines++;

            var sb = new StringBuilder();
            for (var i = 1; i <= lines; i++)
            {
                if (i > 1) sb.Append("\r\n");
                sb.Append(i);
            }
            return sb.ToString();
        }

        // ================= EC Configuration 页签 =================
        private void InitEcTab()
        {
            if (EcRootTextBox == null) return;
            EcRootTextBox.Text = _chamberRoot;
            ReloadEcCategories(null);
            Log($"EC 目录: {_chamberRoot}");
        }

        /// <summary>重新扫描 ChamberConfig：左侧按 Dry / Pen / Roller / Polish 分组。</summary>
        private void ReloadEcCategories(string preferredFileName)
        {
            var first = (EcCategory)null;
            _ecLoading = true;
            try
            {
                _ecCategories.Clear();
                _ecCategories.AddRange(EcConfigService.Scan(_chamberRoot));

                EcTypeList.ItemsSource = null;
                EcTypeList.ItemsSource = _ecCategories;

                first = _ecCategories.FirstOrDefault(c => c.Files.Count > 0);
                EcTypeList.SelectedItem = first;
            }
            finally
            {
                _ecLoading = false;
            }

            PopulateEcFiles(first, preferredFileName);
            UpdateEcUiState();
        }

        private void PopulateEcFiles(EcCategory category, string preferredFileName)
        {
            if (category == null || category.Files.Count == 0)
            {
                _ecLoading = true;
                try
                {
                    EcFileList.ItemsSource = null;
                    EcSheetList.ItemsSource = null;
                    EcDataGrid.ItemsSource = null;
                }
                finally { _ecLoading = false; }

                _ecFileData = null;
                _ecViewEntries.Clear();
                _ecSheetName = null;
                EcGridEmpty.Visibility = Visibility.Visible;
                return;
            }

            EcFileItem selected;
            _ecLoading = true;
            try
            {
                EcFileList.ItemsSource = null;
                EcFileList.ItemsSource = category.Files;
                selected = category.Files.FirstOrDefault(f =>
                    string.Equals(f.FileName, preferredFileName, StringComparison.OrdinalIgnoreCase))
                    ?? category.Files[0];
                EcFileList.SelectedItem = selected;
            }
            finally { _ecLoading = false; }

            if (selected != null) LoadEcFile(selected.FullPath, _ecSheetName);
            UpdateEcUiState();
        }

        private void OnEcTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ecLoading) return;
            PopulateEcFiles(EcTypeList.SelectedItem as EcCategory, null);
        }

        private void OnEcFileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ecLoading) return;
            if (EcFileList.SelectedItem is EcFileItem item)
                LoadEcFile(item.FullPath, _ecSheetName);
        }

        /// <summary>加载一个真实的 ChamberConfig 文件，并按 SheetName 建分类按钮。</summary>
        private void LoadEcFile(string path, string preferredSheet)
        {
            try
            {
                var data = EcConfigService.Load(path);
                _ecFileData = data;

                var sheet = !string.IsNullOrEmpty(preferredSheet) && data.Sheets.Contains(preferredSheet)
                    ? preferredSheet
                    : data.Sheets.FirstOrDefault();

                _ecLoading = true;
                try
                {
                    EcSheetList.ItemsSource = null;
                    EcSheetList.ItemsSource = data.Sheets;
                    EcSheetList.SelectedItem = sheet;
                }
                finally { _ecLoading = false; }

                ShowEcSheet(sheet);
                Log($"EC 已加载: {Path.GetFileName(path)} · {data.Entries.Count} 行 · {data.Sheets.Count} 个 SheetName");
            }
            catch (Exception ex)
            {
                _ecFileData = null;
                _ecViewEntries.Clear();
                _ecSheetName = null;
                _ecLoading = true;
                try
                {
                    EcSheetList.ItemsSource = null;
                    EcDataGrid.ItemsSource = null;
                }
                finally { _ecLoading = false; }

                EcGridEmpty.Visibility = Visibility.Visible;
                Log($"EC 加载失败: {Path.GetFileName(path)} → {ex.Message}");
                MessageDialog.Error(this, L.T("ec.msg.loadFailed", Path.GetFileName(path), ex.Message),
                    L.T("msg.title.error"));
            }

            UpdateEcUiState();
        }

        private void ShowEcSheet(string sheetName)
        {
            _ecSheetName = sheetName;

            if (_ecFileData == null)
            {
                _ecViewEntries.Clear();
                EcDataGrid.ItemsSource = null;
                EcGridEmpty.Visibility = Visibility.Visible;
                UpdateEcUiState();
                return;
            }

            _ecViewEntries = string.IsNullOrEmpty(sheetName)
                ? _ecFileData.Entries.ToList()
                : _ecFileData.Entries
                    .Where(x => string.Equals(x.SheetName, sheetName, StringComparison.Ordinal))
                    .ToList();

            EcDataGrid.ItemsSource = null;
            EcDataGrid.ItemsSource = _ecViewEntries;
            _ecScopeTemplate = _ecViewEntries.FirstOrDefault();   // 切 SheetName 后用首行当批量条件模板
            EcGridEmpty.Visibility = _ecViewEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateEcUiState();
        }

        private void OnEcSheetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ecLoading) return;
            if (EcSheetList.SelectedItem is string sheet)
                ShowEcSheet(sheet);
        }

        private void OnEcSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ecScopeTemplate = (EcDataGrid?.SelectedItems.Cast<EcEntry>().FirstOrDefault())
                                ?? _ecViewEntries.FirstOrDefault();
            UpdateEcUiState();
        }

        private void UpdateEcUiState()
        {
            if (EcRowsHint == null) return;

            var category = CurrentEcCategory;
            RefreshEcScope(category);

            var selected = EcDataGrid == null ? 0 : EcDataGrid.SelectedItems.Count;
            EcRowsHint.Text = L.T("ec.rows", _ecViewEntries.Count, selected,
                category == null ? "" : category.Name, _ecScopeFileCount, _ecScopeRowCount);
            EcSheetHint.Text = _ecFileData == null ? "" : L.T("ec.sheetHint", _ecFileData.Sheets.Count);
            EcRootHintText.Text = Directory.Exists(_chamberRoot)
                ? L.T("ec.rootHint", _ecCategories.Sum(c => c.Files.Count))
                : L.T("status.dirMissing");

            // 批量作用域 = 当前类别全部文件中的当前 SheetName，与选中行无关
            var hasScopeRows = _ecScopeRowCount > 0;
            EcEditBtn.IsEnabled = hasScopeRows;
            EcDeleteBtn.IsEnabled = hasScopeRows;
            EcMenuEdit.IsEnabled = hasScopeRows;
            EcMenuDelete.IsEnabled = hasScopeRows;

            var canAdd = category != null && category.Files.Count > 0 && !string.IsNullOrEmpty(_ecSheetName);
            EcMenuAdd.IsEnabled = canAdd;
            EcAddBtn.IsEnabled = canAdd;
        }

        private EcCategory CurrentEcCategory => EcTypeList?.SelectedItem as EcCategory;

        private string _ecScopeKey;
        private int _ecScopeFileCount;
        private int _ecScopeRowCount;
        private EcEntry _ecScopeTemplate;   // 作用域过滤模板（SheetName + Class + Name 三键精确匹配），null 表示无模板

        /// <summary>
        /// 批量作用域 = 当前类别下所有文件中 SheetName + Class + Name 与模板完全一致的行。
        /// SheetName / Class / Name 一律按 Ordinal（大小写敏感、不 Trim）精确比较；空串也只匹配空串。
        /// </summary>
        private void RefreshEcScope(EcCategory category)
        {
            var tpl = _ecScopeTemplate;
            var tSheet = (tpl == null || string.IsNullOrEmpty(tpl.SheetName)) ? _ecSheetName : tpl.SheetName;
            var tClass = tpl?.Class ?? "";
            var tName = tpl?.Name ?? "";
            var key = (category == null ? "" : category.Name) + "\u0001"
                      + (tSheet ?? "") + "\u0001" + tClass + "\u0001" + tName;
            if (string.Equals(key, _ecScopeKey, StringComparison.Ordinal)) return;

            _ecScopeKey = key;
            _ecScopeFileCount = 0;
            _ecScopeRowCount = 0;
            // 没有模板行时批量作用域为 0：宁可少改，不可错改
            if (category == null || tpl == null || string.IsNullOrEmpty(tSheet)) return;

            foreach (var file in category.Files)
            {
                try
                {
                    var data = EcConfigService.Load(file.FullPath);
                    var rows = data.Entries.Count(e => MatchesBatchCondition(e, tSheet, tClass, tName));
                    if (rows > 0)
                    {
                        _ecScopeFileCount++;
                        _ecScopeRowCount += rows;
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 三键精确匹配：null 表示该键不参与过滤（仅内部调用），空串表示必须与空串完全相同。
        /// 一律 Ordinal（大小写敏感、不 Trim），避免把近似 Name / 大小写不同的行误纳入批量范围。
        /// </summary>
        private static bool MatchesBatchCondition(EcEntry e, string sheet, string clazz, string name)
        {
            if (e == null) return false;
            if (!string.Equals(e.SheetName, sheet, StringComparison.Ordinal)) return false;
            if (clazz != null && !string.Equals(e.Class, clazz, StringComparison.Ordinal)) return false;
            if (name != null && !string.Equals(e.Name, name, StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>从 DataGrid 当前选中行（优先）或视图第一行（无选中）解析批量操作模板行。</summary>
        private EcEntry ResolveBatchTemplate()
        {
            if (EcDataGrid != null && EcDataGrid.SelectedItems.Count > 0)
                return EcDataGrid.SelectedItems[0] as EcEntry;
            return _ecViewEntries.FirstOrDefault();
        }

        private void InvalidateEcScope() => _ecScopeKey = null;

        /// <summary>读取当前类别下所有文件中与模板 SheetName + Class + Name 匹配的行。</summary>
        private List<EcBatchFile> LoadEcScopeFiles(EcCategory category, string sheet, out List<string> errors,
            string classFilter = null, string nameFilter = null)
        {
            var result = new List<EcBatchFile>();
            errors = new List<string>();
            if (category == null || string.IsNullOrEmpty(sheet)) return result;

            foreach (var file in category.Files)
            {
                try
                {
                    var data = EcConfigService.Load(file.FullPath);
                    var rows = data.Entries
                        .Where(e => MatchesBatchCondition(e, sheet, classFilter, nameFilter))
                        .ToList();
                    if (rows.Count > 0)
                        result.Add(new EcBatchFile { File = file, Data = data, Rows = rows });
                }
                catch (Exception ex)
                {
                    errors.Add(Path.GetFileName(file.FullPath) + ": " + ex.Message);
                }
            }
            return result;
        }

        private bool EnsureEcBatchScope(EcCategory category, string sheet)
        {
            if (category == null || category.Files.Count == 0)
            {
                MessageDialog.Warning(this, L.T("msg.noFile"), L.T("msg.title.warning"));
                return false;
            }
            if (string.IsNullOrEmpty(sheet))
            {
                MessageDialog.Warning(this, L.T("ec.msg.sheetRequired"), L.T("msg.title.warning"));
                return false;
            }
            return true;
        }

        private void OnEcBrowseFolder(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = L.T("ec.dlg.root"),
                ShowNewFolderButton = false,
                SelectedPath = Directory.Exists(_chamberRoot) ? _chamberRoot : DefaultChamberRoot
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(dlg.SelectedPath)) return;

            _chamberRoot = Path.GetFullPath(dlg.SelectedPath);
            EcRootTextBox.Text = _chamberRoot;
            _settings.ChamberRoot = _chamberRoot;
            SettingsService.Save(_settings);

            _ecSheetName = null;
            ReloadEcCategories(null);
            Log($"EC 目录已切换: {_chamberRoot}");
        }

        private void OnEcRefresh(object sender, RoutedEventArgs e)
        {
            var current = EcFileList.SelectedItem as EcFileItem;
            ReloadEcCategories(current?.FileName);
            Log($"EC 文件列表已刷新（{_ecCategories.Sum(c => c.Files.Count)} 个文件）");
        }

        /// <summary>右键时先选中光标所在行（多选时保留已选行），保证上下文菜单作用于选中集合。</summary>
        private void OnEcRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is DataGridRow))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row && !row.IsSelected)
            {
                EcDataGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }
        }

        /// <summary>批量修改：仅作用于当前类别下所有文件中 SheetName + Class + Name 与当前选中行一致的行。</summary>
        private void OnEcEditSelected(object sender, RoutedEventArgs e)
        {
            var category = CurrentEcCategory;
            var sheet = _ecSheetName;
            var tpl = ResolveBatchTemplate();
            if (!EnsureEcBatchScope(category, sheet)) return;
            if (tpl == null)
            {
                MessageDialog.Warning(this, L.T("ec.msg.pickRowFirst"), L.T("msg.title.warning"));
                return;
            }
            _ecScopeTemplate = tpl;

            var scope = LoadEcScopeFiles(category, sheet, out var loadErrors, tpl.Class, tpl.Name);
            if (loadErrors.Count > 0)
            {
                Log("EC 批量修改读取失败: " + string.Join(" | ", loadErrors));
                MessageDialog.Error(this, L.T("ec.msg.batchLoadFailed", category.Name, string.Join("\n", loadErrors)),
                    L.T("msg.title.error"));
                return;
            }

            var rowCount = scope.Sum(x => x.Rows.Count);
            if (rowCount == 0)
            {
                MessageDialog.Warning(this,
                    L.T("ec.msg.noMatchingRows", category.Name, sheet, tpl.Class, tpl.Name),
                    L.T("msg.title.warning"));
                return;
            }

            var allRows = scope.SelectMany(x => x.Rows).ToList();
            var templateType = tpl.TemplateType == 1 ? 1 : 3;
            var dlg = new EcEditDialog(allRows, sheet, false, templateType, 0, allRows, category.Name, scope.Count)
            { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            if (!MessageDialog.Confirm(this,
                    L.T("ec.msg.batchEditConfirm", category.Name, scope.Count, sheet, rowCount, tpl.Class, tpl.Name),
                    L.T("msg.title.confirmWrite"))) return;

            var updated = 0;
            var failedFiles = new List<string>();
            foreach (var batchFile in scope)
            {
                foreach (var entry in batchFile.Rows)
                    EcConfigService.Apply(entry.Source, dlg.Result);

                try
                {
                    var backup = EcConfigService.Save(batchFile.Data.FilePath, batchFile.Data.Array,
                        batchFile.Data.Format, _chamberRoot);
                    updated += batchFile.Rows.Count;
                    Log($"EC 批量修改: {batchFile.File.FileName} · Class={tpl.Class} Name={tpl.Name} · {batchFile.Rows.Count} 行 · 备份 {backup}");
                }
                catch (Exception ex)
                {
                    failedFiles.Add(batchFile.File.FileName + ": " + ex.Message);
                }
            }

            InvalidateEcScope();
            if (_ecFileData != null) LoadEcFile(_ecFileData.FilePath, sheet);

            if (failedFiles.Count == 0)
            {
                MessageDialog.Info(this,
                    L.T("ec.msg.batchEditDone", category.Name, scope.Count, sheet, updated),
                    L.T("msg.title.info"));
            }
            else
            {
                Log("EC 批量修改部分失败: " + string.Join(" | ", failedFiles));
                MessageDialog.Warning(this,
                    L.T("ec.msg.batchPartial", updated, failedFiles.Count, string.Join("\n", failedFiles)),
                    L.T("msg.title.partial"));
            }
        }

        /// <summary>批量新增：按当前模板（SheetName + Class）在类别每个文件里插 N 行；Name 由弹框用户填写。</summary>
        private void OnEcAdd(object sender, RoutedEventArgs e)
        {
            var category = CurrentEcCategory;
            var sheet = _ecSheetName;
            var reference = ResolveBatchTemplate();
            if (!EnsureEcBatchScope(category, sheet)) return;

            // Text 状态的 TemplateType 固定为 3
            var templateType = reference == null || reference.TemplateType != 1 ? 3 : 1;
            var nextId = _ecFileData == null ? 2 : EcConfigService.NextEcId(_ecFileData.Entries);

            var dlg = new EcEditDialog(null, sheet, true, templateType, nextId, _ecFileData?.Entries,
                category.Name, category.Files.Count) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;
            dlg.Result.Class = reference == null ? "" : reference.Class;

            var total = category.Files.Count * dlg.Count;
            if (!MessageDialog.Confirm(this,
                    L.T("ec.msg.batchAddConfirmWithKey", category.Name, category.Files.Count, sheet,
                        reference?.Class ?? "", dlg.Result.Name ?? "", dlg.Count, total),
                    L.T("msg.title.confirmWrite"))) return;

            var inserted = 0;
            var failedFiles = new List<string>();
            var currentNewIds = new List<long>();
            var currentPath = _ecFileData?.FilePath;
            foreach (var file in category.Files)
            {
                try
                {
                    var data = EcConfigService.Load(file.FullPath);
                    var start = EcConfigService.NextEcId(data.Entries);
                    for (var i = 0; i < dlg.Count; i++)
                    {
                        var id = start + i * 2;
                        data.Array.Add(EcConfigService.CreateEntry(id, sheet, dlg.Result));
                        inserted++;
                        if (currentPath != null
                            && string.Equals(file.FullPath, currentPath, StringComparison.OrdinalIgnoreCase))
                            currentNewIds.Add(id);
                    }

                    var backup = EcConfigService.Save(file.FullPath, data.Array, data.Format, _chamberRoot);
                    Log($"EC 批量新增: {file.FileName} · SheetName={sheet} Class={dlg.Result.Class} Name={dlg.Result.Name} · {dlg.Count} 行 · 备份 {backup}");
                }
                catch (Exception ex)
                {
                    failedFiles.Add(file.FileName + ": " + ex.Message);
                }
            }

            InvalidateEcScope();
            if (_ecFileData != null) LoadEcFile(_ecFileData.FilePath, sheet);
            SelectEcRowsByIds(currentNewIds);

            if (failedFiles.Count == 0)
            {
                MessageDialog.Info(this,
                    L.T("ec.msg.batchAddDone", category.Name, category.Files.Count, dlg.Count, inserted, sheet),
                    L.T("msg.title.added"));
            }
            else
            {
                Log("EC 批量新增部分失败: " + string.Join(" | ", failedFiles));
                MessageDialog.Warning(this,
                    L.T("ec.msg.batchPartial", inserted, failedFiles.Count, string.Join("\n", failedFiles)),
                    L.T("msg.title.partial"));
            }
        }

        /// <summary>批量删除：删除当前类别下全部文件的当前 SheetName 的所有行（逐文件写前备份）。</summary>
        /// <summary>批量删除：删除当前类别下所有文件中 SheetName + Class + Name 与选中行一致的行。</summary>
        private void OnEcDeleteSelected(object sender, RoutedEventArgs e)
        {
            var category = CurrentEcCategory;
            var sheet = _ecSheetName;
            var tpl = ResolveBatchTemplate();
            if (!EnsureEcBatchScope(category, sheet)) return;
            if (tpl == null)
            {
                MessageDialog.Warning(this, L.T("ec.msg.pickRowFirst"), L.T("msg.title.warning"));
                return;
            }
            _ecScopeTemplate = tpl;

            var scope = LoadEcScopeFiles(category, sheet, out var loadErrors, tpl.Class, tpl.Name);
            if (loadErrors.Count > 0)
            {
                Log("EC 批量删除读取失败: " + string.Join(" | ", loadErrors));
                MessageDialog.Error(this, L.T("ec.msg.batchLoadFailed", category.Name, string.Join("\n", loadErrors)),
                    L.T("msg.title.error"));
                return;
            }

            var rowCount = scope.Sum(x => x.Rows.Count);
            if (rowCount == 0)
            {
                MessageDialog.Warning(this,
                    L.T("ec.msg.noMatchingRows", category.Name, sheet, tpl.Class, tpl.Name),
                    L.T("msg.title.warning"));
                return;
            }

            if (!MessageDialog.Confirm(this,
                    L.T("ec.msg.batchDeleteConfirm", category.Name, scope.Count, sheet, rowCount, tpl.Class, tpl.Name),
                    L.T("msg.title.confirmWrite"))) return;

            var deleted = 0;
            var failedFiles = new List<string>();
            foreach (var batchFile in scope)
            {
                foreach (var entry in batchFile.Rows.OrderByDescending(x => x.Index))
                {
                    if (entry.Index >= 0 && entry.Index < batchFile.Data.Array.Count)
                        batchFile.Data.Array.RemoveAt(entry.Index);
                }

                try
                {
                    var backup = EcConfigService.Save(batchFile.Data.FilePath, batchFile.Data.Array,
                        batchFile.Data.Format, _chamberRoot);
                    deleted += batchFile.Rows.Count;
                    Log($"EC 批量删除: {batchFile.File.FileName} · Class={tpl.Class} Name={tpl.Name} · {batchFile.Rows.Count} 行 · 备份 {backup}");
                }
                catch (Exception ex)
                {
                    failedFiles.Add(batchFile.File.FileName + ": " + ex.Message);
                }
            }

            InvalidateEcScope();
            if (_ecFileData != null) LoadEcFile(_ecFileData.FilePath, sheet);

            if (failedFiles.Count == 0)
            {
                MessageDialog.Info(this,
                    L.T("ec.msg.batchDeleteDone", category.Name, scope.Count, sheet, deleted),
                    L.T("msg.title.deleted"));
            }
            else
            {
                Log("EC 批量删除部分失败: " + string.Join(" | ", failedFiles));
                MessageDialog.Warning(this,
                    L.T("ec.msg.batchPartial", deleted, failedFiles.Count, string.Join("\n", failedFiles)),
                    L.T("msg.title.partial"));
            }
        }

        private void SelectEcRowsByIds(IEnumerable<long> ids)
        {
            var set = new HashSet<long>(ids);
            EcDataGrid.SelectedItems.Clear();
            foreach (var item in EcDataGrid.ItemsSource as IEnumerable<EcEntry> ?? Enumerable.Empty<EcEntry>())
            {
                if (!set.Contains(item.ECID)) continue;
                EcDataGrid.SelectedItems.Add(item);
            }
            if (EcDataGrid.SelectedItems.Count > 0)
                EcDataGrid.ScrollIntoView(EcDataGrid.SelectedItems[EcDataGrid.SelectedItems.Count - 1]);
            UpdateEcUiState();
        }

        /// <summary>批量操作时一个类别下单个文件及其参与的数据行。</summary>
        private sealed class EcBatchFile
        {
            public EcFileItem File { get; set; }
            public EcFileData Data { get; set; }
            public List<EcEntry> Rows { get; set; }
        }

        // ================= 辅助 =================
    }
}
