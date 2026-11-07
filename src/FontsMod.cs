using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(POGMods.Fonts.FontsMod), "POG Fonts", POGMods.Fonts.FontsMod.VERSION, "largo", "https://github.com/largoz/POGFonts")]
[assembly: MelonOptionalDependencies("POGConfig")]

namespace POGMods.Fonts
{
    public class FontsMod : MelonMod
    {
        public const string VERSION = "1.0.0";

        private const string DataFolderName = "POGFonts";
        private const string ResourcePrefix = "POGFonts.";

        // The built-in font: Passero One (the menu font) with Cyrillic added.
        private const string BuiltinFontFile = "GoblinMenu-Regular.ttf";
        private const string BuiltinName = "Goblin Menu (Cyrillic)";
        private const string BuiltinTarget = "PasseroOne-Regular SDF";
        private const string BuiltinSample = "АЖЩЇҐЄжщїґєё";
        // The font's license has to travel with every copy of the font, so it is written beside it.
        private static readonly string[] BuiltinFiles = { BuiltinFontFile, "OFL.txt" };

        // While a selected font's game font has not been loaded yet, look again this often (seconds).
        private const float RetryInterval = 2f;

        private List<FontSlot> _slots = new List<FontSlot>();
        private FontSettings _settings;
        private FallbackPatcher _patcher;
        private bool _applyRequested, _pending;
        private float _nextTry;

        public override void OnInitializeMelon()
        {
            _patcher = new FallbackPatcher(LoggerInstance);
            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, DataFolderName);

            var builtIns = new List<(string, FontChoice)>();
            if (BuiltinFiles.All(f => ExtractBuiltin(f, dir)))
                builtIns.Add((BuiltinTarget, new FontChoice
                {
                    Name = BuiltinName,
                    FontFile = Path.Combine(dir, BuiltinFontFile),
                    BuiltIn = true,
                    Sample = BuiltinSample,
                }));

            _slots = FontCatalog.Build(builtIns, dir, msg => LoggerInstance.Warning(msg));
            foreach (var slot in _slots)
                LoggerInstance.Msg($"{slot.Label} ({slot.Target}): {string.Join(", ", slot.OptionNames())}; default {slot.DefaultName}");

            _settings = new FontSettings();
            // POG Config records each row's current value as its default, so register before the
            // saved choices are bound: until then every slot reports its default.
            try { ConfigPage.Register(_slots, s => _settings.Selected(s), (s, name) => _settings.Select(s, name)); }
            catch (Exception ex) when (ex is FileNotFoundException || ex is TypeLoadException || ex is MissingMethodException)
            { LoggerInstance.Msg("POG Config not installed: choose fonts in UserData/MelonPreferences.cfg [POG_Fonts]"); }

            foreach (var slot in _slots)
            {
                _settings.Bind(slot);
                if (_settings.StoredIsUnknown(slot, out var stored))
                    LoggerInstance.Warning($"{slot.Label}: saved font \"{stored}\" is not installed, using {slot.DefaultName}");
            }
            _settings.Changed += () => _applyRequested = true;
            _applyRequested = true;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => Apply();

        public override void OnUpdate()
        {
            if (_applyRequested || (_pending && Time.unscaledTime >= _nextTry)) Apply();
        }

        private void Apply()
        {
            _applyRequested = false;
            _nextTry = Time.unscaledTime + RetryInterval;
            if (_slots.Count == 0) { _pending = false; return; }
            _pending = _patcher.Apply(_slots, _settings.Selected);
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
