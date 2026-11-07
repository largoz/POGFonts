import sys
from fontTools.ttLib import TTFont
f = TTFont(sys.argv[1])
print("tables:", sorted(f.keys()))
for rec in f["name"].names:
    if rec.nameID in (0,1,2,3,4,5,6,7,8,9,11,13,14) and rec.platformID == 3:
        print(f"  name[{rec.nameID}]: {rec.toUnicode()[:200]}")
print("unitsPerEm:", f["head"].unitsPerEm, " glyphs:", f["maxp"].numGlyphs, " os2.fsType:", f["OS/2"].fsType, " vendor:", f["OS/2"].achVendID)
cmap = f.getBestCmap()
print("mapped codepoints:", len(cmap))
blocks = {"ASCII": range(0x20,0x7f), "Latin-1": range(0xa0,0x100), "Cyrillic": range(0x400,0x460), "Cyr ext": range(0x490,0x492)}
for k, r in blocks.items():
    have = [c for c in r if c in cmap]
    print(f"  {k}: {len(have)}/{len(r)}")
ua = "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюя’"
missing = [ch for ch in ua if ord(ch) not in cmap]
print("Ukrainian alphabet missing:", " ".join(missing) or "none")
ru_only = "ЁЪЫЭёъыэ"
print("Russian-only present:", " ".join(ch for ch in ru_only if ord(ch) in cmap))
lower_cyr = [chr(c) for c in range(0x430,0x450) if c in cmap]
print("lowercase cyrillic present:", len(lower_cyr))
# are lowercase glyphs distinct from uppercase?
same = sum(1 for c in range(0x410,0x430) if c in cmap and c+0x20 in cmap and cmap[c]==cmap[c+0x20])
print("Cyrillic upper/lower share the same glyph:", same, "of 32")
same_lat = sum(1 for c in range(0x41,0x5b) if c in cmap and c+0x20 in cmap and cmap[c]==cmap[c+0x20])
print("Latin upper/lower share the same glyph:", same_lat, "of 26")
print("glyph outline format:", "glyf (TrueType)" if "glyf" in f else ("CFF" if "CFF " in f else "?"))
