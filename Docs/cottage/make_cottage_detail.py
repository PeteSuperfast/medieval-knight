"""Cottage DETAIL pass (v2) for medieval-knight. Run:
  blender --background --factory-startup --python make_cottage_detail.py
Writes cottage_detail.blend, preview_detail_front.png, preview_detail_back.png, compare_detail.png next to this script.
Main massing (footprint, heights, colours, roof, chimney, wall lean) is copied unchanged from make_cottage.py (v1).
No FBX export. Does not modify Art_kit/style_kit.py nor any v1 file. All randomness seeded (fixed).
Axes: Z up, door faces -Y, chimney on +X, roof ridge runs along Y (gable ends face front/back). Origin = base centre.
"""
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Art_kit"))
import style_kit as K

K.PAL.update({"roof_purple": "#5E5878", "opening_dark": "#3A3A3A", "window_glow": "#F5C25A"})
GLOW_STRENGTH = 1.6

# ---- v1 dimensions (unchanged)
BASE_X, BASE_Y, BASE_H = 5.0, 4.0, 0.80
WALL_H = 2.50
WALL_BOT = (4.70, 3.70)
WALL_TOP = (4.50, 3.50)
ROOF_RIDGE_Z = 6.50
ROOF_OVER = 0.50
ROOF_T = 0.20
ROOF_SAG = 0.30
CH_TOP_Z = 7.40
Z_WALL_TOP = BASE_H + WALL_H
POCKET_BACK_Y = -1.55
WIN_POCKET_BACK_Y = POCKET_BACK_Y - 0.05
SX, SY = 0.04, -0.02          # wall top shift (v1)

# ---- openings (v1 positions, now arched on top)
DOOR = dict(cx=0.0, xh=0.55, z0=-0.2, zsp=1.70, zap=2.10)
WINS = [dict(cx=-1.4, xh=0.40, z0=1.55, zsp=2.15, zap=2.45), dict(cx=1.4, xh=0.40, z0=1.55, zsp=2.15, zap=2.45)]

# ---- wall surface functions (v1 wall frustum: bottom z=0.75, top z=3.30, top ring shifted by SX,SY)
def tz(z): return (z - 0.75) / (Z_WALL_TOP - 0.75)
def yF(z): return -1.85 + 0.08 * tz(z)
def yB(z): return 1.85 - 0.12 * tz(z)
def xL(z): return -2.35 + 0.14 * tz(z)
def xR(z): return 2.35 - 0.06 * tz(z)
def plinth_front(z): return -2.0 + 0.04 * (z + 0.05) / 0.85

def unit(*a): return Vector(a).normalized()
FACES = {
    "F": (lambda a, z: Vector((a, yF(z), z)), unit(0, -1, 0.0314)),
    "B": (lambda a, z: Vector((a, yB(z), z)), unit(0, 1, 0.0471)),
    "L": (lambda a, z: Vector((xL(z), a, z)), unit(-1, 0, 0.0549)),
    "R": (lambda a, z: Vector((xR(z), a, z)), unit(1, 0, 0.0235)),
}

# ---------------------------------------------------------------- helpers
def prism_mesh(name, poly_xz, y0, y1):
    me = bpy.data.meshes.new(name)
    n = len(poly_xz)
    vs = [(x, y0, z) for x, z in poly_xz] + [(x, y1, z) for x, z in poly_xz]
    fs = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        fs.append((i, j, n + j, n + i))
    me.from_pydata(vs, [], fs); me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    return me


def box_mesh(name, x0, x1, y0, y1, z0, z1):
    return prism_mesh(name, [(x0, z0), (x1, z0), (x1, z1), (x0, z1)], y0, y1)


def cut(solid_ob, cutters):
    for c in cutters:
        md = solid_ob.modifiers.new("cut", 'BOOLEAN')
        md.operation = 'DIFFERENCE'; md.solver = 'EXACT'; md.object = c
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    return bpy.data.meshes.new_from_object(solid_ob.evaluated_get(dg))


def in_box(p, bb, e=0.02):
    return (bb[0] - e <= p.x <= bb[1] + e and bb[2] - e <= p.y <= bb[3] + e and bb[4] - e <= p.z <= bb[5] + e)


def arch_pts(cx, xh, zsp, zap, n=6, grow=0.0):
    """Points of a segmental arch from the right spring over the top to the left spring (x, z)."""
    r = zap - zsp
    R = (xh * xh + r * r) / (2 * r)
    zc = zap - R
    th0 = math.asin(xh / R)
    return [(cx + (R + grow) * math.sin(th0 - 2 * th0 * k / n), zc + (R + grow) * math.cos(th0 - 2 * th0 * k / n))
            for k in range(n + 1)], R, zc, th0


def opening_poly(o, shrink=0.0, bottom=None, n=6):
    pts, R, zc, th0 = arch_pts(o["cx"], o["xh"], o["zsp"], o["zap"], n, -shrink)
    zb = o["z0"] if bottom is None else bottom
    return [(pts[-1][0], zb), (pts[0][0], zb)] + pts


