using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenaralDBTools.Dialogs;
using GenaralDBTools.Models;
using GenaralDBTools.Services;

namespace GenaralDBTools.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseProviderRegistry _registry;
    private readonly ConnectionStore _store;
    private readonly List<ConnectionProfile> _profiles = new();
    private readonly Dictionary<string, DatabaseNode> _roots = new();
    private int _sqlTabCounter;
    private bool _initialized;

    [ObservableProperty] private string statusText = "就绪";
    [ObservableProperty] private string filterText = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private ConnectionProfile? selectedConnection;
    [ObservableProperty] private DatabaseNode? selectedNode;
    [ObservableProperty] private WorkspaceTab? selectedTab;
    [ObservableProperty] private string outputText = "";
    [ObservableProperty] private string connectionSummary = "未选择连接";
    [ObservableProperty] private bool hasTabs;

    public ObservableCollection<DatabaseNode> Connections { get; } = new();
    public ObservableCollection<WorkspaceTab> Tabs { get; } = new();
    public string StorePath => _store.FilePath;

    public MainViewModel(DatabaseProviderRegistry registry, ConnectionStore store)
    {
        _registry = registry;
        _store = store;
        Tabs.CollectionChanged += (_, _) =>
        {
            HasTabs = Tabs.Count > 0;
            if (SelectedTab == null && Tabs.Count > 0) SelectedTab = Tabs[^1];
        };
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _profiles.Clear();
            _profiles.AddRange(await _store.LoadAsync());
            RebuildTree();
            StatusText = _profiles.Count == 0
                ? "暂无连接配置，点击“新建连接”开始。"
                : $"已加载 {_profiles.Count} 个连接配置。";
            AppendLog($"连接配置文件：{_store.FilePath}");
        }
        catch (Exception ex)
        {
            StatusText = "连接配置加载失败";
            AppendLog(ex.ToString());
            DialogService.Error("加载连接配置失败", ex);
        }

        if (Tabs.Count == 0) NewSqlTab();
    }

    // ---------------- 连接管理 ----------------

    private DatabaseNode? RootFor(ConnectionProfile profile) => _roots.TryGetValue(profile.Id, out var root) ? root : null;

    private void RebuildTree()
    {
        var keyword = FilterText.Trim();
        Connections.Clear();
        foreach (var profile in _profiles)
        {
            if (keyword.Length > 0
                && !profile.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                && !ConnectionProfile.KindName(profile.Kind).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                continue;

            var root = _roots.TryGetValue(profile.Id, out var existing) ? existing : CreateRoot(profile);
            root.Name = profile.Name;
            root.Icon = IconFor(profile.Kind);
            Connections.Add(root);
        }
    }

    private DatabaseNode CreateRoot(ConnectionProfile profile)
    {
        var root = new DatabaseNode
        {
            Name = profile.Name,
            NodeType = NodeTypes.Connection,
            ProfileId = profile.Id,
            Icon = IconFor(profile.Kind),
            Children =
            {
                new DatabaseNode { Name = "展开或双击加载对象", NodeType = NodeTypes.Hint, Icon = "·" }
            }
        };
        _roots[profile.Id] = root;
        return root;
    }

    private static string IconFor(DatabaseKind kind) => kind switch
    {
        DatabaseKind.Oracle => "◈",
        DatabaseKind.MySql => "◆",
        DatabaseKind.Sqlite => "▦",
        DatabaseKind.Redis => "▣",
        _ => "◫"
    };

    private static bool IsHint(DatabaseNode node) => node.NodeType == NodeTypes.Hint || node.NodeType == NodeTypes.Column;

    public void SelectNode(DatabaseNode? node)
    {
        SelectedNode = node;
        if (node?.ProfileId is not { } profileId) return;
        var profile = _profiles.FirstOrDefault(item => item.Id == profileId);
        if (profile == null) return;
        SelectedConnection = profile;
        ConnectionSummary = profile.Describe();
    }

    partial void OnFilterTextChanged(string value)
    {
        if (_initialized) RebuildTree();
    }

    [RelayCommand]
    private async Task AddConnectionAsync()
    {
        if (IsBusy) return;
        var dialog = new ConnectionDialog();
        if (dialog.ShowDialog() != true || dialog.Profile == null) return;

        _profiles.Add(dialog.Profile);
        if (!await PersistAsync()) return;

        var root = RootFor(dialog.Profile);
        SelectedConnection = dialog.Profile;
        ConnectionSummary = dialog.Profile.Describe();
        SelectedNode = root;
        StatusText = $"连接“{dialog.Profile.Name}”已保存，点击“打开连接”开始浏览。";
        AppendLog($"新增连接：{dialog.Profile.Name}（{dialog.Profile.Describe()}）");
    }

    [RelayCommand]
    private async Task EditConnectionAsync()
    {
        if (IsBusy) return;
        var profile = SelectedConnection;
        if (profile == null)
        {
            WarnNoConnection("编辑连接");
            return;
        }

        var dialog = new ConnectionDialog(profile.Clone());
        if (dialog.ShowDialog() != true || dialog.Profile == null) return;

        var updated = dialog.Profile;
        var index = _profiles.FindIndex(item => item.Id == updated.Id);
        if (index >= 0) _profiles[index] = updated;
        SelectedConnection = updated;
        ConnectionSummary = updated.Describe();

        CloseTabsForProfile(updated.Id);
        ResetRoot(updated, "连接参数已更新，展开或双击重新加载对象。");
        if (!await PersistAsync()) return;
        StatusText = $"连接“{updated.Name}”已更新。";
        AppendLog($"更新连接：{updated.Name}（{updated.Describe()}）");
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (IsBusy) return;
        var profile = SelectedConnection;
        if (profile == null)
        {
            WarnNoConnection("删除连接");
            return;
        }

        if (!DialogService.Confirm("删除连接",
                $"确定删除连接“{profile.Name}”吗？",
                "只删除本地保存的连接配置，不会删除数据库中的数据。", "删除"))
            return;

        try
        {
            await _registry.Get(profile.Kind).CloseAsync(profile);
        }
        catch (Exception ex)
        {
            AppendLog($"关闭连接会话时出错：{ex.Message}");
        }

        CloseTabsForProfile(profile.Id);
        _profiles.RemoveAll(item => item.Id == profile.Id);
        _roots.Remove(profile.Id);
        SelectedConnection = null;
        SelectedNode = null;
        ConnectionSummary = "未选择连接";
        if (!await PersistAsync()) return;
        StatusText = $"连接“{profile.Name}”已删除。";
        AppendLog($"删除连接：{profile.Name}");
    }

    [RelayCommand]
    private async Task TestSelectedConnectionAsync()
    {
        if (IsBusy) return;
        var profile = SelectedConnection;
        if (profile == null)
        {
            WarnNoConnection("测试连接");
            return;
        }

        IsBusy = true;
        StatusText = $"正在测试连接“{profile.Name}”…";
        try
        {
            await _registry.Get(profile.Kind).TestConnectionAsync(profile);
            StatusText = $"连接测试成功：{profile.Name}";
            AppendLog($"连接测试成功：{profile.Name}（{profile.Describe()}）");
        }
        catch (Exception ex)
        {
            StatusText = $"连接测试失败：{profile.Name}";
            AppendLog($"连接测试失败：{profile.Name}\n{ex}");
            DialogService.Error($"连接测试失败：{profile.Name}", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenConnectionAsync()
    {
        if (IsBusy) return;
        var profile = SelectedConnection;
        if (profile == null)
        {
            WarnNoConnection("打开连接");
            return;
        }
        var root = RootFor(profile) ?? CreateRoot(profile);
        if (!Connections.Contains(root)) Connections.Add(root);
        await ConnectCoreAsync(profile, root);
    }

    private async Task ConnectCoreAsync(ConnectionProfile profile, DatabaseNode root)
    {
        IsBusy = true;
        root.IsLoading = true;
        root.LoadError = null;
        StatusText = $"正在连接“{profile.Name}”…";
        AppendLog($"正在连接：{profile.Name}（{profile.Describe()}）");
        try
        {
            var provider = _registry.Get(profile.Kind);
            await provider.TestConnectionAsync(profile);
            var objects = await provider.GetObjectsAsync(profile);
            root.Children.Clear();
            foreach (var node in objects) root.Children.Add(node);
            root.HasLoadedChildren = true;
            root.IsConnected = true;

            var realCount = objects.Count(node => !IsHint(node));
            StatusText = $"已连接“{profile.Name}”，发现 {realCount} 个对象。";
            AppendLog($"连接成功：{profile.Name}");
            if (profile.Kind == DatabaseKind.Redis) await OpenRedisTabAsync(profile);
        }
        catch (Exception ex)
        {
            root.IsConnected = false;
            root.HasLoadedChildren = false;
            root.LoadError = ex.Message;
            ShowPlaceholder(root, $"连接失败：{ex.Message}");
            StatusText = $"连接失败：{profile.Name}";
            AppendLog($"连接失败：{profile.Name}\n{ex}");
            DialogService.Error($"连接“{profile.Name}”失败", ex);
        }
        finally
        {
            root.IsLoading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        if (IsBusy) return;
        var profile = SelectedConnection;
        if (profile == null)
        {
            WarnNoConnection("断开连接");
            return;
        }

        IsBusy = true;
        try
        {
            await _registry.Get(profile.Kind).CloseAsync(profile);
            CloseTabsForProfile(profile.Id, onlyRedis: true);
            ResetRoot(profile, "已断开连接。展开或双击可重新连接。");
            StatusText = $"已断开“{profile.Name}”。";
            AppendLog($"断开连接：{profile.Name}");
        }
        catch (Exception ex)
        {
            StatusText = "断开连接失败";
            AppendLog(ex.ToString());
            DialogService.Error("断开连接失败", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetRoot(ConnectionProfile profile, string hint)
    {
        var root = RootFor(profile);
        if (root == null) return;
        root.IsConnected = false;
        root.HasLoadedChildren = false;
        root.LoadError = null;
        ShowPlaceholder(root, hint);
    }

    private static void ShowPlaceholder(DatabaseNode root, string text)
    {
        root.Children.Clear();
        root.Children.Add(new DatabaseNode { Name = text, NodeType = NodeTypes.Hint, Icon = "·" });
    }

    [RelayCommand]
    private async Task RefreshConnectionsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var loaded = await _store.LoadAsync();
            _profiles.Clear();
            _profiles.AddRange(loaded);
            foreach (var id in _roots.Keys.Except(_profiles.Select(p => p.Id)).ToList()) _roots.Remove(id);
            if (SelectedConnection != null && !_profiles.Any(p => p.Id == SelectedConnection.Id))
            {
                SelectedConnection = null;
                SelectedNode = null;
                ConnectionSummary = "未选择连接";
            }
            RebuildTree();
            StatusText = $"连接列表已刷新，共 {_profiles.Count} 个连接。";
        }
        catch (Exception ex)
        {
            StatusText = "刷新连接列表失败";
            AppendLog(ex.ToString());
            DialogService.Error("刷新连接列表失败", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshObjectsAsync()
    {
        if (IsBusy) return;
        var profile = SelectedConnection;
        if (profile == null)
        {
            WarnNoConnection("刷新对象");
            return;
        }
        var root = RootFor(profile);
        if (root == null) return;
        await ReloadObjectsAsync(profile, root, quiet: false);
        StatusText = $"已刷新“{profile.Name}”的对象。";
    }

    private async Task ReloadObjectsAsync(ConnectionProfile profile, DatabaseNode root, bool quiet)
    {
        IsBusy = true;
        root.IsLoading = true;
        try
        {
            var provider = _registry.Get(profile.Kind);
            var objects = await provider.GetObjectsAsync(profile);
            root.Children.Clear();
            foreach (var node in objects) root.Children.Add(node);
            root.HasLoadedChildren = true;
            root.IsConnected = true;
            root.LoadError = null;
            if (quiet) StatusText = $"已刷新“{profile.Name}”的对象。";
        }
        catch (Exception ex)
        {
            root.IsConnected = false;
            root.HasLoadedChildren = false;
            root.LoadError = ex.Message;
            ShowPlaceholder(root, $"加载失败：{ex.Message}");
            AppendLog($"加载对象失败：{profile.Name}\n{ex}");
            if (!quiet) DialogService.Error("加载对象失败", ex);
        }
        finally
        {
            root.IsLoading = false;
            IsBusy = false;
        }
    }

    // ---------------- 树节点展开 / 打开 ----------------

    public async Task ExpandNodeAsync(DatabaseNode? node)
    {
        if (node == null || node.IsLoading) return;
        if (node.NodeType == NodeTypes.Connection)
        {
            var connectionProfile = ResolveProfile(node.ProfileId) ?? SelectedConnection;
            if (connectionProfile != null && !node.IsConnected && !node.HasLoadedChildren)
                await ConnectCoreAsync(connectionProfile, node);
            return;
        }
        if (node.NodeType == NodeTypes.Column || node.NodeType == NodeTypes.Hint || node.NodeType == NodeTypes.RedisKey)
            return;
        if (node.HasLoadedChildren) return;

        var profile = ResolveProfile(node.ProfileId);
        if (profile == null) return;
        node.IsLoading = true;
        try
        {
            var children = await _registry.Get(profile.Kind).GetChildObjectsAsync(profile, node);
            node.Children.Clear();
            foreach (var child in children) child.ProfileId ??= profile.Id;
            foreach (var child in children) node.Children.Add(child);
            node.HasLoadedChildren = true;
            node.LoadError = null;
            StatusText = $"已加载 {node.Name} 的子对象（{children.Count} 项）。";
        }
        catch (Exception ex)
        {
            node.LoadError = ex.Message;
            node.HasLoadedChildren = false;
            node.Children.Clear();
            node.Children.Add(new DatabaseNode { Name = $"加载失败：{ex.Message}", NodeType = NodeTypes.Hint, Icon = "·" });
            StatusText = $"加载“{node.Name}”失败";
            AppendLog($"加载子对象失败：{node.Name}\n{ex}");
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    public async Task OpenNodeAsync(DatabaseNode? node)
    {
        if (node == null) return;
        switch (node.NodeType)
        {
            case NodeTypes.Connection:
                SelectNode(node);
                await OpenConnectionAsync();
                break;
            case NodeTypes.Table:
            case NodeTypes.View:
                SelectNode(node);
                await OpenTableAsync(node);
                break;
            case NodeTypes.RedisDatabase:
            case NodeTypes.RedisKey:
            {
                var profile = ResolveProfile(node.ProfileId);
                if (profile == null) return;
                SelectNode(node);
                await OpenRedisTabAsync(profile, node.NodeType == NodeTypes.RedisKey ? node.ObjectName : null);
                break;
            }
            case NodeTypes.Schema:
            case NodeTypes.MongoDatabase:
            case NodeTypes.Collection:
                await ExpandNodeAsync(node);
                break;
        }
    }

    private ConnectionProfile? ResolveProfile(string? profileId) =>
        profileId == null ? null : _profiles.FirstOrDefault(profile => profile.Id == profileId);

    private async Task OpenTableAsync(DatabaseNode node)
    {
        var profile = ResolveProfile(node.ProfileId);
        if (profile == null || string.IsNullOrWhiteSpace(node.ObjectName)) return;

        var tableKey = $"{profile.Id}|{node.SchemaName ?? ""}|{node.ObjectName}";
        var existing = Tabs.OfType<TableBrowserTab>().FirstOrDefault(tab => tab.TableKey == tableKey);
        if (existing != null)
        {
            SelectedTab = existing;
            await existing.RefreshAsync();
            return;
        }

        IRelationalTableProvider provider;
        try
        {
            provider = _registry.GetRelational(profile.Kind);
        }
        catch (NotSupportedException ex)
        {
            DialogService.Warn(ex.Message, "不支持表级操作");
            return;
        }

        var isView = node.NodeType == NodeTypes.View;
        var tab = new TableBrowserTab(provider, profile, node.SchemaName, node.ObjectName, isView,
            () => ReloadObjectsQuietAsync(profile),
            (name, view) => OpenTableByNameAsync(profile, node.SchemaName, name, view),
            CloseTab);
        Tabs.Add(tab);
        SelectedTab = tab;
        StatusText = $"正在打开{(isView ? "视图" : "表")}“{node.ObjectName}”…";
    }

    private async Task OpenTableByNameAsync(ConnectionProfile profile, string? schema, string table, bool isView)
    {
        await OpenTableAsync(new DatabaseNode
        {
            Name = table,
            NodeType = isView ? NodeTypes.View : NodeTypes.Table,
            ProfileId = profile.Id,
            SchemaName = schema,
            ObjectName = table,
            Icon = isView ? "◫" : "▦"
        });
    }

    private async Task ReloadObjectsQuietAsync(ConnectionProfile profile)
    {
        var root = RootFor(profile);
        if (root == null) return;
        await ReloadObjectsAsync(profile, root, quiet: true);
    }

    private async Task OpenRedisTabAsync(ConnectionProfile profile, string? keyToFocus = null)
    {
        IRedisBrowser browser;
        try
        {
            browser = _registry.GetRedis(profile.Kind);
        }
        catch (NotSupportedException ex)
        {
            DialogService.Warn(ex.Message, "Redis 浏览器不可用");
            return;
        }

        var tab = Tabs.OfType<RedisTab>().FirstOrDefault(item => item.Profile.Id == profile.Id);
        if (tab == null)
        {
            tab = new RedisTab(browser, profile, () => ReloadObjectsQuietAsync(profile));
            Tabs.Add(tab);
        }
        SelectedTab = tab;
        if (keyToFocus != null) await tab.FocusKeyAsync(keyToFocus);
    }

    // ---------------- SQL 编辑器 ----------------

    [RelayCommand]
    private void NewSqlTab()
    {
        _sqlTabCounter++;
        var tab = new SqlTab
        {
            Title = $"SQL 编辑器 {_sqlTabCounter}",
            Sql = SelectedConnection?.Kind == DatabaseKind.Oracle ? "SELECT SYSDATE FROM DUAL;" : "SELECT 1;"
        };
        Tabs.Add(tab);
        SelectedTab = tab;
        StatusText = "已新建 SQL 编辑器。";
    }

    [RelayCommand]
    private async Task ExecuteSqlAsync()
    {
        if (IsBusy) return;
        if (SelectedTab is not SqlTab tab)
        {
            StatusText = "请先切换到 SQL 编辑器标签页，再执行 SQL。";
            return;
        }
        var profile = SelectedConnection;
        if (profile == null)
        {
            DialogService.Warn("请先在左侧选择一个数据库连接，再执行 SQL。", "未选择连接");
            StatusText = "未选择连接";
            return;
        }
        if (!_registry.Get(profile.Kind).SupportsSql)
        {
            DialogService.Warn($"{ConnectionProfile.KindName(profile.Kind)} 不支持 SQL，请使用对应的数据浏览器。", "不支持 SQL");
            StatusText = "当前连接不支持 SQL";
            return;
        }
        if (string.IsNullOrWhiteSpace(tab.Sql))
        {
            tab.ErrorText = "SQL 不能为空。";
            StatusText = "SQL 不能为空";
            return;
        }

        IsBusy = true;
        tab.IsBusy = true;
        tab.ErrorText = null;
        tab.TargetConnection = profile.Describe();
        try
        {
            var provider = _registry.Get(profile.Kind);
            var result = await provider.ExecuteAsync(profile, tab.Sql);
            tab.ResetResult();
            if (result.HasTable)
            {
                tab.ResultData = result.Table!.DefaultView;
                tab.HasResult = true;
            }
            tab.ResultSummary = result.Summary;
            tab.OutputText = $"{DateTime.Now:HH:mm:ss}  {result.Summary}\n{tab.OutputText}";
            StatusText = $"SQL 执行成功：{result.Summary}";
            AppendLog($"[{profile.Name}] {result.Summary}");
        }
        catch (Exception ex)
        {
            tab.ErrorText = ex.Message;
            tab.OutputText = $"{DateTime.Now:HH:mm:ss}  执行失败：{ex.Message}\n{tab.OutputText}";
            StatusText = $"SQL 执行失败：{ex.Message}";
            AppendLog($"[{profile.Name}] SQL 执行失败\n{ex}");
        }
        finally
        {
            tab.IsBusy = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseTab(WorkspaceTab? tab)
    {
        if (tab == null) return;
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (SelectedTab == tab || SelectedTab == null)
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        StatusText = "标签页已关闭。";
    }

    private void CloseTabsForProfile(string profileId, bool onlyRedis = false)
    {
        var toClose = Tabs.Where(tab =>
            tab switch
            {
                RedisTab redis => redis.Profile.Id == profileId,
                TableBrowserTab table => !onlyRedis && table.Profile.Id == profileId,
                _ => false
            }).ToList();
        foreach (var tab in toClose)
        {
            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);
            if (SelectedTab == tab) SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }
    }

    // ---------------- 辅助 ----------------

    private void WarnNoConnection(string action)
    {
        StatusText = $"请先选择一个连接（{action}）。";
        DialogService.Warn($"请先在左侧选择一个连接，再执行“{action}”。", "未选择连接");
    }

    private async Task<bool> PersistAsync()
    {
        try
        {
            await _store.SaveAsync(_profiles);
            RebuildTree();
            StatusText = "连接配置已保存。";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = "保存连接配置失败";
            AppendLog(ex.ToString());
            DialogService.Error("保存连接配置失败", ex);
            return false;
        }
    }

    public void AppendLog(string text)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {text}";
        OutputText = entry + "\n" + OutputText;
        if (OutputText.Length > 40000)
            OutputText = OutputText[..40000];
    }

    [RelayCommand]
    private void ClearOutput() => OutputText = "";

    [RelayCommand]
    private void ShowAbout()
    {
        DialogService.Info(
            "GenaralDBTools · 数据库工作台\n\n" +
            "支持 MySQL / SQLite / Oracle 的表级浏览、分页与增删改，Redis 的键级管理，MongoDB 的库/集合浏览。\n" +
            "连接配置保存在当前 Windows 用户目录，密码使用 DPAPI 加密。\n\n" +
            $"配置文件：{_store.FilePath}",
            "关于 GenaralDBTools");
    }

    [RelayCommand]
    private void Exit() => System.Windows.Application.Current.Shutdown();

    public async Task ShutdownAsync()
    {
        foreach (var profile in _profiles)
        {
            try { await _registry.Get(profile.Kind).CloseAsync(profile); }
            catch { /* 退出阶段忽略关闭异常 */ }
        }
    }
}
