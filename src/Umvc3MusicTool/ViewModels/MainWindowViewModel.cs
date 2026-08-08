using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Umvc3MusicTool.Models;
using Umvc3MusicTool.Services;

namespace Umvc3MusicTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private SngwConverter? _converter;
    private string? _ffmpegPath;

    public MainWindowViewModel()
    {
        _settings = SettingsService.Load();

        _ffmpegPath = FfmpegLocator.FindFfmpeg(_settings.FfmpegPath);

        if (_ffmpegPath is not null)
        {
            var soxr = FfmpegCapabilities.HasSoxr(_ffmpegPath);
            _converter = new SngwConverter(_ffmpegPath, soxr);
            UseSoxr = soxr;
            FfmpegPath = _ffmpegPath;
            FfmpegStatus = $"ffmpeg found: {_ffmpegPath}" + (soxr ? " (soxr resampler available)" : " (using built-in swr resampler)");
        }
        else
        {
            FfmpegStatus = "ffmpeg not found. Set the path in Advanced, or place ffmpeg.exe in the tools folder.";
        }

        OutputDirectory = _settings.OutputDirectory ?? string.Empty;
        OutputName = _settings.OutputName ?? string.Empty;
        LoopEnabled = _settings.LoopEnabled;
        Quality = _settings.Quality;
        Ver = _settings.Ver;
        _loopStartSamples = Math.Max(0, _settings.LoopStartSamples);
        _loopEndSamples = Math.Max(0, _settings.LoopEndSamples);
    }

    public ObservableCollection<SourceFileItem> Files { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    private bool isBusy;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string outputDirectory;

    [ObservableProperty]
    private string outputName;

    [ObservableProperty]
    private bool loopEnabled;

    [ObservableProperty]
    private int quality = 10;

    [ObservableProperty]
    private string ver = "0002";

    [ObservableProperty]
    private string ffmpegPath = string.Empty;

    [ObservableProperty]
    private string ffmpegStatus = string.Empty;

    [ObservableProperty]
    private bool useSoxr;

    [ObservableProperty]
    private SourceFileItem? selectedFile;

    private long _loopStartSamples;
    private long _loopEndSamples;

    [RelayCommand]
    private void ApplyFfmpegPath()
    {
        var path = FfmpegPath?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            FfmpegStatus = "Path is empty.";
            return;
        }

        var ffmpeg = FfmpegLocator.FindFfmpeg(path);
        if (ffmpeg is null)
        {
            FfmpegStatus = "Could not find ffmpeg.exe at that location.";
            return;
        }

        _ffmpegPath = ffmpeg;
        var soxr = FfmpegCapabilities.HasSoxr(ffmpeg);
        _converter = new SngwConverter(ffmpeg, soxr);
        UseSoxr = soxr;
        _settings.FfmpegPath = ffmpeg;
        SettingsService.Save(_settings);
        FfmpegStatus = $"ffmpeg found: {ffmpeg}"
            + (soxr ? " (soxr resampler available)" : " (using built-in swr resampler)");
        ConvertCommand.NotifyCanExecuteChanged();
    }

    public long LoopStartSamples
    {
        get => _loopStartSamples;
        set => SetProperty(ref _loopStartSamples, Math.Max(0, value));
    }

    public long LoopEndSamples
    {
        get => _loopEndSamples;
        set => SetProperty(ref _loopEndSamples, Math.Max(0, value));
    }

    public string OutputPreview
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(OutputName) ? "<input name>" : OutputName;
            return $"{name}.sngw";
        }
    }

    partial void OnOutputNameChanged(string value)
    {
        OnPropertyChanged(nameof(OutputPreview));
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path))
                continue;

            var existing = Files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                continue;

            var info = new FileInfo(path);
            Files.Add(new SourceFileItem(path, info.Name, info.Length));
            Log($"Added: {info.Name}");
        }

        if (Files.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
                OutputDirectory = Path.GetDirectoryName(Files[0].Path) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(OutputName) && Files.Count == 1)
                OutputName = Path.GetFileNameWithoutExtension(Files[0].Name);

            OnPropertyChanged(nameof(OutputPreview));
        }

        ConvertCommand.NotifyCanExecuteChanged();
    }

    public void RemoveSelected(SourceFileItem item)
    {
        if (item is not null && Files.Remove(item))
            Log($"Removed: {item.Name}");

        ConvertCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public void ClearFiles()
    {
        Files.Clear();
        Log("Cleared file list.");
        ConvertCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SetWholeSongLoop()
    {
        var first = Files.FirstOrDefault();
        if (first is null)
        {
            Log("Select or add an input file first to set a whole-song loop.");
            return;
        }

        try
        {
            var info = _converter?.Probe(first.Path);
            if (info is null)
            {
                Log("ffmpeg is not available.");
                return;
            }

            var endSamples = info.SampleRate > 0
                ? (long)Math.Round(info.DurationSeconds * info.SampleRate)
                : info.SampleCountAt48k;

            LoopStartSamples = 0;
            LoopEndSamples = endSamples;
            Log($"Loop set to whole song (0 → {LoopEndSamples} samples @ {info.SampleRate} Hz).");
        }
        catch (Exception ex)
        {
            Log($"Could not read file: {ex.Message}");
        }
    }

    private bool CanConvert() => !IsBusy && Files.Count > 0 && _converter is not null;

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync(CancellationToken ct)
    {
        IsBusy = true;
        Progress = 0;
        try
        {
            var converter = _converter ?? throw new InvalidOperationException("ffmpeg is not available.");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                Log("Output directory is empty.");
                return;
            }

            Directory.CreateDirectory(OutputDirectory);

            var single = Files.Count == 1;
            var done = 0;

            foreach (var file in Files.ToList())
            {
                if (ct.IsCancellationRequested)
                {
                    Log("Conversion cancelled.");
                    return;
                }

                Log($"--- {file.Name} ---");
                AudioInfo info;
                try
                {
                    info = converter.Probe(file.Path);
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: {ex.Message}");
                    continue;
                }

                Log($"  Source: {info.SampleRate} Hz, {info.Channels} ch, {info.DurationSeconds:F2}s");

                var baseName = single
                    ? (string.IsNullOrWhiteSpace(OutputName) ? Path.GetFileNameWithoutExtension(file.Name) : OutputName)
                    : Path.GetFileNameWithoutExtension(file.Name);

                var options = new ConversionOptions
                {
                    LoopEnabled = LoopEnabled,
                    Quality = Quality,
                    Ver = string.IsNullOrWhiteSpace(Ver) ? "0002" : Ver.Trim(),
                };

                if (LoopEnabled)
                {
                    options.LoopStartSamples = LoopStartSamples;
                    options.LoopEndSamples = LoopEndSamples;
                }
                else
                {
                    options.LoopStartSamples = -1;
                    options.LoopEndSamples = -1;
                }

                var outputPath = Path.Combine(OutputDirectory, baseName + ".sngw");

                var localProgress = new Progress<string>(msg =>
                {
                    Log(msg);
                    if (msg.StartsWith("Encoding...", StringComparison.Ordinal))
                    {
                        var pctStr = msg.Split(' ')[1].TrimEnd('%');
                        if (int.TryParse(pctStr, out var pct))
                            Progress = (pct + done * 100) / (double)Files.Count;
                    }
                });

                try
                {
                    var final = await converter.ConvertAsync(
                        file.Path, outputPath, options, info, localProgress, ct);

                    Log($"  OK → {Path.GetFileName(final)}" +
                        $" (LoopStart={options.LoopStartSamples}, LoopEnd={options.LoopEndSamples})");
                }
                catch (OperationCanceledException)
                {
                    Log("  Cancelled.");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: {ex.Message}");
                }

                done++;
                Progress = done * 100.0 / Files.Count;
            }

            Log("Done.");
            SaveSettings();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Log(string message)
    {
        LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void SaveSettings()
    {
        _settings.FfmpegPath = _ffmpegPath;
        _settings.OutputDirectory = OutputDirectory;
        _settings.OutputName = OutputName;
        _settings.Quality = Quality;
        _settings.Ver = Ver;
        _settings.LoopEnabled = LoopEnabled;
        _settings.LoopStartSamples = LoopStartSamples;
        _settings.LoopEndSamples = LoopEndSamples;
        SettingsService.Save(_settings);
    }
}

public sealed class SourceFileItem(string path, string name, long length)
{
    public string Path { get; } = path;
    public string Name { get; } = name;
    public long Length { get; } = length;
    public string SizeText => FormatBytes(Length);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
