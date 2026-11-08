# POG Fonts

A font manager for Pit of Goblin. Font mods add letters the game's fonts are missing, for languages the game's fonts don't cover. POG Fonts loads them and lets you pick, for each game font, which font mod it uses, or none at all. So two font mods never fight over the same letters, and going back to the game's own look is one click.

On its own it changes nothing. Install a font mod that depends on it, for example [Goblin Menu Cyrillic](https://thunderstore.io/c/pit-of-goblin/p/Largo/Goblin_Menu_Cyrillic/) (Ukrainian and Russian letters for the main menu).

![The main menu in Ukrainian with Goblin Menu Cyrillic](https://raw.githubusercontent.com/largoz/POGFonts/main/docs/menu-uk.png)

## Choosing a font

A font mod is used as soon as it is installed. To switch:

- **With [POG Config](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/):** open MODS, then POG Fonts. Each game font that has font mods gets a row: pick **Vanilla** or one of them. The change shows at once.
- **Without it:** in `UserData/MelonPreferences.cfg`, under `[POG_Fonts]`, set the game font's entry (for the menu font, `PasseroOne_Regular_SDF`) to `"Vanilla"` or to a font's name.

Vanilla means exactly what the game shows without any font mod.

## Making a font mod

A font mod is a Thunderstore package with no code. Copy [packs/GoblinMenuCyrillic](https://github.com/largoz/POGFonts/tree/main/packs/GoblinMenuCyrillic) as a template:

```
manifest.json      the usual package manifest, with "Largo-POG_Fonts-1.1.0" in "dependencies"
icon.png           256x256
README.md
pogfont.json       what the font is and which game font it adds letters to
MyFont.ttf         the font (.ttf or .otf)
OFL.txt            the font's license, if it has one
```

`pogfont.json`:

```json
{
  "format": 1,
  "name": "My Font",
  "font": "MyFont.ttf",
  "target": "PasseroOne-Regular SDF",
  "sample": "ĄĆĘŁŃÓŚŹŻ"
}
```

- `name` is what players see in the list.
- `font` is the file, next to `pogfont.json`.
- `target` is the game font to add letters to. POG Fonts lists every game font's name in the MelonLoader log at startup: `PasseroOne-Regular SDF` is the main menu; the others are `RulerGold SDF`, `Roboto-Medium SDF`, `LiberationSans SDF`, `NotoSansJP-SemiBold SDF`, `NotoSansSC-SemiBold SDF`, `NotoSansTC-SemiBold SDF` and `ZCOOLKuaiLe-Regular SDF`.
- `sample` is optional: a few of your letters. After loading, the log says whether each one is now drawn from your font, which makes problems easy to spot.

Your font only supplies the letters the game font doesn't have. Its Latin letters, if it has any, are not used where the game already has them.

To try a font without making a package, put the font file into `UserData/POGFonts/<game font name>/`, for example `UserData/POGFonts/PasseroOne-Regular SDF/MyFont.ttf`, and restart the game. It shows up under its file name.

How Goblin Menu was drawn, with the scripts, is in [font/README.md](https://github.com/largoz/POGFonts/blob/main/font/README.md).

## How it works

Nothing in the game files is changed. TextMeshPro, which draws the game's text, looks in a font's fallback list for every letter the font doesn't have. When a game font loads, POG Fonts builds the chosen font from its file, with the game font's own size settings, and puts it first in that list. When you switch, the old font leaves the list and TextMeshPro's memory of letters it already found there is cleared, so no letter stays in the old font.

## Installation

- With a mod manager: install a font mod; POG Fonts comes with it.
- By hand: put `POGFonts.dll` into the game's `Mods` folder. You need MelonLoader 0.7.2 or newer.

POG Config is optional and only adds the settings page.

---

## For developers

Build against your game install (the folder with `MelonLoader/`):

```sh
dotnet build POGFonts.csproj -c Release -p:GameDir="<game folder>"
python build_pack.py packs/GoblinMenuCyrillic     # zip a font mod for Thunderstore
```

`python build_and_deploy.py` builds and copies the DLL into the game's `Mods` folder.
