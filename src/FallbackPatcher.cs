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
    /// The TextMeshPro side. TextMeshPro looks in a font asset's fallback list for any character
    /// the asset itself cannot draw, so a chosen font goes first in that list: it changes only the
    /// letters the game font lacks and leaves every existing glyph alone. Vanilla means none of
    /// this mod's fonts is in the list.
    /// </summary>
    internal sealed class FallbackPatcher
    {
        private readonly MelonLogger.Instance _log;
        private bool _listedGameFonts;

        public FallbackPatcher(MelonLogger.Instance log) => _log = log;

        /// <summary>
        /// Makes every loaded game font match the selection. Safe to call any number of times.
        /// Returns true while a selected font's game font has not been loaded yet.
        /// </summary>
        public bool Apply(List<FontSlot> slots, Func<FontSlot, string> selected)
        {
            var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (!_listedGameFonts && loaded.Length > 0)
            {
                _listedGameFonts = true;
                var names = loaded.Where(f => f != null).Select(f => f.name).Distinct().OrderBy(n => n);
                _log.Msg($"game fonts (folder names for your own fonts): {string.Join(", ", names)}");
            }

            bool pending = false, changed = false;
            var toCheck = new List<(FontSlot slot, FontChoice choice, TMP_FontAsset target)>();
            foreach (var slot in slots)
            {
                TMP_FontAsset target = null;
                foreach (var f in loaded)
                    if (f != null && f.name == slot.Target) { target = f; break; }

                var choice = slot.Find(selected(slot));
                if (target == null)
                {
                    if (choice != null)
                    {
                        pending = true;
                        if (!slot.WarnedMissing && Time.unscaledTime > 60f)
                        {
                            slot.WarnedMissing = true;
                            _log.Warning($"no game font named \"{slot.Target}\" has loaded yet. " +
                                         "If that is your own folder, check its name against the list above.");
                        }
                    }
                    continue;
                }

                if (choice != null && choice.Asset == null && !choice.Failed)
                    choice.Asset = Create(choice, target);
                var desired = choice != null && !choice.Failed ? choice.Asset : null;

                if (SetFallback(target, slot, desired))
                {
                    changed = true;
                    _log.Msg(desired != null ? $"{target.name}: using {choice.Name}" : $"{target.name}: vanilla");
                    toCheck.Add((slot, desired != null ? choice : null, target));
                }
            }

            if (changed)
            {
                ForgetRememberedLetters(loaded);
                foreach (var (slot, choice, target) in toCheck) SelfCheck(slot, choice, target);
                RefreshTexts();
            }
            return pending;
        }

        /// <summary>
        /// Builds a dynamic font asset from the file with the target's own sampling size, padding,
        /// render mode and atlas size, so its letters come out the same size and softness as the
        /// target's. As a fallback it is drawn with the target's material, outline and all.
        /// </summary>
        private TMP_FontAsset Create(FontChoice choice, TMP_FontAsset target)
        {
            int pointSize = Mathf.RoundToInt(target.faceInfo.pointSize);
            TMP_FontAsset asset = null;
            try
            {
                asset = TMP_FontAsset.CreateFontAsset(choice.FontFile, 0, pointSize, target.atlasPadding,
                    target.atlasRenderMode, target.atlasWidth, target.atlasHeight);
            }
            catch (Exception ex)
            {
                _log.Error($"{choice.Name}: {ex.Message}");
            }
            if (asset == null)
            {
                choice.Failed = true;
                _log.Error($"{choice.Name}: TextMeshPro could not load {choice.FontFile}. Is it a valid TTF/OTF font?");
                return null;
            }

            asset.name = choice.Name + " (POG Fonts)";
            // Referenced only from the target's fallback list; keep Unity's unused-asset sweep off it.
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (asset.material != null) asset.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var atlases = asset.atlasTextures;
            if (atlases != null)
                foreach (var tex in atlases)
                    if (tex != null) tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

            _log.Msg($"{choice.Name}: loaded for {target.name} (sampling {pointSize}, padding {target.atlasPadding}, " +
                     $"{target.atlasRenderMode}, atlas {target.atlasWidth}x{target.atlasHeight})");
            return asset;
        }

        /// <summary>
        /// Leaves <paramref name="desired"/> (or nothing, for vanilla) as the slot's only font in the
        /// target's fallback list, at the front. True if the list had to change.
        /// </summary>
        private static bool SetFallback(TMP_FontAsset target, FontSlot slot, TMP_FontAsset desired)
        {
            var list = target.fallbackFontAssetTable;
            if (list == null)
            {
                if (desired == null) return false;
                list = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                target.fallbackFontAssetTable = list;
            }

            bool IsOurs(TMP_FontAsset f) => f != null && slot.Choices.Any(c => c.Asset != null && c.Asset == f);

            int ours = 0;
            for (int i = 0; i < list.Count; i++) if (IsOurs(list[i])) ours++;
            bool inPlace = desired == null ? ours == 0 : ours == 1 && list.Count > 0 && list[0] == desired;
            if (inPlace) return false;

            for (int i = list.Count - 1; i >= 0; i--)
                if (IsOurs(list[i])) list.RemoveAt(i);
            if (desired != null) list.Insert(0, desired);
            return true;
        }

        /// <summary>
        /// TextMeshPro remembers, in a font asset's own lookup table, every character it once found
        /// through that asset's fallbacks, and looks there first next time. After the fallback list
        /// changes, those remembered characters still point at the old font, so letters already drawn
        /// once would keep it: switching to Vanilla would leave part of a word in this mod's font.
        /// Rebuilding the table from the asset's own characters forgets them.
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

        /// <summary>Entries in the font's lookup table that belong to another font asset.</summary>
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
        /// Logs where the sample letters now come from, so a bug report shows the wiring: from the
        /// chosen font, or for Vanilla from anything but this mod's fonts.
        /// </summary>
        private void SelfCheck(FontSlot slot, FontChoice choice, TMP_FontAsset target)
        {
            string sample = choice?.Sample ?? slot.Choices.Select(c => c.Sample).FirstOrDefault(x => !string.IsNullOrEmpty(x));
            if (string.IsNullOrEmpty(sample)) return;
            string what = choice != null ? choice.Name : "vanilla";
            var bad = new StringBuilder();
            foreach (char c in sample)
            {
                bool alt;
                var ch = TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, target, true, FontStyles.Normal, FontWeight.Regular, out alt);
                var from = ch?.textAsset;
                bool ok = choice != null
                    ? from != null && from == choice.Asset
                    : from == null || !slot.Choices.Any(x => x.Asset != null && x.Asset == from);
                if (!ok) bad.Append(c).Append('→').Append(from != null ? from.name : "none").Append(' ');
            }
            if (bad.Length == 0)
                _log.Msg(choice != null ? $"{what}: check OK, {sample} drawn from this font"
                                        : $"{target.name} vanilla: check OK, none of {sample} from POG Fonts");
            else
                _log.Warning($"{what}: check failed for {target.name}: {bad.ToString().Trim()}");
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
