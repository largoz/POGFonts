using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;

namespace POGMods.Fonts
{
    /// <summary>
    /// The TextMeshPro side. TextMeshPro draws a letter from the text's font asset if the asset has
    /// it, otherwise from the first asset in its fallback list that does. So:
    /// <list type="bullet">
    /// <item>Original: the game font's own letters, then font mods made for it (first in the fallback
    /// list), then the game's own fallbacks. With no such font mod this is exactly the game.</item>
    /// <item>Another font chosen: the game font's own letters are switched off and its fallback list
    /// starts with the chosen font's own letters (no fallbacks of its own), then a fresh copy of the
    /// game font, then the font mods made for it: every letter the chosen font lacks stays as in the
    /// original. Every text that uses the game font changes, materials (outline, shadow) kept, and
    /// texts created later too, since nothing on the texts themselves is touched.</item>
    /// </list>
    /// </summary>
    internal sealed class FallbackPatcher
    {
        public const string OwnSuffix = " (POG Fonts)";
        private const string ReplaceSample = "AaGgZz09";

        private readonly MelonLogger.Instance _log;
        private readonly Dictionary<string, TMP_FontAsset> _assets = new Dictionary<string, TMP_FontAsset>(); // "<font>|<game font>"
        private readonly HashSet<string> _failedFiles = new HashSet<string>();
        private readonly Dictionary<string, TMP_FontAsset> _originalCopies = new Dictionary<string, TMP_FontAsset>();
        private readonly Dictionary<string, TargetState> _states = new Dictionary<string, TargetState>();

        /// <summary>Called with the name of every game font asset found loaded.</summary>
        public Action<string> GameFontSeen;

        // A slider dragged across a dozen options would log a dozen changes: each game font's
        // change is logged once, after it has stayed put for this long.
        private const float LogSettleSeconds = 1f;
        private readonly Dictionary<string, (string text, float at)> _pendingLog = new Dictionary<string, (string, float)>();

        /// <summary>Writes the settled change lines; call every frame.</summary>
        public void FlushLog()
        {
            if (_pendingLog.Count == 0) return;
            foreach (var key in _pendingLog.Keys.ToList())
            {
                var (text, at) = _pendingLog[key];
                if (Time.unscaledTime - at < LogSettleSeconds) continue;
                _log.Msg(text);
                _pendingLog.Remove(key);
            }
        }

        public FallbackPatcher(MelonLogger.Instance log) => _log = log;

        /// <summary>
        /// Makes every loaded game font match the selection. Safe to call any number of times.
        /// Returns true while something selected waits for a font that has not been loaded yet.
        /// </summary>
        public bool Apply(List<FontSlot> slots, Func<FontSlot, string> selected)
        {
            var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            var byName = new Dictionary<string, TMP_FontAsset>();
            foreach (var f in loaded)
            {
                if (f == null) continue;
                string n = f.name;
                if (n.EndsWith(OwnSuffix) || byName.ContainsKey(n)) continue;
                byName[n] = f;
            }
            foreach (var n in byName.Keys) GameFontSeen?.Invoke(n);

            bool pending = false, changed = false;
            var checks = new List<(FontSlot slot, FontOption option, TMP_FontAsset target, TMP_FontAsset chosen, List<TMP_FontAsset> addOns)>();
            foreach (var slot in slots)
            {
                var option = slot.Find(selected(slot)) ?? FontOption.VanillaOption;
                if (!byName.TryGetValue(slot.Target, out var target))
                {
                    if (option.Kind != OptionKind.Vanilla) { pending = true; WarnMissing(slot, slot.Target); }
                    continue;
                }

                // The original: the game font's own letters, then letters from font mods made for it.
                var addOns = new List<TMP_FontAsset>();
                foreach (var c in slot.AddOns)
                {
                    var a = AssetFor(c, target);
                    if (a != null) addOns.Add(a);
                }

                var head = new List<TMP_FontAsset>(addOns);
                bool replace = false;
                TMP_FontAsset chosen = null;
                if (option.Kind == OptionKind.Replace)
                {
                    if (option.Choice != null) chosen = AssetFor(option.Choice, target);
                    else if (byName.TryGetValue(option.GameFont, out var game)) chosen = OwnLettersOf(game, target);
                    else
                    {
                        pending = true; // that game font loads later; stay as the original until it does
                        WarnMissing(slot, option.GameFont);
                    }
                    if (chosen != null)
                    {
                        // The chosen font's own letters first; every letter it lacks as in the original.
                        head.Clear();
                        head.Add(chosen);
                        var original = OriginalCopy(target);
                        if (original != null) head.Add(original);
                        head.AddRange(addOns);
                        replace = true;
                    }
                }

                var state = StateFor(target);
                bool lettersChanged = SetOwnLetters(target, state, !replace);
                bool listChanged = SetFallbacks(target, state, head);
                if (lettersChanged || listChanged)
                {
                    changed = true;
                    string what = replace ? $"drawn in {option.Name.Substring(9)}"
                                : addOns.Count > 0 ? $"original, with letters from {string.Join(", ", slot.AddOns.Select(c => c.Name))}"
                                : "original";
                    _pendingLog[target.name] = ($"{target.name}: {what}", Time.unscaledTime);
                    checks.Add((slot, replace ? option : FontOption.VanillaOption, target, chosen, addOns));
                }
            }

            if (changed)
            {
                ForgetRememberedLetters(loaded);
                foreach (var (slot, option, target, chosen, addOns) in checks) SelfCheck(slot, option, target, chosen, addOns);
                RefreshTexts();
            }
            return pending;
        }

