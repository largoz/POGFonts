"""List glyphs that contain a closed counter (a hole: a contour wound opposite to the outline)."""
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.areaPen import AreaPen
f = TTFont(sys.argv[1]); gs = f.getGlyphSet(); cm = f.getBestCmap()
chars = sys.argv[2] if len(sys.argv) > 2 else "".join(chr(c) for c in sorted(cm) if 0x400 <= c < 0x530)
for ch in chars:
    rec = DecomposingRecordingPen(gs); gs[cm[ord(ch)]].draw(rec)
    signed, cur = [], []
    for op, args in rec.value:
        cur.append((op, args))
        if op in ("closePath", "endPath"):
            ap = AreaPen()
            for o, a in cur: getattr(ap, o)(*a)
            signed.append(ap.value); cur = []
    if not signed: continue
    outer = max(signed, key=abs)
    holes = [round(abs(a)) for a in signed if a * outer < 0]
    if holes:
        print(f"{ch}: {len(holes)} closed counter(s), area {holes}")
