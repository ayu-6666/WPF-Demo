using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace WxChatApp.Services;

/// <summary>
/// 语音消息服务：基于 NAudio 的录音（16kHz 单声道 WAV）与播放。
/// 同一时刻只允许一段录音/一路播放，播放状态通过事件通知界面。
/// </summary>
public sealed class VoiceService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _recordFile;
    private readonly Stopwatch _stopwatch = new();

    private WaveOutEvent? _player;
    private AudioFileReader? _reader;
    private string? _playingId;
    private CancellationTokenSource? _playCts;

    public bool IsRecording { get; private set; }
    public string? PlayingId => _playingId;

    /// <summary>录音状态变化（true=开始，false=结束）。</summary>
    public event Action<bool>? RecordingChanged;
    /// <summary>播放的消息 id 变化（null 表示全部停止）。</summary>
    public event Action<string?>? PlayStateChanged;

    // ---------------- 录音 ----------------

    public void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }
        var dir = Path.Combine(Path.GetTempPath(), "WxChatVoice");
        Directory.CreateDirectory(dir);
        _recordFile = Path.Combine(dir, "rec_" + Guid.NewGuid().ToString("N") + ".wav");

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
        _writer = new WaveFileWriter(_recordFile, _waveIn.WaveFormat);
        _waveIn.DataAvailable += (_, e) => _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        _waveIn.RecordingStopped += (_, _) => { };
        _waveIn.StartRecording();

        _stopwatch.Restart();
        IsRecording = true;
        RecordingChanged?.Invoke(true);
    }

    /// <summary>停止录音并返回 (wav 文件路径, 秒数, 实际时长)；失败返回 null。</summary>
    public (string File, int Seconds, double ElapsedSeconds)? StopRecording()
    {
        if (!IsRecording)
        {
            return null;
        }
        IsRecording = false;
        _stopwatch.Stop();
        RecordingChanged?.Invoke(false);

        try
        {
            _waveIn?.StopRecording();
        }
        catch
        {
            // 设备已经停止
        }
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;

        var elapsed = _stopwatch.Elapsed.TotalSeconds;
        var seconds = Math.Max(1, (int)Math.Round(elapsed));
        var file = _recordFile;
        _recordFile = null;
        if (file == null || !File.Exists(file))
        {
            return null;
        }
        return (file, seconds, elapsed);
    }

    /// <summary>放弃当前录音（不产出文件）。</summary>
    public void CancelRecording()
    {
        if (!IsRecording)
        {
            return;
        }
        IsRecording = false;
        _stopwatch.Stop();
        RecordingChanged?.Invoke(false);
        try
        {
            _waveIn?.StopRecording();
        }
        catch
        {
            // 设备已经停止
        }
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
        if (_recordFile != null && File.Exists(_recordFile))
        {
            try
            {
                File.Delete(_recordFile);
            }
            catch
            {
                // 临时文件删除失败不影响流程
            }
        }
        _recordFile = null;
    }

    // ---------------- 播放 ----------------

    /// <summary>播放 wav 文件；完成后自动回调 PlayStateChanged(null)。</summary>
    public void Play(string messageId, string filePath)
    {
        StopPlayback();
        _playingId = messageId;
        PlayStateChanged?.Invoke(messageId);

        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;
        _ = Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                using var output = new WaveOutEvent();
                output.Init(reader);
                _reader = reader;
                _player = output;
                output.Play();
                while (output.PlaybackState == PlaybackState.Playing && !token.IsCancellationRequested)
                {
                    Thread.Sleep(80);
                }
            }
            catch
            {
                // 播放设备不可用等情况：直接结束
            }
            finally
            {
                _player = null;
                _reader = null;
                if (_playingId == messageId)
                {
                    _playingId = null;
                    PlayStateChanged?.Invoke(null);
                }
            }
        }, token);
    }

    public void StopPlayback()
    {
        _playCts?.Cancel();
        _playCts = null;
        try
        {
            _player?.Stop();
        }
        catch
        {
            // 播放器已停止
        }
        if (_playingId != null)
        {
            _playingId = null;
            PlayStateChanged?.Invoke(null);
        }
    }

    public void Dispose()
    {
        CancelRecording();
        StopPlayback();
    }
}
