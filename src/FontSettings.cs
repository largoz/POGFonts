using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MelonLoader;

namespace POGMods.Fonts
{
    /// <summary>
    /// The player's choice per game font, kept in <c>UserData/MelonPreferences.cfg</c> under
    /// [POG_Fonts]. The stored value is the option's name ("Vanilla", "Add: …", "Replace: …"), not a
    /// list position, so adding or removing a font never shifts another choice. This entry is the
    /// only record of the choice: the POG Config page, the cfg file and any other mod all go through
    /// it, and every change to it is applied in game.
    /// </summary>
    internal sealed class FontSettings
    {
        public const string Category = "POG_Fonts";
        private const string AddPrefix = "Add: ";

        private readonly MelonPreferences_Category _category;
        private readonly Dictionary<string, MelonPreferences_Entry<string>> _entries = new Dictionary<string, MelonPreferences_Entry<string>>();

        public event Action Changed;

        /// <summary>
        /// While true every slot reports its default: POG Config records the value a row has when it
        /// is registered as the one RESTORE DEFAULTS goes back to.
        /// </summary>
        public bool CapturingDefaults;

        public FontSettings()
        {
            _category = MelonPreferences.CreateCategory(Category, "POG Fonts");
        }

        private static string Key(FontSlot slot) => Regex.Replace(slot.Target, "[^A-Za-z0-9_]", "_");

        public void Bind(FontSlot slot, Action<string> warn)
        {
            string key = Key(slot);
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = _category.GetEntry<string>(key) ?? _category.CreateEntry(key, slot.DefaultName, slot.Label);
                entry.OnEntryValueChanged.Subscribe((_, _) => Changed?.Invoke());
                _entries[key] = entry;
            }
            entry.Description = $"Font for the game font \"{slot.Target}\". One of: {string.Join(", ", slot.OptionNames())}";

            // Earlier values that now mean the original: POG Fonts 1.1.0 stored a font mod's bare name
            // (add its letters), 1.2.0 test builds "Add: <font mod>"; a font mod made for this game
            // font is part of its original now, so "Replace: <it>" means the same.
            string v = entry.Value ?? "";
            bool addOn = slot.AddOns.Exists(c => v == c.Name || v == AddPrefix + c.Name || v == "Replace: " + c.Name);
            if (!slot.Has(v) && addOn)
            {
                entry.Value = FontSlot.Vanilla;
                _category.SaveToFile(false);
            }
            else if (!slot.Has(entry.Value))
                warn($"{slot.Label}: saved choice \"{entry.Value}\" is not installed, using {slot.DefaultName}");
        }

        /// <summary>
        /// The selected option for <paramref name="slot"/>. A stored name that no longer exists (its
        /// font was removed) falls back to the default for this session but stays stored, so putting
        /// the font back restores the choice.
        /// </summary>
        public string Selected(FontSlot slot)
        {
            if (CapturingDefaults || !_entries.TryGetValue(Key(slot), out var entry)) return slot.DefaultName;
            return slot.Has(entry.Value) ? entry.Value : slot.DefaultName;
        }

        public void Select(FontSlot slot, string name)
        {
            if (!slot.Has(name) || !_entries.TryGetValue(Key(slot), out var entry) || entry.Value == name) return;
            entry.Value = name;
            _category.SaveToFile(false);
        }
    }
}