def box(b, x0, x1, y0, y1, z0, z1, mat, j=0.0, rng=None):
    def p(x, y, z):
        if j:
            return Vector((x + rng.uniform(-j, j), y + rng.uniform(-j, j), z + rng.uniform(-j, j)))
        return Vector((x, y, z))
    x0, x1 = min(x0, x1), max(x0, x1)
    y0, y1 = min(y0, y1), max(y0, y1)
    b.solid([p(x0, y0, z0), p(x1, y0, z0), p(x1, y1, z0), p(x0, y1, z0)],
            [p(x0, y0, z1), p(x1, y0, z1), p(x1, y1, z1), p(x0, y1, z1)], mat)


def arch_prism(b, poly, y0, y1, mat):
    b.solid([Vector((x, y0, z)) for x, z in poly], [Vector((x, y1, z)) for x, z in poly], mat)


def bar(b, p0, p1, n, w, t, mat, embed=0.02):
    """Timber bar lying on a surface with outward normal n, from p0 to p1, width w, sticking out t."""
    d = (p1 - p0).normalized()
    s = d.cross(n).normalized() * (w / 2)
    def ring(p):
        return [p - s + n * (-embed), p + s + n * (-embed), p + s + n * t, p - s + n * t]
    b.solid(ring(p0), ring(p1), mat)


def face_bar(b, face, a0, z0, a1, z1, w, t, mat="wood_dark"):
    P, n = FACES[face]
    bar(b, P(a0, z0), P(a1, z1), n, w, t, mat)


def loft(b, rings, mat, closed=False, caps=True):
    base = len(b.v); n = len(rings[0]); m = b.mi(mat)
    for r in rings: b.v += [tuple(p) for p in r]
    N = len(rings)
    last = N if closed else N - 1
    for i in range(last):
        i2 = (i + 1) % N
        for k in range(n):
            j = (k + 1) % n
            b.f.append((base + i * n + k, base + i * n + j, base + i2 * n + j, base + i2 * n + k)); b.m.append(m)
    if caps and not closed:
        b.f.append(tuple(base + k for k in range(n - 1, -1, -1))); b.m.append(m)
        b.f.append(tuple(base + (N - 1) * n + k for k in range(n))); b.m.append(m)


def ico_blob(b, c, radii, rng, subdiv, mats, jit=0.14, up_mat=None):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    idx = {}
    for v in bm.verts:
        s = 1 + rng.uniform(-jit, jit)
        idx[v.index] = len(b.v)
        b.v.append(tuple(c + Vector((v.co.x * radii[0] * s, v.co.y * radii[1] * s, v.co.z * radii[2] * s))))
    for f in bm.faces:
        b.f.append(tuple(idx[v.index] for v in f.verts))
        b.m.append(b.mi(up_mat if (up_mat and f.normal.z > 0.55) else mats))
    bm.free()


def stone_chunk(b, c, n, a, depth, h, rng):
    """Slightly bulging faceted stone half sunk into a surface. c = point on surface, n = outward normal."""
    u = Vector((0, 0, 1)); t = u.cross(n).normalized(); u2 = n.cross(t).normalized()
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    base = "rock_top" if rng.random() < 0.45 else "rock_side"
    rot = rng.uniform(-0.5, 0.5)
    cr, sr = math.cos(rot), math.sin(rot)
    idx = {}
    for v in bm.verts:
        s = 1 + rng.uniform(-0.15, 0.15)
        px, pz = v.co.x * a * s, v.co.z * h * s
        px, pz = px * cr - pz * sr, px * sr + pz * cr
        py = max(v.co.y * depth * s, -0.03)
        idx[v.index] = len(b.v)
        b.v.append(tuple(c + t * px + n * py + u2 * pz))
    for f in bm.faces:
        fn = (t * f.normal.x + n * f.normal.y + u2 * f.normal.z)
        m = "rock_top" if (base == "rock_top" or fn.z > 0.5) else "rock_side"
        b.f.append(tuple(idx[v.index] for v in f.verts)); b.m.append(b.mi(m))
    bm.free()


