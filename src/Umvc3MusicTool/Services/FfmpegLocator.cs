using System;
using System.IO;

namespace Umvc3MusicTool.Services;

public static class FfmpegLocator
{
    public static string? FindFfmpeg(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return Path.GetFullPath(configuredPath);

        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");
        if (File.Exists(bundled))
            return bundled;

        var fromPath = Which("ffmpeg.exe");
        if (fromPath is not null)
            return fromPath;

        return null;
    }

    private static string? Which(string fileName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            try
            {
                var full = Path.Combine(dir, fileName);
                if (File.Exists(full))
                    return Path.GetFullPath(full);
            }
            catch
            {
                // ignore unreadable PATH entries
            }
        }

        return null;
    }
}
