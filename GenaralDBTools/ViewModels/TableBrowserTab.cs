using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenaralDBTools.Models;
using GenaralDBTools.Services;

namespace GenaralDBTools.ViewModels;

public partial class TableBrowserTab : WorkspaceTab
{
    private readonly IRelationalTableProvider _provider;
    private readonly Func<Task> _refreshTree;
    private readonly Func<string, bool, Task> _openTable;
    private readonly Action<WorkspaceTab> _closeTab;

    public ConnectionProfile Profile { get; }
    public string? SchemaName { get; }
    public string TableName { get; }
    public bool IsView { get; }
    public string TableKey => $"{Profile.Id}|{SchemaName ?? ""}|{TableName}";

    public ObservableCollection<ColumnInfo> Columns { get; } = new();
    public int[] PageSizeOptions { get; } = { 20, 50, 100, 200 };

    [ObservableProperty] private DataView? rows;
    [ObservableProperty] private object? selectedRow;
    [ObservableProperty] private ColumnInfo? selectedColumn;
    [ObservableProperty] private long totalRows;
    [ObservableProperty] private int pageIndex = 1;
    [ObservableProperty] private int pageSize = 50;
    [ObservableProperty] private long totalPages = 1;
    [ObservableProperty] private string pageInput = "1";
    [ObservableProperty] private string filterText = "";
    [ObservableProperty] private string statusText = "尚未加载数据。";
    [ObservableProperty] private string? errorText;

    private bool _ready;

    public TableBrowserTab(IRelationalTableProvider provider, ConnectionProfile profile, string? schema,
        string table, bool isView, Func<Task> refreshTree, Func<string, bool, Task> openTable, Action<WorkspaceTab> closeTab)
    {
        _provider = provider;
        Profile = profile;
        SchemaName = schema;
        TableName = table;
        IsView = isView;
        _refreshTree = refreshTree;
        _openTable = openTable;
        _closeTab = closeTab;

        Title = $"{table} · {profile.Name}";
        _ready = true;
        _ = RefreshAsync();
    }

    public bool HasPrimaryKey => Columns.Any(column => column.IsPrimaryKey);
    public bool CanEditRows => !IsView && HasPrimaryKey;

    partial void OnPageIndexChanged(int value) => OnPropertyChanged(nameof(PageLabel));
    partial void OnTotalPagesChanged(long value) => OnPropertyChanged(nameof(PageLabel));
    public string PageLabel => $"{PageIndex} / {TotalPages}";

    partial void OnPageSizeChanged(int value)
    {
        if (!_ready) return;
        PageIndex = 1;
        _ = RefreshAsync();
    }

