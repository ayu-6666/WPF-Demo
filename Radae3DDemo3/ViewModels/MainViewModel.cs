using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radae3DDemo3.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Radae3DDemo3.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {


        public double ScanAngle
        {
            get { return scanAngle; }
            set 
            {
                SetProperty(ref scanAngle,value);
            }
        }

        private double scanAngle; // 当前扫描角度（0-360°）
        public bool IsScanning
        {
            get { return isScanning; }
            set
            {
                SetProperty(ref isScanning, value);
            }
        }
        private bool isScanning;  // 是否正在扫描
        public ObservableCollection<RadarTarget> Targets
        {
            get { return targets; }
            set
            {
                SetProperty(ref targets, value);
            }
        }
        private ObservableCollection<RadarTarget> targets; // 目标集合
        public RadarTarget SelectedTarget
        {
            get { return selectedTarget; }
            set
            {
                SetProperty(ref selectedTarget, value);
            }
        }
        private RadarTarget selectedTarget; // 选中的目标
        public int RadarRange
        {
            get { return radarRange; }
            set
            {
                SetProperty(ref radarRange, value);
            }
        }
        private int radarRange = 100; // 雷达探测范围（公里）

        private readonly DispatcherTimer _scanTimer;
        private const double CenterX = 400; // 雷达Canvas中心X
        private const double CenterY = 400; // 雷达Canvas中心Y
        private const double PixelsPerKm = 4; // 比例尺：1公里=4像素（100公里对应400像素）
        private const double ScanSpeed = 2; // 每次扫描增加的角度（度）
        private const double ScanLineWidth = 2; // 扫描线探测宽度（度）
        private const double TrailAngle = 30; // 扫描尾迹角度（度）
        private static readonly Random _random = new Random();

        public RelayCommand StartScanCommand { get; set; }
        public RelayCommand StopScanCommand { get; set; }
        public RelayCommand GenerateTargetsCommand { get; set; }
        public RelayCommand ClearTargetsCommand { get; set; }
        public RelayCommand<RadarTarget> SelectTargetCommand { get; set; }


        public MainViewModel()
        {
            ScanAngle = 0;
            IsScanning = false;
            Targets = new ObservableCollection<RadarTarget>();
            GenerateInitialTargets();
            StartScanCommand = new RelayCommand(StartScan);
            StopScanCommand = new RelayCommand(StopScan);
            GenerateTargetsCommand = new RelayCommand(GenerateTargets);
            ClearTargetsCommand = new RelayCommand(ClearTargets);
            SelectTargetCommand = new RelayCommand<RadarTarget>(SelectTarget);
            // 初始化扫描定时器（50ms刷新一次，扫描周期约9秒）
            _scanTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _scanTimer.Tick += ScanTimer_Tick;
        }

        #region 命令
        private void StartScan()
        {
            _scanTimer.Start();
            IsScanning = true;
        }

        private void StopScan()
        {
            _scanTimer.Stop();
            IsScanning = false;
        }


        private void GenerateTargets()
        {
            for (int i = 0; i < 10; i++)
            {
                var target = new RadarTarget();
                target.UpdateScreenPosition(CenterX, CenterY, PixelsPerKm);
                Targets.Add(target);
            }
        }


        private void ClearTargets()
        {
            Targets.Clear();
            SelectedTarget = null;
        }


        private void SelectTarget(RadarTarget target)
        {
            SelectedTarget = target;
        }
        #endregion

        /// <summary>
        /// 生成初始目标
        /// </summary>
        private void GenerateInitialTargets()
        {
            for (int i = 0; i < 20; i++)
            {
                var target = new RadarTarget();
                target.UpdateScreenPosition(CenterX, CenterY, PixelsPerKm);
                Targets.Add(target);
            }
        }

        /// <summary>
        /// 扫描定时器回调
        /// </summary>
        private void ScanTimer_Tick(object sender, EventArgs e)
        {
            // 更新扫描角度
            ScanAngle += ScanSpeed;
            if (ScanAngle >= 360) ScanAngle = 0;

            // 执行目标探测
            DetectTargets();

            // 更新所有目标位置（模拟目标移动）
            foreach (var target in Targets)
            {
                // 计算时间步长内的移动距离（速度×时间）
                var moveDistance = target.Speed * 50 / 3600.0; // 50ms对应的小时数
                target.Distance += moveDistance;

                // 超出探测范围则重置
                if (target.Distance > RadarRange)
                {
                    target.Distance = RadarRange;
                    target.Azimuth = _random.Next(0, 360);
                    target.Speed = _random.Next(100, 1200);
                    target.IsDetected = false;
                }

                target.UpdateScreenPosition(CenterX, CenterY, PixelsPerKm);
            }
        }

        /// <summary>
        /// 目标探测逻辑：判断目标是否在扫描线范围内
        /// </summary>
        private void DetectTargets()
        {
            foreach (var target in Targets)
            {
                // 计算目标方位角与扫描角度的差值（处理360°边界）
                var angleDiff = Math.Abs(target.Azimuth - ScanAngle);
                if (angleDiff > 180) angleDiff = 360 - angleDiff;

                // 差值在扫描线宽度内则标记为已探测
                if (angleDiff <= ScanLineWidth)
                {
                    target.IsDetected = true;
                    target.LastDetectedTime = DateTime.Now;
                }
            }
        }
    }
}
