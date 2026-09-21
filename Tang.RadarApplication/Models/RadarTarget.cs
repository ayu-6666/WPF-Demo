using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Tang.RadarApplication.Models
{
    public partial class RadarTarget : ObservableObject
    {
        [ObservableProperty] private string id = string.Empty;
        [ObservableProperty] private double distanceKm;
        [ObservableProperty] private double azimuthDegree;
        [ObservableProperty] private double speedKmh;
        [ObservableProperty] private DateTime lastSeenUtc;
        public double X => DistanceKm * Math.Sin(AzimuthDegree * Math.PI / 180.0);
        public double Y => -DistanceKm * Math.Cos(AzimuthDegree * Math.PI / 180.0);
        partial void OnDistanceKmChanged(double value) { OnPropertyChanged(nameof(X)); OnPropertyChanged(nameof(Y)); }
        partial void OnAzimuthDegreeChanged(double value) { OnPropertyChanged(nameof(X)); OnPropertyChanged(nameof(Y)); }
    }
}
