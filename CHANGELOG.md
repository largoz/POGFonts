## 1.1.0

- POG Fonts is now a font manager only. Fonts are separate mods: a folder with a `pogfont.json` and a font file anywhere under `Mods`, which is how mod managers install a font package. See "Making a font mod" in the README.
- The Cyrillic menu font moved to its own mod, Goblin Menu Cyrillic. If you installed POG Fonts 1.0.0 for it, install that. The files 1.0.0 left in `UserData/POGFonts/` (`GoblinMenu-Regular.ttf`, `OFL.txt`) are no longer used and can be deleted.
- Fonts put into `UserData/POGFonts/<game font name>/` by hand still work.

## 1.0.0

- First release. Adds Cyrillic letters drawn to match the main menu font (Passero One), so Ukrainian and Russian menus no longer switch to a plain fallback font.
- Pick the font per game font: Vanilla, the built-in Goblin Menu, or your own. The choice is in POG Config (optional) or in MelonPreferences.cfg, and it applies without a restart.
- Your own fonts: put a .ttf or .otf into UserData/POGFonts/<game font name>/.
