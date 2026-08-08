using Avalonia.Controls;

namespace Umvc3MusicTool.Views;

public partial class BgmGuideWindow : Window
{
    public BgmGuideWindow()
    {
        InitializeComponent();
        CrText.Text = CharacterThemes;
        GmText.Text = GameModes;
        StText.Text = Stages;
    }

    private const string CharacterThemes = """
        000 - Ryu
        001 - Chun-Li
        002 - Chris
        003 - Wesker
        004 - Viewtiful Joe
        005 - Dante
        006 - Trish
        007 - Frank West
        008 - Spencer
        009 - Arthur
        010 - Amaterasu
        011 - Zero
        012 - Tron Bonne
        013 - Morrigan
        014 - Felicia
        015 - C. Viper
        016 - Akuma/Gouki
        017 - Haggar
        018 - Hsien-Ko/Lei-Lei
        019 - Jill
        020 - Spider Man
        021 - Cpt. America
        022 - Wolverine
        023 - Magneto
        024 - Hulk
        025 - She-Hulk
        026 - Taskmaster
        027 - Iron Man
        028 - Thor
        029 - Dr. Doom
        030 - Deadpool
        031 - Super Skrull
        032 - X-23
        033 - M.O.D.O.K.
        034 - Phoenix (Jean Grey)
        035 - Dormammu

        Here lies the remains of Dr. Octopus' character slot

        037 - Storm
        038 - Sentinel
        039 - Shuma Gorath
        040 - Galactus
        041 - Phoenix Wright/Ryuuchi Naruhodou
        042 - Phoenix Wright/Ryuuchi Naruhodou (Turnabout Mode)
        043 - Vergil
        044 - Nemesis
        045 - Strider Hiryu
        046 - Firebrand
        047 - Nova
        048 - Iron Fist
        049 - Ghost Rider
        050 - Rocket Raccoon
        051 - Doctor Strange
        052 - Hawkeye
        """;

    private const string GameModes = """
        [WIP]

        011 - Continue accepted
        014 - Gallery
        015 - Online Lobby
        016 - ???
        017 - Mission Menu
        018 - Arcade mode final results
        019 - Character Select alt. 1 (Take You For A Ride)
        020 - Arcade mode ending #1
        021 - ???
        022 - ???
        023 - ???
        024 - ???
        026 - License menu
        027 - License menu (again?)
        028 - Character Select alt. 2 (Take You For A Ride techno)
        029 - Character Select alt. 3 (Take You For A Ride rock)
        030 - Character Select (Classic BGM)
        031 - Character Select (Dynamic BGM)
        032 - VS splash screen
        033 - Results (Classic BGM)
        034 - Results (Dynamic BGM)
        035 - Main Menu (Classic BGM)
        036 - Main Menu (Dynamic BGM)
        037 - Comic book intro
        038 - Heroes & Heralds mode
        039 - Heroes Menu (1)
        040 - Heroes Menu (2)
        041 - Heralds Menu (1)
        042 - Heralds Menu (2)
        043 - Credits? (after each character theme plays)
        """;

    private const string Stages = """
        [WIP]
        """;
}