        private void WarnMissing(FontSlot slot, string name)
        {
            if (slot.WarnedMissing || Time.unscaledTime < 120f) return;
            slot.WarnedMissing = true;
            _log.Msg($"{slot.Label}: waiting for the game font \"{name}\" to load (some load only in levels)");
        }

        private TargetState StateFor(TMP_FontAsset target)
        {
            if (_states.TryGetValue(target.name, out var s) && s.Instance == target.Pointer) return s;
            // First time this instance is seen (or the asset was reloaded): remember it as the game made it.
            s = new TargetState { Instance = target.Pointer, OriginalFallbacks = new List<TMP_FontAsset>() };
            var list = target.fallbackFontAssetTable;
            if (list != null)
                for (int i = 0; i < list.Count; i++) s.OriginalFallbacks.Add(list[i]);
            _states[target.name] = s;
            return s;
        }

        /// <summary>Switches the asset's own letters on (as the game had them) or off. True if anything changed.</summary>
        private static bool SetOwnLetters(TMP_FontAsset target, TargetState state, bool on)
        {
            var chars = target.characterTable;
            if (on)
            {
                if (state.OwnCharacters == null) return false;
                chars.Clear();
                foreach (var c in state.OwnCharacters) chars.Add(c);
                target.atlasPopulationMode = state.OwnMode;
                state.OwnCharacters = null;
            }
            else
            {
                if (state.OwnCharacters != null) return false;
                state.OwnCharacters = new List<TMP_Character>();
                for (int i = 0; i < chars.Count; i++) state.OwnCharacters.Add(chars[i]);
                state.OwnMode = target.atlasPopulationMode;
                chars.Clear();
                // Static: a dynamic asset would otherwise add each letter back from its own font file.
                target.atlasPopulationMode = AtlasPopulationMode.Static;
            }
            target.ReadFontAssetDefinition(); // rebuild the lookup from the character table
            return true;
        }

        /// <summary>Sets the fallback list to <paramref name="head"/> followed by the game's own list. True if it changed.</summary>
        private static bool SetFallbacks(TMP_FontAsset target, TargetState state, List<TMP_FontAsset> head)
        {
            var desired = new List<TMP_FontAsset>(head);
            foreach (var f in state.OriginalFallbacks)
                if (!desired.Any(d => d == f)) desired.Add(f);

            var list = target.fallbackFontAssetTable;
            if (list == null)
            {
                if (desired.Count == 0) return false;
                list = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                target.fallbackFontAssetTable = list;
            }
            bool same = list.Count == desired.Count;
            for (int i = 0; same && i < desired.Count; i++) same = list[i] == desired[i];
            if (same) return false;

            list.Clear();
            foreach (var f in desired) list.Add(f);
            return true;
        }

