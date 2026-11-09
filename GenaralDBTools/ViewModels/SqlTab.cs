using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GenaralDBTools.ViewModels;

public partial class SqlTab : WorkspaceTab
{
    [ObservableProperty] private string sql = "";
    [ObservableProperty] private DataView? resultData;
    [ObservableProperty] private bool hasResult;
    [ObservableProperty] private string resultSummary = "尚未执行 SQL。";
    [ObservableProperty] private string outputText = "";
    [ObservableProperty] private string? errorText;
    [ObservableProperty] private string targetConnection = "未选择连接";

    public SqlTab()
    {
        Title = "SQL 编辑器";
        Sql = "SELECT 1;";
    }

    public void ResetResult()
    {
        ResultData = null;
        HasResult = false;
    }
}
