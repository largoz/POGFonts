# POG Fonts

Cyrillic letters for the Pit of Goblin main menu font.

The main menu is drawn in Passero One, a font with no Cyrillic letters at all. So when you switch the game to Ukrainian or Russian, every menu button falls back to a plain sans-serif and the menu loses its look. This mod adds the missing letters, drawn to match the original, so that «ПОЧАТИ» and «НАЛАШТУВАННЯ» sit next to «MODS» as if they had always been there.

![The main menu in Ukrainian with this mod](https://raw.githubusercontent.com/largoz/POGFonts/main/docs/menu-uk.png)

## A note from me

I'm not a type designer. I tried to build every letter out of the font's own pieces: Cyrillic letters that look like Latin ones are simply the Latin glyphs (А В Е К М Н О Р С Т Х І), some are mirrored (И from N, Я from R), and the rest are put together from parts of existing glyphs. Ц and Щ borrow the tail of Q, Ч is a turned h, Л and Д use the leg of A, and Б and Ь are built on b. Then I smoothed the joins.

Some letters came out better than others. If you know how to make any of them better, or you want to do the same for another language, I'd be glad to hear from you. Open an issue or a pull request on [GitHub](https://github.com/largoz/POGFonts). The scripts that build the font are in the repository too.

## What it covers

- The whole Ukrainian and Russian alphabets, upper and lower case: 74 letters, from А to Я and а to я, plus Ґ Є І Ї Ё Ъ Ы Э.
- Text the game draws in Passero One, which is the main menu.
- Text that uses the game's other fonts is not changed.

## How it works

Nothing in the game files is changed. When the menu font loads, the mod builds a font from the file it carries inside and adds it to the front of the menu font's fallback list. TextMeshPro only looks there for letters the menu font doesn't have. Latin text looks exactly as before, and other languages are not affected.

On its first run the mod writes the font and its license to `UserData/POGFonts/`, because TextMeshPro reads letters from the file as it needs them.

To remove the mod, delete `POGFonts.dll`.

## Installation

- With a mod manager: just install it.
- By hand: put `POGFonts.dll` into the game's `Mods` folder. You need MelonLoader 0.7.2 or newer.

It works on its own and does not need POG Config.

## Plans

- Font packs: put your own font into a folder to add letters for any language, no code needed.
- Turning font packs on and off from the POG Config panel.

## License

The font is under the SIL Open Font License 1.1 ([font/OFL.txt](https://github.com/largoz/POGFonts/blob/main/font/OFL.txt)). It is based on Passero One by Viktoriya Grabowska (Sorkin Type Co.). "Passero" is a Reserved Font Name under that license, so the modified font is called Goblin Menu.

---

## Українською

Головне меню гри намальоване шрифтом Passero One, у якому немає жодної кириличної літери. Тому українською кнопки меню малюються звичайним шрифтом без засічок. Цей мод додає кирилицю, намальовану під оригінал.

Я не шрифтовий дизайнер. Кожну літеру я намагався зібрати з частин самого шрифту: літери, однакові з латиницею, взяті як є, частина віддзеркалена, решта складена зі шматків інших гліфів і згладжена. Деякі вийшли краще, деякі гірше. Якщо знаєте, як зробити краще, або хочете зробити так само для іншої мови, пишіть на [GitHub](https://github.com/largoz/POGFonts). Там є й скрипти, якими зібрано шрифт.

Мод нічого не змінює у файлах гри й не чіпає латиницю. Щоб прибрати його, видаліть `POGFonts.dll`.

---

## For developers

Build against your game install (the folder with `MelonLoader/`):

```sh
dotnet build POGFonts.csproj -c Release -p:GameDir="<game folder>"
```

`python build_and_deploy.py` builds and copies the DLL into the game's `Mods` folder. The font is embedded in the DLL. How it was made and how to rebuild it is described in [font/README.md](font/README.md).
