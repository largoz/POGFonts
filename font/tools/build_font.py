"""Build GoblinMenu: Passero One (OFL 1.1) + Cyrillic (Russian + Ukrainian).

Every new letter is either a copy of a Latin glyph (a TrueType composite: identical rendering) or
assembled from pieces of real Passero One outlines, so the hand-drawn irregularity carries over.
Lowercase forms that do not exist in Latin are small caps of the uppercase, scaled to x-height and
re-thickened so stroke weight matches (scale, then grow the outline by the weight that scaling lost).

OFL: "Passero" is a Reserved Font Name, so the derived font is renamed; license and copyright stay.

usage: build_font.py <PasseroOne-Regular.ttf> <out.ttf> [--smooth R]

--smooth R  corner radius in font units (2048 per em), applied to assembled letters only
            (Latin copies are left exactly as drawn): an opening with a round pen of radius R
            cuts off splinters and rounds corners by about R. 0 = off.
--close C   closing radius (default R/2): fills notches narrower than 2*C left at the joins
            of pieces. Keep it small, or it seals the font's deliberate openings.
"""
import sys
import pathops
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.boundsPen import BoundsPen

SRC, OUT = sys.argv[1], sys.argv[2]
SMOOTH = float(sys.argv[sys.argv.index("--smooth") + 1]) if "--smooth" in sys.argv else 0.0
TAIL_STYLE = sys.argv[sys.argv.index("--tail") + 1] if "--tail" in sys.argv else "q"   # q (default): the tail of Q; "side": a trial cedilla curl
PE_STYLE = sys.argv[sys.argv.index("--pe") + 1] if "--pe" in sys.argv else "plain"   # plain (default): П built like the plain H
# Closing fills gaps narrower than 2*R. The font's own openings (the cut in O, the mouth of b's
# bowl) are 50-130 units wide, so the closing radius must stay well under the opening radius
# or it seals them into knobs. Default: half of --smooth.
SMOOTH_CLOSE = float(sys.argv[sys.argv.index("--close") + 1]) if "--close" in sys.argv else SMOOTH / 2
font = TTFont(SRC)
gs = font.getGlyphSet()
cmap = font.getBestCmap()
hmtx = font["hmtx"]

CAP = 1310      # cap height (glyphs vary 1290-1323)
XH = 983        # x-height
STROKE = 230    # typical stem / bar thickness

# ── path helpers ─────────────────────────────────────────────────────────────

def G(name, sx=1.0, sy=1.0, dx=0.0, dy=0.0):
    """Outline of a source glyph, transformed x' = sx*x + dx, y' = sy*y + dy."""
    p = pathops.Path()
    gs[name].draw(TransformPen(p.getPen(), (sx, 0, 0, sy, dx, dy)))
    return p

def xf(path, sx=1.0, sy=1.0, dx=0.0, dy=0.0):
    out = pathops.Path()
    path.draw(TransformPen(out.getPen(), (sx, 0, 0, sy, dx, dy)))
    return out

def poly(*pts):
    p = pathops.Path()
    p.moveTo(*pts[0])
    for pt in pts[1:]:
        p.lineTo(*pt)
    p.close()
    return p

def rect(x0, y0, x1, y1):
    return poly((x0, y0), (x1, y0), (x1, y1), (x0, y1))

def union(*paths):
    out = paths[0]
    for p in paths[1:]:
        out = pathops.op(out, p, pathops.PathOp.UNION, fix_winding=True)
    return out

def minus(a, b):
    return pathops.op(a, b, pathops.PathOp.DIFFERENCE, fix_winding=True)

def clip(a, x0, y0, x1, y1):
    return pathops.op(a, rect(x0, y0, x1, y1), pathops.PathOp.INTERSECTION, fix_winding=True)

def bounds(path):
    bp = BoundsPen(None)
    path.draw(bp)
    return bp.bounds

def mirror(path, adv):
    """Flip horizontally inside an advance width."""
    return xf(path, sx=-1, dx=adv)

def grow(path, d):
    """Grow the outline by d on every side (weight compensation)."""
    if d <= 0:
        return path
    s = pathops.Path(path)
    s.stroke(2 * d, pathops.LineCap.BUTT_CAP, pathops.LineJoin.MITER_JOIN, 4)
    return union(path, s)

def _clean(path):
    c = pathops.Path(path)
    c.convertConicsToQuads()
    c.simplify(fix_winding=True)
    return c

def _band(path, r):
    s = pathops.Path(path)
    s.stroke(2 * r, pathops.LineCap.ROUND_CAP, pathops.LineJoin.ROUND_JOIN, 4)
    return _clean(s)   # round joins are conics and the band self-intersects; ops need it clean

