using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
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
    private string? _oggencPath;

    public MainWindowViewModel()
    {
        _settings = SettingsService.Load();

        _ffmpegPath = FfmpegLocator.FindFfmpeg(_settings.FfmpegPath);
        _oggencPath = OggencLocator.FindOggenc();

        if (_ffmpegPath is not null && _oggencPath is not null)
        {
            var soxr = FfmpegCapabilities.HasSoxr(_ffmpegPath);
            _converter = new SngwConverter(_ffmpegPath, _oggencPath, soxr);
            UseSoxr = soxr;
            FfmpegPath = _ffmpegPath;
            FfmpegStatus = $"ffmpeg found: {_ffmpegPath}" + (soxr ? " (soxr resampler available)" : " (using built-in swr resampler)")
                + $"\noggenc2 found: {_oggencPath}";
        }
        else if (_ffmpegPath is null)
        {
            FfmpegStatus = "ffmpeg not found. Set the path in Advanced, or place ffmpeg.exe in the tools folder.";
        }
        else
        {
            FfmpegStatus = "oggenc2 not found. Place oggenc2.exe in the tools folder.";
        }

        OutputDirectory = _settings.OutputDirectory ?? string.Empty;
        OutputName = _settings.OutputName ?? string.Empty;
        LoopEnabled = _settings.LoopEnabled;
        Quality = _settings.Quality;
        Ver = _settings.Ver;
        _loopStartSamples = Math.Max(0, _settings.LoopStartSamples);
        _loopEndSamples = Math.Max(0, _settings.LoopEndSamples);

        DynamicFile1 = _settings.DynamicFile1 ?? string.Empty;
        DynamicFile2 = _settings.DynamicFile2 ?? string.Empty;
        VolumeDb = _settings.VolumeDb;
        Dynamic1LoopEnabled = _settings.Dynamic1LoopEnabled;
        Dynamic2LoopEnabled = _settings.Dynamic2LoopEnabled;
        _dynamic1LoopStartSamples = Math.Max(0, _settings.Dynamic1LoopStartSamples);
        _dynamic1LoopEndSamples = Math.Max(0, _settings.Dynamic1LoopEndSamples);
        _dynamic2LoopStartSamples = Math.Max(0, _settings.Dynamic2LoopStartSamples);
        _dynamic2LoopEndSamples = Math.Max(0, _settings.Dynamic2LoopEndSamples);

        Theme = string.IsNullOrWhiteSpace(_settings.Theme) ? "Default" : _settings.Theme;
        UseCustomBackground = _settings.UseCustomBackground;
        UseGradient = _settings.UseGradientBackground;
        GradientDirection = string.IsNullOrWhiteSpace(_settings.GradientDirection) ? "Vertical" : _settings.GradientDirection;
        BackgroundColor = Color.TryParse(_settings.BackgroundColor, out var c1) ? c1 : DefaultBackgroundColor;
        BackgroundColor2 = Color.TryParse(_settings.BackgroundColor2, out var c2) ? c2 : DefaultBackgroundColor2;
    }

    public ObservableCollection<SourceFileItem> Files { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(DynamicConvertCommand))]
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
    private int quality = 7;

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

    [ObservableProperty]
    private string dynamicFile1 = string.Empty;

    [ObservableProperty]
    private string dynamicFile2 = string.Empty;

    [ObservableProperty]
    private double volumeDb;

    [ObservableProperty]
    private bool dynamic1LoopEnabled = true;

    [ObservableProperty]
    private bool dynamic2LoopEnabled = true;

    public string[] ThemeOptions { get; } = ["Default", "Dark", "Light"];
    public string[] GradientDirectionOptions { get; } = ["Vertical", "Horizontal"];

    private static readonly Color DefaultBackgroundColor = Color.FromArgb(0xFF, 0x24, 0x24, 0x2E);
    private static readonly Color DefaultBackgroundColor2 = Color.FromArgb(0xFF, 0x3A, 0x3A, 0x4A);

    [ObservableProperty]
    private string theme = "Default";

    [ObservableProperty]
    private bool useCustomBackground;

    [ObservableProperty]
    private Color backgroundColor = DefaultBackgroundColor;

    [ObservableProperty]
    private Color backgroundColor2 = DefaultBackgroundColor2;

    [ObservableProperty]
    private bool useGradient = true;

    [ObservableProperty]
    private string gradientDirection = "Vertical";

    public IBrush? BackgroundBrush => BuildBackgroundBrush();

    partial void OnThemeChanged(string value)
    {
        Application.Current?.RequestedThemeVariant = value switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
        SaveAppearanceSettings();
    }

    partial void OnUseCustomBackgroundChanged(bool value) => RefreshBackground();

    partial void OnBackgroundColorChanged(Color value) => RefreshBackground();

    partial void OnBackgroundColor2Changed(Color value) => RefreshBackground();

    partial void OnUseGradientChanged(bool value) => RefreshBackground();

    partial void OnGradientDirectionChanged(string value) => RefreshBackground();

    private static string FormatHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private void RefreshBackground()
    {
        OnPropertyChanged(nameof(BackgroundBrush));
        SaveAppearanceSettings();
    }

    private IBrush? BuildBackgroundBrush()
    {
        if (!UseCustomBackground)
            return null;

        var c1 = BackgroundColor;

        if (!UseGradient)
            return new SolidColorBrush(c1);

        var c2 = BackgroundColor2;
        var horizontal = GradientDirection == "Horizontal";
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = horizontal
                ? new RelativePoint(1, 0, RelativeUnit.Relative)
                : new RelativePoint(0, 1, RelativeUnit.Relative),
        };
        brush.GradientStops.Add(new GradientStop(c1, 0));
        brush.GradientStops.Add(new GradientStop(c2, 1));
        return brush;
    }

    private void SaveAppearanceSettings()
    {
        _settings.Theme = Theme;
        _settings.UseCustomBackground = UseCustomBackground;
        _settings.BackgroundColor = FormatHex(BackgroundColor);
        _settings.BackgroundColor2 = FormatHex(BackgroundColor2);
        _settings.UseGradientBackground = UseGradient;
        _settings.GradientDirection = GradientDirection;
        SettingsService.Save(_settings);
    }

    private long _loopStartSamples;
    private long _loopEndSamples;
    private long _dynamic1LoopStartSamples;
    private long _dynamic1LoopEndSamples;
    private long _dynamic2LoopStartSamples;
    private long _dynamic2LoopEndSamples;

    [RelayCommand]
    private void ResetVolume() => VolumeDb = 0;

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
        _oggencPath ??= OggencLocator.FindOggenc();
        if (_oggencPath is null)
        {
            FfmpegStatus = "oggenc2 not found. Place oggenc2.exe in the tools folder.";
            return;
        }

        var soxr = FfmpegCapabilities.HasSoxr(ffmpeg);
        _converter = new SngwConverter(ffmpeg, _oggencPath, soxr);
        UseSoxr = soxr;
        _settings.FfmpegPath = ffmpeg;
        SettingsService.Save(_settings);
        FfmpegStatus = $"ffmpeg found: {ffmpeg}"
            + (soxr ? " (soxr resampler available)" : " (using built-in swr resampler)")
            + $"\noggenc2 found: {_oggencPath}";
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

    public long Dynamic1LoopStartSamples
    {
        get => _dynamic1LoopStartSamples;
        set => SetProperty(ref _dynamic1LoopStartSamples, Math.Max(0, value));
    }

    public long Dynamic1LoopEndSamples
    {
        get => _dynamic1LoopEndSamples;
        set => SetProperty(ref _dynamic1LoopEndSamples, Math.Max(0, value));
    }

    public long Dynamic2LoopStartSamples
    {
        get => _dynamic2LoopStartSamples;
        set => SetProperty(ref _dynamic2LoopStartSamples, Math.Max(0, value));
    }

    public long Dynamic2LoopEndSamples
    {
        get => _dynamic2LoopEndSamples;
        set => SetProperty(ref _dynamic2LoopEndSamples, Math.Max(0, value));
    }

    public string OutputPreview
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(OutputName) ? "<input name>" : OutputName;
            return $"{name}.sngw";
        }
    }

    public string DynamicOutputPreview
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(OutputName) ? "<input name>" : OutputName;
            return $"{name}.sngw  ·  {name}_b.sngw";
        }
    }

    partial void OnOutputNameChanged(string value)
    {
        OnPropertyChanged(nameof(OutputPreview));
        OnPropertyChanged(nameof(DynamicOutputPreview));
    }

    partial void OnDynamicFile1Changed(string value)
    {
        DynamicConvertCommand.NotifyCanExecuteChanged();

        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
                OutputDirectory = Path.GetDirectoryName(value) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(OutputName))
                OutputName = Path.GetFileNameWithoutExtension(value);
        }
    }

    partial void OnDynamicFile2Changed(string value)
    {
        DynamicConvertCommand.NotifyCanExecuteChanged();
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
    private void SetWholeSongLoop() =>
        SetWholeSongLoopFor(
            Files.FirstOrDefault()?.Path,
            (start, end) => { LoopStartSamples = start; LoopEndSamples = end; },
            "an input file");

    [RelayCommand]
    private void SetDynamic1WholeSongLoop() =>
        SetWholeSongLoopFor(
            DynamicFile1,
            (start, end) => { Dynamic1LoopStartSamples = start; Dynamic1LoopEndSamples = end; },
            "File 1");

    [RelayCommand]
    private void SetDynamic2WholeSongLoop() =>
        SetWholeSongLoopFor(
            DynamicFile2,
            (start, end) => { Dynamic2LoopStartSamples = start; Dynamic2LoopEndSamples = end; },
            "File 2");

    private void SetWholeSongLoopFor(string? path, Action<long, long> apply, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Log($"Select or add {label} first to set a whole-song loop.");
            return;
        }

        try
        {
            var info = _converter?.Probe(path);
            if (info is null)
            {
                Log("ffmpeg is not available.");
                return;
            }

            var endSamples = info.SampleRate > 0
                ? (long)Math.Round(info.DurationSeconds * info.SampleRate)
                : info.SampleCountAt48k;

            apply(0, endSamples);
            Log($"Loop set to whole song (0 → {endSamples} samples @ {info.SampleRate} Hz).");
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
                    GainDb = VolumeDb,
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
                        file.Path, outputPath, options, info, localProgress, SngwOutputLayout.Standard, ct);

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

    private bool CanDynamicConvert() =>
        !IsBusy && _converter is not null &&
        !string.IsNullOrWhiteSpace(DynamicFile1) && File.Exists(DynamicFile1) &&
        !string.IsNullOrWhiteSpace(DynamicFile2) && File.Exists(DynamicFile2);

    [RelayCommand(CanExecute = nameof(CanDynamicConvert))]
    private async Task DynamicConvertAsync(CancellationToken ct)
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

            var baseName = string.IsNullOrWhiteSpace(OutputName)
                ? Path.GetFileNameWithoutExtension(DynamicFile1)
                : OutputName.Trim();

            var jobs = new (string Input, string OutputBase, SngwOutputLayout Layout, bool LoopEnabled, long LoopStart, long LoopEnd)[]
            {
                (DynamicFile1, baseName, SngwOutputLayout.DynamicMain, Dynamic1LoopEnabled, Dynamic1LoopStartSamples, Dynamic1LoopEndSamples),
                (DynamicFile2, baseName + "_b", SngwOutputLayout.DynamicB, Dynamic2LoopEnabled, Dynamic2LoopStartSamples, Dynamic2LoopEndSamples),
            };

            var done = 0;

            foreach (var (input, outputBase, layout, loopEnabled, loopStart, loopEnd) in jobs)
            {
                if (ct.IsCancellationRequested)
                {
                    Log("Conversion cancelled.");
                    return;
                }

                Log($"--- {Path.GetFileName(input)} ---");
                AudioInfo info;
                try
                {
                    info = converter.Probe(input);
                }
                catch (Exception ex)
                {
                    Log($"  ERROR: {ex.Message}");
                    continue;
                }

                Log($"  Source: {info.SampleRate} Hz, {info.Channels} ch, {info.DurationSeconds:F2}s");

                var options = new ConversionOptions
                {
                    LoopEnabled = loopEnabled,
                    Quality = Quality,
                    Ver = string.IsNullOrWhiteSpace(Ver) ? "0002" : Ver.Trim(),
                    GainDb = VolumeDb,
                };

                if (loopEnabled)
                {
                    options.LoopStartSamples = loopStart;
                    options.LoopEndSamples = loopEnd;
                }
                else
                {
                    options.LoopStartSamples = -1;
                    options.LoopEndSamples = -1;
                }

                var outputPath = Path.Combine(OutputDirectory, outputBase + ".sngw");

                var localProgress = new Progress<string>(msg =>
                {
                    Log(msg);
                    if (msg.StartsWith("Encoding...", StringComparison.Ordinal))
                    {
                        var pctStr = msg.Split(' ')[1].TrimEnd('%');
                        if (int.TryParse(pctStr, out var pct))
                            Progress = (pct + done * 100) / 2.0;
                    }
                });

                try
                {
                    var final = await converter.ConvertAsync(
                        input, outputPath, options, info, localProgress, layout, ct);

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
                Progress = done * 100.0 / jobs.Length;
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
        _settings.DynamicFile1 = string.IsNullOrWhiteSpace(DynamicFile1) ? null : DynamicFile1;
        _settings.DynamicFile2 = string.IsNullOrWhiteSpace(DynamicFile2) ? null : DynamicFile2;
        _settings.VolumeDb = VolumeDb;
        _settings.Dynamic1LoopEnabled = Dynamic1LoopEnabled;
        _settings.Dynamic1LoopStartSamples = Dynamic1LoopStartSamples;
        _settings.Dynamic1LoopEndSamples = Dynamic1LoopEndSamples;
        _settings.Dynamic2LoopEnabled = Dynamic2LoopEnabled;
        _settings.Dynamic2LoopStartSamples = Dynamic2LoopStartSamples;
        _settings.Dynamic2LoopEndSamples = Dynamic2LoopEndSamples;
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
