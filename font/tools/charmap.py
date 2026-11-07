"""Render every character the font maps, grouped: Latin, Cyrillic, digits/punctuation, other.
usage: charmap.py <font> <out.png> [scale]   (scale 1 = 112 px cells, 2.5 = print quality)"""
import sys, unicodedata
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont
ttf, out = sys.argv[1], sys.argv[2]
cps = sorted(c for c in TTFont(ttf).getBestCmap() if c > 0x20 and not unicodedata.category(chr(c)).startswith("C"))
def group(c):
    cat = unicodedata.category(chr(c))
    if 0x400 <= c < 0x530:
        return "Кирилиця"
    if cat.startswith("L"):
        return "Латиниця"
    if cat.startswith("N") or cat.startswith("P"):
        return "Цифри і розділові знаки"
    return "Інші символи"
order = ["Латиниця", "Кирилиця", "Цифри і розділові знаки", "Інші символи"]
groups = {g: [c for c in cps if group(c) == g] for g in order}
SCALE = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
COLS, CELL, GLYPH = 16, int(112 * SCALE), int(64 * SCALE)
font = ImageFont.truetype(ttf, GLYPH)
small = ImageFont.truetype(ttf, int(15 * SCALE))
head = ImageFont.truetype(ttf, int(34 * SCALE))
H = 20
for g in order:
    H += int(60 * SCALE) + ((len(groups[g]) + COLS - 1) // COLS) * CELL + 10
img = Image.new("RGB", (COLS * CELL + 40, H), (24, 22, 30)); d = ImageDraw.Draw(img)
y = 20
for g in order:
    d.text((20, y), f"{g} ({len(groups[g])})", font=head, fill=(255, 200, 90)); y += int(60 * SCALE)
    for i, c in enumerate(groups[g]):
        x0, y0 = 20 + (i % COLS) * CELL, y + (i // COLS) * CELL
        d.rectangle((x0, y0, x0 + CELL - 4, y0 + CELL - 4), outline=(55, 52, 66))
        w = font.getlength(chr(c))
        d.text((x0 + (CELL - 4 - w) / 2, y0 + 6 * SCALE), chr(c), font=font, fill=(235, 225, 235))
        d.text((x0 + 5 * SCALE, y0 + CELL - 24 * SCALE), f"{c:04X}", font=small, fill=(120, 118, 140))
    y += ((len(groups[g]) + COLS - 1) // COLS) * CELL + 10
img.save(out)
print(out, img.size, {g: len(v) for g, v in groups.items()})
