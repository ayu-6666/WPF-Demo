using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.Services
{
    public sealed class SimulatedRadarDataSource : IRadarDataSource
    {
        private readonly Random random = new Random();
        private CancellationTokenSource? cts;
        private readonly Dictionary<string, RadarTarget> targets = new Dictionary<string, RadarTarget>();
        public string Name => "模拟数据源";
        public event EventHandler<RadarTarget>? TargetReceived;
        public async Task StartAsync(CancellationToken token)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            for (var i = 1; i <= 8; i++) targets["SIM-" + i.ToString("00")] = new RadarTarget { Id = "SIM-" + i.ToString("00"), DistanceKm = random.Next(5, 96), AzimuthDegree = random.NextDouble() * 360, SpeedKmh = random.Next(0, 180) };
            while (!cts.IsCancellationRequested)
            {
                foreach (var item in targets.Values)
                {
                    item.DistanceKm = Math.Max(1, Math.Min(100, item.DistanceKm + (random.NextDouble() - .5) * 1.2));
                    item.AzimuthDegree = (item.AzimuthDegree + random.NextDouble() * 2 - 1 + 360) % 360;
                    item.SpeedKmh = Math.Max(0, item.SpeedKmh + (random.NextDouble() - .5) * 8);
                    item.LastSeenUtc = DateTime.UtcNow;
                    TargetReceived?.Invoke(this, Clone(item));
                }
                await Task.Delay(250, cts.Token).ConfigureAwait(false);
            }
        }
        private static RadarTarget Clone(RadarTarget x) => new RadarTarget { Id = x.Id, DistanceKm = x.DistanceKm, AzimuthDegree = x.AzimuthDegree, SpeedKmh = x.SpeedKmh, LastSeenUtc = x.LastSeenUtc };
        public Task StopAsync() { cts?.Cancel(); return Task.CompletedTask; }
        public void Dispose() { cts?.Cancel(); cts?.Dispose(); }
    }
}
