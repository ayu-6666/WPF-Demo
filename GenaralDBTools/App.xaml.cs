using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GenaralDBTools.Services;
using GenaralDBTools.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GenaralDBTools;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider, SqliteDatabaseProvider>();
        services.AddSingleton<IDatabaseProvider, MySqlDatabaseProvider>();
        services.AddSingleton<IDatabaseProvider, OracleDatabaseProvider>();
        services.AddSingleton<IDatabaseProvider, RedisDatabaseProvider>();
        services.AddSingleton<IDatabaseProvider, MongoDatabaseProvider>();
        services.AddSingleton<DatabaseProviderRegistry>();
        services.AddSingleton<ConnectionStore>();
        services.AddSingleton<MainViewModel>();
        Services = services.BuildServiceProvider();

        // 兜底：任何未被局部处理的异常都要让用户看到，而不是静默吞掉。
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        SafeError("界面线程发生未处理异常", e.Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        SafeError("后台任务发生未处理异常", e.Exception);
    }

    private static void SafeError(string title, Exception exception)
    {
        try
        {
            DialogService.Error(title, exception);
        }
        catch
        {
            // 连弹框都失败时退回系统对话框，保证异常信息不会被静默丢弃。
            MessageBox.Show($"{title}\n\n{exception}", "GenaralDBTools", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Services?.Dispose();
        }
        catch (Exception)
        {
            // 退出阶段释放失败不再影响进程结束。
        }
        base.OnExit(e);
    }
}
