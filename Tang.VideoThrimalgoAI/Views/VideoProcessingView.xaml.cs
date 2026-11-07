using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Tang.VideoThrimalgoAI.Models;
using Tang.VideoThrimalgoAI.ViewModels;

namespace Tang.VideoThrimalgoAI.Views
{
    public partial class VideoProcessingView : UserControl
    {
        private readonly VideoProcessingViewModel _viewModel;
        private Point? _dragStart;
        private Rectangle? _dragRectangle;

        public VideoProcessingView()
        {
            InitializeComponent();
            _viewModel = new VideoProcessingViewModel();
            DataContext = _viewModel;

            _viewModel.WatermarkRegions.CollectionChanged += (_, _) => RedrawRegions();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            PreviewHost.SizeChanged += (_, _) => RedrawRegions();
            Loaded += (_, _) =>
            {
                RedrawRegions();
                UpdateCursor();

                // 支持命令行直接打开视频（如：Tang.VideoThrimalgoAI.exe "D:\video.mp4"）
                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1 && System.IO.File.Exists(args[1]))
                    _ = _viewModel.LoadVideoAsync(args[1]);
            };
            Unloaded += (_, _) => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

            RoiCanvas.MouseLeftButtonDown += OnCanvasMouseDown;
            RoiCanvas.MouseMove += OnCanvasMouseMove;
            RoiCanvas.MouseLeftButtonUp += OnCanvasMouseUp;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoProcessingViewModel.ShowWatermark))
                UpdateCursor();
            if (e.PropertyName == nameof(VideoProcessingViewModel.VideoWidth))
                RedrawRegions();
        }

        private void UpdateCursor()
        {
            RoiCanvas.Cursor = _viewModel.ShowWatermark ? Cursors.Cross : Cursors.Arrow;
            if (!_viewModel.ShowWatermark)
                CancelDrag();
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_viewModel.ShowWatermark || _viewModel.VideoWidth <= 0 || _viewModel.VideoHeight <= 0)
                return;

            _dragStart = e.GetPosition(RoiCanvas);
            _dragRectangle = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44)),
                StrokeThickness = 2
            };
            Canvas.SetLeft(_dragRectangle, _dragStart.Value.X);
            Canvas.SetTop(_dragRectangle, _dragStart.Value.Y);
            _dragRectangle.Width = 0;
            _dragRectangle.Height = 0;
            RoiCanvas.Children.Add(_dragRectangle);
            RoiCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStart == null || _dragRectangle == null)
                return;

            var current = e.GetPosition(RoiCanvas);
            var left = Math.Min(_dragStart.Value.X, current.X);
            var top = Math.Min(_dragStart.Value.Y, current.Y);
            Canvas.SetLeft(_dragRectangle, left);
            Canvas.SetTop(_dragRectangle, top);
            _dragRectangle.Width = Math.Abs(current.X - _dragStart.Value.X);
            _dragRectangle.Height = Math.Abs(current.Y - _dragStart.Value.Y);
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragStart == null)
                return;

            var end = e.GetPosition(RoiCanvas);
            var region = MapToVideo(_dragStart.Value, end);
            CancelDrag();

            if (region != null)
                _viewModel.AddRegion(region);

            e.Handled = true;
        }

        private void CancelDrag()
        {
            if (_dragRectangle != null)
                RoiCanvas.Children.Remove(_dragRectangle);

            _dragRectangle = null;
            _dragStart = null;
            if (RoiCanvas.IsMouseCaptured)
                RoiCanvas.ReleaseMouseCapture();
        }

        /// <summary>把预览画布坐标换算为源视频像素坐标（Image 使用 Uniform 缩放并居中）。</summary>
        private WatermarkRegion? MapToVideo(Point start, Point end)
        {
            var canvasWidth = RoiCanvas.ActualWidth;
            var canvasHeight = RoiCanvas.ActualHeight;
            var videoWidth = _viewModel.VideoWidth;
            var videoHeight = _viewModel.VideoHeight;
            if (canvasWidth <= 1 || canvasHeight <= 1 || videoWidth <= 0 || videoHeight <= 0)
                return null;

            var scale = Math.Min(canvasWidth / videoWidth, canvasHeight / videoHeight);
            var offsetX = (canvasWidth - videoWidth * scale) / 2;
            var offsetY = (canvasHeight - videoHeight * scale) / 2;

            var x1 = Math.Clamp((Math.Min(start.X, end.X) - offsetX) / scale, 0, videoWidth);
            var y1 = Math.Clamp((Math.Min(start.Y, end.Y) - offsetY) / scale, 0, videoHeight);
            var x2 = Math.Clamp((Math.Max(start.X, end.X) - offsetX) / scale, 0, videoWidth);
            var y2 = Math.Clamp((Math.Max(start.Y, end.Y) - offsetY) / scale, 0, videoHeight);

            var width = (int)Math.Round(x2 - x1);
            var height = (int)Math.Round(y2 - y1);
            if (width < 4 || height < 4)
                return null;

            return new WatermarkRegion
            {
                X = (int)Math.Round(x1),
                Y = (int)Math.Round(y1),
                Width = width,
                Height = height
            };
        }

        private void RedrawRegions()
        {
            var existing = RoiCanvas.Children.OfType<Rectangle>().ToList();
            foreach (var rectangle in existing)
                RoiCanvas.Children.Remove(rectangle);

            var canvasWidth = RoiCanvas.ActualWidth;
            var canvasHeight = RoiCanvas.ActualHeight;
            var videoWidth = _viewModel.VideoWidth;
            var videoHeight = _viewModel.VideoHeight;
            if (canvasWidth <= 1 || canvasHeight <= 1 || videoWidth <= 0 || videoHeight <= 0)
                return;

            var scale = Math.Min(canvasWidth / videoWidth, canvasHeight / videoHeight);
            var offsetX = (canvasWidth - videoWidth * scale) / 2;
            var offsetY = (canvasHeight - videoHeight * scale) / 2;

            foreach (var region in _viewModel.WatermarkRegions)
            {
                var rectangle = new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    Fill = new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44)),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(rectangle, offsetX + region.X * scale);
                Canvas.SetTop(rectangle, offsetY + region.Y * scale);
                rectangle.Width = region.Width * scale;
                rectangle.Height = region.Height * scale;
                RoiCanvas.Children.Add(rectangle);
            }
        }
    }
}
