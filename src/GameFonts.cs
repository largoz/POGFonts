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

        // Friendlier names for the settings. Where a font is used was seen in game; see the README.
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["PasseroOne-Regular SDF"] = "Menu font (Passero One)",
        };

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
