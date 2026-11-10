using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(POGMods.Fonts.FontsMod), "POG Fonts", POGMods.Fonts.FontsMod.VERSION, "largo", "https://github.com/largoz/POGFonts")]
[assembly: MelonOptionalDependencies("POGConfig")]

namespace POGMods.Fonts
{
    /// <summary>
    /// Font manager. For every font the game uses, the player picks: Vanilla, add a font mod's
    /// letters to it, or replace it with any installed font or another of the game's fonts.
    /// </summary>
    public class FontsMod : MelonMod
    {
        public const string VERSION = "1.2.0";

        private const string UserFolderName = "POGFonts";
        private const string GameFontsFile = "game_fonts.txt";
        private const string AddPrefix = "Add: ";
        private const string ReplacePrefix = "Replace: ";
        private const string GameSuffix = " (game)";

        // While a selected font waits for a game font that has not been loaded yet, look again this often (seconds).
        private const float RetryInterval = 2f;

        private List<FontChoice> _choices = new List<FontChoice>();
        private GameFonts _gameFonts;
        private List<FontSlot> _slots = new List<FontSlot>();
        private FontSettings _settings;
        private FallbackPatcher _patcher;
        private bool _configAvailable = true;
        private bool _applyRequested, _rebuildRequested, _pending;
        private float _nextTry;

        public override void OnInitializeMelon()
        {
            _patcher = new FallbackPatcher(LoggerInstance);
            string userFolder = Path.Combine(MelonEnvironment.UserDataDirectory, UserFolderName);
            try { Directory.CreateDirectory(userFolder); } catch { }

            _choices = FontCatalog.Build(MelonEnvironment.ModsDirectory, userFolder,
                msg => LoggerInstance.Msg(msg), msg => LoggerInstance.Warning(msg));
            _gameFonts = new GameFonts(Path.Combine(userFolder, GameFontsFile), msg => LoggerInstance.Warning(msg));
            _settings = new FontSettings();
            _settings.Changed += () => _applyRequested = true;
            _patcher.GameFontSeen = name => { if (_gameFonts.Add(name)) _rebuildRequested = true; };

            Rebuild();
            LoggerInstance.Msg($"{_choices.Count} fonts installed, {_slots.Count} game fonts");
        }

        /// <summary>(Re)creates one settings row per game font, with every option it has now.</summary>
        private void Rebuild()
        {
            _slots = BuildSlots(_choices, _gameFonts.Names);
            // POG Config records each row's value at registration as its default.
            _settings.CapturingDefaults = true;
            try
            {
                if (_configAvailable) ConfigPage.Register(_slots, s => _settings.Selected(s), (s, name) => _settings.Select(s, name));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is TypeLoadException || ex is MissingMethodException)
            {
                _configAvailable = false;
                LoggerInstance.Msg("POG Config not installed: choose fonts in UserData/MelonPreferences.cfg [POG_Fonts]");
            }
            finally { _settings.CapturingDefaults = false; }

            foreach (var slot in _slots) _settings.Bind(slot, msg => LoggerInstance.Warning(msg));
            _applyRequested = true;
        }

        private static List<FontSlot> BuildSlots(List<FontChoice> choices, IReadOnlyCollection<string> gameFonts)
        {
            var targets = new SortedSet<string>(gameFonts, StringComparer.Ordinal);
            foreach (var c in choices) if (!string.IsNullOrEmpty(c.Target)) targets.Add(c.Target);

            var slots = new List<FontSlot>();
            foreach (var target in targets)
            {
                var slot = new FontSlot { Target = target, Label = GameFonts.Label(target) };
                slot.Options.Add(FontOption.VanillaOption);
                foreach (var c in choices.Where(c => c.Target == target))
                    slot.Options.Add(new FontOption { Kind = OptionKind.Add, Name = AddPrefix + c.Name, Choice = c });
                foreach (var c in choices)
                    slot.Options.Add(new FontOption { Kind = OptionKind.Replace, Name = ReplacePrefix + c.Name, Choice = c });
                foreach (var g in gameFonts.Where(g => g != target))
                    slot.Options.Add(new FontOption { Kind = OptionKind.Replace, Name = ReplacePrefix + GameFonts.Display(g) + GameSuffix, GameFont = g });
                // A font mod made for this game font is installed to be used: it is the default.
                slot.DefaultName = slot.Options.FirstOrDefault(o => o.Kind == OptionKind.Add)?.Name ?? FontSlot.Vanilla;
                slots.Add(slot);
            }
            // Rows with a named use first (the menu font), then the rest by name.
            return slots.OrderBy(s => s.Label == GameFonts.Display(s.Target) ? 1 : 0).ThenBy(s => s.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => _applyRequested = true;

        public override void OnUpdate()
        {
            if (_applyRequested || (_pending && Time.unscaledTime >= _nextTry)) Apply();
            if (_rebuildRequested)
            {
                _rebuildRequested = false;
                LoggerInstance.Msg("new game font seen; settings rows updated");
                Rebuild();
            }
        }

        private void Apply()
        {
            _applyRequested = false;
            _nextTry = Time.unscaledTime + RetryInterval;
            _pending = _patcher.Apply(_slots, _settings.Selected);
        }
    }
}
