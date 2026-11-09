using System.IO;
using System.Windows;
using System.Windows.Media;
using GenaralDBTools.Models;
using GenaralDBTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace GenaralDBTools.Dialogs;

public sealed record KindOption(DatabaseKind Kind, string DisplayName);

public partial class ConnectionDialog : Window
{
    public ConnectionProfile? Profile { get; private set; }
    private readonly DatabaseProviderRegistry _registry;
    private readonly string? _editingId;
    private bool _loading = true;
    private DatabaseKind _lastKind = DatabaseKind.Sqlite;

    public ConnectionDialog(ConnectionProfile? source = null)
    {
        InitializeComponent();
        _registry = App.Services.GetRequiredService<DatabaseProviderRegistry>();
        KindBox.ItemsSource = new[]
        {
            new KindOption(DatabaseKind.MySql, "MySQL"),
            new KindOption(DatabaseKind.Sqlite, "SQLite"),
            new KindOption(DatabaseKind.Redis, "Redis"),
            new KindOption(DatabaseKind.Oracle, "Oracle"),
            new KindOption(DatabaseKind.MongoDb, "MongoDB")
        };

        if (source == null)
        {
            SelectKind(DatabaseKind.Sqlite);
            NameBox.Text = "新建连接";
            HostBox.Text = "localhost";
            FileBox.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GenaralDBTools", "data", "GenaralDBTools.db");
            _lastKind = DatabaseKind.Sqlite;
        }
        else
        {
            _editingId = source.Id;
            NameBox.Text = source.Name;
            SelectKind(source.Kind);
            _lastKind = source.Kind;
            HostBox.Text = source.Host;
            PortBox.Text = source.Port.ToString();
            DatabaseBox.Text = source.Database;
            UserBox.Text = source.Username;
            PasswordBox.Password = source.Password;
            FileBox.Text = source.FilePath;
            SslBox.IsChecked = source.UseSsl;
        }

        _loading = false;
        ApplyKindVisibility(setDefaultPort: false);
    }

    private void SelectKind(DatabaseKind kind)
    {
        foreach (var option in (KindOption[])KindBox.ItemsSource)
        {
            if (option.Kind == kind)
            {
                KindBox.SelectedItem = option;
                return;
            }
        }
    }

    private DatabaseKind SelectedKind =>
        (KindBox.SelectedItem as KindOption)?.Kind ?? DatabaseKind.Sqlite;

