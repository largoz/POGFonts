using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace POGMods.Fonts
{
    /// <summary>
    /// Finds every installed font. Two ways to install one:
    /// <list type="bullet">
    /// <item>A font mod: a folder anywhere under <c>Mods/</c> holding a <c>pogfont.json</c> next to
    /// its font file. Mod managers unpack each package into its own folder there, so a font mod
    /// is a Thunderstore package with no code that depends on POG Fonts.</item>
    /// <item>By hand: a font file in <c>UserData/POGFonts/&lt;game font name&gt;/</c>. The folder
    /// name says which game font it adds letters to.</item>
    /// </list>
    /// </summary>
    internal static class FontCatalog
    {
        public const string DescriptorName = "pogfont.json";
        public const int DescriptorFormat = 1;
        private static readonly string[] FontExtensions = { ".ttf", ".otf" };

        /// <summary>Every installed font, font mods first, each name once.</summary>
        public static List<FontChoice> Build(string modsFolder, string userFolder, Action<string> info, Action<string> warn)
        {
            var choices = new List<FontChoice>();
            void Add(FontChoice choice, string source)
            {
                if (choices.Exists(c => c.Name == choice.Name))
                {
                    warn($"skipped {source}: a font named \"{choice.Name}\" is already installed");
                    return;
                }
                choices.Add(choice);
                info($"font \"{choice.Name}\" (made for {choice.Target}) from {source}");
            }

            // Font mods first, then fonts added by hand; each group in a stable order.
            var descriptors = Find(modsFolder, DescriptorName, warn).Concat(Find(userFolder, DescriptorName, warn)).ToList();
            foreach (var path in descriptors)
            {
                var choice = ReadDescriptor(path, warn);
                if (choice != null) Add(choice, path);
            }

            if (Directory.Exists(userFolder))
            {
                var described = new HashSet<string>(descriptors.Select(Path.GetDirectoryName), StringComparer.OrdinalIgnoreCase);
                foreach (var dir in Directory.GetDirectories(userFolder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    if (described.Contains(dir)) continue; // a font mod copied here by hand: already read above
                    var files = Directory.GetFiles(dir)
                        .Where(f => FontExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                    foreach (var file in files)
                        Add(new FontChoice { Name = Path.GetFileNameWithoutExtension(file), FontFile = file, Target = Path.GetFileName(dir) }, file);
                }
            }
            return choices;
        }

        private static IEnumerable<string> Find(string folder, string fileName, Action<string> warn)
        {
            if (!Directory.Exists(folder)) return Array.Empty<string>();
            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                };
                return Directory.EnumerateFiles(folder, fileName, options)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                warn($"could not search {folder}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Reads a font mod's <c>pogfont.json</c>:
        /// <code>{ "format": 1, "name": "...", "font": "File.ttf", "target": "PasseroOne-Regular SDF", "sample": "..." }</code>
        /// <c>font</c> is relative to the json's folder; <c>format</c> and <c>sample</c> are optional.
        /// </summary>
        private static FontChoice ReadDescriptor(string path, Action<string> warn)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path),
                    new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var root = doc.RootElement;
                string Str(string key) =>
                    root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                int format = root.TryGetProperty("format", out var f) && f.TryGetInt32(out var n) ? n : DescriptorFormat;
                if (format > DescriptorFormat)
                {
                    warn($"skipped {path}: written for a newer POG Fonts (format {format}); update POG Fonts");
                    return null;
                }
                string name = Str("name"), font = Str("font"), target = Str("target");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(font) || string.IsNullOrWhiteSpace(target))
                {
                    warn($"skipped {path}: \"name\", \"font\" and \"target\" are required");
                    return null;
                }
                string file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), font));
                if (!File.Exists(file))
                {
                    warn($"skipped {path}: font file {file} not found");
                    return null;
                }
                return new FontChoice { Name = name, FontFile = file, Target = target, Sample = Str("sample") };
            }
            catch (Exception ex)
            {
                warn($"skipped {path}: {ex.Message}");
                return null;
            }
        }
    }
}
