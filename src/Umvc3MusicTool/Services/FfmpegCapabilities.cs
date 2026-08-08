using System;
using System.Diagnostics;

namespace Umvc3MusicTool.Services;

public static class FfmpegCapabilities
{
    private static readonly bool? SoxrAvailable = null;

    public static bool HasSoxr(string ffmpegPath)
    {
        if (SoxrAvailable.HasValue)
            return SoxrAvailable.Value;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-filters");

            using var proc = Process.Start(psi);
            if (proc is null)
                return false;

            var output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);
            return output.Contains("soxr", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