def _morph(path, r, grow_it):
    """Dilate (grow_it) or erode by r. Skia occasionally rejects a band; nudging r fixes it."""
    for rr in (r, r * 0.97, r * 1.03, r * 0.9):
        try:
            band = _band(path, rr)
            op = pathops.PathOp.UNION if grow_it else pathops.PathOp.DIFFERENCE
            return _clean(pathops.op(_clean(path), band, op, fix_winding=True))
        except pathops.PathOpsError:
            continue
    raise RuntimeError(f"smoothing failed at r={r}")

def _area(path):
    return abs(_clean(path).area)

def _contours(path):
    return sum(1 for _ in _clean(path).contours)

SMOOTH_MAX_AREA_CHANGE = 0.05   # a smoothing pass may change a glyph's area by at most 5%

# Per-glyph corner rounding (opening radius) where a letter wants softer outer corners than
# the global --smooth. The closing radius stays global, so counters do not fill in.
ROUND_CORNERS = {0x0414: 90, 0x0434: 65}   # Д д

def smooth(path, r, label="?", r_open=None):
    """Closing with radius r (fill notches and gaps at the joins of pieces), then opening with
    radius r_open (cut off splinters and give corners a radius of about r_open). Checked: Skia can return a wrong result without an
    error (it once dropped the whole bowl of Ю), so the result must keep its contour count and
    stay within SMOOTH_MAX_AREA_CHANGE of the original area, or another radius is tried; if none
    passes, the glyph is left unsmoothed and a warning is printed."""
    r_open = r if r_open is None else r_open
    if r <= 0 and r_open <= 0:
        return path
    a0, n0 = _area(path), _contours(path)
    for k in (1.0, 0.93, 1.07, 0.85, 0.75):
        rc, ro = r * k, r_open * k
        try:
            closed = _morph(_morph(path, rc, True), rc, False) if rc > 0 else path
            out = _morph(_morph(closed, ro, False), ro, True) if ro > 0 else closed
        except RuntimeError:
            continue
        a1, n1 = _area(out), _contours(out)
        if n1 == n0 and abs(a1 - a0) / a0 <= SMOOTH_MAX_AREA_CHANGE:
            return out
    print(f"  WARNING {label}: smoothing r={r} failed its check, glyph left unsmoothed")
    return path

SC_S = (XH - STROKE) / (CAP - STROKE)   # small-cap scale
SC_D = STROKE * (1 - SC_S) / 2          # weight restored by growing the outline
SC_W = 1.06                              # small caps a little wider than a plain scale

def smallcap(path, widen=SC_W):
    """Uppercase -> lowercase: scale to x-height, then restore stroke weight."""
    return grow(xf(path, sx=SC_S * widen, sy=SC_S, dy=SC_D), SC_D)

OPENING = 44   # width of a re-cut opening, kept above 2 * the closing radius

def _holes(path):
    """Closed counters of a path: contours wound opposite to its largest contour."""
    from fontTools.pens.recordingPen import RecordingPen
    from fontTools.pens.areaPen import AreaPen
    rec = RecordingPen()
    _clean(path).draw(rec)
    contours, cur = [], []
    for op, args in rec.value:
        cur.append((op, args))
        if op in ("closePath", "endPath"):
            contours.append(cur); cur = []
    signed = []
    for c in contours:
        ap = AreaPen()
        for op, args in c:
            getattr(ap, op)(*args)
        sub = pathops.Path()
        pen = sub.getPen()
        for op, args in c:
            getattr(pen, op)(*args)
        signed.append((ap.value, sub))
    if not signed:
        return []
    outer = max(signed, key=lambda t: abs(t[0]))[0]
    return [sub for a, sub in signed if a * outer < 0]

def open_counter(path, side, pick="top", h=OPENING):
    """Cut the font's typical opening into a closed counter: a band h tall at the top of the
    counter, from its middle out through the stroke on `side` ("left"/"right"). Found from the
    real counter, so it always connects the counter to the outside."""
    holes = _holes(path)
    if not holes:
        return path
    key = (lambda b: b[3]) if pick == "top" else (lambda b: -b[1])
    hb = max((bounds(hp) for hp in holes), key=key)
    gx0, _, gx1, _ = bounds(path)
    hx0, hy0, hx1, hy1 = hb
    # the band spans the whole counter roof, so the roof stays level (no step where it starts)
    x0, x1 = (gx0 - 60, hx1 - 1) if side == "left" else (hx0 + 1, gx1 + 60)
    return minus(path, rect(x0, hy1 - h, x1, hy1))

