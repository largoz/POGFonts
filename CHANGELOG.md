## 1.2.0

- Pick the font for each kind of text in the game: the main menu, the in-game UI, lift displays, painted signs, price tags and the rest. Choose one of the game's own fonts or a font mod. Changes show at once.
- A chosen font draws only the letters it has. Every other letter stays as in the original, so a font without Cyrillic leaves Cyrillic text as it was.
- Font mods made for a game font (Goblin Menu Cyrillic for the menu font) are part of that font's original and fill in its missing letters. Your 1.1.0 choice carries over.
- With POG Config 1.1.0 or newer each row opens a list with search and filters, and says where the font is seen in the game. Older POG Config shows sliders.
- Switching back no longer leaves letters in the old font.
- Quieter log: one line per change.

## 1.1.0

- POG Fonts is now a font manager only. Fonts are separate mods: a folder with a `pogfont.json` and a font file anywhere under `Mods`, which is how mod managers install a font package. See "Making a font mod" in the README.
- The Cyrillic menu font moved to its own mod, Goblin Menu Cyrillic. If you installed POG Fonts 1.0.0 for it, install that. The files 1.0.0 left in `UserData/POGFonts/` (`GoblinMenu-Regular.ttf`, `OFL.txt`) are no longer used and can be deleted.
- Fonts put into `UserData/POGFonts/<game font name>/` by hand still work.

## 1.0.0

- First release. Adds Cyrillic letters drawn to match the main menu font (Passero One), so Ukrainian and Russian menus no longer switch to a plain fallback font.
- Pick the font per game font: Vanilla, the built-in Goblin Menu, or your own. The choice is in POG Config (optional) or in MelonPreferences.cfg, and it applies without a restart.
- Your own fonts: put a .ttf or .otf into UserData/POGFonts/<game font name>/.
