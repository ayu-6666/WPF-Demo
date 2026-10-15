using BitTomrrorApp.Models;
using BitTomrrorApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BitTomrrorApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public ObservableCollection<TorrentItem> TorrentItems => TorrentEngineService.Instance.TorrentItems;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private async Task AddTorrentAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            try
            {
                await TorrentEngineService.Instance.AddTorrentAsync(input.Trim());
                MessageBox.Show("任务添加成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "download-error.log");
                try
                {
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:O}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
                }
                catch
                {
                }

                MessageBox.Show($"添加失败：{ex.GetBaseException().Message}\n详细异常已写入：{logPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task LoadedAsync( )
        {
            string baseurl = "https://github.com/ayu-6666/WPF-Demo";
            for (int i = 10; i >= 0; i--)
            {
                await TorrentEngineService.Instance.AddTorrentAsync(baseurl + $"_{i}.mp4");
            }
        }


        [RelayCommand]
        private async Task PauseAsync(TorrentItem item)
        {
            await TorrentEngineService.Instance.PauseTorrentAsync(item);
        }

        [RelayCommand]
        private async Task DeleteAsync(TorrentItem item)
        {
            var result = MessageBox.Show($"确定要删除「{item.Name}」吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await TorrentEngineService.Instance.DeleteTorrentAsync(item);
            }
        }

        // 使用 IList 以兼容 ListView.SelectedItems（SelectedItemCollection）
        [RelayCommand]
        private async Task BatchPauseAsync(IList items)
        {
            var list = items?.OfType<TorrentItem>().ToList();
            if (list == null || !list.Any()) return;
            await TorrentEngineService.Instance.BatchPauseAsync(list);
        }

        [RelayCommand]
        private async Task BatchDeleteAsync(IList items)
        {
            var list = items?.OfType<TorrentItem>().ToList();
            if (list == null || !list.Any()) return;
            var result = MessageBox.Show($"确定要删除选中的{list.Count}个任务吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await TorrentEngineService.Instance.BatchDeleteAsync(list);
            }
        }
    }
}