def reopen(path, x0, x1, yc, h=OPENING, small=False):
    """This font closes no counters: each has a cut. Growing a small cap seals cuts narrower
    than 2*SC_D, so cut it again, a band h tall centred on yc, from x0 to x1. Coordinates are
    those of the uppercase source; small=True maps them through the small-cap transform."""
    if small:
        x0, x1, yc = x0 * SC_S * SC_W, x1 * SC_S * SC_W, yc * SC_S + SC_D
    return minus(path, rect(x0, yc - h / 2, x1, yc + h / 2))

# ── glyph table helpers ──────────────────────────────────────────────────────

new_glyphs = {}   # name -> (Glyph, advance, lsb)
new_cmap = {}     # codepoint -> name

def uname(cp):
    return f"uni{cp:04X}"

def add_copy(cp, src, dx=0):
    """Exact copy of a source glyph as a TrueType composite."""
    g = Glyph()
    c = GlyphComponent()
    c.glyphName, c.x, c.y, c.flags = src, dx, 0, 0x4  # ROUND_XY_TO_GRID
    g.components = [c]
    g.numberOfContours = -1
    adv, lsb = hmtx[src]
    new_glyphs[uname(cp)] = (g, adv + 2 * dx, lsb + dx)
    new_cmap[cp] = uname(cp)

SPECK = 1500   # units^2: contours smaller than this (about 39x39) are construction debris

def drop_specks(path):
    """Remove debris left by boolean ops: contours, filled or hollow, below SPECK in area."""
    from fontTools.pens.recordingPen import RecordingPen
    from fontTools.pens.areaPen import AreaPen
    rec = RecordingPen()
    _clean(path).draw(rec)
    out = pathops.Path()
    pen = out.getPen()
    cur = []
    for op, args in rec.value:
        cur.append((op, args))
        if op in ("closePath", "endPath"):
            ap = AreaPen()
            for o, a in cur:
                getattr(ap, o)(*a)
            if abs(ap.value) >= SPECK:
                for o, a in cur:
                    getattr(pen, o)(*a)
            cur = []
    return out

def to_glyph(path, label="?", cp=None):
    pen = TTGlyphPen(None)
    path = drop_specks(path)
    clean = pathops.Path(smooth(path, SMOOTH_CLOSE, label, ROUND_CORNERS.get(cp, SMOOTH)))
    clean.simplify(fix_winding=True, keep_starting_points=False, clockwise=True)
    clean.draw(Cu2QuPen(pen, max_err=1.0, reverse_direction=False))
    return pen.glyph()

def add_path(cp, path, lsb=None, rsb=None, adv=None):
    """Add an assembled glyph. Either keep the path's own position inside `adv`,
    or re-space it with the given side bearings."""
    x0, _, x1, _ = bounds(path)
    if adv is None:
        lsb = 120 if lsb is None else lsb
        rsb = 120 if rsb is None else rsb
        path = xf(path, dx=lsb - x0)
        adv = round(lsb + (x1 - x0) + rsb)
    g = to_glyph(path, f"U+{cp:04X} {chr(cp)}", cp)
    g.recalcBounds(None)
    new_glyphs[uname(cp)] = (g, adv, g.xMin if g.numberOfContours else 0)
    new_cmap[cp] = uname(cp)
    return path

built = {}  # codepoint -> path, so lowercase can reuse uppercase constructions

def P(cp):
    return built[cp]

# ── pieces ───────────────────────────────────────────────────────────────────

def h_left_stem(y0=-40, y1=1320):
    return clip(G("H"), 150, y0, 431, y1)

def h_right_stem(y0=-40, y1=1320):
    return clip(G("H"), 808, y0, 1080, y1)

def h_no_bar():
    """H without its crossbar, cut exactly between the crossbar's four inner corners
    (430,680) (817,694) (811,473) (428,469), so both stems keep their own straight edges.
    The bottom edge bends at (694,467); following it leaves no sliver."""
    return minus(G("H"), poly((429, 682), (818, 696), (812, 471), (694, 465), (427, 467)))

def t_bar():
    """T's top bar only, x 40..859. Its underside wanders (1028 left, 1047 middle, 1065 right),
    which leaves bumps where it meets a stem, so it is cut straight at 1066 (224 thick)."""
    return clip(G("T"), 30, 1066, 870, 1300)

def e_bottom_arm():                # E bottom arm, x 387..872
    return clip(G("E"), 380, -80, 880, 232)

def e_mid_arm():                   # E middle arm, x 393..823, y 508..743
    return clip(G("E"), 393, 500, 830, 750)