        private TMP_FontAsset AssetFor(FontChoice choice, TMP_FontAsset target)
        {
            string key = choice.Name + "|" + target.name;
            if (_assets.TryGetValue(key, out var cached) && cached != null) return cached;
            if (_failedFiles.Contains(choice.FontFile)) return null;

            int pointSize = Mathf.RoundToInt(target.faceInfo.pointSize);
            TMP_FontAsset asset = null;
            try
            {
                asset = TMP_FontAsset.CreateFontAsset(choice.FontFile, 0, pointSize, target.atlasPadding,
                    target.atlasRenderMode, target.atlasWidth, target.atlasHeight);
            }
            catch (Exception ex) { _log.Error($"{choice.Name}: {ex.Message}"); }
            if (asset == null)
            {
                _failedFiles.Add(choice.FontFile);
                _log.Error($"{choice.Name}: TextMeshPro could not load {choice.FontFile}. Is it a valid TTF/OTF font?");
                return null;
            }
            Keep(asset, choice.Name + OwnSuffix);
            MelonDebug.Msg($"[POG Fonts] {choice.Name}: loaded for {target.name} (sampling {pointSize}, padding {target.atlasPadding}, " +
                     $"{target.atlasRenderMode}, atlas {target.atlasWidth}x{target.atlasHeight})");
            _assets[key] = asset;
            return asset;
        }

        /// <summary>
        /// Another game font's own letters, without its fallback list: chosen for this text, it must
        /// draw only what it has, and leave every other letter to this text's original (otherwise
        /// Lacquer, say, would bring Roboto's Cyrillic along). Built from its font file at this text's
        /// sampling settings, or, for a fixed-atlas font with no file, as a clone with no fallbacks.
        /// </summary>
        private TMP_FontAsset OwnLettersOf(TMP_FontAsset game, TMP_FontAsset target)
        {
            string key = "game:" + game.name + "|" + target.name;
            if (_assets.TryGetValue(key, out var cached) && cached != null) return cached;

            TMP_FontAsset copy = null;
            string what;
            var source = game.sourceFontFile;
            try
            {
                if (source != null)
                {
                    copy = TMP_FontAsset.CreateFontAsset(source, Mathf.RoundToInt(target.faceInfo.pointSize), target.atlasPadding,
                        target.atlasRenderMode, target.atlasWidth, target.atlasHeight, AtlasPopulationMode.Dynamic, true);
                    what = "from its font file";
                }
                else
                {
                    // A fixed atlas never adds glyphs, so sharing its atlas texture is safe.
                    copy = UnityEngine.Object.Instantiate(game);
                    copy.fallbackFontAssetTable = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                    // If that game font is itself switched to another font here, its letters are off;
                    // the clone gets them back from what was saved.
                    if (_states.TryGetValue(game.name, out var st) && st.Instance == game.Pointer && st.OwnCharacters != null)
                    {
                        var chars = copy.characterTable;
                        chars.Clear();
                        foreach (var c in st.OwnCharacters) chars.Add(c);
                    }
                    copy.ReadFontAssetDefinition();
                    what = "as a clone of its fixed atlas";
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"{game.name}: could not copy its letters for {target.name} ({ex.Message})");
                return null;
            }
            if (copy == null) return null;
            Keep(copy, GameFonts.Display(game.name) + " for " + GameFonts.Display(target.name) + OwnSuffix);
            MelonDebug.Msg($"[POG Fonts] {game.name}: letters copied for {target.name} {what}");
            _assets[key] = copy;
            return copy;
        }

        /// <summary>
        /// A fresh copy of the game font built from its own font file, used after a replacement font
        /// for letters that one lacks. Null for an asset with no font file (a fixed atlas).
        /// </summary>
        private TMP_FontAsset OriginalCopy(TMP_FontAsset target)
        {
            if (_originalCopies.TryGetValue(target.name, out var cached)) return cached; // null too: no font file
            TMP_FontAsset copy = null;
            var source = target.sourceFontFile;
            if (source != null)
            {
                try
                {
                    copy = TMP_FontAsset.CreateFontAsset(source, Mathf.RoundToInt(target.faceInfo.pointSize), target.atlasPadding,
                        target.atlasRenderMode, target.atlasWidth, target.atlasHeight, AtlasPopulationMode.Dynamic, true);
                }
                catch (Exception ex) { _log.Warning($"{target.name}: no copy of its own font ({ex.Message})"); }
            }
            if (copy != null) Keep(copy, GameFonts.Display(target.name) + " original" + OwnSuffix);
            _originalCopies[target.name] = copy;
            return copy;
        }

