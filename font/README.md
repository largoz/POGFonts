# Goblin Menu: how the font is made

`GoblinMenu-Regular.ttf` is Passero One (the game's main menu font) with 74 Cyrillic letters added. The mod embeds this file. Everything here is so that anyone can rebuild it, improve a letter, or make the same kind of font for another language.

| File | What it is |
|---|---|
| `GoblinMenu-Regular.ttf` | The font the mod ships. |
| `OFL.txt` | Its license (SIL Open Font License 1.1). |
| `source/PasseroOne-Regular.ttf` | The original font, taken from the game (`sharedassets0.assets`, Font object `PasseroOne-Regular`). sha256 `0b259fd9c6da1e4cab5717edb23e5ab63d69e694a718caa225c99e29902cdb86`. |
| `tools/` | The build script and helpers to preview and inspect letters. |

## Rebuild

You need Python 3 with `fonttools`, `skia-pathops` and `Pillow`, plus `UnityPy` for the extraction tools.

```sh
python tools/build_font.py source/PasseroOne-Regular.ttf GoblinMenu-Regular.ttf --smooth 30
```

The result matches the shipped font glyph for glyph. Then build the mod (see the main README) and the new font is inside the DLL.

Looking at the result:

```sh
python tools/preview.py  GoblinMenu-Regular.ttf preview.png 64        # a text sample
python tools/charmap.py  GoblinMenu-Regular.ttf charmap.png 2.5       # every glyph, Latin and Cyrillic
python tools/pairs.py    GoblinMenu-Regular.ttf pairs.png "Цц Щщ" "цукор"   # chosen lines, large
python tools/zoom_glyph.py GoblinMenu-Regular.ttf Д zoom.png          # one outline with its points
python tools/rings.py    GoblinMenu-Regular.ttf                       # letters with closed counters
```

## How the letters are made

The font is rough and chiselled, with open counters (the bowls of letters are not closed rings). Freehand curves would not fit, so every new letter is made from the font's own outlines:

- **Copies of Latin letters**, stored as composites so they render identically: А В Е К М Н О Р С Т Х І Ї Ё, а е о р с у х і ї ё, п from n, б from the old-style 6.
- **Mirrors:** И from N, Я from R, Э from Є. Й is И with the font's own caron, which reads as a breve in this style.
- **Assembled from pieces**: Г is F without its middle arm, and Ґ adds an upturn. П uses the stems of H under the bar of T. Ц, Ш and Щ come from U, with the tail of Q. Ч is a turned h. Л and Д use the left leg of A. Ж is K with a mirrored K. З is a raised 3. Є is C with the arm of E. Ь, Б, Ъ and Ы are built on b. Ю is a stem and an O. Ф is two halves of O and a stem. У is a stretched y.
- **Lowercase letters with no Latin twin** are small caps: the capital scaled to x-height, then thickened by the weight the scaling took away, so the strokes stay even.

**Smoothing (`--smooth R`, in font units, 2048 per em; 30 is used).** Only assembled letters are smoothed. Latin copies stay exactly as drawn. A closing fills the small notches where pieces meet, and an opening with a round pen of radius R removes splinters and rounds corners. Each result is checked: the number of contours must stay the same and the area must not change by more than 5%. Otherwise another radius is tried, and if none passes the letter is left unsmoothed with a warning. R of 40 or more fills the counters of Ф.

Every recipe is in `tools/build_font.py`, one letter at a time.

## Making a font for another language

The same approach works for any script whose letters can be built from Latin shapes:

1. `python tools/list_fonts.py "<game>/Pit of Goblin_Data"` lists the game's fonts. `extract_font.py` pulls one out as a TTF.
2. Add your letters, either with your own script modelled on `build_font.py` or in a font editor such as FontForge.
3. Rename the font. Passero One's license reserves the name "Passero", so a modified version must not use it. Keep the copyright and the license.

4. Put the file into `UserData/POGFonts/<game font name>/` and restart the game. It shows up in the POG Fonts settings next to Vanilla. No rebuild of the mod is needed. If you make a better Goblin Menu, a pull request here is welcome.

## License

SIL Open Font License 1.1, see `OFL.txt`. Original: Passero One, Copyright (c) 2011 Sorkin Type Co, designed by Viktoriya Grabowska, Reserved Font Name "Passero". The Cyrillic additions are under the same license.
