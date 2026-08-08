namespace Umvc3MusicTool.Models;

public sealed class ConversionOptions
{
    public const int TargetSampleRate = 48000;

    public bool LoopEnabled { get; set; } = true;
    public long LoopStartSamples { get; set; }
    public long LoopEndSamples { get; set; }
    public int Quality { get; set; } = 10;
    public string Ver { get; set; } = "0002";
}