def fit_x(path, x0, x1):
    bx0, _, bx1, _ = bounds(path)
    sx = (x1 - x0) / (bx1 - bx0)
    return xf(path, sx=sx, dx=x0 - bx0 * sx)

def bar_between(piece, x0, x1):
    """A bar from `piece` spanning exactly x0..x1 with straight vertical ends: stretched a little
    past both ends, then cut, so its own slanted ends never stick out as splinters at a join."""
    return clip(fit_x(piece, x0 - 80, x1 + 80), x0, -2000, x1, 3000)

def breve(cap=False):
    """The font's own caron: in this chopped style its V shape reads as a breve.
    Cap: raised like the dieresis on Ï (+321)."""
    return xf(G("caron"), dy=321 if cap else 0)

def center_on(path, base):
    bx0, _, bx1, _ = bounds(base)
    px0, _, px1, _ = bounds(path)
    return xf(path, dx=(bx0 + bx1) / 2 - (px0 + px1) / 2)

def tail(x0, x1, depth=290):
    """Descender piece cut from a real stem, hanging under the baseline."""
    st = clip(G("H"), 150, 120, 431, 120 + depth + 40)
    st = fit_x(st, x0, x1)
    return xf(st, dy=-120 - depth)

# ── copies of Latin letters ──────────────────────────────────────────────────

for cp, src in {0x0410: "A", 0x0412: "B", 0x0415: "E", 0x041A: "K", 0x041C: "M", 0x041D: "H",
                0x041E: "O", 0x0420: "P", 0x0421: "C", 0x0422: "T", 0x0425: "X", 0x0406: "I",
                0x0407: "Idieresis", 0x0401: "Edieresis",
                0x0430: "a", 0x0435: "e", 0x043E: "o", 0x0440: "p", 0x0441: "c", 0x0443: "y",
                0x0445: "x", 0x043F: "n", 0x0456: "i", 0x0457: "idieresis", 0x0451: "edieresis",
                0x0431: "six"}.items():
    add_copy(cp, src)
for cp, src in {0x0410: "A", 0x0412: "B", 0x041A: "K", 0x041C: "M", 0x041D: "H", 0x0422: "T"}.items():
    built[cp] = G(src)

# ── uppercase constructions ──────────────────────────────────────────────────

# И, Я: mirrored N, R.  Й: И + breve.
built[0x0418] = add_path(0x0418, mirror(G("N"), hmtx["N"][0]), adv=hmtx["N"][0])
built[0x042F] = add_path(0x042F, mirror(G("R"), hmtx["R"][0]), adv=hmtx["R"][0])
built[0x0419] = add_path(0x0419, union(P(0x0418), center_on(breve(cap=True), P(0x0418))), adv=hmtx["N"][0])

# Г: F without its middle arm.  Ґ: Г + an upturn cut from a real stem.
gamma = minus(G("F"), rect(404, 478, 900, 736))
built[0x0413] = add_path(0x0413, gamma, adv=hmtx["F"][0])
GHE_END = 800                                   # arm end, cut vertical so the upturn sits flush
upturn = fit_x(clip(G("H"), 150, 300, 431, 640), GHE_END - 236, GHE_END + 24)
upturn = clip(xf(upturn, dy=1150 - 300), -100, -100, GHE_END, 3000)   # flush with the arm end
built[0x0490] = add_path(0x0490, union(clip(gamma, -100, -100, GHE_END, 2000), upturn), adv=hmtx["F"][0])

# П: H without crossbar + T's bar across the top.  Ц: + bottom bar + tail.  Ш, Щ: three stems.
if PE_STYLE == "plain":      # H stems under T's bar (first version: too plain for this font)
    pe = union(clip(h_no_bar(), 0, -100, 2000, 1200), bar_between(t_bar(), 166, 1071))
    built[0x041F] = add_path(0x041F, pe, adv=hmtx["H"][0])
