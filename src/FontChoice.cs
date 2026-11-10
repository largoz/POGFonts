using System;
using System.Collections.Generic;
using Il2CppTMPro;

namespace POGMods.Fonts
{
    /// <summary>One installed font file: a font mod, or a font added by hand.</summary>
    internal sealed class FontChoice
    {
        public string Name;      // shown in the settings
        public string FontFile;  // absolute path of the TTF/OTF
        public string Target;    // the game font it was made to add letters to
        public string Sample;    // its own letters, checked after wiring; null to skip the check
    }

    internal enum OptionKind { Vanilla, Replace }

    /// <summary>
    /// One entry in a game font's list: the original, or another font drawing this text. A chosen
    /// font draws only the letters it has; every other letter stays as in the original.
    /// </summary>
    internal sealed class FontOption
    {
        public OptionKind Kind;
        public string Name;          // shown in the settings and stored in the preferences
        public FontChoice Choice;    // the font file, for a font mod
        public string GameFont;      // the game font's asset name, for Replace with another game font

        public static readonly FontOption VanillaOption = new FontOption { Kind = OptionKind.Vanilla, Name = FontSlot.Vanilla };
    }

    /// <summary>One game font (a TextMeshPro font asset) and what can be done with it.</summary>
    internal sealed class FontSlot
    {
        public const string Vanilla = "Vanilla";

        public string Target;      // the game's TMP_FontAsset name, e.g. "PasseroOne-Regular SDF"
        // Font mods made for this game font. They are part of its original: they fill in the
        // letters it lacks whichever option is chosen, and are not offered as options here.
        public readonly List<FontChoice> AddOns = new List<FontChoice>();
        public string Label;       // row label in the settings
        public string DefaultName; // the option used until the player picks another
        public readonly List<FontOption> Options = new List<FontOption>();
        public bool WarnedMissing;

        public string[] OptionNames() => Options.ConvertAll(o => o.Name).ToArray();
        public FontOption Find(string name) => Options.Find(o => o.Name == name);
        public bool Has(string name) => Find(name) != null;
    }

    /// <summary>
    /// What this mod changed on one loaded game font asset, so that Vanilla can put back exactly
    /// what was there. Keyed by the asset instance: a reloaded asset starts untouched.
    /// </summary>
    internal sealed class TargetState
    {
        public IntPtr Instance;
        public List<TMP_FontAsset> OriginalFallbacks;   // the list as the game had it
        public List<TMP_Character> OwnCharacters;      // set while the asset's own letters are switched off
        public AtlasPopulationMode OwnMode;
    }
}
