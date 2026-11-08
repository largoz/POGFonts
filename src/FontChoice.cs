using System.Collections.Generic;
using Il2CppTMPro;

namespace POGMods.Fonts
{
    /// <summary>One font file that can supply the letters a game font is missing.</summary>
    internal sealed class FontChoice
    {
        public string Name;      // shown in the settings, and what the setting stores
        public string FontFile;  // absolute path of the TTF/OTF
        public string Sample;    // letters whose source is logged after wiring; null to skip the check

        public TMP_FontAsset Asset; // created the first time this choice is used
        public bool Failed;         // the file could not be loaded; never retried
    }

    /// <summary>
    /// One game font and what can be added to it: nothing (vanilla) or one of the choices.
    /// The selection itself lives in <see cref="FontSettings"/>.
    /// </summary>
    internal sealed class FontSlot
    {
        public const string Vanilla = "Vanilla";

        public string Target;      // the game's TMP_FontAsset name, e.g. "PasseroOne-Regular SDF"
        public string Label;       // row label in the settings
        public string DefaultName; // the choice used until the player picks another
        public readonly List<FontChoice> Choices = new List<FontChoice>();
        public bool WarnedMissing;

        /// <summary>Option names in display order: Vanilla first, then every choice.</summary>
        public string[] OptionNames()
        {
            var names = new string[Choices.Count + 1];
            names[0] = Vanilla;
            for (int i = 0; i < Choices.Count; i++) names[i + 1] = Choices[i].Name;
            return names;
        }

        /// <summary>The choice named <paramref name="name"/>, or null for Vanilla or an unknown name.</summary>
        public FontChoice Find(string name) => Choices.Find(c => c.Name == name);

        public bool Has(string name) => name == Vanilla || Find(name) != null;
    }
}
