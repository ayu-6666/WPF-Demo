using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Tang.RadarApplication.Models;
using Tang.RadarApplication.Services;

namespace Tang.RadarApplication.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IRadarDataSource source;
        private readonly OpenAiAnalysisService ai;
        // sweep is handled by RadarScope control; ViewModel only exposes IsScanning state
        [ObservableProperty] private bool isScanning;
        private CancellationTokenSource? cts;
        [ObservableProperty] private double scanAngle;
        [ObservableProperty] private double rangeKm = 100;
        [ObservableProperty] private string statusText = "待机";
        [ObservableProperty] private string dataSourceText = "未连接";
        [ObservableProperty] private string analysisText = "提示：没有 Kvaser CANlib 时会自动进入模拟模式。";
        public ObservableCollection<RadarTarget> Targets { get; } = new ObservableCollection<RadarTarget>();
        public MainViewModel(IRadarDataSource source, OpenAiAnalysisService ai)
        {
            this.source = source; this.ai = ai; source.TargetReceived += OnTarget;
        }
        [RelayCommand] private async Task StartAsync()
        {
            if (cts != null) return;
            cts = new CancellationTokenSource();
            StatusText = "扫描中";
            // start scanning state immediately so UI and control react while data source starts
            IsScanning = true;
            try { await source.StartAsync(cts.Token); } catch (OperationCanceledException) { } catch (Exception ex) { StatusText = "启动失败：" + ex.Message; IsScanning = false; }
            DataSourceText = source.Name;
        }
        [RelayCommand] private async Task StopAsync()
        {
            // stop scanning immediately for UI responsiveness
            IsScanning = false;
            try
            {
                cts?.Cancel();
                cts = null;
                await source.StopAsync();
            }
            catch { }
            StatusText = "已停止";
        }
        [RelayCommand] private void Clear()
        {
            IsScanning = false;
            Targets.Clear();
        }
        [RelayCommand] private async Task AnalyzeAsync() { AnalysisText = await ai.AnalyzeAsync(Targets.ToArray()); }
        private void OnTarget(object? sender, RadarTarget target)
        {
            // update UI list whenever a target arrives; source.Start/Stop controls whether events are emitted
            App.Current.Dispatcher.Invoke(() =>
            {
                var old = Targets.FirstOrDefault(x => x.Id == target.Id);
                if (old == null) Targets.Add(target);
                else
                {
                    old.DistanceKm = target.DistanceKm;
                    old.AzimuthDegree = target.AzimuthDegree;
                    old.SpeedKmh = target.SpeedKmh;
                    old.LastSeenUtc = target.LastSeenUtc;
                }
            });
        }
    }
}
