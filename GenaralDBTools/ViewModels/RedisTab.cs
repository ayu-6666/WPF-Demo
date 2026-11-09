using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenaralDBTools.Dialogs;
using GenaralDBTools.Models;
using GenaralDBTools.Services;

namespace GenaralDBTools.ViewModels;

public partial class RedisTab : WorkspaceTab
{
    private readonly IRedisBrowser _browser;
    private readonly Func<Task> _refreshTree;
    private bool _suppressSelection;

    public ConnectionProfile Profile { get; }
    public ObservableCollection<RedisKeySummary> Keys { get; } = new();

    [ObservableProperty] private RedisKeySummary? selectedKey;
    [ObservableProperty] private RedisKeyDetail? detail;
    [ObservableProperty] private string valueText = "";
    [ObservableProperty] private string ttlInput = "";
    [ObservableProperty] private string pattern = "*";
    [ObservableProperty] private string maxKeysText = "200";
    [ObservableProperty] private string commandText = "PING";
    [ObservableProperty] private string commandOutput = "";
    [ObservableProperty] private string statusText = "正在扫描 key…";
    [ObservableProperty] private string? errorText;

    public RedisTab(IRedisBrowser browser, ConnectionProfile profile, Func<Task> refreshTree)
    {
        _browser = browser;
        Profile = profile;
        _refreshTree = refreshTree;
        Title = $"Redis · {profile.Name}";
        _ = ScanAsync();
    }

