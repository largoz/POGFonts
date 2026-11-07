using System;
using System.IO;
using System.Reflection;
using System.Text;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(POGMods.Fonts.FontsMod), "POG Fonts", POGMods.Fonts.FontsMod.VERSION, "largo", "https://github.com/largoz/POGFonts")]

namespace POGMods.Fonts
{
    /// <summary>
    /// One font file that adds the letters a game font is missing. TextMeshPro looks in a font
    /// asset's fallback list for any character the asset itself cannot draw, so putting this font
    /// first in that list changes only those characters and leaves every existing glyph alone.
    /// </summary>
    internal sealed class FontPack
    {
        public string Name;     // shown in the log and used for the created font asset
        public string FontFile; // absolute path of the TTF/OTF
        public string Target;   // name of the game's TMP_FontAsset this pack extends
        public string Sample;   // letters checked after wiring, to log where each one now comes from

        public TMP_FontAsset Asset; // created on first sight of Target; null until then
        public bool Failed;         // the font file could not be loaded; never retried
    }

    public class FontsMod : MelonMod
    {
        public const string VERSION = "1.0.0";

        private const string ResourcePrefix = "POGFonts.";
        private const string BuiltinFontFile = "GoblinMenu-Regular.ttf";
        // The font's license has to travel with every copy of the font, so it is written beside it.
        private static readonly string[] BuiltinFiles = { BuiltinFontFile, "OFL.txt" };
        private const string DataFolderName = "POGFonts";

        // While a target font has not been loaded yet, look again this often (seconds).
        private const float RetryInterval = 2f;

        private readonly System.Collections.Generic.List<FontPack> _packs = new System.Collections.Generic.List<FontPack>();
        private bool _pending;
        private float _nextTry;

        public override void OnInitializeMelon()
        {
            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, DataFolderName);
            foreach (var file in BuiltinFiles)
                if (!ExtractBuiltin(file, dir)) return;
            string ttf = Path.Combine(dir, BuiltinFontFile);

            _packs.Add(new FontPack
            {
                Name = "Goblin Menu Cyrillic",
                FontFile = ttf,
                Target = "PasseroOne-Regular SDF",
                Sample = "АЖЩЇҐЄжщїґєё",
            });
            _pending = true;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => TryApply();

        public override void OnUpdate()
        {
            if (_pending && Time.unscaledTime >= _nextTry) TryApply();
        }

        /// <summary>
        /// Wires every pack whose target font is loaded. Safe to call any number of times: a pack
        /// already first in its target's fallback list is left as it is.
        /// </summary>
        private void TryApply()
        {
            _nextTry = Time.unscaledTime + RetryInterval;
            if (_packs.Count == 0) { _pending = false; return; }

            var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            bool pending = false, changed = false;

            foreach (var pack in _packs)
            {
                if (pack.Failed) continue;

                TMP_FontAsset target = null;
                foreach (var f in loaded)
                    if (f != null && f.name == pack.Target) { target = f; break; }
                if (target == null) { pending = true; continue; }

                if (pack.Asset == null)
                {
                    pack.Asset = CreateFor(pack, target);
                    if (pack.Asset == null) { pack.Failed = true; continue; }
                }

                if (EnsureFirstFallback(target, pack.Asset))
                {
                    changed = true;
                    LoggerInstance.Msg($"{pack.Name}: added to {target.name}");
                    SelfCheck(pack, target);
                }
            }

            _pending = pending;
            if (changed) RefreshTexts();
        }

        /// <summary>
        /// Builds a dynamic font asset from the pack's file with the target's own sampling size,
        /// padding, render mode and atlas size, so its letters come out the same size and
        /// softness as the target's and take the target's outline/shadow material settings.
        /// </summary>
        private TMP_FontAsset CreateFor(FontPack pack, TMP_FontAsset target)
        {
            int pointSize = Mathf.RoundToInt(target.faceInfo.pointSize);
            TMP_FontAsset asset;
            try
            {
                asset = TMP_FontAsset.CreateFontAsset(pack.FontFile, 0, pointSize, target.atlasPadding,
                    target.atlasRenderMode, target.atlasWidth, target.atlasHeight);
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"{pack.Name}: could not create a font asset from {pack.FontFile}: {ex.Message}");
                return null;
            }
            if (asset == null)
            {
                LoggerInstance.Error($"{pack.Name}: TextMeshPro could not load {pack.FontFile}");
                return null;
            }

            asset.name = pack.Name + " SDF";
            // Referenced only from the target's fallback list; keep Unity's unused-asset sweep off it.
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (asset.material != null) asset.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var atlases = asset.atlasTextures;
            if (atlases != null)
                foreach (var tex in atlases)
                    if (tex != null) tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

            LoggerInstance.Msg($"{pack.Name}: font asset created (sampling {pointSize}, padding {target.atlasPadding}, " +
                               $"{target.atlasRenderMode}, atlas {target.atlasWidth}x{target.atlasHeight})");
            return asset;
        }

        /// <summary>Puts <paramref name="asset"/> at the front of the target's fallback list. True if anything changed.</summary>
        private static bool EnsureFirstFallback(TMP_FontAsset target, TMP_FontAsset asset)
        {
            var list = target.fallbackFontAssetTable;
            if (list == null)
            {
                list = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                target.fallbackFontAssetTable = list;
            }
            if (list.Count > 0 && list[0] == asset) return false;

            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == asset) list.RemoveAt(i);
            list.Insert(0, asset);
            return true;
        }

        /// <summary>Logs which font each sample letter now resolves to, so a bug report shows the wiring.</summary>
        private void SelfCheck(FontPack pack, TMP_FontAsset target)
        {
            if (string.IsNullOrEmpty(pack.Sample)) return;
            var ours = new StringBuilder();
            var other = new StringBuilder();
            foreach (char c in pack.Sample)
            {
                bool alt;
                var ch = TMP_FontAssetUtilities.GetCharacterFromFontAsset(c, target, true, FontStyles.Normal, FontWeight.Regular, out alt);
                var from = ch?.textAsset;
                if (from != null && from == pack.Asset) ours.Append(c);
                else other.Append(c).Append('→').Append(from != null ? from.name : "none").Append(' ');
            }
            if (other.Length == 0)
                LoggerInstance.Msg($"{pack.Name}: check OK, {ours} drawn from this font");
            else
                LoggerInstance.Warning($"{pack.Name}: check: {ours.Length}/{pack.Sample.Length} from this font; others: {other.ToString().Trim()}");
        }

        /// <summary>Rebuilds visible text so labels already on screen pick up the new letters.</summary>
        private void RefreshTexts()
        {
            int n = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                try { t.ForceMeshUpdate(false, true); n++; } catch { }
            }
            if (n > 0) LoggerInstance.Msg($"refreshed {n} text objects");
        }

        /// <summary>
        /// Writes a file shipped inside this DLL to <paramref name="dir"/>, only when the copy there is
        /// missing or different. TextMeshPro reads glyphs from the font file on demand, so it must stay on disk.
        /// </summary>
        private bool ExtractBuiltin(string fileName, string dir)
        {
            byte[] bytes;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + fileName))
            {
                if (s == null)
                {
                    LoggerInstance.Error($"{fileName} is missing from the DLL");
                    return false;
                }
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    bytes = ms.ToArray();
                }
            }

            string path = Path.Combine(dir, fileName);
            try
            {
                Directory.CreateDirectory(dir);
                if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                    File.WriteAllBytes(path, bytes);
                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"could not write {path}: {ex.Message}");
                return false;
            }
        }
    }
}
