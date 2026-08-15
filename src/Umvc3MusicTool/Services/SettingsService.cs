using System;
using System.IO;
using System.Text.Json;

namespace Umvc3MusicTool.Services;

public sealed class AppSettings
{
    public string? FfmpegPath { get; set; }
    public string? OutputDirectory { get; set; }
    public string? OutputName { get; set; }
    public int Quality { get; set; } = 7;
    public string Ver { get; set; } = "0002";
    public bool LoopEnabled { get; set; } = true;
    public long LoopStartSamples { get; set; }
    public long LoopEndSamples { get; set; }
    public double VolumeDb { get; set; }
    public string? DynamicFile1 { get; set; }
    public string? DynamicFile2 { get; set; }
    public bool Dynamic1LoopEnabled { get; set; } = true;
    public long Dynamic1LoopStartSamples { get; set; }
    public long Dynamic1LoopEndSamples { get; set; }
    public bool Dynamic2LoopEnabled { get; set; } = true;
    public long Dynamic2LoopStartSamples { get; set; }
    public long Dynamic2LoopEndSamples { get; set; }
    public string Theme { get; set; } = "Default";
    public bool UseCustomBackground { get; set; }
    public string BackgroundColor { get; set; } = "#FF24242E";
    public string BackgroundColor2 { get; set; } = "#FF3A3A4A";
    public bool UseGradientBackground { get; set; } = true;
    public string GradientDirection { get; set; } = "Vertical";
}

public sealed class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Umvc3MusicTool");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // best-effort persistence
        }
    }
}
