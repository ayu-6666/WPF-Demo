using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GenaralDBTools.ViewModels;

namespace GenaralDBTools.Views;

public partial class SqlEditorView : UserControl
{
    private SqlTab? _tab;
    private bool _syncing;

    public SqlEditorView()
    {
        InitializeComponent();
        // AvalonEdit 的 Text 不是依赖属性，直接做 WPF 绑定会触发回写递归（StackOverflow）。
        // 这里改为显式同步：初始化/ViewModel 变化时写入编辑器，用户编辑时回写 ViewModel。
        DataContextChanged += (_, _) => AttachTab(DataContext as SqlTab);
        Editor.TextChanged += (_, _) =>
        {
            if (_syncing || _tab is null) return;
            if (_tab.Sql != Editor.Text) _tab.Sql = Editor.Text;
        };
    }

    private void AttachTab(SqlTab? tab)
    {
        if (_tab is not null)
            _tab.PropertyChanged -= OnTabPropertyChanged;

        _tab = tab;
        if (_tab is null) return;

        _tab.PropertyChanged += OnTabPropertyChanged;
        _syncing = true;
        Editor.Text = _tab.Sql;
        _syncing = false;
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SqlTab.Sql) || _tab is null) return;
        if (Editor.Text == _tab.Sql) return;

        _syncing = true;
        Editor.Text = _tab.Sql;
        _syncing = false;
    }

    private void ClearResult_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SqlTab tab) return;
        tab.ResetResult();
        tab.ResultSummary = "尚未执行 SQL。";
    }
}