elif PE_STYLE == "n":        # lowercase n made tall: its own arch and notch, stems lengthened
    # Not scaled (that thickens the arch): n is cut below the arch, the top lifted by the
    # height difference, and the gap filled with a slice of the stems stretched vertically,
    # which keeps both the arch and the stems at their drawn weight.
    lift = CAP - 983
    cut = 520
    n_bottom = clip(G("n"), 0, -100, 1200, cut)
    n_top = xf(clip(G("n"), 0, cut, 1200, 1100), dy=lift)
    n_slice = clip(G("n"), 0, cut - 120, 1200, cut)
    filler = xf(n_slice, sy=(lift + 120) / 120, dy=(cut - 120) - (cut - 120) * (lift + 120) / 120)
    tall_n = union(n_bottom, filler, n_top)
    # widen to capital proportions the same way: split in the middle of the flat arch top
    # (x 561..807), spread the halves, bridge with a slice of the arch stretched sideways
    widen, xcut = 150, 640
    left, right = clip(tall_n, -100, -200, xcut, 2000), clip(tall_n, xcut, -200, 1300, 2000)
    bridge = clip(tall_n, xcut - 40, CAP - 400, xcut, 2000)
    bridge = xf(bridge, sx=(widen + 40) / 40, dx=(xcut - 40) - (xcut - 40) * (widen + 40) / 40)
    pe = union(left, bridge, xf(right, dx=widen))
    built[0x041F] = add_path(0x041F, pe, lsb=hmtx["n"][1], rsb=hmtx["n"][0] - 917)
else:                        # U turned upside down: U's chamfers become the top corners
    pe = xf(G("U"), sx=-1, sy=-1, dx=125 + 1059, dy=-3 + 1307)
    built[0x041F] = add_path(0x041F, pe, lsb=125, rsb=92)
# Ц Ш Щ Ч are built from the font's own U, u and h rather than from straight H stems, so they
# keep its handwriting: U's chamfered corners, u's V-notch at the join, h's chopped arch.
# Descenders continue the right stem straight down with a flat end, the way q does.
# The descender of Ц/Щ is Q's own tail (a short chopped stroke going down and to the right),
# shortened and set under the right stem so it sticks out past it. A plain downward stem made ц
# read as у, whose right stem is exactly that.
def q_tail(sx=1.0, sy=0.62):
    t = clip(G("Q"), 480, -560, 900, 2)            # the tail below the bowl: x 512..870, y -526..0
    return xf(t, sx=sx, sy=sy)

CEDILLA_NECK = (182, 323, 64)   # cedilla's neck top: x from..to, y

def ced_tail(stem_left, stem_right, sx, sy, overlap=40):
    """The cedilla of ç (a chopped hook like a small 5), its neck centred under a stem."""
    t = xf(G("cedilla"), sx=sx, sy=sy)
    neck_c = (CEDILLA_NECK[0] + CEDILLA_NECK[1]) / 2 * sx
    return xf(t, dx=(stem_left + stem_right) / 2 - neck_c, dy=overlap - CEDILLA_NECK[2] * sy)

def tail_under(stem_left, stem_right, sy=0.62, overlap=40):
    small = sy < 0.6
    if TAIL_STYLE == "side":
        # The curl of ç's cedilla (its neck removed) hung on the OUTER side of the stem: the
        # stem's outer edge flares out from y~200 down into the curl, the floor stays clean.
        k = 0.85 if small else 1.0
        # below the neck's bend, and without the neck's remains and the left nub above the
        # curl's bottom stroke (x < 270, y > -289 in the cedilla's own coordinates)
        curl = minus(clip(G("cedilla"), 0, -700, 700, -100), rect(0, -289, 270, 0))
        drop = 60 * k
        dx, dy = stem_right + 10 - 291 * k, -drop + 112 * k    # curl's top-left -> under the outer corner
        curl = xf(curl, sx=k, sy=k, dx=dx, dy=dy)
        m = lambda x, y: (x * k + dx, y * k + dy)
        # left foot of the bracket sits ON the floor's bottom line (-10), so no ledge is left
        bracket = poly((stem_right - 170 * k, -10), (stem_right - 4, 200 * k),
                       m(450, -150), m(341, -112), m(291, -100))
        return union(curl, bracket)
    if TAIL_STYLE == "cedfit":
        # cedilla stretched sideways so its neck top spans exactly the stem's bottom
        k = 0.85 if small else 1.0
        sx_ = (stem_right - stem_left) / (CEDILLA_NECK[1] - CEDILLA_NECK[0])
        t = xf(G("cedilla"), sx=sx_, sy=k)
        return xf(t, dx=stem_left - CEDILLA_NECK[0] * sx_, dy=overlap - CEDILLA_NECK[2] * k)
    if TAIL_STYLE.startswith("ced"):
        k = 0.85 if small else 1.0                      # lowercase gets a slightly smaller hook
        sx_, sy_ = {"ced1": (1.0, 1.0), "ced2": (1.25, 1.25), "ced3": (1.5, 1.0)}[TAIL_STYLE]
        return ced_tail(stem_left, stem_right, sx_ * k, sy_ * k, overlap)
    return q_tail_under(stem_left, stem_right, sy, overlap)

