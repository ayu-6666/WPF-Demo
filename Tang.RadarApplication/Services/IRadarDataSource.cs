using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.Services
{
    public interface IRadarDataSource : IDisposable
    {
        string Name { get; }
        event EventHandler<RadarTarget>? TargetReceived;
        Task StartAsync(CancellationToken token);
        Task StopAsync();
    }

    public sealed class SwitchingRadarDataSource : IRadarDataSource
    {
        private readonly IRadarDataSource can;
        private readonly IRadarDataSource simulation;
        private IRadarDataSource active;
        public string Name => active.Name;
        public event EventHandler<RadarTarget>? TargetReceived;
        public SwitchingRadarDataSource(IRadarDataSource can, IRadarDataSource simulation)
        {
            this.can = can; this.simulation = simulation; active = can;
            can.TargetReceived += Forward; simulation.TargetReceived += Forward;
        }
        private void Forward(object? sender, RadarTarget e) => TargetReceived?.Invoke(this, e);
        public async Task StartAsync(CancellationToken token)
        {
            try { await active.StartAsync(token).ConfigureAwait(false); }
            catch { active = simulation; await active.StartAsync(token).ConfigureAwait(false); }
        }
        public Task StopAsync() => active.StopAsync();
        public void Dispose() { can.Dispose(); simulation.Dispose(); }
    }
}
