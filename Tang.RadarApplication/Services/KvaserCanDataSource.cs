using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.Services
{
    /// <summary>
    /// Kvaser CANlib adapter boundary. The vendor CANlib SDK is intentionally not bundled.
    /// Install Kvaser CANlib from Kvaser, then replace ReadFramesAsync with the SDK calls for
    /// the exact card/channel and protocol. This demo accepts 0x180..0x27F frames:
    /// bytes 0-1 distance in 0.01 km, bytes 2-3 azimuth in 0.1 degree, bytes 4-5 speed km/h.
    /// </summary>
    public sealed class KvaserCanDataSource : IRadarDataSource
    {
        private CancellationTokenSource? cts;
        public string Name => "Kvaser CAN 卡";
        public event EventHandler<RadarTarget>? TargetReceived;
        public Task StartAsync(CancellationToken token)
        {
            var dll = Environment.GetEnvironmentVariable("KVASER_CAN_DLL") ?? "canlib32.dll";
            if (!File.Exists(dll) && !CanLoadNativeLibrary(dll)) throw new InvalidOperationException("未检测到 Kvaser CANlib：" + dll);
            cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            // Hardware integration is isolated here; use the Kvaser SDK's canOpenChannel/canReadWait.
            // Keeping this explicit prevents accidentally sending or receiving unknown CAN traffic.
            throw new NotSupportedException("请在 KvaserCanDataSource.ReadFramesAsync 中按设备协议接入 CANlib。");
        }
        private static bool CanLoadNativeLibrary(string name) { try { return NativeMethods.LoadLibrary(name) != IntPtr.Zero; } catch { return false; } }
        private async Task ReadFramesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Replace this block with canReadWait(channel,...), then call Publish(frame).
                await Task.Delay(10, token).ConfigureAwait(false);
            }
        }
        public void Publish(CanFrame frame)
        {
            if (frame.Id < 0x180 || frame.Id > 0x27F || frame.Data.Length < 6) return;
            var d = frame.Data;
            TargetReceived?.Invoke(this, new RadarTarget { Id = "CAN-" + (frame.Id - 0x180).ToString("X2"), DistanceKm = BitConverter.ToUInt16(d, 0) / 100.0, AzimuthDegree = BitConverter.ToUInt16(d, 2) / 10.0, SpeedKmh = BitConverter.ToUInt16(d, 4), LastSeenUtc = frame.TimestampUtc });
        }
        public Task StopAsync() { cts?.Cancel(); return Task.CompletedTask; }
        public void Dispose() { cts?.Cancel(); cts?.Dispose(); }
        private static class NativeMethods { [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)] public static extern IntPtr LoadLibrary(string lpFileName); }
    }
}