# ---------------------------------------------------------------- roof (v1 loft, rings returned)
def loft_roof(b, rng):
    half_x = WALL_TOP[0] / 2 + ROOF_OVER
    half_y = WALL_TOP[1] / 2 + ROOF_OVER
    N = 10
    slope = (ROOF_RIDGE_Z - 2.85) / half_x
    vt = ROOF_T / math.cos(math.atan(slope))
    m = b.mi("roof_purple")
    rings = []
    for i in range(N + 1):
        y = -half_y + 2 * half_y * i / N
        sag = ROOF_SAG * (1 - (y / half_y) ** 2)
        hr = ROOF_RIDGE_Z - sag
        he = ROOF_RIDGE_Z - slope * half_x - sag * 0.5
        he_r = he + rng.uniform(-0.03, 0.03); he_l = he + rng.uniform(-0.03, 0.03)
        xo = rng.uniform(-0.02, 0.02)
        rings.append([(0.0, y, hr), (half_x + xo, y, he_r), (half_x + xo, y, he_r - vt),
                      (0.0, y, hr - vt), (-half_x - xo, y, he_l - vt), (-half_x - xo, y, he_l)])
    base = len(b.v)
    for r in rings: b.v += r
    n = 6
    for i in range(N):
        for k in range(n):
            j = (k + 1) % n
            b.f.append((base + i * n + k, base + i * n + j, base + (i + 1) * n + j, base + (i + 1) * n + k)); b.m.append(m)
    b.f.append(tuple(base + k for k in range(n - 1, -1, -1))); b.m.append(m)
    b.f.append(tuple(base + N * n + k for k in range(n))); b.m.append(m)
    return half_x, half_y, slope, rings, vt


def roof_z(x, y, half_x, half_y, slope):
    """Top surface of the v1 roof at (x, y) (jitter ignored)."""
    sag = ROOF_SAG * (1 - (y / half_y) ** 2)
    return ROOF_RIDGE_Z - sag - (slope - sag * 0.5 / half_x) * abs(x)


# ---------------------------------------------------------------- detail builders
def add_timber(b):
    H, V, D = 0.13, 0.12, 0.10        # widths: horizontals, posts, diagonals
    tH, tV, tD = 0.030, 0.035, 0.025  # stand-off from wall (3 cm; posts 3.5 so they sit over the beams)
    zl, zm, zt = 1.25, 2.70, 3.17     # low rail, mid rail (above windows), top plate
    # ---- front wall (door + windows)
    for z, segs in ((zl, [(-2.12, -0.75), (0.75, 2.12)]), (zm, [(-2.12, 2.12)]), (zt, [(-2.12, 2.12)])):
        for a0, a1 in segs: face_bar(b, "F", a0, z, a1, z, H, tH)
    for a in (-1.4, 1.4):
        face_bar(b, "F", a, 0.80, a, zl, V, tV)
    for a in (-1.4, 0.0, 1.4):
        face_bar(b, "F", a, zm, a, zt, V, tV)
    face_bar(b, "F", -2.08, zm, -1.44, zt - 0.04, D, tD)
    face_bar(b, "F", 2.08, zm, 1.44, zt - 0.04, D, tD)
    face_bar(b, "F", -1.36, zt - 0.04, -0.06, zm + 0.04, D, tD)
    face_bar(b, "F", 1.36, zt - 0.04, 0.06, zm + 0.04, D, tD)
    # ---- back wall
    for z in (zl, zm, zt): face_bar(b, "B", -2.12, z, 2.12, z, H, tH)
    for a in (-1.2, 0.0, 1.2): face_bar(b, "B", a, 0.80, a, zt, V, tV)
    pan = [(-2.08, -1.24), (-1.16, -0.04), (0.04, 1.16), (1.24, 2.08)]
    for k, (a0, a1) in enumerate(pan):
        if k % 2 == 0: face_bar(b, "B", a0, zl + 0.08, a1, zm - 0.08, D, tD)
        else: face_bar(b, "B", a1, zl + 0.08, a0, zm - 0.08, D, tD)
    # ---- -X side (full) and +X side (chimney in the middle)
    for face, chim in (("L", False), ("R", True)):
        for z in (zl, zm, zt):
            if chim:
                face_bar(b, face, -1.67, z, -0.56, z, H, tH); face_bar(b, face, 0.56, z, 1.67, z, H, tH)
            else:
                face_bar(b, face, -1.67, z, 1.67, z, H, tH)
        posts = (-0.95, 0.95) if chim else (-0.9, 0.0, 0.9)
        for a in posts: face_bar(b, face, a, 0.80, a, zt, V, tV)
        pans = [(-1.62, -1.0), (0.98, 1.62)] if chim else [(-1.62, -0.96), (-0.84, -0.06), (0.06, 0.84), (0.96, 1.62)]
        for k, (a0, a1) in enumerate(pans):
            if (k + (1 if chim else 0)) % 2 == 0: face_bar(b, face, a0, zl + 0.08, a1, zm - 0.08, D, tD)
            else: face_bar(b, face, a1, zl + 0.08, a0, zm - 0.08, D, tD)
    # ---- gable triangles (front y=-1.76, back y=+1.72): centre post, collar, two V braces, upper collar
    for sgn, yg in ((-1, -1.76), (1, 1.72)):
        n = Vector((0, sgn, 0))
        def gp(x, z): return Vector((SX + x, yg, z))
        bar(b, gp(0, 3.28), gp(0, 5.50), n, V, tV, "wood_dark")
        bar(b, gp(-1.30, 4.15), gp(1.30, 4.15), n, H, tH, "wood_dark")
        bar(b, gp(-1.95, 3.30), gp(-0.05, 4.10), n, D, tD, "wood_dark")
        bar(b, gp(1.95, 3.30), gp(0.05, 4.10), n, D, tD, "wood_dark")
        bar(b, gp(-0.62, 4.95), gp(0.62, 4.95), n, H * 0.85, tH, "wood_dark")
        bar(b, gp(-1.15, 4.20), gp(-0.10, 4.88), n, D, tD, "wood_dark")
        bar(b, gp(1.15, 4.20), gp(0.10, 4.88), n, D, tD, "wood_dark")


