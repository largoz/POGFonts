"""Draw one glyph's outline huge, with its on-curve points, to spot slivers and micro-steps."""
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from PIL import Image, ImageDraw
ttf, ch, out = sys.argv[1], sys.argv[2], sys.argv[3]
f = TTFont(ttf); gs = f.getGlyphSet(); name = f.getBestCmap()[ord(ch)]
rec = DecomposingRecordingPen(gs); gs[name].draw(rec)
W, H, pad = 1100, 1100, 60
xs, ys = [], []
for op, args in rec.value:
    for p in args:
        xs.append(p[0]); ys.append(p[1])
x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
sc = min((W - 2 * pad) / (x1 - x0), (H - 2 * pad) / (y1 - y0))
T = lambda p: (pad + (p[0] - x0) * sc, H - pad - (p[1] - y0) * sc)
img = Image.new("RGB", (W, H), (24, 22, 30)); d = ImageDraw.Draw(img)
# flatten quadratic curves for drawing
contours, cur, start = [], [], None
for op, args in rec.value:
    if op == "moveTo":
        cur = [args[0]]
    elif op == "lineTo":
        cur.append(args[0])
    elif op == "qCurveTo":
        pts = list(args); prev = cur[-1]
        offs, on = pts[:-1], pts[-1]
        segs = []
        for i, o in enumerate(offs):
            nxt = on if i == len(offs) - 1 else ((o[0] + offs[i + 1][0]) / 2, (o[1] + offs[i + 1][1]) / 2)
            segs.append((prev, o, nxt)); prev = nxt
        for a, b, c in segs:
            for t in [i / 8 for i in range(1, 9)]:
                cur.append(((1-t)**2*a[0] + 2*(1-t)*t*b[0] + t*t*c[0], (1-t)**2*a[1] + 2*(1-t)*t*b[1] + t*t*c[1]))
    elif op in ("closePath", "endPath"):
        contours.append(cur); cur = []
for c in contours:
    d.polygon([T(p) for p in c], fill=(70, 66, 82), outline=(235, 225, 235))
for op, args in rec.value:
    if op in ("moveTo", "lineTo"):
        x, y = T(args[0]); d.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(255, 190, 60))
img.save(out); print(out, "points:", sum(len(a) for _, a in rec.value))
