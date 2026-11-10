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
    /// <item>Add: the chosen font goes first in the fallback list; only letters the game font lacks change.</item>
    /// <item>Replace: the game font's own letters are switched off and its fallback list starts with the
    /// chosen font, then a fresh copy of the game font itself for letters the chosen one lacks. Every
    /// text that uses the game font changes, with its materials (outline, shadow) kept, and texts
    /// created later are covered too, since nothing on the texts themselves is touched.</item>
    /// <item>Vanilla: the asset gets back exactly the letters and fallback list it had.</item>
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
            var checks = new List<(FontSlot slot, FontOption option, TMP_FontAsset target)>();
            foreach (var slot in slots)
            {
                var option = slot.Find(selected(slot)) ?? FontOption.VanillaOption;
                if (!byName.TryGetValue(slot.Target, out var target))
                {
                    if (option.Kind != OptionKind.Vanilla) { pending = true; WarnMissing(slot, slot.Target); }
                    continue;
                }

                var head = new List<TMP_FontAsset>();
                bool replace = false;
                if (option.Kind == OptionKind.Add)
                {
                    var a = AssetFor(option.Choice, target);
                    if (a != null) head.Add(a);
                }
                else if (option.Kind == OptionKind.Replace)
                {
                    TMP_FontAsset r = null;
                    if (option.Choice != null) r = AssetFor(option.Choice, target);
                    else if (!byName.TryGetValue(option.GameFont, out r))
                    {
                        pending = true; // that game font loads later; stay vanilla until it does
                        WarnMissing(slot, option.GameFont);
                    }
                    if (r != null)
                    {
                        head.Add(r);
                        var original = OriginalCopy(target);
                        if (original != null) head.Add(original);
                        replace = true;
                    }
                }

                var state = StateFor(target);
                bool lettersChanged = SetOwnLetters(target, state, !replace);
                bool listChanged = SetFallbacks(target, state, head);
                if (lettersChanged || listChanged)
                {
                    changed = true;
                    string what = head.Count == 0 ? "vanilla" : replace ? $"replaced by {option.Name.Substring(9)}" : $"adds {option.Choice.Name}";
                    _log.Msg($"{target.name}: {what}");
                    checks.Add((slot, head.Count == 0 ? FontOption.VanillaOption : option, target));
                }
            }

            if (changed)
            {
                ForgetRememberedLetters(loaded);
                foreach (var (slot, option, target) in checks) SelfCheck(slot, option, target);
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
            _log.Msg($"{choice.Name}: loaded for {target.name} (sampling {pointSize}, padding {target.atlasPadding}, " +
                     $"{target.atlasRenderMode}, atlas {target.atlasWidth}x{target.atlasHeight})");
            _assets[key] = asset;
            return asset;
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
            if (fonts > 0)
                _log.Msg($"forgot {letters} letters remembered from fallbacks in {fonts} fonts" +
                         (left > 0 ? $" ({left} still remembered)" : ""));
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
        /// Logs where sample letters now come from, so a bug report shows the wiring: for Add from the
        /// added font, for Replace from anything but the game font itself, for Vanilla Latin from the
        /// game font and nothing from this mod.
        /// </summary>
        private void SelfCheck(FontSlot slot, FontOption option, TMP_FontAsset target)
        {
            string sample = option.Kind == OptionKind.Add ? option.Choice.Sample
                          : option.Kind == OptionKind.Replace ? ReplaceSample
                          : ReplaceSample + (slot.Options.Where(o => o.Kind == OptionKind.Add).Select(o => o.Choice.Sample).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "");
            if (string.IsNullOrEmpty(sample)) return;
            _originalCopies.TryGetValue(target.name, out var original);

            var bad = new StringBuilder();
            string from0 = null;
            foreach (char c in sample)
            {
                bool alt;
                var ch = TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, target, true, FontStyles.Normal, FontWeight.Regular, out alt);
                var from = ch?.textAsset;
                string fromName = from != null ? from.name : "none";
                from0 ??= fromName;
                bool ours = from != null && from.name.EndsWith(OwnSuffix) && from != original;
                bool ok = option.Kind switch
                {
                    OptionKind.Add => from != null && from.name == option.Choice.Name + OwnSuffix,
                    OptionKind.Replace => from != null && from != target && from != original,
                    _ => char.IsAscii(c) ? from == target : !ours,
                };
                if (!ok) bad.Append(c).Append('→').Append(fromName).Append(' ');
            }
            if (bad.Length == 0)
                _log.Msg(option.Kind switch
                {
                    OptionKind.Add => $"{target.name}: check OK, {sample} from {option.Choice.Name}",
                    OptionKind.Replace => $"{target.name}: check OK, {sample} no longer from the game font (e.g. from {from0})",
                    _ => $"{target.name}: check OK, vanilla",
                });
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
            if (n > 0) _log.Msg($"refreshed {n} text objects");
        }
    }
}