def add_quoins_and_plinth_stones(b):
    rng = random.Random(1234)
    # corner quoin blocks up the wall corners, alternating long side
    zs = [0.80 + i * (Z_WALL_TOP - 0.80) / 6 for i in range(7)]
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for ci, (sx_, sy_) in enumerate(corners):
        for i in range(6):
            z0, z1 = zs[i] + 0.012, zs[i + 1] - 0.012
            zm = (z0 + z1) / 2
            cx = xR(zm) if sx_ > 0 else xL(zm)
            cy = yB(zm) if sy_ > 0 else yF(zm)
            long_x = (i + ci) % 2 == 0
            Lx, Ly = (0.36 + rng.uniform(-0.03, 0.03), 0.17 + rng.uniform(-0.02, 0.02)) if long_x else \
                     (0.17 + rng.uniform(-0.02, 0.02), 0.36 + rng.uniform(-0.03, 0.03))
            out = 0.035
            box(b, cx - sx_ * Lx, cx + sx_ * out, cy - sy_ * Ly, cy + sy_ * out, z0, z1,
                "rock_top" if rng.random() < 0.5 else "rock_side", j=0.006, rng=rng)
    # random stones pressed into the plinth sides
    placed = []
    sides = [("F", 9, lambda r: (r.uniform(-2.35, 2.35), None)), ("B", 8, lambda r: (r.uniform(-2.35, 2.35), None)),
             ("L", 7, lambda r: (r.uniform(-1.85, 1.85), None)), ("R", 6, lambda r: (r.uniform(-1.85, 1.85), None))]
    for face, count, pick in sides:
        got, tries = 0, 0
        while got < count and tries < 400:
            tries += 1
            a = pick(rng)[0]
            z = rng.uniform(0.14, 0.66)
            if face == "F" and abs(a) < 0.98: continue          # door jamb stones are there
            if face == "R" and abs(a) < 0.70: continue          # chimney
            if any(f == face and abs(a - a2) < 0.5 and abs(z - z2) < 0.28 for f, a2, z2 in placed): continue
            placed.append((face, a, z)); got += 1
            if face == "F":
                p = Vector((a, plinth_front(z), z)); n = unit(0, -1, 0.05)
            elif face == "B":
                p = Vector((a, -plinth_front(z), z)); n = unit(0, 1, 0.05)
            elif face == "L":
                p = Vector((-(2.5 - 0.04 * (z + 0.05) / 0.85), a, z)); n = unit(-1, 0, 0.05)
            else:
                p = Vector((2.5 - 0.04 * (z + 0.05) / 0.85, a, z)); n = unit(1, 0, 0.05)
            stone_chunk(b, p, n, rng.uniform(0.13, 0.26), rng.uniform(0.045, 0.075), rng.uniform(0.08, 0.15), rng)


