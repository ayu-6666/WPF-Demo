using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MediaStudioWpf48.Commands;
using MediaStudioWpf48.Models;
using MediaStudioWpf48.Services;
using MediaStudioWpf48.Views;
using Microsoft.Win32;

namespace MediaStudioWpf48.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private const int MaxLogEntries = 2000;
        private static readonly string[] SlideExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

        private readonly FfmpegService _ffmpeg = new FfmpegService();
        private readonly List<string> _tempFiles = new List<string>();
        private string _input = "";
        private string _output = "";
        private string _music = "";
        private string _statusKey = "StatusReady";
        private string _start = "00:00:00";
        private string _duration = "10";
        private string _fps = "15";
        private string _x = "0";
        private string _y = "0";
        private string _w = "160";
        private string _h = "60";
        private string _filter = "hflip";
        private bool _busy;
        private double _progress;
        private bool _openAfterDone;
        private bool _detailedLog = true;
        private ToolItem _selected;
        private bool _showTiming;
        private bool _showFps;
        private bool _showRegion;
        private bool _showFilter;
        private bool _showMusic;

        private static LocalizationManager L => LocalizationManager.Instance;

        public MainViewModel()
        {
            Tools = new ObservableCollection<ToolItem>
            {
                T("GroupVideo", "ToolAudioName", "ToolAudioHelp", "audio"),
                T("GroupVideo", "ToolMusicName", "ToolMusicHelp", "music"),
                T("GroupVideo", "ToolConcatName", "ToolConcatHelp", "concat"),
                T("GroupVideo", "ToolTrimName", "ToolTrimHelp", "trim"),
                T("GroupVideo", "ToolM3u8Name", "ToolM3u8Help", "m3u8"),
                T("GroupVideo", "ToolSlidesName", "ToolSlidesHelp", "slides"),
                T("GroupVideo", "ToolVdelogoName", "ToolVdelogoHelp", "vdelogo"),
                T("GroupVideo", "ToolVenhanceName", "ToolVenhanceHelp", "venhance"),
                T("GroupVideo", "ToolGifName", "ToolGifHelp", "gif"),
                T("GroupVideo", "ToolRotateName", "ToolRotateHelp", "rotate"),
                T("GroupImage", "ToolIdelogoName", "ToolIdelogoHelp", "idelogo"),
                T("GroupImage", "ToolIenhanceName", "ToolIenhanceHelp", "ienhance")
            };

            LocalizationManager.Instance.PropertyChanged += (s, e) => ApplyLanguage();
            _ffmpeg.Log += (line, isError) => Application.Current.Dispatcher.Invoke(() =>
            {
                if (DetailedLog || isError) AppendLog(line, isError);
            });

            ExecuteCommand = new RelayCommand(async _ => await Execute(), _ => CanRun());
            Selected = Tools[0];
            ApplyLanguage();
            RefreshEngine();
        }

        private static ToolItem T(string group, string name, string help, string key) =>
            new ToolItem { GroupKey = group, NameKey = name, HelpKey = help, Key = key };

        public ObservableCollection<ToolItem> Tools { get; }
        public string[] Filters { get; } = { "hflip", "vflip", "transpose=1", "transpose=2" };

        public ToolItem Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                N(nameof(Selected));
                UpdateToolState();
                ExecuteCommand?.Raise();
            }
        }

        public string Input
        {
            get => _input;
            set { _input = value; N(nameof(Input)); AfterPathChanged(); }
        }

        public string Output
        {
            get => _output;
            set { _output = value; N(nameof(Output)); AfterPathChanged(); }
        }

        public string Music
        {
            get => _music;
            set { _music = value; N(nameof(Music)); AfterPathChanged(); }
        }

        public ObservableCollection<LogEntry> LogLines { get; } = new ObservableCollection<LogEntry>();

        private void AppendLog(string text, bool isError = false, bool isSuccess = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (LogLines.Count >= MaxLogEntries) LogLines.RemoveAt(0);
            LogLines.Add(new LogEntry { Text = text, IsError = isError, IsSuccess = isSuccess });
        }

        public string Status => L[_statusKey];

        public double Progress
        {
            get => _progress;
            set { _progress = value; N(nameof(Progress)); }
        }

        public bool Busy
        {
            get => _busy;
            set
            {
                _busy = value;
                N(nameof(Busy));
                N(nameof(IsIdle));
                N(nameof(ActionHint));
                ExecuteCommand?.Raise();
            }
        }

        public bool IsIdle => !Busy;

        public bool OpenAfterDone
        {
            get => _openAfterDone;
            set { _openAfterDone = value; N(nameof(OpenAfterDone)); }
        }

        public bool DetailedLog
        {
            get => _detailedLog;
            set { _detailedLog = value; N(nameof(DetailedLog)); }
        }

        public string Start { get => _start; set { _start = value; N(nameof(Start)); } }
        public string Duration { get => _duration; set { _duration = value; N(nameof(Duration)); } }
        public string Fps { get => _fps; set { _fps = value; N(nameof(Fps)); } }
        public string X { get => _x; set { _x = value; N(nameof(X)); } }
        public string Y { get => _y; set { _y = value; N(nameof(Y)); } }
        public string W { get => _w; set { _w = value; N(nameof(W)); } }
        public string H { get => _h; set { _h = value; N(nameof(H)); } }
        public string Filter { get => _filter; set { _filter = value; N(nameof(Filter)); } }

        public bool ShowTiming { get => _showTiming; private set { _showTiming = value; N(nameof(ShowTiming)); } }
        public bool ShowFps { get => _showFps; private set { _showFps = value; N(nameof(ShowFps)); } }
        public bool ShowRegion { get => _showRegion; private set { _showRegion = value; N(nameof(ShowRegion)); } }
        public bool ShowFilter { get => _showFilter; private set { _showFilter = value; N(nameof(ShowFilter)); } }
        public bool ShowMusic { get => _showMusic; private set { _showMusic = value; N(nameof(ShowMusic)); } }
        public bool ShowAnyParam => ShowTiming || ShowFps || ShowRegion || ShowFilter;
        public bool ShowNoParams => !ShowAnyParam;

        public string InputHint => L[Selected != null && IsFolderTool(Selected.Key)
            ? "InputHintFolder" : "InputHintFile"];

        public string ActionHint
        {
            get
            {
                if (Busy) return L["HintBusy"];
                if (string.IsNullOrWhiteSpace(Input)) return L["HintNeedInput"];
                if (string.IsNullOrWhiteSpace(Output)) return L["HintNeedOutput"];
                if (Selected != null && Selected.Key == "music" && string.IsNullOrWhiteSpace(Music)) return L["HintNeedMusic"];
                return L["HintReady"];
            }
        }

        public bool EngineReady
        {
            get
            {
                try { return !string.IsNullOrEmpty(_ffmpeg.Exe) && File.Exists(_ffmpeg.Exe); }
                catch { return false; }
            }
        }

        public string EngineText => L[EngineReady ? "EngineReady" : "EngineMissing"];

        public RelayCommand ExecuteCommand { get; }

        public RelayCommand BrowseInput => new RelayCommand(_ =>
        {
            string key = Selected == null ? "" : Selected.Key;
            if (IsFolderTool(key))
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) Input = dialog.SelectedPath;
                return;
            }

            var picker = new OpenFileDialog
            {
                Filter = IsImageTool(key) ? L["PickImage"] : L["PickVideo"]
            };
            if (picker.ShowDialog() == true) Input = picker.FileName;
        });

        public RelayCommand BrowseMusic => new RelayCommand(_ =>
        {
            var dialog = new OpenFileDialog { Filter = L["PickAudio"] };
            if (dialog.ShowDialog() == true) Music = dialog.FileName;
        });

        public RelayCommand BrowseOutput => new RelayCommand(_ =>
        {
            string key = Selected == null ? "" : Selected.Key;
            string ext;
            string filter;
            switch (key)
            {
                case "audio":
                    ext = ".mp3";
                    filter = L["PickAudioOut"];
                    break;
                case "gif":
                    ext = ".gif";
                    filter = L["PickGifOut"];
                    break;
                case "idelogo":
                case "ienhance":
                    ext = ".png";
                    filter = L["PickImageOut"];
                    break;
                default:
                    ext = ".mp4";
                    filter = L["PickVideoOut"];
                    break;
            }
            var dialog = new SaveFileDialog { Filter = filter, DefaultExt = ext, AddExtension = true };
            if (dialog.ShowDialog() == true) Output = dialog.FileName;
        });

        private static bool IsFolderTool(string key) => key == "concat" || key == "slides";

        private static bool IsImageTool(string key) => key == "idelogo" || key == "ienhance";

        private bool CanRun()
        {
            if (Busy) return false;
            if (string.IsNullOrWhiteSpace(Input) || string.IsNullOrWhiteSpace(Output)) return false;
            if (Selected != null && Selected.Key == "music" && string.IsNullOrWhiteSpace(Music)) return false;
            return true;
        }

        private void AfterPathChanged()
        {
            N(nameof(ActionHint));
            ExecuteCommand?.Raise();
        }

        private void UpdateToolState()
        {
            string key = Selected?.Key ?? "";
            ShowTiming = key == "trim";
            ShowFps = key == "slides" || key == "gif";
            ShowRegion = key == "vdelogo" || key == "idelogo";
            ShowFilter = key == "rotate";
            ShowMusic = key == "music";
            N(nameof(ShowAnyParam));
            N(nameof(ShowNoParams));
            N(nameof(InputHint));
            N(nameof(ActionHint));
        }

        private void ApplyLanguage()
        {
            foreach (var tool in Tools) tool.Localize(k => L[k]);
            N(nameof(Status));
            N(nameof(ActionHint));
            N(nameof(InputHint));
            N(nameof(EngineText));
        }

        private void RefreshEngine()
        {
            N(nameof(EngineReady));
            N(nameof(EngineText));
        }

        private void SetStatus(string key)
        {
            _statusKey = key;
            N(nameof(Status));
            N(nameof(ActionHint));
        }

        private string Q(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        private List<string> GetSlideFiles()
        {
            if (string.IsNullOrWhiteSpace(Input) || !Directory.Exists(Input)) return new List<string>();
            return Directory.GetFiles(Input)
                .Where(f => SlideExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string SlideFps()
        {
            double fps;
            if (!double.TryParse(Fps, NumberStyles.Float, CultureInfo.InvariantCulture, out fps) || fps <= 0.1)
                fps = 15;
            return fps.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private string BuildSlideList(IList<string> files, string fps)
        {
            double seconds = 1.0 / double.Parse(fps, CultureInfo.InvariantCulture);
            string duration = seconds.ToString("0.######", CultureInfo.InvariantCulture);
            var builder = new StringBuilder();
            foreach (string file in files)
            {
                builder.AppendLine("file '" + ConcatPath(file) + "'");
                builder.AppendLine("duration " + duration);
            }
            // concat 清单需要重复最后一帧，否则最后一秒不会被输出。
            builder.AppendLine("file '" + ConcatPath(files[files.Count - 1]) + "'");

            string list = Path.Combine(Path.GetTempPath(), "slides_" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(list, builder.ToString(), new UTF8Encoding(false));
            _tempFiles.Add(list);
            return list;
        }

        private static string ConcatPath(string path) =>
            Path.GetFullPath(path).Replace("\\", "/").Replace("'", "'\\''");

        private string Args()
        {
            string input = Q(Input);
            string output = Q(Output);
            switch (Selected.Key)
            {
                case "audio":
                    return "-y -i " + input + " -vn -codec:a libmp3lame -q:a 2 " + output;
                case "music":
                    return "-y -i " + input + " -stream_loop -1 -i " + Q(Music) +
                           " -filter_complex \"[1:a]volume=0.25[a]\" -map 0:v -map \"[a]\" -c:v copy -shortest " + output;
                case "concat":
                    string list = Path.Combine(Path.GetTempPath(), "concat_" + Guid.NewGuid() + ".txt");
                    File.WriteAllLines(list, Directory.GetFiles(Input)
                        .Where(f => new[] { ".mp4", ".mov", ".mkv" }.Contains(Path.GetExtension(f).ToLower()))
                        .OrderBy(f => f)
                        .Select(f => "file '" + f.Replace("'", "'\\''") + "'"));
                    _tempFiles.Add(list);
                    return "-y -f concat -safe 0 -i " + Q(list) + " -c copy " + output;
                case "trim":
                    return "-y -ss " + Start + " -i " + input + " -t " + Duration + " -c:v libx264 -c:a aac " + output;
                case "m3u8":
                    return "-y -protocol_whitelist file,http,https,tcp,tls,crypto -i " + input +
                           " -c copy -bsf:a aac_adtstoasc " + output;
                case "slides":
                    List<string> slides = GetSlideFiles();
                    if (slides.Count == 0) throw new Exception(L["NoImages"]);
                    string slideList = BuildSlideList(slides, SlideFps());
                    return "-y -f concat -safe 0 -i " + Q(slideList) +
                           " -r " + SlideFps() +
                           " -c:v libx264 -pix_fmt yuv420p -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2,setsar=1\" " + output;
                case "vdelogo":
                    return "-y -i " + input + " -vf \"delogo=x=" + X + ":y=" + Y + ":w=" + W + ":h=" + H +
                           ":show=0\" -c:a copy " + output;
                case "idelogo":
                    return "-y -i " + input + " -vf \"delogo=x=" + X + ":y=" + Y + ":w=" + W + ":h=" + H +
                           ":show=0\" " + output;
                case "venhance":
                    return "-y -i " + input +
                           " -vf \"hqdn3d,scale=iw*2:ih*2:flags=lanczos,unsharp=5:5:0.7\" -c:v libx264 -crf 18 -c:a copy " + output;
                case "ienhance":
                    return "-y -i " + input + " -vf \"scale=iw*2:ih*2:flags=lanczos,unsharp=5:5:0.7\" " + output;
                case "gif":
                    return "-y -i " + input + " -vf \"fps=" + Fps +
                           ",scale=480:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse\" -loop 0 " + output;
                case "rotate":
                    return "-y -i " + input + " -vf " + Q(Filter) + " -c:a copy " + output;
                default:
                    throw new Exception("未知工具");
            }
        }

        private async Task Execute()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Input) || string.IsNullOrWhiteSpace(Output))
                    throw new Exception(L["NeedPaths"]);
                if (Selected.Key == "music" && string.IsNullOrWhiteSpace(Music))
                    throw new Exception(L["NeedMusic"]);
                if (Selected.Key == "slides" && GetSlideFiles().Count == 0)
                {
                    MessageDialog.ShowMessage(MessageDialogKind.Warning, L["NoImages"]);
                    return;
                }

                Progress = 0;
                Busy = true;
                SetStatus("StatusProcessing");
                AppendLog(L["LogStart"] + Selected.Name);

                await _ffmpeg.Run(Args());

                Progress = 100;
                SetStatus("StatusDone");
                AppendLog(L["LogDone"] + Output, false, true);
                if (OpenAfterDone) OpenOutputFolder();
                MessageDialog.ShowMessage(MessageDialogKind.Success, L["Completed"]);
            }
            catch (Exception ex)
            {
                SetStatus("StatusFailed");
                AppendLog(ex.Message, true);
                MessageDialog.ShowMessage(MessageDialogKind.Error, ex.Message, L["ErrorTitle"]);
            }
            finally
            {
                Busy = false;
                RefreshEngine();
                foreach (var file in _tempFiles)
                {
                    try { File.Delete(file); } catch { }
                }
                _tempFiles.Clear();
            }
        }

        private void OpenOutputFolder()
        {
            try
            {
                if (File.Exists(Output))
                    Process.Start("explorer.exe", "/select,\"" + Output + "\"");
                else
                {
                    string folder = Path.GetDirectoryName(Output);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        Process.Start("explorer.exe", "\"" + folder + "\"");
                }
            }
            catch { }
        }

        private void N(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