    private void KindBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var kind = SelectedKind;
        var previousDefault = ConnectionProfile.DefaultPort(_lastKind);
        var portText = PortBox.Text.Trim();
        if (string.IsNullOrEmpty(portText) || (int.TryParse(portText, out var port) && port == previousDefault))
            PortBox.Text = ConnectionProfile.DefaultPort(kind).ToString();
        _lastKind = kind;
        ApplyKindVisibility(setDefaultPort: false);
    }

    private void ApplyKindVisibility(bool setDefaultPort)
    {
        var kind = SelectedKind;
        var isSqlite = kind == DatabaseKind.Sqlite;

        NetworkPanel.Visibility = isSqlite ? Visibility.Collapsed : Visibility.Visible;
        DatabaseLabel.Visibility = isSqlite ? Visibility.Collapsed : Visibility.Visible;
        DatabaseBox.Visibility = isSqlite ? Visibility.Collapsed : Visibility.Visible;
        FileLabel.Visibility = isSqlite ? Visibility.Visible : Visibility.Collapsed;
        FilePanel.Visibility = isSqlite ? Visibility.Visible : Visibility.Collapsed;

        var showCredentials = !isSqlite;
        UserLabel.Visibility = showCredentials ? Visibility.Visible : Visibility.Collapsed;
        UserBox.Visibility = showCredentials ? Visibility.Visible : Visibility.Collapsed;
        PasswordLabel.Visibility = showCredentials ? Visibility.Visible : Visibility.Collapsed;
        PasswordBox.Visibility = showCredentials ? Visibility.Visible : Visibility.Collapsed;
        UserHint.Visibility = kind == DatabaseKind.Redis ? Visibility.Visible : Visibility.Collapsed;
        SslBox.Visibility = kind is DatabaseKind.MySql or DatabaseKind.Redis or DatabaseKind.MongoDb
            ? Visibility.Visible
            : Visibility.Collapsed;

        DatabaseLabel.Text = kind switch
        {
            DatabaseKind.Oracle => "Service Name / SID",
            DatabaseKind.MySql => "数据库名称（可选；留空则浏览 Schema 列表）",
            DatabaseKind.Redis => "默认数据库编号（0-15，可选）",
            _ => "认证数据库（可选，默认 admin）"
        };
        DatabaseBox.ToolTip = kind == DatabaseKind.Oracle ? "例如 ORCLPDB1" : DatabaseLabel.Text;
        FileLabel.Text = "SQLite 数据库文件";
        HintText.Text = kind switch
        {
            DatabaseKind.Oracle => "Oracle 通过 主机:端口/ServiceName 连接，浏览当前用户下的表与视图。",
            DatabaseKind.MySql => "MySQL 需填写数据库名称；未填写时连接成功后可选择 Schema 浏览。",
            DatabaseKind.Redis => "Redis 用户名可选（ACL），默认端口 6379；TTL、键类型与命令均可在浏览器中操作。",
            DatabaseKind.MongoDb => "MongoDB 使用标准主机/端口连接串，认证数据库一般保持 admin。",
            _ => "SQLite 为本地文件数据库，文件不存在时会自动创建。"
        };

        if (setDefaultPort && !isSqlite)
            PortBox.Text = ConnectionProfile.DefaultPort(kind).ToString();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "选择或新建 SQLite 数据库文件",
            Filter = "SQLite 数据库 (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|所有文件 (*.*)|*.*",
            OverwritePrompt = false,
            CheckPathExists = true,
            FileName = Path.GetFileName(FileBox.Text)
        };
        if (dialog.ShowDialog() == true)
            FileBox.Text = dialog.FileName;
    }

    private ConnectionProfile? BuildProfile(bool requireName, out string? error)
    {
        error = null;
        var name = NameBox.Text.Trim();
        if (requireName && string.IsNullOrWhiteSpace(name))
        {
            error = "请输入连接名称。";
            return null;
        }

        var kind = SelectedKind;
        var host = HostBox.Text.Trim();
        var port = 0;

        if (kind != DatabaseKind.Sqlite)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                error = "请输入主机地址。";
                return null;
            }
            if (!int.TryParse(PortBox.Text.Trim(), out port) || port is < 1 or > 65535)
            {
                error = "端口必须是 1 - 65535 之间的整数。";
                return null;
            }
        }

        if (kind == DatabaseKind.Redis && !string.IsNullOrWhiteSpace(DatabaseBox.Text))
        {
            if (!int.TryParse(DatabaseBox.Text.Trim(), out var index) || index is < 0 or > 15)
            {
                error = "Redis 数据库编号必须是 0 - 15 之间的整数。";
                return null;
            }
        }

        var filePath = FileBox.Text.Trim();
        if (kind == DatabaseKind.Sqlite)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "请选择 SQLite 数据库文件路径。";
                return null;
            }
            filePath = Path.GetFullPath(filePath);
        }

        return new ConnectionProfile
        {
            Id = _editingId ?? Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? "临时连接" : name,
            Kind = kind,
            Host = host,
            Port = port,
            Database = DatabaseBox.Text.Trim(),
            Username = UserBox.Text.Trim(),
            Password = PasswordBox.Password,
            FilePath = filePath,
            UseSsl = SslBox.IsChecked == true
        };
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var profile = BuildProfile(requireName: false, out var error);
        if (profile == null)
        {
            TestResult.Text = error ?? "连接参数不完整。";
            TestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC2626"));
            return;
        }

        TestButton.IsEnabled = false;
        TestResult.Text = "正在测试连接…";
        TestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF64748B"));
        try
        {
            await _registry.Get(profile.Kind).TestConnectionAsync(profile);
            TestResult.Text = "连接成功，可以保存。";
            TestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF16A34A"));
        }
        catch (Exception ex)
        {
            TestResult.Text = $"连接失败：{ex.Message}";
            TestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC2626"));
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var profile = BuildProfile(requireName: true, out var error);
        if (profile == null)
        {
            TestResult.Text = error ?? "连接参数不完整。";
            TestResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDC2626"));
            return;
        }
        Profile = profile;
        DialogResult = true;
    }
}
