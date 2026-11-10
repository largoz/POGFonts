using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using POGMods.Config;

namespace POGMods.Fonts
{
    /// <summary>
    /// The optional POG Config page: one row per game font. With a POG Config that has list rows
    /// (ChoiceRow) each row opens a list with search and filters; with an older one it falls back to
    /// sliders. Everything that touches POG Config types is in here, behind NoInlining calls, so the
    /// mod loads and works without POG Config installed.
    /// </summary>
    internal static class ConfigPage
    {
        private const string PageName = "POG Fonts";
        private const string TagOriginal = "Original", TagMods = "Font mods", TagGame = "Game fonts";

        /// <summary>
        /// Must run while every row still shows its default: POG Config records the value each row
        /// has at registration as the one RESTORE DEFAULTS goes back to.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register(List<FontSlot> slots, Func<FontSlot, string> selected, Action<FontSlot, string> select)
        {
            try { RegisterWithLists(slots, selected, select); }
            catch (Exception ex) when (ex is TypeLoadException || ex is MissingMethodException)
            { RegisterWithSliders(slots, selected, select); }
        }

        private static ModInfo Info() => new ModInfo(FontsMod.VERSION,
            "Font manager: pick the font for each kind of text in the game. A chosen font draws the letters it has; the rest stay as in the original. " +
            "A font file put into UserData/POGFonts/<game font name>/ shows up here after a restart.", "largo");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterWithLists(List<FontSlot> slots, Func<FontSlot, string> selected, Action<FontSlot, string> select)
        {
            var entries = new List<ConfigEntry>();
            foreach (var slot in slots)
            {
                var s = slot;
                var options = s.Options.Select(o => ToOption(s, o)).ToList();
                var row = new ChoiceRow(s.Label, () => selected(s), v => select(s, v), options)
                    .Filters(TagOriginal, TagMods, TagGame);
                string use = GameFonts.Use(s.Target);
                if (use != null) row.Description(use);
                entries.Add(row);
            }
            POGConfig.Register(PageName, entries, Info());
        }

        private static Option ToOption(FontSlot slot, FontOption o)
        {
            string game = GameFonts.Display(slot.Target);
            const string rest = "letters it lacks stay as in the original";
            if (o.Kind == OptionKind.Replace && o.Choice != null)
                return new Option(o.Name, o.Choice.Name, $"Draws this text; {rest}", TagMods);
            if (o.Kind == OptionKind.Replace)
                return new Option(o.Name, $"{GameFonts.Display(o.GameFont)} (game font)",
                    $"The game's {GameFonts.Display(o.GameFont)}; {rest}", TagGame);
            string with = slot.AddOns.Count > 0 ? ", with letters from " + string.Join(", ", slot.AddOns.Select(c => c.Name)) : ", unchanged";
            return new Option(o.Name, $"{game} (original)", "The game's own font for this text" + with, TagOriginal);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterWithSliders(List<FontSlot> slots, Func<FontSlot, string> selected, Action<FontSlot, string> select)
        {
            var entries = new List<ConfigEntry>();
            foreach (var slot in slots)
            {
                var s = slot;
                var options = s.OptionNames();
                entries.Add(new OptionsSliderEntry(s.Label,
                    () => Math.Max(0, Array.IndexOf(options, selected(s))),
                    i => select(s, options[Math.Clamp(i, 0, options.Length - 1)]),
                    options));
            }
            POGConfig.Register(PageName, entries, Info());
        }
    }
}