        private static void Keep(TMP_FontAsset asset, string name)
        {
            asset.name = name;
            // Referenced only from fallback lists; keep Unity's unused-asset sweep off it.
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (asset.material != null) asset.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var atlases = asset.atlasTextures;
            if (atlases != null)
                foreach (var tex in atlases)
                    if (tex != null) tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        /// <summary>
        /// TextMeshPro remembers, in a font asset's own lookup table, every character it once found
        /// through that asset's fallbacks, and looks there first next time. After a change those
        /// remembered characters still point at the old font, so letters already drawn once would keep
        /// it. Rebuilding the table from the asset's own characters forgets them.
        /// </summary>
        private void ForgetRememberedLetters(IEnumerable<TMP_FontAsset> loaded)
        {
            int fonts = 0, letters = 0, left = 0;
            foreach (var f in loaded)
            {
                if (f == null) continue;
                int foreign = CountRemembered(f);
                if (foreign == 0) continue;
                f.ReadFontAssetDefinition();
                fonts++;
                letters += foreign;
                left += CountRemembered(f);
            }
            if (left > 0)
                _log.Warning($"forgot {letters} letters remembered from fallbacks in {fonts} fonts, but {left} are still remembered");
            else if (fonts > 0)
                MelonDebug.Msg($"[POG Fonts] forgot {letters} letters remembered from fallbacks in {fonts} fonts");
        }

        private static int CountRemembered(TMP_FontAsset font)
        {
            var table = font.characterLookupTable;
            if (table == null) return 0;
            int n = 0;
            foreach (var c in table.Values)
                if (c != null && c.textAsset != null && c.textAsset != font) n++;
            return n;
        }

        /// <summary>
        /// Logs where sample letters now come from, so a bug report shows the wiring. Original: Latin
        /// from the game font, a font mod's own sample letters from that font mod. Another font chosen:
        /// Latin from the chosen font, and a font mod's sample letters (the ones the chosen font may
        /// lack) from the chosen font, the original or the font mod, never from somewhere else.
        /// </summary>
        private void SelfCheck(FontSlot slot, FontOption option, TMP_FontAsset target, TMP_FontAsset chosen, List<TMP_FontAsset> addOns)
        {
            _originalCopies.TryGetValue(target.name, out var original);
            string addOnSample = string.Concat(slot.AddOns.Select(c => c.Sample ?? ""));
            var bad = new StringBuilder();
            void Check(string letters, Func<TMP_Asset, bool> ok)
            {
                foreach (char c in letters)
                {
                    bool alt;
                    var from = TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, target, true, FontStyles.Normal, FontWeight.Regular, out alt)?.textAsset;
                    if (!ok(from)) bad.Append(c).Append('→').Append(from != null ? from.name : "none").Append(' ');
                }
            }
            bool IsAddOn(TMP_Asset a) => a != null && addOns.Any(x => x == a);

            if (option.Kind == OptionKind.Replace)
            {
                Check(ReplaceSample, a => a != null && a == chosen);
                Check(addOnSample, a => a != null && (a == chosen || a == original || IsAddOn(a)));
            }
            else
            {
                Check(ReplaceSample, a => a != null && a == target);
                Check(addOnSample, IsAddOn);
            }
            if (bad.Length == 0)
                MelonDebug.Msg($"[POG Fonts] {target.name}: check OK ({option.Name})");
            else
                _log.Warning($"{target.name}: check failed for {option.Name}: {bad.ToString().Trim()}");
        }

        /// <summary>Rebuilds visible text so labels already on screen pick up the change.</summary>
        private void RefreshTexts()
        {
            int n = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                try { t.ForceMeshUpdate(false, true); n++; } catch { }
            }
            if (n > 0) MelonDebug.Msg($"[POG Fonts] refreshed {n} text objects");
        }
    }
}
