using Cmp.UTools.GUI.Models;
using Cmp.UTools.GUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Cmp.UTools.GUI.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly FileConversionService _svc = new FileConversionService();

        private string _inputPath;
        public string InputPath
        {
            get => _inputPath;
            set => SetProperty(ref _inputPath, value);
        }

        private TabularData _previewData;
        public TabularData PreviewData
        {
            get => _previewData;
            set
            {
                if (SetProperty(ref _previewData, value))
                {
                    // update preview table
                    PreviewTable = _previewData?.ToDataTable().DefaultView;
                    TotalRecords = _previewData?.Rows.Count ?? 0;
                    LogMessage?.Invoke($"已加载 {_previewData?.Rows.Count ?? 0} 条记录");
                }
            }
        }

        public int TotalRecords

        {
            get { return _TotalRecords; }
            set => SetProperty(ref _TotalRecords, value);
        }
        public int _TotalRecords = 0;
        private DataView _previewTable;
        public DataView PreviewTable
        {
            get => _previewTable;
            set => SetProperty(ref _previewTable, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ObservableCollection<string> TargetFormats { get; } = new ObservableCollection<string>(new[] { "CSV", "Excel", "JSON", "XML", "Config" });

        private string _selectedTargetFormat = "CSV";
        public string SelectedTargetFormat
        {
            get => _selectedTargetFormat;
            set => SetProperty(ref _selectedTargetFormat, value);
        }

        public RelayCommand BrowseFileCommand { get; }
        public AsyncRelayCommand PreviewCommand { get; }
        public AsyncRelayCommand ConvertCommand { get; }

        public event Action<string> LogMessage;
        public event Action<int> ProgressChanged;

        public MainViewModel()
        {
            BrowseFileCommand = new RelayCommand(BrowseFile);
            PreviewCommand = new AsyncRelayCommand(PreviewAsync);
            ConvertCommand = new AsyncRelayCommand(ConvertAsync);
        }

        private void BrowseFile()
        {
            var dlg = new OpenFileDialog { Filter = "All supported|*.csv;*.json;*.xml;*.config;*.xlsx|CSV|*.csv|JSON|*.json|XML|*.xml|Config|*.config|Excel|*.xlsx" };
            if (dlg.ShowDialog() == true)
            {
                InputPath = dlg.FileName;
                LogMessage?.Invoke($"已选择文件: {InputPath}");
            }
        }

        private async Task PreviewAsync()
        {
            if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
            {
                LogMessage?.Invoke("请选择有效的输入文件");
                MessageBox.Show("请选择有效的输入文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                IsBusy = true;
                LogMessage?.Invoke("开始解析文件...");
                if(PreviewData != null)
                {
                    PreviewData.Columns.Clear();
                    PreviewData.Rows.Clear();
                }
                else
                {
                    PreviewData = new TabularData();
                }

                PreviewData = await _svc.ImportAsync(InputPath);
                LogMessage?.Invoke("解析完成");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"解析失败：{ex.Message}");
                MessageBox.Show($"解析失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ConvertAsync()
        {
            if (PreviewData == null) { LogMessage?.Invoke("请先预览或加载输入文件"); MessageBox.Show("请先预览或加载输入文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var dlg = new SaveFileDialog();
            dlg.Filter = SelectedTargetFormat switch
            {
                "CSV" => "CSV|*.csv",
                "Excel" => "Excel|*.xlsx",
                "JSON" => "JSON|*.json",
                "XML" => "XML|*.xml",
                "Config" => "Config|*.config",
                _ => "All|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                IsBusy = true;
                var path = dlg.FileName;
                LogMessage?.Invoke($"开始导出到 {path}");
                ProgressChanged?.Invoke(0);
                switch (SelectedTargetFormat)
                {
                    case "CSV": await _svc.ExportCsvAsync(PreviewData, path); break;
                    case "Excel": await _svc.ExportExcelAsync(PreviewData, path); break;
                    case "JSON": await _svc.ExportJsonAsync(PreviewData, path); break;
                    case "XML": await _svc.ExportXmlAsync(PreviewData, path); break;
                    case "Config": await _svc.ExportXmlAsync(PreviewData, path, "add", "appSettings"); break;
                }
                ProgressChanged?.Invoke(100);
                LogMessage?.Invoke("导出完成");
                MessageBox.Show("导出完成", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"导出失败：{ex.Message}");
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
