using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Umvc3MusicTool.Models;

namespace Umvc3MusicTool.Services;

public sealed class AudioInfo
{
    public double DurationSeconds { get; init; }
    public int Channels { get; init; }
    public int SampleRate { get; init; }
    public long SampleCountAt48k => (long)Math.Round(DurationSeconds * ConversionOptions.TargetSampleRate);
}

public enum SngwOutputLayout
{
    Standard,
    DynamicMain,
    DynamicB,
}

public sealed class SngwConverter
{
    private readonly string _ffmpeg;
    private readonly string _oggenc;
    private readonly bool _useSoxr;
    private static readonly Regex TimeRegex = new(@"time=(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex StreamRegex = new(@"Audio:\s*\w+[^,]*, (\d+) Hz, ([^,]+)", RegexOptions.Compiled);
    private static readonly Regex OggencPctRegex = new(@"\[ *(\d+)[.,]\d+%", RegexOptions.Compiled);

    public SngwConverter(string ffmpegPath, string oggencPath, bool useSoxr)
    {
        _ffmpeg = ffmpegPath;
        _oggenc = oggencPath;
        _useSoxr = useSoxr;
    }

    public AudioInfo Probe(string inputPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpeg,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);

        using var proc = Process.Start(psi);
        if (proc is null)
            throw new InvalidOperationException("Failed to start ffmpeg.");

        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        var durationMatch = DurationRegex.Match(stderr);
        if (!durationMatch.Success)
            throw new InvalidOperationException("ffmpeg could not read the input file:\n" + Trim(stderr));

        var h = double.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var m = double.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        var s = double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture);
        var duration = h * 3600 + m * 60 + s;

        var channels = 0;
        var sampleRate = 0;
        foreach (Match match in StreamRegex.Matches(stderr))
        {
            int.TryParse(match.Groups[1].Value, out sampleRate);
            channels = CountChannels(match.Groups[2].Value);
            break;
        }

        if (duration <= 0)
            throw new InvalidOperationException("ffmpeg returned no valid duration for the input file.");