    partial void OnSelectedColumnChanged(ColumnInfo? value)
    {
        // 仅用于视图中的字段详情联动。
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorText = null;
        try
        {
            var offset = Math.Max(0, (PageIndex - 1) * PageSize);
            var page = await _provider.LoadTablePageAsync(Profile, SchemaName, TableName, IsView,
                FilterText, offset, PageSize);
            TotalRows = page.TotalRows;
            TotalPages = Math.Max(1, (TotalRows + PageSize - 1) / PageSize);
            if (PageIndex > TotalPages)
            {
                PageIndex = (int)TotalPages;
                IsBusy = false;
                await RefreshAsync();
                return;
            }

            if (Columns.Count != page.Columns.Count || !Columns.SequenceEqual(page.Columns))
            {
                Columns.Clear();
                foreach (var column in page.Columns) Columns.Add(column);
                OnPropertyChanged(nameof(HasPrimaryKey));
                OnPropertyChanged(nameof(CanEditRows));
            }

            Rows = page.Table.DefaultView;
            var from = page.Table.Rows.Count == 0 ? 0 : offset + 1;
            var to = Math.Min(offset + page.Table.Rows.Count, TotalRows);
            StatusText = TotalRows == 0
                ? "该查询没有数据。"
                : $"显示第 {from}-{to} 行 / 共 {TotalRows} 行 · {page.Columns.Count} 列";
            PageInput = PageIndex.ToString();
        }
        catch (Exception ex)
        {
            ErrorText = ErrorTextOf(ex);
            StatusText = "加载失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task FirstPageAsync()
    {
        if (PageIndex == 1) return Task.CompletedTask;
        PageIndex = 1;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task PreviousPageAsync()
    {
        if (PageIndex <= 1) return Task.CompletedTask;
        PageIndex--;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task NextPageAsync()
    {
        if (PageIndex >= TotalPages) return Task.CompletedTask;
        PageIndex++;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task LastPageAsync()
    {
        if (PageIndex == TotalPages) return Task.CompletedTask;
        PageIndex = (int)TotalPages;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task GoToPageAsync()
    {
        if (int.TryParse(PageInput.Trim(), out var page) && page >= 1 && page <= TotalPages)
        {
            PageIndex = page;
            return RefreshAsync();
        }
        ErrorText = $"页码必须是 1 - {TotalPages} 之间的整数。";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ApplyFilterAsync()
    {
        PageIndex = 1;
        return RefreshAsync();
    }

    [RelayCommand]
    private Task ClearFilterAsync()
    {
        FilterText = "";
        PageIndex = 1;
        return RefreshAsync();
    }

    [RelayCommand]
    private async Task InsertRowAsync()
    {
        if (!EnsureEditable()) return;
        var input = DialogService.EditRow($"新增记录 · {TableName}", Columns, null, insertMode: true);
        if (input == null) return;

        await RunWriteAsync("新增记录", () => _provider.InsertRowAsync(Profile, SchemaName, TableName, input));
    }

    [RelayCommand]
    private async Task EditRowAsync()
    {
        if (!EnsureEditable()) return;
        if (SelectedRow is not DataRowView rowView)
        {
            ErrorText = "请先在数据表格中选择要修改的行。";
            return;
        }

        var initial = Columns.ToDictionary(column => column.Name, column => ReadValue(rowView, column.Name));
        var input = DialogService.EditRow($"修改记录 · {TableName}", Columns, initial, insertMode: false);
        if (input == null) return;

        var keys = BuildPrimaryKey(rowView);
        var values = input
            .Where(pair => !keys.ContainsKey(pair.Key))
            .Where(pair => Columns.FirstOrDefault(c => c.Name == pair.Key) is { IsBinary: false })
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        if (values.Count == 0)
        {
            ErrorText = "没有可更新的字段（主键与二进制列不可修改）。";
            return;
        }

        await RunWriteAsync("修改记录", () => _provider.UpdateRowAsync(Profile, SchemaName, TableName, values, keys));
    }

    [RelayCommand]
    private async Task DeleteRowAsync()
    {
        if (!EnsureEditable()) return;
        if (SelectedRow is not DataRowView rowView)
        {
            ErrorText = "请先在数据表格中选择要删除的行。";
            return;
        }

        var keys = BuildPrimaryKey(rowView);
        var describe = string.Join(", ", keys.Select(pair => $"{pair.Key} = {DisplayValue(pair.Value)}"));
        if (!DialogService.Confirm("删除记录", $"确定删除该记录吗？\n{describe}", "删除后无法恢复。", "删除"))
            return;

        await RunWriteAsync("删除记录", () => _provider.DeleteRowAsync(Profile, SchemaName, TableName, keys));
    }

    [RelayCommand]
    private async Task CreateTableAsync()
    {
        var request = DialogService.CreateTable(Profile.Kind, SchemaName ?? Profile.Database);
        if (request == null) return;

        IsBusy = true;
        ErrorText = null;
        try
        {
            await _provider.CreateTableAsync(Profile, SchemaName, request);
            StatusText = $"表 {request.TableName} 创建成功。";
            await _refreshTree();
            await _openTable(request.TableName, false);
        }
        catch (Exception ex)
        {
            ErrorText = ErrorTextOf(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DropTableAsync()
    {
        if (IsView)
        {
            ErrorText = "视图不能通过该操作删除，请执行 DROP VIEW。";
            return;
        }
        if (!DialogService.Confirm("删除表", $"确定删除表 “{TableName}” 吗？", "该操作会删除表结构与全部数据，且无法恢复。", "删除表"))
            return;

        IsBusy = true;
        ErrorText = null;
        try
        {
            await _provider.DropTableAsync(Profile, SchemaName, TableName);
            await _refreshTree();
            _closeTab(this);
        }
        catch (Exception ex)
        {
            ErrorText = ErrorTextOf(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsureEditable()
    {
        if (IsView)
        {
            ErrorText = "视图为只读对象，不能进行行级增删改。";
            return false;
        }
        if (!HasPrimaryKey)
        {
            ErrorText = "该表没有主键，无法安全定位记录，行级增删改已禁用。可通过 SQL 编辑器手动处理。";
            return false;
        }
        if (IsBusy)
        {
            ErrorText = "上一个操作仍在执行，请稍候。";
            return false;
        }
        return true;
    }

    private async Task RunWriteAsync(string caption, Func<Task<int>> action)
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            var affected = await action();
            if (affected <= 0)
                ErrorText = $"{caption}未影响任何行，记录可能已被其他客户端修改或删除。";
            else
                StatusText = $"{caption}成功，影响 {affected} 行。";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ErrorText = ErrorTextOf(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Dictionary<string, object?> BuildPrimaryKey(DataRowView rowView)
    {
        var keys = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in Columns.Where(column => column.IsPrimaryKey))
            keys[column.Name] = ReadValue(rowView, column.Name);
        return keys;
    }

    private static object? ReadValue(DataRowView rowView, string columnName)
    {
        var value = rowView.Row.Table.Columns.Contains(columnName) ? rowView.Row[columnName] : null;
        return value is DBNull ? null : value;
    }

    private static string DisplayValue(object? value) => value switch
    {
        null => "NULL",
        byte[] bytes => $"(二进制 {bytes.Length} 字节)",
        _ => value.ToString() ?? ""
    };

    private static string ErrorTextOf(Exception ex) =>
        ex.InnerException != null && ex.Message.Contains("See inner exception", StringComparison.OrdinalIgnoreCase)
            ? $"{ex.Message}\n{ex.InnerException.Message}"
            : ex.Message;
}
