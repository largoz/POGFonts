using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace POGMods.Fonts
{
    /// <summary>
    /// Finds every font the player can choose from: the fonts built into this mod, and font files
    /// the player put into <c>UserData/POGFonts/&lt;game font name&gt;/</c>. The folder name says
    /// which game font the files add letters to, so a custom font needs no code and no config file.
    /// </summary>
    internal static class FontCatalog
    {
        private static readonly string[] FontExtensions = { ".ttf", ".otf" };

        // Friendlier row labels for game fonts players are likely to care about.
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["PasseroOne-Regular SDF"] = "Menu font",
        };

        public static List<FontSlot> Build(IEnumerable<(string target, FontChoice choice)> builtIns,
                                           string userFolder, Action<string> warn)
        {
            var slots = new List<FontSlot>();

            FontSlot SlotFor(string target)
            {
                var slot = slots.Find(s => s.Target == target);
                if (slot == null)
                {
                    slot = new FontSlot { Target = target, Label = Labels.TryGetValue(target, out var l) ? l : target };
                    slots.Add(slot);
                }
                return slot;
            }

            // Built-in fonts are the default for their game font.
            foreach (var (target, choice) in builtIns)
            {
                var slot = SlotFor(target);
                slot.Choices.Add(choice);
                slot.DefaultName ??= choice.Name;
            }

            if (Directory.Exists(userFolder))
            {
                foreach (var dir in Directory.GetDirectories(userFolder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var files = Directory.GetFiles(dir)
                        .Where(f => FontExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (files.Count == 0) continue;

                    var slot = SlotFor(Path.GetFileName(dir));
                    foreach (var file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (slot.Has(name))
                        {
                            warn($"skipped {file}: a font named \"{name}\" is already listed for {slot.Target}");
                            continue;
                        }
                        slot.Choices.Add(new FontChoice { Name = name, FontFile = file });
                    }
                    // A game font with only the player's own fonts uses the first of them:
                    // putting a file there is the request to use it.
                    slot.DefaultName ??= slot.Choices.Count > 0 ? slot.Choices[0].Name : FontSlot.Vanilla;
                }
            }

            return slots.Where(s => s.Choices.Count > 0).ToList();
        }
    }
}
