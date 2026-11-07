using BitTomrrorApp.Models;

namespace BitTomrrorApp.Services;

public interface IDownloadService
{
    DownloadType Type { get; }
    Task StartAsync(DownloadTask task);
    Task PauseAsync(DownloadTask task);
    Task ResumeAsync(DownloadTask task);
    Task RemoveAsync(DownloadTask task);
    void Tick();
    Task ShutdownAsync();
}
