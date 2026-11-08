using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using POGMods.Config;

namespace POGMods.Fonts
{
    /// <summary>
    /// The optional POG Config page: one row per game font, choosing Vanilla or one of its fonts.
    /// Everything that touches POG Config types is in here, behind a NoInlining call, so the mod
    /// loads and works without POG Config installed.
    /// </summary>
    internal static class ConfigPage
    {
        /// <summary>
        /// Must run while every row still shows its default: POG Config records the value each
        /// row has at registration as the one RESTORE DEFAULTS goes back to.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register(List<FontSlot> slots, Func<FontSlot, string> selected, Action<FontSlot, string> select)
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
            POGConfig.Register("POG Fonts", entries, new ModInfo(FontsMod.VERSION,
                "Font manager: for each game font, pick Vanilla or one of the installed font mods. " +
                "A font file put into UserData/POGFonts/<game font name>/ shows up here after a restart.", "largo"));
        }
    }
}