    partial void OnSelectedKeyChanged(RedisKeySummary? value)
    {
        if (_suppressSelection) return;
        if (value == null)
        {
            Detail = null;
            ValueText = "";
            TtlInput = "";
            return;
        }
        _ = LoadDetailAsync();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = null;
        try
        {
            var maxKeys = int.TryParse(MaxKeysText.Trim(), out var parsed) ? Math.Clamp(parsed, 10, 1000) : 200;
            var keys = await _browser.ScanKeysAsync(Profile, Pattern, maxKeys);
            var previousKey = SelectedKey?.Key;
            _suppressSelection = true;
            Keys.Clear();
            foreach (var key in keys) Keys.Add(key);
            _suppressSelection = false;

            StatusText = $"匹配 {keys.Count} 个 key（最多扫描 {maxKeys} 个）。";
            if (previousKey != null)
                SelectedKey = Keys.FirstOrDefault(key => key.Key == previousKey);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "Key 扫描失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadDetailAsync()
    {
        if (SelectedKey == null) return;
        IsBusy = true;
        ErrorText = null;
        try
        {
            var detail = await _browser.GetKeyDetailAsync(Profile, SelectedKey.Key);
            Detail = detail;
            ValueText = detail.ValueText;
            TtlInput = detail.Ttl is { TotalSeconds: > 0 }
                ? ((int)detail.Ttl.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                : "";
            StatusText = $"类型 {detail.Type} · {detail.TtlText} · {detail.Length} 项";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Detail = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddKeyAsync()
    {
        var input = DialogService.EditRedisValue(null, "string", "", null, editMode: false);
        if (input == null) return;
        await SaveKeyAsync(input);
    }

    [RelayCommand]
    private async Task EditKeyAsync()
    {
        if (SelectedKey == null)
        {
            ErrorText = "请先选择一个 key。";
            return;
        }
        var ttl = Detail?.Ttl is { } current && current.TotalSeconds > 0 ? current : (TimeSpan?)null;
        var input = DialogService.EditRedisValue(SelectedKey.Key, Detail?.Type ?? SelectedKey.Type, ValueText, ttl, editMode: true);
        if (input == null) return;
        if (!DialogService.Confirm("覆盖 Key", $"确定用新内容覆盖 Key “{input.Key}” 吗？", "覆盖会先删除原 Key 再写入新内容，无法恢复。", "覆盖保存"))
            return;
        await SaveKeyAsync(input);
    }

    [RelayCommand]
    private async Task SaveValueAsync()
    {
        if (SelectedKey == null || Detail == null)
        {
            ErrorText = "请先选择一个 key。";
            return;
        }
        if (!DialogService.Confirm("保存 Key 内容", $"确定保存对 Key “{SelectedKey.Key}” 的修改吗？", "保存会覆盖该 Key 的全部内容。", "保存"))
            return;

        TimeSpan? ttl = null;
        var ttlText = TtlInput.Trim();
        if (!string.IsNullOrEmpty(ttlText))
        {
            if (!int.TryParse(ttlText, out var seconds) || seconds < 0)
            {
                ErrorText = "TTL 必须是不小于 0 的整数秒。";
                return;
            }
            if (seconds > 0) ttl = TimeSpan.FromSeconds(seconds);
        }

        await SaveKeyAsync(new RedisValueInput(SelectedKey.Key, Detail.Type, ValueText, ttl));
    }

    private async Task SaveKeyAsync(RedisValueInput input)
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            await _browser.SaveKeyAsync(Profile, input.Key, input.Type, input.Value, input.Ttl);
            await ScanAsyncInternal();
            if (Keys.All(item => item.Key != input.Key))
            {
                Pattern = "*";
                await ScanAsyncInternal();
            }
            SelectKey(input.Key);
            StatusText = $"Key “{input.Key}” 已保存。";
            await LoadDetailInternal();
            await _refreshTree();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SelectKey(string key)
    {
        _suppressSelection = true;
        SelectedKey = Keys.FirstOrDefault(item => item.Key == key);
        _suppressSelection = false;
    }

    [RelayCommand]
    private async Task DeleteKeyAsync()
    {
        if (SelectedKey == null)
        {
            ErrorText = "请先选择一个 key。";
            return;
        }
        if (!DialogService.Confirm("删除 Key", $"确定删除 Key “{SelectedKey.Key}” 吗？", "删除后无法恢复。", "删除"))
            return;

        IsBusy = true;
        ErrorText = null;
        try
        {
            await _browser.DeleteKeyAsync(Profile, SelectedKey.Key);
            StatusText = $"Key “{SelectedKey.Key}” 已删除。";
            Detail = null;
            ValueText = "";
            await ScanAsyncInternal();
            await _refreshTree();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyTtlAsync()
    {
        if (SelectedKey == null)
        {
            ErrorText = "请先选择一个 key。";
            return;
        }
        var ttlText = TtlInput.Trim();
        if (string.IsNullOrEmpty(ttlText))
        {
            await SetTtlAsync(null);
            return;
        }
        if (!int.TryParse(ttlText, out var seconds) || seconds <= 0)
        {
            ErrorText = "TTL 必须是大于 0 的整数秒；要取消过期请点击“永久”。";
            return;
        }
        await SetTtlAsync(TimeSpan.FromSeconds(seconds));
    }

    [RelayCommand]
    private Task PersistKeyAsync() => SetTtlAsync(null);

    private async Task SetTtlAsync(TimeSpan? ttl)
    {
        if (SelectedKey == null) return;
        IsBusy = true;
        ErrorText = null;
        try
        {
            var ok = await _browser.SetExpireAsync(Profile, SelectedKey.Key, ttl);
            if (!ok)
                ErrorText = "设置 TTL 失败，Key 可能已被删除。";
            await ScanAsyncInternal();
            SelectKey(SelectedKey?.Key ?? "");
            await LoadDetailInternal();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CommandText))
        {
            ErrorText = "请输入 Redis 命令。";
            return;
        }
        IsBusy = true;
        ErrorText = null;
        try
        {
            var output = await _browser.ExecuteCommandAsync(Profile, CommandText);
            CommandOutput = $"> {CommandText}\n{output}\n\n" + CommandOutput;
            StatusText = "命令执行成功。";
            await ScanAsyncInternal();
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            CommandOutput = $"> {CommandText}\n(error) {message}\n\n" + CommandOutput;
            ErrorText = message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearCommandOutput() => CommandOutput = "";

    private async Task ScanAsyncInternal()
    {
        var maxKeys = int.TryParse(MaxKeysText.Trim(), out var parsed) ? Math.Clamp(parsed, 10, 1000) : 200;
        var keys = await _browser.ScanKeysAsync(Profile, Pattern, maxKeys);
        var previousKey = SelectedKey?.Key;
        _suppressSelection = true;
        Keys.Clear();
        foreach (var key in keys) Keys.Add(key);
        _suppressSelection = false;
        if (previousKey != null)
            SelectedKey = Keys.FirstOrDefault(key => key.Key == previousKey);
        StatusText = $"匹配 {keys.Count} 个 key（最多扫描 {maxKeys} 个）。";
    }

    private async Task LoadDetailInternal()
    {
        if (SelectedKey == null)
        {
            Detail = null;
            return;
        }
        try
        {
            var detail = await _browser.GetKeyDetailAsync(Profile, SelectedKey.Key);
            Detail = detail;
            ValueText = detail.ValueText;
            TtlInput = detail.Ttl is { TotalSeconds: > 0 }
                ? ((int)detail.Ttl.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                : "";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Detail = null;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ScanAsyncInternal();
        await _refreshTree();
    }

    /// <summary>从左侧树打开某个 key 时调用：必要时扩大扫描范围并加载详情。</summary>
    public async Task FocusKeyAsync(string key)
    {
        SelectKey(key);
        if (SelectedKey == null)
        {
            Pattern = "*";
            await ScanAsyncInternal();
            SelectKey(key);
        }
        await LoadDetailInternal();
    }
}