def q_tail_under(stem_left, stem_right, sy=0.62, overlap=40):
    """Q's tail grown out of a stem: its top spans the stem's whole bottom (Q's own tail top is
    x 512..683 at y=-8), so the stem's edges run on into the tail's edges with no ledge or notch."""
    t = xf(q_tail(1.0, sy), dx=-512)
    k = (stem_right - stem_left) / (683 - 512)
    t = xf(t, sx=k, dx=stem_left)
    _, _, _, ty1 = bounds(t)
    return xf(t, dy=overlap - ty1)

U_CORNER = poly((731, 227), (966, 285), (962, -2), (740, -8))   # square U's bottom-right corner
U_DESC = union(U_CORNER, tail_under(740, 962))
tse = union(G("U"), U_DESC)
built[0x0426] = add_path(0x0426, tse, lsb=125, rsb=90)

# Ш: U split down the middle and spread apart, its own bottom stroke bridging the gap, and its
#    left stem (above the chamfer) standing in the middle.
SHA_SPREAD = 460
u_left, u_right = clip(G("U"), 0, -100, 600, 1400), clip(G("U"), 600, -100, 1200, 1400)
u_floor = clip(G("U"), 440, -20, 720, 228)
# U's left stem down to y=195: its outer edge only chamfers below 191, so the piece stays straight
# and still overlaps the floor (top at 222) without any extension seam
mid_stem = clip(G("U"), 100, 195, 421, 1400)   # 421: on the stem's own inner edge (420,270)
mx0, _, mx1, _ = bounds(mid_stem)
centre = (420 + 731 + SHA_SPREAD) / 2
sha = union(u_left, xf(u_right, dx=SHA_SPREAD), bar_between(u_floor, 590, 610 + SHA_SPREAD),
            xf(mid_stem, dx=centre - (mx0 + mx1) / 2))
built[0x0428] = add_path(0x0428, sha, lsb=125, rsb=90)
built[0x0429] = add_path(0x0429, union(sha, xf(U_DESC, dx=SHA_SPREAD)), lsb=125, rsb=90)

# Ч: h turned upside down IS Ч: short stem on top left, h's arch as the join, tall stem right.
turned_h = xf(G("h"), sx=-1, sy=-1, dx=130 + 893, dy=-72 + 1475)
che_sy = (CAP - 18) / (1475 + 72)
che = grow(xf(turned_h, sx=0.95, sy=che_sy, dy=-18 + 72 * che_sy), 12)
built[0x0427] = add_path(0x0427, che, lsb=110, rsb=110)

# Л: A's real left leg cut out of the outline (crossbar removed), under T's bar, + H right stem.
#    Д: the same body, narrowed, on E's bottom arm with two tails.
# crossbar cut follows the leg's inner edge (399,354)-(436,547) exactly, so no step is left
a_leg = minus(clip(G("A"), 60, -40, 560, 1080), poly((392, 310), (700, 310), (700, 556), (438, 556)))
# the bar's left end is cut along the leg's outer edge (202,557)-(338,1237), extended upward
left_of_leg = poly((-300, 557), (202, 557), (390, 1497), (-300, 1497))
def el_body(right=1013):
    bar = minus(bar_between(t_bar(), 150, right), left_of_leg)
    return union(a_leg, bar, xf(h_right_stem(), dx=right - 1073))
el = el_body()
built[0x041B] = add_path(0x041B, el, lsb=85, rsb=120)
de_body = xf(el_body(), sx=0.86, dx=110)
# slit between the leg and the bar (like the cut under the top of B and R), so the counter is open
DE_SLIT = (0, 0.86 * 560 + 110, 1007)
de_body = reopen(de_body, *DE_SLIT, h=56)
de = union(de_body, bar_between(e_bottom_arm(), 60, 1170), tail(60, 290), tail(940, 1170))
built[0x0414] = add_path(0x0414, de, lsb=60, rsb=60)

# Ж: K and mirrored K sharing one stem.
k = G("K")
kc = (162 + 418) / 2
zhe = union(xf(k, dx=520), xf(mirror(k, 2 * kc), dx=520))
built[0x0416] = add_path(0x0416, zhe, lsb=60, rsb=60)

# З: old-style 3 lifted onto the baseline and fitted to cap height.
three = G("three")
_, y0, _, y1 = bounds(three)
sy = (CAP + 18) / (y1 - y0)
built[0x0417] = add_path(0x0417, xf(three, sy=sy, dy=-18 - y0 * sy), lsb=80, rsb=110)

