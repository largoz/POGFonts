"""Render the mod icon (256x256): three rows of letters in the font, the middle one highlighted.

usage: icon.py <font.ttf> <out.png> [--style menu|primer] [--rows ROW1 ROW2 ROW3]

menu    the game's main menu: a dark cave lit by a teal lantern, the middle row on a gold
        brush stroke like the selected menu button
primer  an old alphabet book: letters inked on parchment over copybook guide lines, the middle
        row swiped with a gold highlighter
"""
import random
import sys
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

font_path, out = sys.argv[1], sys.argv[2]
style = sys.argv[sys.argv.index("--style") + 1] if "--style" in sys.argv else "menu"
rows = sys.argv[sys.argv.index("--rows") + 1:][:3] if "--rows" in sys.argv else ["АБВ", "ЬЪЫ", "ЇЙИ"]

S = 1024            # drawn large, downsampled at the end for clean edges
ROW_H = S / 3
SIZE = 250          # letter size in px at S
rnd = random.Random(7)  # fixed seed: the same icon every run


def vertical_gradient(top, bottom):
    img = Image.new("RGB", (S, S))
    d = ImageDraw.Draw(img)
    for y in range(S):
        t = y / (S - 1)
        d.line([(0, y), (S, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)))
    return img


def glow(img, center, radius, color, strength):
    """Adds a soft light of `color` around `center`."""
    layer = Image.new("RGB", (S, S))
    cx, cy = center
    ImageDraw.Draw(layer).ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=color)
    layer = layer.filter(ImageFilter.GaussianBlur(radius * 0.45))
    return ImageChops.add(img, Image.blend(Image.new("RGB", (S, S)), layer, strength))


def vignette(img, strength):
    mask = Image.new("L", (S, S), 0)
    m = S * 0.08
    ImageDraw.Draw(mask).ellipse([-m, -m, S + m, S + m], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(S * 0.18))
    dark = Image.blend(img, Image.new("RGB", (S, S)), strength)
    return Image.composite(img, dark, mask)


def grain(img, amount):
    noise = Image.effect_noise((S, S), 40).convert("RGB")
    return Image.blend(img, ImageChops.overlay(img, noise), amount)


def ragged_band(y0, y1, x0, x1, jitter, tilt=0.0):
    """Polygon of a paint stroke between x0..x1: wobbly long edges, torn ends."""
    step = 24
    top, bottom = [], []
    for x in range(int(x0), int(x1) + step, step):
        lift = (x - S / 2) * tilt
        top.append((x, y0 + lift + rnd.uniform(-jitter, jitter)))
        bottom.append((x, y1 + lift + rnd.uniform(-jitter, jitter)))
    # torn ends: a few points stepping in and out
    end_r = [(x1 + rnd.uniform(-30, 30), y0 + (y1 - y0) * k / 5 + (x1 - S / 2) * tilt) for k in range(1, 5)]
    end_l = [(x0 + rnd.uniform(-30, 30), y1 - (y1 - y0) * k / 5 + (x0 - S / 2) * tilt) for k in range(1, 5)]
    return top + end_r + bottom[::-1] + end_l


def letters(img, font, colors, shadow=None):
    d = ImageDraw.Draw(img)
    # One grid for every row, from the cap height of a plain letter, so accents (Ї Й) rise
    # above it as in text instead of pushing their row down.
    _, cap_top, _, cap_bottom = d.textbbox((0, 0), "Н", font=font)
    cap_h = cap_bottom - cap_top
    for i, text in enumerate(rows):
        l, _, r, _ = d.textbbox((0, 0), text, font=font)
        x = (S - (r - l)) / 2 - l
        y = ROW_H * i + (ROW_H - cap_h) / 2 - cap_top + (22 if i == 2 else 0)
        if shadow and i != 1:
            d.text((x + 8, y + 10), text, font=font, fill=shadow)
        d.text((x, y), text, font=font, fill=colors[i])


font = ImageFont.truetype(font_path, SIZE)

if style == "menu":
    img = vertical_gradient((18, 20, 40), (46, 30, 20))
    img = glow(img, (S * 0.92, S * 0.04), S * 0.42, (40, 170, 160), 0.55)   # the lantern
    img = glow(img, (S * 0.5, S * 1.05), S * 0.45, (150, 80, 20), 0.35)     # warm light from below
    img = vignette(img, 0.6)
    img = grain(img, 0.25)
    # gold brush stroke behind the middle row
    stroke = Image.new("L", (S, S), 0)
    ImageDraw.Draw(stroke).polygon(ragged_band(ROW_H + 18, 2 * ROW_H - 18, 40, S - 40, 7), fill=255)
    stroke = stroke.filter(ImageFilter.GaussianBlur(2))
    gold = vertical_gradient((222, 174, 60), (176, 124, 30))
    streaks = ImageDraw.Draw(gold)
    for _ in range(40):  # bristle marks
        y = rnd.uniform(ROW_H + 20, 2 * ROW_H - 20)
        x = rnd.uniform(0, S * 0.6)
        streaks.line([(x, y), (x + rnd.uniform(120, 420), y + rnd.uniform(-3, 3))],
                     fill=(150, 104, 24) if rnd.random() < 0.5 else (236, 196, 92), width=rnd.choice([2, 3, 4]))
    gold = gold.filter(ImageFilter.GaussianBlur(1.2))
    img = Image.composite(gold, img, stroke)
    letters(img, font, [(226, 220, 206), (26, 18, 10), (226, 220, 206)], shadow=(8, 8, 14))

elif style == "primer":
    img = vertical_gradient((236, 220, 180), (222, 200, 152))
    stains = Image.new("RGB", (S, S), (255, 255, 255))
    sd = ImageDraw.Draw(stains)
    for _ in range(7):
        cx, cy, rr = rnd.uniform(0, S), rnd.uniform(0, S), rnd.uniform(50, 160)
        sd.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(238, 228, 206))  # age stains
    img = ImageChops.multiply(img, stains.filter(ImageFilter.GaussianBlur(40)))
    img = vignette(img, 0.35)
    img = grain(img, 0.3)
    d = ImageDraw.Draw(img)
    _, cap_top, _, cap_bottom = d.textbbox((0, 0), "Н", font=font)
    cap_h = cap_bottom - cap_top
    for i in range(3):  # copybook guide lines: baseline and cap line of each row
        base = ROW_H * i + (ROW_H + cap_h) / 2 + (22 if i == 2 else 0)
        for yy in (base, base - cap_h):
            d.line([(30, yy), (S - 30, yy)], fill=(150, 160, 190), width=3)
    d.line([(110, 0), (110, S)], fill=(200, 110, 110), width=4)  # margin
    # gold highlighter, multiplied so the paper shows through
    hl = Image.new("RGB", (S, S), (255, 255, 255))
    ImageDraw.Draw(hl).polygon(ragged_band(ROW_H + 30, 2 * ROW_H - 30, 60, S - 50, 6, tilt=-0.03),
                               fill=(250, 196, 60))
    img = ImageChops.multiply(img, hl.filter(ImageFilter.GaussianBlur(3)))
    ink = (38, 28, 22)
    letters(img, font, [ink, ink, ink])

img.resize((256, 256), Image.LANCZOS).save(out)
print("wrote", out)
