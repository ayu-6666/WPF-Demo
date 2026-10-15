using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radae3DDemo3.Models
{
    public partial class RadarTarget : ObservableObject
    {
        public int Id
        {
            get { return id; }
            set
            {
                SetProperty(ref id, value);
            }
        }


        private int id;          // 目标ID
        public double Distance
        {
            get { return distance; }
            set
            {
                SetProperty(ref distance, value);
            }
        }

        private double distance; // 距离雷达站距离（公里）
        public double Azimuth
        {
            get { return azimuth; }
            set
            {
                SetProperty(ref azimuth, value);
            }
        }

        private double azimuth;  // 方位角（度，0°为正北，顺时针增加）
        public double Speed
        {
            get { return speed; }
            set
            {
                SetProperty(ref speed, value);
            }
        }

        private double speed;    // 飞行速度（公里/小时）
        public double Altitude
        {
            get { return altitude; }
            set
            {
                SetProperty(ref altitude, value);
            }
        }

        private double altitude; // 飞行高度（米）
        public bool IsDetected
        {
            get { return isDetected; }
            set
            {
                SetProperty(ref isDetected, value);
            }
        }

        private bool isDetected; // 是否被探测到
        public DateTime LastDetectedTime
        {
            get { return lastDetectedTime; }
            set
            {
                SetProperty(ref lastDetectedTime, value);
            }
        }

        private DateTime lastDetectedTime; // 最后探测时间
        public double X
        {
            get { return x; }
            set
            {
                SetProperty(ref x, value);
            }
        }

        private double x;        // 屏幕X坐标（Canvas坐标系）
        public double Y
        {
            get { return y; }
            set
            {
                SetProperty(ref y, value);
            }
        }

        private double y;        // 屏幕Y坐标（Canvas坐标系）

        private static readonly Random _random = new Random();

        public RadarTarget()
        {
            // 初始化随机目标
            Id = _random.Next(1000, 9999);
            Distance = _random.Next(1, 100); // 1-100公里范围
            Azimuth = _random.Next(0, 360);
            Speed = _random.Next(100, 1200); // 100-1200公里/小时
            Altitude = _random.Next(500, 15000); // 500-15000米
            IsDetected = false;
            LastDetectedTime = DateTime.MinValue;
        }

        /// <summary>
        /// 更新目标的屏幕坐标（极坐标转直角坐标）
        /// </summary>
        public void UpdateScreenPosition(double centerX, double centerY, double pixelsPerKm)
        {
            // 雷达方位角0°为正北（Canvas Y轴向下，因此Y坐标需减去距离分量）
            var angleRad = Azimuth * Math.PI / 180;
            X = centerX + Distance * pixelsPerKm * Math.Sin(angleRad);
            Y = centerY - Distance * pixelsPerKm * Math.Cos(angleRad);
        }
    }
}