# Є: C + a short middle arm.  Э: mirrored Є.
ye = union(G("C"), xf(clip(G("E"), 393, 500, 730, 750), dx=-50))
built[0x0404] = add_path(0x0404, ye, adv=hmtx["C"][0] + 40)
built[0x042D] = add_path(0x042D, mirror(ye, hmtx["C"][0] + 40), adv=hmtx["C"][0] + 40)

# Ь: lowercase b brought to cap height. Its bowl stays open by the stem, as every bowl in this
#    font is (no closed rings).  Б: Ь + F's top with its stem (one piece, so the corner is F's own).
#    Ъ: Ь + left arm.  Ы: Ь + stem.
b_sc = CAP / 1473
soft = grow(xf(G("b"), sx=1.06, sy=b_sc), STROKE * (1 - b_sc) / 2)
sx0, _, _, _ = bounds(soft)
f_top = clip(G("F"), 140, 1020, 830, 1330)
be = union(clip(soft, -100, -100, 2000, 1035), xf(f_top, dx=sx0 - 149))   # top entirely F's
built[0x042C] = add_path(0x042C, soft, lsb=150, rsb=100)
built[0x0411] = add_path(0x0411, be, lsb=150, rsb=90)
hard = union(xf(soft, dx=300), fit_x(clip(G("T"), 30, 1027, 330, 1300), 60, 480))
built[0x042A] = add_path(0x042A, hard, lsb=60, rsb=100)
yery = union(soft, xf(h_right_stem(), dx=420))
built[0x042B] = add_path(0x042B, yery, lsb=170, rsb=120)

# Ю: stem + short bar + slightly narrowed O.
yu = union(h_left_stem(), clip(G("H"), 420, 440, 620, 712),
           grow(xf(G("O"), sx=0.88, dx=450), 14))
built[0x042E] = add_path(0x042E, yu, lsb=166, rsb=110)

# Ф: the two halves of O pushed apart and flattened, a plain H stem through the middle.
def split_oval(spread, sy, dy, sx=1.0, thicken=16):
    o = G("O", sx=sx)
    x0, _, x1, _ = bounds(o)
    mid = (x0 + x1) / 2
    right = clip(o, mid, -100, x1 + 10, 1400)            # the half with O's cut (kept on the right only)
    left = clip(o, x0 - 10, -100, mid, 1400)
    halves = union(left, xf(right, dx=spread))
    return grow(xf(halves, sy=sy, dy=dy), thicken)

def stem_through(oval, y0, y1):
    ox0, _, ox1, _ = bounds(oval)
    c = (ox0 + ox1) / 2
    return fit_x(clip(G("H"), 150, y0 - 40, 431, y1), c - 118, c + 118)

oval = split_oval(spread=150, sy=0.72, dy=190)
built[0x0424] = add_path(0x0424, union(oval, stem_through(oval, -20, 1300)), lsb=80, rsb=80)

# У: lowercase y stretched to cap height (its tail lands on the baseline).
yy = G("y")
_, y0, _, y1 = bounds(yy)
sy = (CAP + 18) / (y1 - y0)
built[0x0423] = add_path(0x0423, grow(xf(yy, sx=1.08, sy=sy, dy=-18 - y0 * sy), 14), lsb=110, rsb=90)

# ── lowercase: small caps of the uppercase constructions ─────────────────────

# Cuts sealed by small-capping, re-opened on the side the uppercase has them:
# side of the cut, and which counter if there are two.
SMALL_REOPEN = {
    0x0432: ("left", "top"),     # в: upper counter opens left, under the top (as in B)
    0x044F: ("right", "top"),    # я: R's cut, mirrored
    0x044E: ("right", "top"),    # ю: O's top-right cut
    0x0434: ("left", "top"),     # д: between leg and bar (as in Д)
}
for lc, uc in {0x0432: 0x0412, 0x0433: 0x0413, 0x0491: 0x0490, 0x0434: 0x0414, 0x0436: 0x0416,
               0x0437: 0x0417, 0x0438: 0x0418, 0x043A: 0x041A, 0x043B: 0x041B, 0x043C: 0x041C,
               0x043D: 0x041D, 0x0442: 0x0422, 0x0447: 0x0427,
               0x0448: 0x0428,
               0x044D: 0x042D, 0x0454: 0x0404, 0x044E: 0x042E, 0x044F: 0x042F}.items():
    path = smallcap(P(uc))
    if lc in SMALL_REOPEN:
        path = open_counter(path, *SMALL_REOPEN[lc])
    built[lc] = add_path(lc, path, lsb=90, rsb=90)

