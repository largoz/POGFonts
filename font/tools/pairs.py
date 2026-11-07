"""Render letter pairs large with baseline / x-height / cap-height guides.
usage: pairs.py <font> <out.png> "Чч Цц" "Тт Уу" ..."""
import sys
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont
ttf, out, rows = sys.argv[1], sys.argv[2], sys.argv[3:]
SIZE = 190
upm = TTFont(ttf)["head"].unitsPerEm
f = ImageFont.truetype(ttf, SIZE)
asc = f.getmetrics()[0]
k = SIZE / upm
ROW_H = 330
W = max(int(f.getlength(r)) for r in rows) + 80
img = Image.new("RGB", (W, ROW_H * len(rows) + 30), (24, 22, 30)); d = ImageDraw.Draw(img)
for i, r in enumerate(rows):
    top = 30 + i * ROW_H
    base = top + asc
    for y, col in ((0, (95, 95, 130)), (983, (60, 110, 70)), (1310, (120, 65, 65))):
        d.line([(10, base - y * k), (W - 10, base - y * k)], fill=col)
    d.text((40, top), r, font=f, fill=(235, 225, 235))
img.save(out); print(out, img.size)