        return new AudioInfo { DurationSeconds = duration, Channels = channels, SampleRate = sampleRate };
    }

    private static int CountChannels(string layout)
    {
        var name = layout.Trim().ToLowerInvariant();
        return name switch
        {
            "mono" => 1,
            "stereo" => 2,
            "3.0" or "3.0(back)" => 3,
            "4.0" or "quad" or "quad(side)" => 4,
            "5.0" => 5,
            "5.1" or "5.1(side)" or "hexagonal" => 6,
            _ when int.TryParse(layout, out var n) => n,
            _ => 0,
        };
    }

    private static string Trim(string text)
    {
        var t = text.Trim();
        return t.Length > 600 ? t[^600..] : t;
    }

    public async Task<string> ConvertAsync(
        string inputPath,
        string outputSngwPath,
        ConversionOptions options,
        AudioInfo info,
        IProgress<string>? progress,
        SngwOutputLayout layout = SngwOutputLayout.Standard,
        CancellationToken ct = default)
    {
        ResolveLoopPoints(options, info);

        var outDir = Path.GetDirectoryName(outputSngwPath)
            ?? throw new ArgumentException("Output path has no directory.");
        var baseTmp = Path.Combine(outDir, Path.GetFileNameWithoutExtension(outputSngwPath));
        var wavPath = baseTmp + ".wav.tmp";
        var oggPath = baseTmp + ".ogg.tmp";

        Directory.CreateDirectory(outDir);

        var resample = _useSoxr ? "aresample=48000:resampler=soxr" : "aresample=48000";
        var gain = options.GainDb != 0
            ? $"volume={options.GainDb.ToString("0.###", CultureInfo.InvariantCulture)}dB,"
            : string.Empty;
        var filter = layout switch
        {
            SngwOutputLayout.DynamicMain =>
                $"{gain}{resample},aformat=channel_layouts=stereo,pan=mono|c0=0.5*FL+0.5*FR,pan=5.1|FL=0.631*c0|FC=0.631*c0|BR=1.585*c0|LFE=1.585*c0,alimiter=limit=1",
            SngwOutputLayout.DynamicB =>
                $"{gain}{resample},aformat=channel_layouts=stereo,pan=mono|c0=0.5*FL+0.5*FR,pan=stereo|FL=c0|FR=c0,volume=0.631",
            _ =>
                $"{gain}{resample},aformat=channel_layouts=stereo,pan=5.1|FL=FL|FC=FR",
        };

        // Pass 1: ffmpeg applies the filter chain and writes 48 kHz PCM (WAV).
        var ffmpegPsi = new ProcessStartInfo
        {
            FileName = _ffmpeg,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        ffmpegPsi.ArgumentList.Add("-y");
        ffmpegPsi.ArgumentList.Add("-nostdin");
        ffmpegPsi.ArgumentList.Add("-hide_banner");
        ffmpegPsi.ArgumentList.Add("-i");
        ffmpegPsi.ArgumentList.Add(inputPath);
        ffmpegPsi.ArgumentList.Add("-map_metadata");
        ffmpegPsi.ArgumentList.Add("-1");
        ffmpegPsi.ArgumentList.Add("-vn");
        ffmpegPsi.ArgumentList.Add("-af");
        ffmpegPsi.ArgumentList.Add(filter);
        ffmpegPsi.ArgumentList.Add("-c:a");
        ffmpegPsi.ArgumentList.Add("pcm_s24le");
        ffmpegPsi.ArgumentList.Add("-f");
        ffmpegPsi.ArgumentList.Add("wav");
        ffmpegPsi.ArgumentList.Add(wavPath);

        try
        {
            await RunAsync(ffmpegPsi, ct, line =>
            {
                var m = TimeRegex.Match(line);
                if (!m.Success || info.DurationSeconds <= 0)
                    return;

                var h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var min = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var s = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                var elapsed = h * 3600 + min * 60 + s;
                var pct = (int)Math.Clamp(elapsed / info.DurationSeconds * 10, 0, 9);
                progress?.Report($"Encoding... {pct}%");
            }).ConfigureAwait(false);

            // Pass 2: oggenc2 encodes to Vorbis with channel coupling disabled so that all
            // 6 channels stay in a single full-range submap (libvorbis's 5.1 template would
            // otherwise route channel 5 to a bass-only LFE submap).
            var oggencPsi = new ProcessStartInfo
            {
                FileName = _oggenc,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var bitrate = layout switch
            {
                SngwOutputLayout.DynamicB => "160k",
                _ => null,
            };
            if (bitrate is null)
            {
                oggencPsi.ArgumentList.Add("-q");
                oggencPsi.ArgumentList.Add(options.Quality.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                oggencPsi.ArgumentList.Add("-b");
                oggencPsi.ArgumentList.Add(bitrate.TrimEnd('k', 'K'));
            }
            oggencPsi.ArgumentList.Add("--advanced-encode-option");
            oggencPsi.ArgumentList.Add("disable_coupling=Y");
            oggencPsi.ArgumentList.Add("-c");
            oggencPsi.ArgumentList.Add($"Ver={options.Ver}");
            oggencPsi.ArgumentList.Add("-c");
            oggencPsi.ArgumentList.Add($"LoopStart={options.LoopStartSamples}");
            oggencPsi.ArgumentList.Add("-c");
            oggencPsi.ArgumentList.Add($"LoopEnd={options.LoopEndSamples}");
            oggencPsi.ArgumentList.Add("-o");
            oggencPsi.ArgumentList.Add(oggPath);
            oggencPsi.ArgumentList.Add(wavPath);

            await RunAsync(oggencPsi, ct, line =>
            {
                var m = OggencPctRegex.Match(line);
                if (!m.Success)
                    return;

                if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                {
                    var pct = (int)Math.Clamp(10 + whole * 89 / 100, 10, 99);
                    progress?.Report($"Encoding... {pct}%");
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(wavPath); } catch { }
        }

        progress?.Report("Writing .sngw...");
        StripEncoderComment(oggPath);
        var finalPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(outputSngwPath) + ".sngw");
        File.Move(oggPath, finalPath, overwrite: true);
        return finalPath;
    }

    private static async Task RunAsync(
        ProcessStartInfo psi,
        CancellationToken ct,
        Action<string> onLine)
    {
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {Path.GetFileName(psi.FileName)}.");

        var tail = new StringBuilder();
        var pending = new StringBuilder();

        void HandleLine(string line)
        {
            lock (tail)
            {
                tail.AppendLine(line);
                if (tail.Length > 8000)
                    tail.Remove(0, tail.Length - 6000);
            }
            onLine(line);
        }

        try
        {
            // Some encoders (oggenc2) update progress with bare '\r', so split on both.
            using var reader = new StreamReader(proc.StandardError.BaseStream);
            var buf = new char[4096];
            while (true)
            {
                var n = await reader.ReadAsync(buf.AsMemory(), ct).ConfigureAwait(false);
                if (n == 0)
                    break;

                for (var i = 0; i < n; i++)
                {
                    var c = buf[i];
                    if (c == '\r' || c == '\n')
                    {
                        if (pending.Length > 0)
                        {
                            var line = pending.ToString();
                            pending.Clear();
                            HandleLine(line);
                        }
                    }
                    else
                    {
                        pending.Append(c);
                    }
                }
            }

            if (pending.Length > 0)
                HandleLine(pending.ToString());

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        if (proc.ExitCode != 0)
        {
            string t;
            lock (tail)
                t = tail.ToString();

            throw new InvalidOperationException(
                $"{Path.GetFileName(psi.FileName)} failed (exit code {proc.ExitCode}).\n{t.Trim()}");
        }
    }

    private static void ResolveLoopPoints(ConversionOptions options, AudioInfo info)
    {
        if (options.LoopStartSamples < 0)
        {
            options.LoopStartSamples = -1;
            options.LoopEndSamples = -1;
            return;
        }

        var sourceRate = info.SampleRate > 0 ? info.SampleRate : ConversionOptions.TargetSampleRate;

        var endSamples = options.LoopEndSamples;
        if (endSamples <= options.LoopStartSamples)
            endSamples = (long)Math.Round(info.DurationSeconds * sourceRate);

        var scale = (double)ConversionOptions.TargetSampleRate / sourceRate;
        options.LoopStartSamples = (long)Math.Round(options.LoopStartSamples * scale);
        options.LoopEndSamples = (long)Math.Round(endSamples * scale);
    }

    private static void StripEncoderComment(string oggPath)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(oggPath);
        }
        catch
        {
            return;
        }

        var changed = false;
        using var ms = new MemoryStream(data.Length);
        var pos = 0;
        while (pos + 27 <= data.Length)
        {
            if (data[pos] != (byte)'O' || data[pos + 1] != (byte)'g' ||
                data[pos + 2] != (byte)'g' || data[pos + 3] != (byte)'S')
                break;

            var nseg = data[pos + 26];
            var segTableStart = pos + 27;
            var segTableEnd = segTableStart + nseg;
            if (segTableEnd > data.Length)
                break;

            var bodyLen = 0;
            for (var i = 0; i < nseg; i++)
                bodyLen += data[segTableStart + i];
            var bodyStart = segTableEnd;
            var bodyEnd = bodyStart + bodyLen;
            if (bodyEnd > data.Length)
                break;

            var page = data[pos..bodyEnd];
            var segs = data[segTableStart..segTableEnd];

            if (!changed && TryStripEncoderCommentFromPage(page, segs, out var newPage))
            {
                page = newPage;
                changed = true;
            }

            ms.Write(page, 0, page.Length);
            pos = bodyEnd;
        }

        if (changed)
        {
            ms.Position = 0;
            File.WriteAllBytes(oggPath, ms.ToArray());
        }
    }

    private static bool TryStripEncoderCommentFromPage(byte[] page, byte[] segs, out byte[] newPage)
    {
        newPage = page;
        if (segs.Length == 0)
            return false;

        var packets = new List<byte[]>();
        var current = new List<byte>();
        var bodyOff = 27 + segs.Length;
        foreach (var s in segs)
        {
            current.AddRange(page.AsSpan(bodyOff, s).ToArray());
            bodyOff += s;
            if (s < 255)
            {
                packets.Add(current.ToArray());
                current.Clear();
            }
        }

        // Bail out if the page ends with a packet that continues onto the next page:
        // re-lacing a partial packet here would desync the continuation pages.
        if (current.Count > 0)
            return false;

        // only rewrite when the comment header packet begins on this page
        if (packets.Count == 0 || packets[0].Length == 0 || packets[0][0] != 3)
            return false;

        var pkt = packets[0];
        if (pkt.Length < 11)
            return false;

        var p = 7; // skip 0x03 + "vorbis"
        var vendorLen = ReadUInt32(pkt, p); p += 4;
        if (p + vendorLen + 4 > pkt.Length)
            return false;
        var vendor = pkt.AsSpan(p, vendorLen).ToArray(); p += vendorLen;

        var count = ReadUInt32(pkt, p); p += 4;
        var comments = new List<byte[]>();
        for (var i = 0; i < count; i++)
        {
            if (p + 4 > pkt.Length)
                return false;
            var clen = ReadUInt32(pkt, p); p += 4;
            if (p + clen > pkt.Length)
                return false;
            comments.Add(pkt.AsSpan(p, clen).ToArray());
            p += clen;
        }

        var kept = comments.Where(c => !IsEncoderComment(c)).ToList();
        if (kept.Count == comments.Count)
            return false;

        using var ms = new MemoryStream();
        ms.WriteByte(3);
        ms.Write(Encoding.ASCII.GetBytes("vorbis"), 0, 6);
        WriteUInt32(ms, (uint)vendor.Length);
        ms.Write(vendor, 0, vendor.Length);
        WriteUInt32(ms, (uint)kept.Count);
        foreach (var c in kept)
        {
            WriteUInt32(ms, (uint)c.Length);
            ms.Write(c, 0, c.Length);
        }
        ms.WriteByte(1);

        var newPackets = new List<byte[]> { ms.ToArray() };
        for (var i = 1; i < packets.Count; i++)
            newPackets.Add(packets[i]);

        var newSegs = new List<byte>();
        foreach (var pk in newPackets)
        {
            var sz = pk.Length;
            while (sz >= 255) { newSegs.Add(255); sz -= 255; }
            newSegs.Add((byte)sz);
        }

        var newBody = new byte[newPackets.Sum(pk => pk.Length)];
        var bOff = 0;
        foreach (var pk in newPackets)
        {
            Buffer.BlockCopy(pk, 0, newBody, bOff, pk.Length);
            bOff += pk.Length;
        }

        var result = new byte[27 + newSegs.Count + newBody.Length];
        Array.Copy(page, 0, result, 0, 27);
        result[26] = (byte)newSegs.Count;
        for (var i = 0; i < newSegs.Count; i++)
            result[27 + i] = newSegs[i];
        Buffer.BlockCopy(newBody, 0, result, 27 + newSegs.Count, newBody.Length);

        // recompute CRC over the whole page with the CRC field zeroed
        result[22] = 0; result[23] = 0; result[24] = 0; result[25] = 0;
        var crc = OggCrc(result);
        result[22] = (byte)(crc & 0xFF);
        result[23] = (byte)((crc >> 8) & 0xFF);
        result[24] = (byte)((crc >> 16) & 0xFF);
        result[25] = (byte)((crc >> 24) & 0xFF);

        newPage = result;
        return true;
    }

    private static bool IsEncoderComment(byte[] comment)
    {
        var span = comment.AsSpan();
        var i = span.IndexOf((byte)'=');
        if (i < 0)
            return false;
        var key = Encoding.ASCII.GetString(span[..i]);
        return string.Equals(key, "encoder", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadUInt32(byte[] b, int off) =>
        b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);

    private static void WriteUInt32(Stream s, uint value)
    {
        s.WriteByte((byte)value);
        s.WriteByte((byte)(value >> 8));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >> 24));
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i << 24;
            for (var j = 0; j < 8; j++)
                r = (r << 1) ^ ((r & 0x80000000) != 0 ? 0x04c11db7u : 0);
            table[i] = r;
        }
        return table;
    }

    private static uint OggCrc(byte[] buf)
    {
        uint reg = 0;
        foreach (var b in buf)
            reg = (reg << 8) ^ CrcTable[((reg >> 24) ^ b) & 0xFF];
        return reg;
    }
}