# щ: small-cap Ш with its bottom-right corner squared, and the tail hung on AFTER small-capping:
#    small-capping grows the outline, which swelled the stem/tail joint into a knob.
sha_sq = union(sha, xf(U_CORNER, dx=SHA_SPREAD))
r0 = (740 + SHA_SPREAD) * SC_S * SC_W - SC_D
r1 = (962 + SHA_SPREAD) * SC_S * SC_W + SC_D
built[0x0449] = add_path(0x0449, union(smallcap(sha_sq), tail_under(r0, r1, sy=0.55)), lsb=90, rsb=90)

# ц: u with the same Q tail under its right stem (u's V-notch at the join stays).
built[0x0446] = add_path(0x0446, union(G("u"), tail_under(714, 880, sy=0.55)),
                         lsb=hmtx["u"][1], rsb=90)

# ь ъ ы: the base of 6 (the same bowl as б), its flag cut off, the stem finished with the top of
#    n's left stem.  ъ: + a small-cap arm.  ы: + i's stem, which has a natural slanted top.
# cut below y=700: above it the inner edge of 6 already bends right into the flag (a bump)
six_base = clip(G("six"), 0, -60, 1000, 700)
n_stem_top = xf(clip(G("n"), 100, 600, 372, 1000), dx=108 - 137)
soft_lc = union(six_base, n_stem_top)
built[0x044C] = add_path(0x044C, soft_lc, lsb=90, rsb=80)
arm_lc = smallcap(clip(G("T"), 30, 1027, 330, 1300))
ax0, _, ax1, _ = bounds(arm_lc)
hard_lc = union(xf(soft_lc, dx=ax1 - ax0 - 40), xf(arm_lc, dx=108 - ax0 - 0))
built[0x044A] = add_path(0x044A, hard_lc, lsb=50, rsb=80)
i_stem = clip(G("idieresis"), 0, -40, 700, 1010)
yery_lc = union(soft_lc, xf(i_stem, dx=903 + 110 - 176))
built[0x044B] = add_path(0x044B, yery_lc, lsb=90, rsb=90)

# й: и + breve at lowercase accent height.
built[0x0439] = add_path(0x0439, union(P(0x0438), center_on(breve(), P(0x0438))), lsb=90, rsb=90)

# ф: the same split oval at x-height, stem from ascender (like l) to descender (like p).
oval_lc = split_oval(spread=110, sy=0.72, dy=0, sx=0.86)
ox0, _, ox1, _ = bounds(oval_lc)
c = (ox0 + ox1) / 2
stem_lc = union(fit_x(clip(G("l"), 120, 300, 400, 1480), c - 118, c + 118),
                fit_x(clip(G("p"), 100, -420, 420, 400), c - 118, c + 118))
built[0x0444] = add_path(0x0444, union(oval_lc, stem_lc), lsb=80, rsb=80)

# ── write the font ───────────────────────────────────────────────────────────

order = font.getGlyphOrder()
glyf = font["glyf"]
for name, (g, adv, lsb) in new_glyphs.items():
    if name not in order:
        order.append(name)
    glyf[name] = g
    hmtx[name] = (int(adv), int(lsb))
font.setGlyphOrder(order)
glyf.glyphOrder = order
for t in font["cmap"].tables:
    if t.isUnicode():
        t.cmap.update(new_cmap)
font["post"].extraNames = []      # let fontTools rebuild post names from the glyph order
if "GDEF" in font and font["GDEF"].table.GlyphClassDef:
    for name in new_glyphs:
        font["GDEF"].table.GlyphClassDef.classDefs[name] = 1

os2 = font["OS/2"]
os2.ulUnicodeRange1 |= 1 << 9                     # Cyrillic
os2.ulCodePageRange1 |= 1 << 2                    # 1251 Cyrillic

FAMILY, PS = "Goblin Menu", "GoblinMenu-Regular"
for rec in font["name"].names:
    nid = rec.nameID
    if nid == 0:
        rec.string = rec.toUnicode() + " Cyrillic additions (c) 2026 Largo."
    elif nid == 1:
        rec.string = FAMILY
    elif nid == 3:
        rec.string = "Largo: GoblinMenu: 2026"
    elif nid == 4:
        rec.string = FAMILY + " Regular"
    elif nid == 5:
        rec.string = "Version 1.003; Cyrillic 0.1"
    elif nid == 6:
        rec.string = PS
font["name"].setName("Goblin Menu: Passero One (SIL OFL 1.1) by Viktoriya Grabowska, "
                     "extended with Cyrillic for Pit of Goblin mods.", 10, 3, 1, 0x409)
font.save(OUT)
print(f"wrote {OUT}: {len(new_glyphs)} new glyphs, {len(new_cmap)} codepoints")
