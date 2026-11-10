using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace POGMods.Fonts
{
    /// <summary>
    /// The game's TextMeshPro font assets, by name. Some load only in levels, so the names seen in
    /// earlier sessions are remembered in UserData/POGFonts/game_fonts.txt, and the list starts with
    /// the ones found in the game's data files (2026-11-10, game 1.0.5), so every font has its row
    /// in the settings from the first launch.
    /// </summary>
    internal sealed class GameFonts
    {
        private static readonly string[] FoundInGameData =
        {
            "LiberationSans SDF", "LiberationSans SDF - Fallback", "Lacquer-Regular SDF", "Nope 8p SDF",
            "NotoSansJP-SemiBold SDF", "NotoSansSC-SemiBold SDF", "NotoSansTC-SemiBold SDF",
            "PasseroOne-Regular SDF", "Roboto-Medium SDF", "RulerGold SDF", "ZCOOLKuaiLe-Regular SDF",
        };

        // Friendlier names for the settings, and where each font shows up. Seen in game on
        // 2026-11-10 with a probe that logged every text and its font (menu, settings, lobby,
        // tavern, shops, HUD, inventory); dungeon floors not covered yet.
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["PasseroOne-Regular SDF"] = "Menu font (Passero One)",
            ["ZCOOLKuaiLe-Regular SDF"] = "In-game UI (ZCOOL KuaiLe)",
            ["RulerGold SDF"] = "Lift displays (Ruler Gold)",
            ["Lacquer-Regular SDF"] = "Painted signs (Lacquer)",
            ["Nope 8p SDF"] = "Price tags (Nope 8p)",
            ["Roboto-Medium SDF"] = "Small labels (Roboto)",
            ["LiberationSans SDF"] = "Mod settings panel (Liberation Sans)",
        };
        private static readonly Dictionary<string, string> Uses = new Dictionary<string, string>
        {
            ["PasseroOne-Regular SDF"] = "Main menu, game settings, intro, news",
            ["ZCOOLKuaiLe-Regular SDF"] = "HUD, item names and descriptions, prompts, chat, loading screen",
            ["RulerGold SDF"] = "Bucket lift floor and time displays, lobby loading text",
            ["Lacquer-Regular SDF"] = "Painted tutorial signs in the tavern",
            ["Nope 8p SDF"] = "Shop price tags, gambling stones",
            ["Roboto-Medium SDF"] = "Version label, a few input fields",
            ["LiberationSans SDF"] = "POG Config's MOD SETTINGS panel",
            ["LiberationSans SDF - Fallback"] = "Backup letters for other fonts",
            ["NotoSansJP-SemiBold SDF"] = "Japanese letters",
            ["NotoSansSC-SemiBold SDF"] = "Simplified Chinese letters",
            ["NotoSansTC-SemiBold SDF"] = "Traditional Chinese letters",
        };

        /// <summary>Settings order: the fonts players notice most first, then the rest by name.</summary>
        public static int Order(string target)
        {
            int i = 0;
            foreach (var k in Labels.Keys) { if (k == target) return i; i++; }
            return 1000;
        }

        /// <summary>Where the font is seen in game, or null if not known.</summary>
        public static string Use(string target) => Uses.TryGetValue(target, out var u) ? u : null;

        private readonly string _file;
        private readonly SortedSet<string> _names = new SortedSet<string>(StringComparer.Ordinal);

        public GameFonts(string file, Action<string> warn)
        {
            _file = file;
            foreach (var n in FoundInGameData) _names.Add(n);
            try
            {
                if (File.Exists(file))
                    foreach (var line in File.ReadAllLines(file))
                        if (!string.IsNullOrWhiteSpace(line)) _names.Add(line.Trim());
            }
            catch (Exception ex) { warn($"could not read {file}: {ex.Message}"); }
        }

        public IReadOnlyCollection<string> Names => _names;

        /// <summary>Adds a newly seen font name. True if it was new (and is now remembered).</summary>
        public bool Add(string name)
        {
            if (!_names.Add(name)) return false;
            try { File.WriteAllLines(_file, _names.Where(n => !FoundInGameData.Contains(n))); } catch { }
            return true;
        }

        public static string Label(string target) =>
            Labels.TryGetValue(target, out var l) ? l : Display(target);

        /// <summary>"RulerGold SDF" → "RulerGold"; the " SDF" suffix means nothing to a player.</summary>
        public static string Display(string target) => target.Replace(" SDF", "");
    }
}
