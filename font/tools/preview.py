"""Render a preview sheet of the built font: alphabet, words, Latin for comparison."""
import sys
from PIL import Image, ImageDraw, ImageFont
ttf, out = sys.argv[1], sys.argv[2]
size = int(sys.argv[3]) if len(sys.argv) > 3 else 64
f = ImageFont.truetype(ttf, size)
lines = [
    "АБВГҐДЕЄЁЖЗИІЇЙКЛМНОП",
    "РСТУФХЦЧШЩЬЮЯЪЫЭ",
    "абвгґдеєёжзиіїйклмноп",
    "рстуфхцчшщьюяъыэ",
    "ПОЧАТИ  НАЛАШТУВАННЯ  ТИТРИ",
    "ПЕРЕГЛЯНУТИ ЗАСТАВКУ  ЗАВЕРШИТИ",
    "Гобліни! Яка дивовижна подорож.",
    "Їжак з'їв ґудзик; щастя є в усьому.",
    "START  MODS  SETTINGS  QUIT",
]
W = max(int(f.getlength(l)) for l in lines) + 40
img = Image.new("RGB", (W, int(size * 1.45) * len(lines) + 30), (24, 22, 30))
d = ImageDraw.Draw(img)
for i, l in enumerate(lines):
    d.text((20, 15 + i * int(size * 1.45)), l, font=f, fill=(235, 225, 235))
img.save(out)
print(out, img.size)
