using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Tang.RadarApplication.Models;

namespace Tang.RadarApplication.ViewModels
{
    public class ChartWinViewModel : ObservableObject
    {
        public ObservableCollection<RadarTarget> Targets { get; }

        private RadarTarget? _selectedTarget;
        public RadarTarget? SelectedTarget
        {
            get => _selectedTarget;
            set => SetProperty(ref _selectedTarget, value);
        }

        private readonly Dictionary<string, List<double>> _distanceSeries = new();
        private readonly Dictionary<string, List<double>> _speedSeries = new();
        private readonly Dictionary<string, List<double>> _azimuthSeries = new();
        private readonly List<double> _time = new();
        private DateTime _start = DateTime.UtcNow;

        public event EventHandler? SeriesUpdated;

        public ChartWinViewModel(ObservableCollection<RadarTarget>? targets)
        {
            Targets = targets ?? new ObservableCollection<RadarTarget>();
            foreach (var t in Targets) TrackTarget(t);
            Targets.CollectionChanged += Targets_CollectionChanged;
        }

        private void Targets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (RadarTarget t in e.NewItems)
                {
                    TrackTarget(t);
                }
            }
        }

        private void TrackTarget(RadarTarget t)
        {
            if (t == null) return;
            if (!_distanceSeries.ContainsKey(t.Id)) _distanceSeries[t.Id] = new List<double>();
            if (!_speedSeries.ContainsKey(t.Id)) _speedSeries[t.Id] = new List<double>();
            t.PropertyChanged += Target_PropertyChanged;
        }

        private void Target_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not RadarTarget t) return;
            var now = (DateTime.UtcNow - _start).TotalSeconds;
            _time.Add(now);
            if (!_distanceSeries.ContainsKey(t.Id)) _distanceSeries[t.Id] = new List<double>();
            if (!_speedSeries.ContainsKey(t.Id)) _speedSeries[t.Id] = new List<double>();
            if (!_azimuthSeries.ContainsKey(t.Id)) _azimuthSeries[t.Id] = new List<double>();
            _distanceSeries[t.Id].Add(t.DistanceKm);
            _speedSeries[t.Id].Add(t.SpeedKmh);
            _azimuthSeries[t.Id].Add(t.AzimuthDegree);
            SeriesUpdated?.Invoke(this, EventArgs.Empty);
        }

        public double[] GetTime() => _time.ToArray();
        public double[] GetDistance(string id) => _distanceSeries.TryGetValue(id, out var l) ? l.ToArray() : Array.Empty<double>();
        public double[] GetSpeed(string id) => _speedSeries.TryGetValue(id, out var l) ? l.ToArray() : Array.Empty<double>();
        public double[] GetAzimuth(string id) => _azimuthSeries.TryGetValue(id, out var l) ? l.ToArray() : Array.Empty<double>();
    }
}
