using System;
using System.IO;

namespace Umvc3MusicTool.Services;

public static class OggencLocator
{
    public static string? FindOggenc()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "oggenc2.exe");
        if (File.Exists(bundled))
            return bundled;

        var fromPath = Which("oggenc2.exe") ?? Which("oggenc.exe");
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
