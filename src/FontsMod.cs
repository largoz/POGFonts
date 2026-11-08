using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(POGMods.Fonts.FontsMod), "POG Fonts", POGMods.Fonts.FontsMod.VERSION, "largo", "https://github.com/largoz/POGFonts")]
[assembly: MelonOptionalDependencies("POGConfig")]

namespace POGMods.Fonts
{
    /// <summary>
    /// Font manager. Font mods add letters to the game's fonts; for each game font this picks
    /// one of them, or none (Vanilla), so installed fonts never fight over the same letters.
    /// </summary>
    public class FontsMod : MelonMod
    {
        public const string VERSION = "1.1.0";

        private const string UserFolderName = "POGFonts";

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
            string userFolder = Path.Combine(MelonEnvironment.UserDataDirectory, UserFolderName);
            try { Directory.CreateDirectory(userFolder); } catch { }

            _slots = FontCatalog.Build(MelonEnvironment.ModsDirectory, userFolder,
                msg => LoggerInstance.Msg(msg), msg => LoggerInstance.Warning(msg));
            if (_slots.Count == 0)
            {
                LoggerInstance.Msg($"no fonts installed. Install a font mod, or put a font into {userFolder}{Path.DirectorySeparatorChar}<game font name>{Path.DirectorySeparatorChar}");
                return;
            }
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
    }
}
