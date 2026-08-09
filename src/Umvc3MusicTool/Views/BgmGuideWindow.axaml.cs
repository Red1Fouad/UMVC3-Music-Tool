using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Umvc3MusicTool.Views;

public partial class BgmGuideWindow : Window
{
    private DispatcherTimer? _clearTimer;

    public BgmGuideWindow()
    {
        InitializeComponent();
        CrItems.ItemsSource = Parse(CharacterThemes);
        GmItems.ItemsSource = Parse(GameModes);
        StItems.ItemsSource = Parse(Stages);
    }

    private async void OnCopyFileName(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: BgmGuideEntry entry } && Clipboard is { } clipboard)
        {
            var name = Path.GetFileNameWithoutExtension(entry.FileName);
            await clipboard.SetTextAsync(name);
            StatusText.Text = $"Copied {name} to clipboard.";

            _clearTimer?.Stop();
            _clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _clearTimer.Tick += (_, _) =>
            {
                StatusText.Text = string.Empty;
                _clearTimer?.Stop();
            };
            _clearTimer.Start();
        }
    }

    private static List<BgmGuideEntry> Parse(string block)
    {
        var entries = new List<BgmGuideEntry>();
        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var dash = line.IndexOf('-');
            var fileName = dash >= 0 ? line[..dash] : line;
            var displayName = dash >= 0 ? line[(dash + 1)..] : string.Empty;
            entries.Add(new BgmGuideEntry(fileName, displayName));
        }
        return entries;
    }

    private const string CharacterThemes = """
        bgm_cr_000.sngw-Ryu
        bgm_cr_001.sngw-Chun-Li
        bgm_cr_002.sngw-Chris
        bgm_cr_003.sngw-Wesker
        bgm_cr_004.sngw-Viewtiful Joe
        bgm_cr_005.sngw-Dante
        bgm_cr_006.sngw-Trish
        bgm_cr_007.sngw-Frank West
        bgm_cr_008.sngw-Spencer
        bgm_cr_009.sngw-Arthur
        bgm_cr_010.sngw-Amaterasu
        bgm_cr_011.sngw-Zero
        bgm_cr_012.sngw-Tron Bonne
        bgm_cr_013.sngw-Morrigan
        bgm_cr_014.sngw-Felicia
        bgm_cr_015.sngw-C. Viper
        bgm_cr_016.sngw-Akuma
        bgm_cr_017.sngw-Haggar
        bgm_cr_018.sngw-Hsien-Ko
        bgm_cr_019.sngw-Jill
        bgm_cr_020.sngw-Spider-Man
        bgm_cr_021.sngw-Captain America
        bgm_cr_022.sngw-Wolverine
        bgm_cr_023.sngw-Magneto
        bgm_cr_024.sngw-Hulk
        bgm_cr_025.sngw-She-Hulk
        bgm_cr_026.sngw-Taskmaster
        bgm_cr_027.sngw-Iron Man
        bgm_cr_028.sngw-Thor
        bgm_cr_029.sngw-Dr. Doom
        bgm_cr_030.sngw-Deadpool
        bgm_cr_031.sngw-Super Skrull
        bgm_cr_032.sngw-X-23
        bgm_cr_033.sngw-M.O.D.O.K.
        bgm_cr_034.sngw-Phoenix
        bgm_cr_035.sngw-Dormammu
        bgm_cr_037.sngw-Storm
        bgm_cr_038.sngw-Sentinel
        bgm_cr_039.sngw-Shuma Gorath
        bgm_cr_040.sngw-Galactus
        bgm_cr_041.sngw-Phoenix Wright
        bgm_cr_042.sngw-Phoenix Wright(Turnabout Mode)
        bgm_cr_043.sngw-Vergil
        bgm_cr_044.sngw-Nemesis
        bgm_cr_045.sngw-Strider Hiryu
        bgm_cr_046.sngw-Firebrand
        bgm_cr_047.sngw-Nova
        bgm_cr_048.sngw-Iron Fist
        bgm_cr_049.sngw-Ghost Rider
        bgm_cr_050.sngw-Rocket Raccoon
        bgm_cr_051.sngw-Doctor Strange
        bgm_cr_052.sngw-Hawkeye
        """;

    private const string GameModes = """
        bgm_gm_007.sngw-Here Comes A New Challenger! (Normal)
        bgm_gm_008.sngw-Continue! (Normal)
        bgm_gm_009.sngw-Continue! (Dynamic)
        bgm_gm_010.sngw-Game Over
        bgm_gm_011.sngw-Continue Accepted
        bgm_gm_014.sngw-Gallery
        bgm_gm_015.sngw-Online Lobby
        bgm_gm_016.sngw-Ranking
        bgm_gm_017.sngw-Mission Menu
        bgm_gm_018.sngw-Arcade mode final results
        bgm_gm_019.sngw-I Wanna Take You For a Ride
        bgm_gm_020.sngw-Arcade Ending (Type A)
        bgm_gm_021.sngw-Arcade Ending (Type B)
        bgm_gm_022.sngw-Arcade Ending (Type C)
        bgm_gm_023.sngw-Arcade Ending (Type D)
        bgm_gm_024.sngw-Arcade Ending (Type E)
        bgm_gm_026.sngw-Theme of Marvel Vs. Capcom 3 - Fate of Two Worlds
        bgm_gm_027.sngw-License
        bgm_gm_028.sngw-I Wanna Take You For a Ride techno
        bgm_gm_029.sngw-I Wanna Take You For a Ride rock
        bgm_gm_030.sngw-Character Select
        bgm_gm_031.sngw-Character Select (Dynamic)
        bgm_gm_032.sngw-Vs.
        bgm_gm_033.sngw-Results
        bgm_gm_034.sngw-Results (Dynamic)
        bgm_gm_035.sngw-Menu
        bgm_gm_036.sngw-Menu (Dynamic)
        bgm_gm_037.sngw-Intro (Comic Book)
        bgm_gm_038.sngw-Heroes & Heralds Menu
        bgm_gm_039.sngw-Heroes Menu 1
        bgm_gm_040.sngw-Heroes Menu 2
        bgm_gm_041.sngw-Heralds Menu 1
        bgm_gm_042.sngw-Heralds Menu 2
        bgm_gm_043.sngw-End Credits
        """;

    private const string Stages = """
        bgm_st_000.sngw-Training Room
        bgm_st_001.sngw-Danger Room
        bgm_st_001_b.sngw-Danger Room B
        bgm_st_002.sngw-Daily Bugle
        bgm_st_002_b.sngw-Daily Bugle B
        bgm_st_003.sngw-Metro City
        bgm_st_003_b.sngw-Metro City B
        bgm_st_004.sngw-Demon Village
        bgm_st_004_b.sngw-Demon Village B
        bgm_st_005.sngw-Kattleox Island
        bgm_st_005_b.sngw-Kattleox Island B
        bgm_st_006.sngw-Hand Hideout
        bgm_st_006_b.sngw-Hand Hideout B
        bgm_st_007.sngw-S.H.I.E.L.D. Helicarrier
        bgm_st_007_b.sngw-S.H.I.E.L.D. Helicarrier B
        bgm_st_008.sngw-Okami Stage (Unused)
        bgm_st_008_b.sngw-Okami Stage B (Unused)
        bgm_st_009.sngw-Asgard
        bgm_st_009_b.sngw-Asgard B
        bgm_st_010.sngw-TRICELL Laboratory
        bgm_st_010_b.sngw-TRICELL Laboratory B
        bgm_st_011_1.sngw-The Battle for Earth (Round 1)
        bgm_st_011_2.sngw-The Battle for Earth (Round 2)
        bgm_st_011_3.sngw-The Battle for Earth (Round 3)
        """;
}

public sealed record BgmGuideEntry(string FileName, string DisplayName);
