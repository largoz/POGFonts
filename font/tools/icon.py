import sys
from PIL import Image, ImageDraw, ImageFont
font_path, out = sys.argv[1], sys.argv[2]
S = 1024  # draw large, downsample for clean edges
img = Image.new("RGB", (S, S))
d = ImageDraw.Draw(img)
top, bot = (26, 26, 48), (52, 34, 22)  # menu: dark navy fading to warm brown
for y in range(S):
    t = y / (S - 1)
    d.line([(0, y), (S, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(top, bot)))
gold, ink, light = (201, 154, 46), (24, 20, 16), (232, 228, 220)
# selected-button bar, as on the main menu
bar_y0, bar_y1 = 560, 860
d.rectangle([0, bar_y0, S, bar_y1], fill=gold)
def centered(text, size, cy, fill):
    f = ImageFont.truetype(font_path, size)
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    d.text(((S - (r - l)) / 2 - l, cy - (b - t) / 2 - t), text, font=f, fill=fill)
centered("Ґж", 430, 300, light)
centered("ЇЩ", 250, (bar_y0 + bar_y1) / 2, ink)
img.resize((256, 256), Image.LANCZOS).save(out)
print("wrote", out)
