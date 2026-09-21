using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using Tang.RadarApplication.Models;
using Tang.RadarApplication.ViewModels;
// ScottPlot optional at runtime; do not require compile-time dependency here.

namespace Tang.RadarApplication
{
    public partial class ChartWindow : Window
    {
        private readonly ChartWinViewModel _vm;

        public ChartWindow(ObservableCollection<RadarTarget> targets)
        {
            InitializeComponent();
            _vm = new ViewModels.ChartWinViewModel(targets);
            DataContext = _vm;
            TargetSelector.ItemsSource = _vm.Targets;
            _vm.SeriesUpdated += (_, _) => Dispatcher.Invoke(UpdatePlot);
            // bind selection change in code-behind to trigger plot update as fallback
            TargetSelector.SelectionChanged += (s, e) => UpdatePlot();
            CompositionTargetRendering();
        }

        // Chart data and updates are handled by ChartWinViewModel. ChartWindow subscribes to SeriesUpdated and updates view.

        private void UpdatePlot()
        {
            // try to find ScottPlot WpfPlot control by name; if not present, do nothing
            // plot three charts: distance, speed, azimuth
            var plotDistanceObj = this.FindName("wpfPlotDistance");
            var plotSpeedObj = this.FindName("wpfPlotSpeed");
            var plotAzObj = this.FindName("wpfPlotAzimuth");
            if (plotDistanceObj == null && plotSpeedObj == null && plotAzObj == null) return;
            if (!(_vm.SelectedTarget is RadarTarget sel)) return;
            var x = _vm.GetTime();
            var yd = _vm.GetDistance(sel.Id);
            var ys = _vm.GetSpeed(sel.Id);
            var ya = _vm.GetAzimuth(sel.Id);
            if (x == null || yd == null || yd.Length == 0) return;

            try
            {
                // obtain Plot property (ScottPlot.Plot)
                // helper to update one plot object
                void UpdateOnePlot(object? plotControl, double[] ydata, string title, string yLabel)
                {
                    if (plotControl == null) return;
                    try
                    {
                        var plotProp = plotControl.GetType().GetProperty("Plot", BindingFlags.Public | BindingFlags.Instance);
                        if (plotProp == null) return;
                        var plot = plotProp.GetValue(plotControl);
                        if (plot == null) return;

                        var clearM = plot.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);
                        clearM?.Invoke(plot, null);

                        MethodInfo? addScatter = null;
                        foreach (var mi in plot.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (mi.Name == "AddScatter")
                            {
                                var pars = mi.GetParameters();
                                if (pars.Length >= 2)
                                {
                                    addScatter = mi;
                                    break;
                                }
                            }
                        }
                        addScatter?.Invoke(plot, new object[] { x, ydata });

                        var titleM = plot.GetType().GetMethod("Title", BindingFlags.Public | BindingFlags.Instance);
                        titleM?.Invoke(plot, new object[] { title });
                        var xlabelM = plot.GetType().GetMethod("XLabel", BindingFlags.Public | BindingFlags.Instance);
                        xlabelM?.Invoke(plot, new object[] { "t (s)" });
                        var ylabelM = plot.GetType().GetMethod("YLabel", BindingFlags.Public | BindingFlags.Instance);
                        ylabelM?.Invoke(plot, new object[] { yLabel });

                        var legendM = plot.GetType().GetMethod("Legend", BindingFlags.Public | BindingFlags.Instance);
                        legendM?.Invoke(plot, null);

                        var renderM = plotControl.GetType().GetMethod("Render", BindingFlags.Public | BindingFlags.Instance);
                        if (renderM != null)
                        {
                            var parms = renderM.GetParameters();
                            if (parms.Length == 0) renderM.Invoke(plotControl, null);
                            else if (parms.Length == 1) renderM.Invoke(plotControl, new object[] { 1.0 });
                        }
                    }
                    catch { }
                }

                UpdateOnePlot(plotDistanceObj, yd, $"Target {sel.Id} Distance (km)", "Distance (km)");
                UpdateOnePlot(plotSpeedObj, ys, $"Target {sel.Id} Speed (km/h)", "Speed (km/h)");
                UpdateOnePlot(plotAzObj, ya, $"Target {sel.Id} Azimuth (deg)", "Azimuth (deg)");
            }
            catch
            {
                // ignore plotting errors at runtime when ScottPlot API differs
            }
        }

        private void TargetSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdatePlot();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void CompositionTargetRendering()
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) =>
            {
                // tick to keep time axis updated
                if (TargetSelector.SelectedItem is RadarTarget sel)
                {
                    var data = _vm.GetDistance(sel.Id);
                    if (data != null && data.Length > 0)
                    {
                        UpdatePlot();
                    }
                }
            };
            timer.Start();
        }
    }
}