def add_door_and_windows(b):
    rng = random.Random(99)
    # ---- door leaf (real wooden door, arched), planks, straps, iron ring
    leaf = dict(cx=0.0, xh=0.50, z0=0.0, zsp=1.69, zap=2.06)
    poly = opening_poly(leaf, n=6)
    y_face, y_back = -1.70, -1.555
    arch_prism(b, poly, y_face, y_back, "wood_light")
    _, Rl, zcl, _ = arch_pts(0, 0.5, 1.69, 2.06)
    for x in (-0.30, -0.10, 0.10, 0.30):
        ztop = zcl + math.sqrt(max(Rl * Rl - x * x, 0)) - 0.10
        box(b, x - 0.011, x + 0.011, y_face - 0.012, y_face + 0.02, 0.06, ztop, "wood_dark")
    for zc, lx in ((0.45, 0.46), (1.32, 0.46)):                     # iron strap hinges from the left edge
        box(b, -0.50, -0.50 + lx, y_face - 0.022, y_face + 0.01, zc - 0.045, zc + 0.045, "barrel_iron")
    box(b, 0.24, 0.36, y_face - 0.03, y_face + 0.01, 1.12, 1.24, "barrel_iron")   # ring backplate
    R, tw, cz, cx_ = 0.075, 0.014, 1.04, 0.30
    rings = []
    for i in range(8):
        a = 2 * math.pi * i / 8
        c = Vector((cx_ + R * math.cos(a), y_face - 0.045, cz + R * math.sin(a)))
        rr = Vector((math.cos(a), 0, math.sin(a)))
        yy = Vector((0, 1, 0))
        rings.append([c + rr * tw + yy * tw, c - rr * tw + yy * tw, c - rr * tw - yy * tw, c + rr * tw - yy * tw])
    loft(b, rings, "barrel_iron", closed=True, caps=False)
    # ---- stone frame: jamb blocks + arch voussoirs
    xi, xo = 0.54, 0.72
    zb_list = [-0.05, 0.40, 0.82, 1.26, 1.70]
    for side in (-1, 1):
        for i in range(4):
            z0, z1 = zb_list[i] + 0.01, zb_list[i + 1] - 0.01
            zm = (z0 + z1) / 2
            yf = (plinth_front(zm) if zm < 0.8 else yF(zm)) - 0.05 + rng.uniform(-0.012, 0.012)
            w = xo - xi + (0.03 if i % 2 else 0.0)
            box(b, side * xi, side * (xi + w), yf, -1.50, max(z0, 0.0) if i == 0 else z0, z1,
                "rock_top" if (i + (side > 0)) % 2 else "rock_side", j=0.005, rng=rng)
    _, R, zc, th0 = arch_pts(0.0, DOOR["xh"], DOOR["zsp"], DOOR["zap"])
    nW = 5
    for k in range(nW):
        ta = th0 - 2 * th0 * k / nW
        tb = th0 - 2 * th0 * (k + 1) / nW
        key = (k == nW // 2)
        ro = R + 0.17 + (0.05 if key else 0.0)
        pts = lambda th, r: Vector((r * math.sin(th), 0, zc + r * math.cos(th)))
        zm = zc + R * math.cos((ta + tb) / 2)
        yf = yF(zm) - (0.075 if key else 0.052)
        r_in = R - 0.01
        ring_f = [pts(ta, r_in), pts(ta, ro), pts(tb, ro), pts(tb, r_in)]
        bot = [Vector((p.x, yf, p.z)) for p in ring_f]
        top = [Vector((p.x, -1.50, p.z)) for p in ring_f]
        b.solid(bot, top, "rock_top" if k % 2 == 0 else "rock_side")
    # ---- windows: arched wood frame, sill, glowing pane with muntins
    for w in WINS:
        inner, _, _, _ = arch_pts(w["cx"], w["xh"], w["zsp"], w["zap"], 6, -0.015)
        outer, _, _, _ = arch_pts(w["cx"], w["xh"], w["zsp"], w["zap"], 6, 0.075)
        pin = [(inner[-1][0], w["z0"] + 0.015), (inner[0][0], w["z0"] + 0.015)] + inner
        pout = [(outer[-1][0], w["z0"] - 0.075), (outer[0][0], w["z0"] - 0.075)] + outer
        yf = yF(2.0) - 0.035
        yk = -1.70
        n = len(pin)
        fi = [Vector((x, yf, z)) for x, z in pin]; fo = [Vector((x, yf, z)) for x, z in pout]
        bi = [Vector((x, yk, z)) for x, z in pin]; bo = [Vector((x, yk, z)) for x, z in pout]
        m = b.mi("wood_dark")
        base = len(b.v)
        b.v += [tuple(p) for p in fi + fo + bi + bo]
        FI, FO, BI, BO = base, base + n, base + 2 * n, base + 3 * n
        for i in range(n):
            j = (i + 1) % n
            b.f.append((FI + i, FI + j, FO + j, FO + i)); b.m.append(m)
            b.f.append((FI + i, BI + i, BI + j, FI + j)); b.m.append(m)
            b.f.append((FO + i, FO + j, BO + j, BO + i)); b.m.append(m)
            b.f.append((BI + i, BO + i, BO + j, BI + j)); b.m.append(m)
        cxw = w["cx"]
        box(b, cxw - 0.50, cxw + 0.50, yF(1.45) - 0.085, -1.76, 1.40, 1.475, "wood_light", j=0.004, rng=rng)  # sill
        pane = dict(cx=cxw, xh=0.395, z0=1.56, zsp=2.15, zap=2.445)
        arch_prism(b, opening_poly(pane), -1.665, -1.605, "window_glow")
        box(b, cxw - 0.022, cxw + 0.022, -1.725, -1.655, 1.56, 2.40, "wood_dark")
        box(b, cxw - 0.395, cxw + 0.395, -1.725, -1.655, 1.96, 2.00, "wood_dark")


def add_roof_trim_and_dormer(b, half_x, half_y, slope, rings, vt):
    # ---- eave fascia boards (long sides) lofted along the roof stations
    for side in (1, -1):
        st = []
        for r in rings:
            top, bot = (r[1], r[2]) if side > 0 else (r[5], r[4])
            xe = top[0]
            st.append([Vector((xe - side * 0.03, top[1], top[2] + 0.03)), Vector((xe + side * 0.055, top[1], top[2] + 0.03)),
                       Vector((xe + side * 0.055, top[1], bot[2] - 0.055)), Vector((xe - side * 0.03, top[1], bot[2] - 0.055))])
        loft(b, st, "wood_dark")
    # ---- rake boards on both gable ends (roof end profile, thickened, pushed outward 5 cm)
    for idx, sgn in ((0, -1), (-1, 1)):
        r = rings[idx]
        prof = [Vector(p) for p in r]
        lift = [0.03, 0.03, -0.055, -0.055, -0.055, 0.03]
        ring0 = [Vector((p.x, p.y, p.z + dz)) for p, dz in zip(prof, lift)]
        ring1 = [Vector((p.x, p.y + sgn * 0.055, p.z)) for p in ring0]
        b.solid(ring0, ring1, "wood_dark")
    # ---- dormer on the +X slope (faces +X, sits toward the front, clear of the chimney)
    yc, hw = -1.05, 0.46
    xf, xb = 1.95, 1.02
    zf = roof_z(xf, yc, half_x, half_y, slope)
    zw0, zw1 = zf - 0.16, zf + 1.07
    box(b, xb, xf, yc - hw, yc + hw, zw0, zw1, "plaster_cream")
    # roof: wedge with ridge sloping down toward the main roof
    fe = [Vector((xf + 0.14, yc - hw - 0.08, zw1)), Vector((xf + 0.14, yc, zw1 + 0.56)), Vector((xf + 0.14, yc + hw + 0.08, zw1))]
    be = [Vector((xb, yc - hw - 0.08, zw1 - 0.42)), Vector((xb, yc, zw1 + 0.14)), Vector((xb, yc + hw + 0.08, zw1 - 0.42))]
    b.solid(fe, be, "roof_purple")
    # window on the dormer front face (+X): frame, glow pane, cross
    wy0, wy1, wz0, wz1 = yc - 0.21, yc + 0.21, zw0 + 0.40, zw0 + 0.40 + 0.56
    box(b, xf - 0.02, xf + 0.012, wy0, wy1, wz0, wz1, "window_glow")
    t = 0.06
    box(b, xf - 0.02, xf + 0.045, wy0 - t, wy1 + t, wz1, wz1 + t, "wood_dark")
    box(b, xf - 0.02, xf + 0.045, wy0 - t, wy1 + t, wz0 - t, wz0, "wood_dark")
    box(b, xf - 0.02, xf + 0.045, wy0 - t, wy0, wz0, wz1, "wood_dark")
    box(b, xf - 0.02, xf + 0.045, wy1, wy1 + t, wz0, wz1, "wood_dark")
    box(b, xf, xf + 0.035, yc - 0.015, yc + 0.015, wz0, wz1, "wood_dark")
    box(b, xf, xf + 0.035, wy0, wy1, (wz0 + wz1) / 2 - 0.015, (wz0 + wz1) / 2 + 0.015, "wood_dark")
    return dict(x=(xb, xf), y=yc)


# ---------------------------------------------------------------- scene
def run():
    rng = random.Random(7)       # same seed + same call order as v1 -> identical chimney / roof jitter
    K.reset_scene()
    sc = bpy.context.scene
    g = K.MATS["window_glow"].node_tree.nodes["Principled BSDF"]
    g.inputs["Emission Color"].default_value = K.lin("#F5C25A")
    g.inputs["Emission Strength"].default_value = GLOW_STRENGTH

    # ---- v1 plinth + wall frustum
    pb = K.B("tmp_base")
    bx, by = BASE_X / 2, BASE_Y / 2
    pb.solid([Vector(p) for p in [(-bx, -by, -0.05), (bx, -by, -0.05), (bx, by, -0.05), (-bx, by, -0.05)]],
             [Vector(p) for p in [(-bx + 0.05, -by + 0.04, BASE_H), (bx - 0.04, -by + 0.04, BASE_H),
                                   (bx - 0.04, by - 0.05, BASE_H), (-bx + 0.05, by - 0.05, BASE_H)]], "rock_side")
    base_ob = pb.build()
    wb = K.B("tmp_wall")
    w0x, w0y = WALL_BOT[0] / 2, WALL_BOT[1] / 2
    w1x, w1y = WALL_TOP[0] / 2, WALL_TOP[1] / 2
    wb.solid([Vector(p) for p in [(-w0x, -w0y, BASE_H - 0.05), (w0x, -w0y, BASE_H - 0.05),
                                   (w0x, w0y, BASE_H - 0.05), (-w0x, w0y, BASE_H - 0.05)]],
             [Vector(p) for p in [(-w1x + SX, -w1y + SY, Z_WALL_TOP), (w1x + SX, -w1y + SY, Z_WALL_TOP),
                                   (w1x + SX, w1y + SY, Z_WALL_TOP), (-w1x + SX, w1y + SY, Z_WALL_TOP)]], "plaster_cream")
    wall_ob = wb.build()

    # ---- arched cutters
    door_c = bpy.data.objects.new("c_door", prism_mesh("c_door", opening_poly(DOOR), -2.4, POCKET_BACK_Y))
    win_c = [bpy.data.objects.new("c_win%d" % i, prism_mesh("c_win%d" % i, opening_poly(w), -2.4, WIN_POCKET_BACK_Y))
             for i, w in enumerate(WINS)]
    for c in [door_c] + win_c: sc.collection.objects.link(c)
    cut_boxes = [(DOOR["cx"] - DOOR["xh"], DOOR["cx"] + DOOR["xh"], -2.4, POCKET_BACK_Y, DOOR["z0"], DOOR["zap"])] + \
                [(w["cx"] - w["xh"], w["cx"] + w["xh"], -2.4, WIN_POCKET_BACK_Y, w["z0"], w["zap"]) for w in WINS]

    base_me = cut(base_ob, [door_c])
    wall_me = cut(wall_ob, [door_c] + win_c)

    b = K.B("Cottage_Detail")
    def absorb(me, painter):
        off = len(b.v)
        b.v += [tuple(v.co) for v in me.vertices]
        for p in me.polygons:
            b.f.append(tuple(off + i for i in p.vertices))
            b.m.append(b.mi(painter(p)))

    def is_pocket(p, me):
        if abs(p.normal.y) > 0.9 and p.center.y < POCKET_BACK_Y - 0.1:
            return False
        for bb in cut_boxes:
            if all(in_box(me.vertices[i].co, bb, 0.03) for i in p.vertices): return True
        return False
    absorb(base_me, lambda p: "opening_dark" if is_pocket(p, base_me) else ("rock_top" if p.normal.z > 0.5 else "rock_side"))
    absorb(wall_me, lambda p: "opening_dark" if is_pocket(p, wall_me) else "plaster_cream")

    # gable fills (v1)
    for sgn in (-1, 1):
        y_out = sgn * (w1y - 0.01) + SY
        y_in = sgn * (w1y - 0.31) + SY
        prof = [(-w1x + 0.01 + SX, Z_WALL_TOP - 0.1), (w1x - 0.01 + SX, Z_WALL_TOP - 0.1), (0.0 + SX, 5.80)]
        base = len(b.v)
        for y in (y_in, y_out):
            for (x, z) in prof: b.v.append((x, y, z))
        m = b.mi("plaster_cream")
        b.f.append((base + 2, base + 1, base + 0)); b.m.append(m)
        b.f.append((base + 3, base + 4, base + 5)); b.m.append(m)
        for i in range(3):
            j = (i + 1) % 3
            b.f.append((base + i, base + j, base + 3 + j, base + 3 + i)); b.m.append(m)

    # chimney (v1)
    ch = K.B("tmp_chimney")
    cj = lambda: rng.uniform(-0.02, 0.02)
    bot = [Vector(p) for p in [(2.20, -0.50, 0.0), (3.15, -0.50 + cj(), 0.0), (3.15, 0.50, 0.0), (2.20, 0.50, 0.0)]]
    top = [Vector(p) for p in [(2.20, -0.38, 7.0), (2.92, -0.38 + cj(), 7.0), (2.92 + 0.03, 0.40, 7.0), (2.20, 0.40, 7.0)]]
    cap0 = [Vector(p) for p in [(2.12, -0.46, 7.0), (3.02, -0.46, 7.0), (3.02, 0.48, 7.0), (2.12, 0.48, 7.0)]]
    cap1 = [Vector(p) for p in [(2.14, -0.44, CH_TOP_Z), (3.00, -0.44, CH_TOP_Z), (3.00, 0.46, CH_TOP_Z), (2.14, 0.46, CH_TOP_Z)]]
    ch.solid(bot, top, "rock_side")
    ch.solid(cap0, cap1, "rock_side")
    ch_ob = ch.build()
    absorb(ch_ob.data, lambda p: "rock_top" if p.normal.z > 0.5 else "rock_side")

    # roof (v1) + new details
    half_x, half_y, slope, rings, vt = loft_roof(b, rng)
    add_timber(b)
    add_quoins_and_plinth_stones(b)
    add_door_and_windows(b)
    dormer = add_roof_trim_and_dormer(b, half_x, half_y, slope, rings, vt)

    for o in (base_ob, wall_ob, ch_ob, door_c, *win_c):
        bpy.data.objects.remove(o)
    house = b.build()
    house.name = "Cottage_Detail"
    for p in house.data.polygons: p.use_smooth = False

    vs = house.data.vertices
    ext = lambda a: (min(getattr(v.co, a) for v in vs), max(getattr(v.co, a) for v in vs))
    tris_house = sum(len(p.vertices) - 2 for p in house.data.polygons)
    print("RESULT house tris", tris_house)
    print("RESULT house bbox x", [round(t, 3) for t in ext("x")], "y", [round(t, 3) for t in ext("y")], "z", [round(t, 3) for t in ext("z")])
    print("RESULT house materials", [m.name for m in house.data.materials])

    # ---- decor (kit pieces + smoke), parented so they follow the house yaw
    decor = []
    def put(ob, x, y, rz=0.0, s=1.0, name=None):
        ob.parent = house
        ob.location = (x, y, 0.0)
        ob.rotation_euler = (0, 0, rz)
        ob.scale = (s, s, s)
        if name: ob.name = name
        decor.append(ob)
        return ob
    drng = random.Random(55)
    put(K.make_barrel("Barrel_Upright", K.BARREL_SEEDS["upright"]), -1.55, -2.75, 0.3, name="Deco_Barrel_Upright")
    put(K.make_barrel("Barrel_Lying", K.BARREL_SEEDS["lying"], lying=True), 1.95, -2.75, 0.65, name="Deco_Barrel_Lying")
    for i, (x, y) in enumerate([(0.05, -2.50), (-0.20, -3.30), (0.15, -4.10), (-0.10, -4.95), (0.22, -5.75)]):
        put(K.make_stepping_stone("Stepping_Stone"), x, y, drng.uniform(0, 3.1), 1.9, name="Deco_StepStone_%d" % i)
    put(K.make_mushroom_cluster("Mushroom_Cluster"), 2.60, -2.35, 0.5, 1.7, name="Deco_Mushrooms_A")
    put(K.make_mushroom_cluster("Mushroom_Cluster"), -2.75, 1.10, 2.2, 1.5, name="Deco_Mushrooms_B")
    for i, (x, y) in enumerate([(-2.70, -2.35), (0.95, -2.65), (-0.95, -3.55), (2.15, -3.35), (-2.95, -0.60),
                                (-2.80, 2.35), (2.95, 1.60), (1.20, 2.50), (-1.30, 2.60), (3.4, -1.3)]):
        put(K.make_grass_tuft("Grass_Tuft"), x, y, drng.uniform(0, 6.28), drng.uniform(1.6, 2.1), name="Deco_Grass_%d" % i)

    # smoke: 3 faceted puffs drifting up/away from the chimney cap (#D8CCB0 cream; lowest puff #ADABB7 light stone grey)
    sm = K.B("Deco_Smoke")
    srng = random.Random(77)
    cxs, cys = 2.57, 0.01
    ico_blob(sm, Vector((cxs + 0.05, cys, CH_TOP_Z + 0.45)), (0.30, 0.28, 0.26), srng, 2, "rock_top")
    ico_blob(sm, Vector((cxs + 0.35, cys - 0.10, CH_TOP_Z + 1.00)), (0.42, 0.40, 0.36), srng, 2, "plaster_cream")
    ico_blob(sm, Vector((cxs + 0.80, cys - 0.20, CH_TOP_Z + 1.70)), (0.56, 0.52, 0.46), srng, 2, "plaster_cream")
    smoke = sm.build()
    for p in smoke.data.polygons: p.use_smooth = False
    smoke.parent = house
    decor.append(smoke)

    def tri_count(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)
    tris_deco = sum(tri_count(o) for o in decor)
    print("RESULT decor tris", tris_deco, "objects", len(decor))
    for o in decor: print("RESULT decor", o.name, tri_count(o))
    print("RESULT TOTAL tris", tris_house + tris_deco)
    print("RESULT dormer", dormer)

    # ---- renders: same camera as v1 (dist 21, target_z 3.4); only the house object is passed so decor stays put
    house.rotation_euler = (0, 0, math.radians(-20))
    pf = os.path.join(HERE, "preview_detail_front.png")
    K.render_lineup([house], pf, cell=1, cols=1, res=(1024, 768), dist=21, target_z=3.4)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "cottage_detail.blend"))

    for o in list(sc.objects):
        if o.name.startswith(("Ground_Flat", "Sun_Warm", "Cam")): bpy.data.objects.remove(o)
    house.rotation_euler = (0, 0, math.radians(240))
    pb_ = os.path.join(HERE, "preview_detail_back.png")
    K.render_lineup([house], pb_, cell=1, cols=1, res=(1024, 768), dist=21, target_z=3.4)
    house.rotation_euler = (0, 0, math.radians(-20))

    def load(path):
        im = bpy.data.images.load(path)
        w, h = im.size
        return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    a, c = load(pf), load(pb_)
    r = load(os.path.join(ROOT, "Docs", "ref", "env_tower.jpg"))
    oh = a.shape[0]
    sc_ = oh / r.shape[0]
    nw = int(r.shape[1] * sc_)
    yi = (np.arange(oh) / sc_).astype(int).clip(0, r.shape[0] - 1)
    xi = (np.arange(nw) / sc_).astype(int).clip(0, r.shape[1] - 1)
    big = r[yi][:, xi]
    gap = np.ones((oh, 12, 4), np.float32)
    out = np.concatenate([a, gap, c, gap, big], axis=1)
    img = bpy.data.images.new("compare_detail", out.shape[1], oh, alpha=False)
    img.pixels = out.ravel()
    img.filepath_raw = os.path.join(HERE, "compare_detail.png")
    img.file_format = 'PNG'
    img.save()
    print("DONE")


run()
