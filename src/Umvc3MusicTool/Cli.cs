using System;
using System.IO;
using Umvc3MusicTool.Models;
using Umvc3MusicTool.Services;

namespace Umvc3MusicTool;

/// <summary>
/// Headless conversion mode: Umvc3MusicTool.exe --convert &lt;input&gt; [options]
/// Useful for scripts and for verifying the conversion pipeline.
/// </summary>
internal static class Cli
{
    public static void Run(string[] args)
    {
        try
        {
            var ffmpeg = FfmpegLocator.FindFfmpeg(null)
                ?? throw new InvalidOperationException("ffmpeg not found (PATH or tools folder).");
            var oggenc = OggencLocator.FindOggenc()
                ?? throw new InvalidOperationException("oggenc2 not found (tools folder or PATH); required for correct 6-channel output.");

            var useSoxr = FfmpegCapabilities.HasSoxr(ffmpeg);
            var converter = new SngwConverter(ffmpeg, oggenc, useSoxr);

            var input = string.Empty;
            var output = string.Empty;
            var name = string.Empty;
            var quality = 7;
            var ver = "0002";
            var loop = true;
            double? loopStart = null;
            double? loopEnd = null;
            var samples = false;
            var dynamic = false;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--out": output = args[++i]; break;
                    case "--name": name = args[++i]; break;
                    case "--quality": quality = int.Parse(args[++i]); break;
                    case "--ver": ver = args[++i]; break;
                    case "--loop-start": loopStart = double.Parse(args[++i]); break;
                    case "--loop-end": loopEnd = double.Parse(args[++i]); break;
                    case "--samples": samples = true; break;
                    case "--dynamic": dynamic = true; break;
                    case "--no-loop": loop = false; break;
                    default:
                        if (input.Length == 0)
                            input = args[i];
                        else
                            throw new InvalidOperationException($"Unknown argument: {args[i]}");
                        break;
                }
            }

            if (string.IsNullOrEmpty(input))
                throw new InvalidOperationException("No input file specified.");

            var info = converter.Probe(input);
            Console.WriteLine($"Input : {input}");
            Console.WriteLine($"Source: {info.SampleRate} Hz, {info.Channels} ch, {info.DurationSeconds:F2}s");

            var outDir = string.IsNullOrEmpty(output) ? Path.GetDirectoryName(input) ?? "." : output;
            var baseName = string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(input) : name;

            var options = new ConversionOptions
            {
                Quality = quality,
                Ver = ver,
            };

            if (loop)
            {
                var sourceRate = info.SampleRate > 0 ? info.SampleRate : 48000;
                options.LoopStartSamples = loopStart is null
                    ? 0
                    : (long)Math.Round(samples ? loopStart.Value : loopStart.Value * sourceRate);
                options.LoopEndSamples = loopEnd is null
                    ? 0
                    : (long)Math.Round(samples ? loopEnd.Value : loopEnd.Value * sourceRate);
            }
            else
            {
                options.LoopStartSamples = -1;
                options.LoopEndSamples = -1;
            }

            var target = Path.Combine(outDir, baseName + ".sngw");
            var progress = new Progress<string>(msg => Console.WriteLine(msg));

            var layout = dynamic ? SngwOutputLayout.DynamicMain : SngwOutputLayout.Standard;
            var final = converter.ConvertAsync(input, target, options, info, progress, layout)
                .GetAwaiter().GetResult();

            Console.WriteLine($"OK -> {final}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
