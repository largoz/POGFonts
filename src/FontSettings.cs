using System;
using System.Text.RegularExpressions;
using MelonLoader;

namespace POGMods.Fonts
{
    /// <summary>
    /// The player's choice per game font, kept in <c>UserData/MelonPreferences.cfg</c> under
    /// [POG_Fonts]. The stored value is a font name, not a list position, so adding or removing
    /// a font file never shifts another choice. This entry is the only record of the choice: the
    /// POG Config page, the cfg file and any other mod all go through it, and every change to it
    /// is applied in game.
    /// </summary>
    internal sealed class FontSettings
    {
        public const string Category = "POG_Fonts";

        private readonly MelonPreferences_Category _category;
        private readonly System.Collections.Generic.Dictionary<FontSlot, MelonPreferences_Entry<string>> _entries =
            new System.Collections.Generic.Dictionary<FontSlot, MelonPreferences_Entry<string>>();

        public event Action Changed;

        public FontSettings()
        {
            _category = MelonPreferences.CreateCategory(Category, "POG Fonts");
        }

        public void Bind(FontSlot slot)
        {
            string key = Regex.Replace(slot.Target, "[^A-Za-z0-9_]", "_");
            var entry = _category.GetEntry<string>(key)
                        ?? _category.CreateEntry(key, slot.DefaultName, slot.Label,
                               $"Font that adds letters to the game font \"{slot.Target}\": " +
                               $"{string.Join(", ", slot.OptionNames())}");
            entry.OnEntryValueChanged.Subscribe((_, _) => Changed?.Invoke());
            _entries[slot] = entry;
        }

        /// <summary>
        /// The selected font for <paramref name="slot"/>. A stored name that no longer exists
        /// (its file was removed) falls back to the default for this session but stays stored,
        /// so putting the file back restores the choice.
        /// </summary>
        public string Selected(FontSlot slot)
        {
            if (!_entries.TryGetValue(slot, out var entry)) return slot.DefaultName;
            return slot.Has(entry.Value) ? entry.Value : slot.DefaultName;
        }

        public bool StoredIsUnknown(FontSlot slot, out string stored)
        {
            stored = _entries.TryGetValue(slot, out var e) ? e.Value : null;
            return stored != null && !slot.Has(stored);
        }

        public void Select(FontSlot slot, string name)
        {
            if (!slot.Has(name) || !_entries.TryGetValue(slot, out var entry) || entry.Value == name) return;
            entry.Value = name;
            _category.SaveToFile(false);
        }
    }
}
