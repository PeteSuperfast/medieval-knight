"""World map blockout (medieval-knight / world-map-preview).
Run:  blender --background --factory-startup --python make_world_map.py [-- --low] [-- --norender]
Coordinates: Blender X = East (E), Y = North (N), Z = height. Map 380 x 380 m, SW corner = (0,0).
Writes (next to this file): world_map.blend, preview_top.png, preview.png, preview_objects.png
"""
import bpy, bmesh, math, random, os, sys
import numpy as np
from mathutils import Vector, Matrix

OUT = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\world-map-preview"
LOW = "--low" in sys.argv
NORENDER = "--norender" in sys.argv
rng = random.Random(2026)
nrng = np.random.RandomState(11)

# ------------------------------------------------------------------ palette
PAL = {
    "grass_light": "#8DB04A", "grass_mid": "#6C8535", "pine_dark": "#465820", "leaf_light": "#A0B85B",
    "wood_light": "#A1773D", "wood_dark": "#715332", "dirt": "#8A5A32", "sand": "#D9BC7E",
    "stone_light": "#ADABB7", "stone_mid": "#958A80", "plaster": "#D8CCB0",
    "roof_purple": "#5E5878", "roof_red": "#B5523B", "iron": "#3A3A3A",
    "glow": "#F5C25A", "ember": "#C8483A", "water": "#1594BB",
    "flower_y": "#F5C25A", "flower_w": "#D8CCB0", "flower_p": "#5E5878", "flower_r": "#C8483A",
    # snow / ice colours (approved by Pete, decisions.md #16)
    "snow_light": "#EEF3F8", "snow_shadow": "#C9D8E8", "ice_light": "#BFE6F2", "ice_dark": "#6FB8D6",
}
EMIT = {"glow": 4.0, "ember": 3.0}


def lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = "WorldMap"
MATS = {}


def mk_mat(name, hexcol, emis=0.0, rough=0.9, alpha=None):
    m = bpy.data.materials.new("MK_" + name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = lin(hexcol)
    b.inputs["Roughness"].default_value = rough
    if emis:
        b.inputs["Emission Color"].default_value = lin(hexcol)
        b.inputs["Emission Strength"].default_value = emis
    if alpha is not None:
        b.inputs["Alpha"].default_value = alpha
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
    m.diffuse_color = lin(hexcol)
    MATS[name] = m
    return m


for k, v in PAL.items():
    mk_mat(k, v, EMIT.get(k, 0.0), 0.35 if k == "water" else 0.9)
mk_mat("cloud", "#EEF3F8", alpha=0.45)
mk_mat("label", "#222222", emis=1.0)
mk_mat("sheet_floor", "#DAD3C6")
mk_mat("lake_ice", "#BFE6F2", rough=0.25)

COLS = {}


def col(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    COLS[name] = c
    return c


C_TERR, C_WATER, C_PATH = col("Terrain"), col("Water"), col("Path_Waypoints")
C_BLD, C_PLANT, C_ROCK = col("Buildings"), col("Vegetation"), col("Rocks_Cliffs")
C_SNOW, C_CASTLE, C_MTN = col("Snow_Samples"), col("Castle"), col("Backdrop_Mountains")
C_MISC, C_LABEL = col("Misc"), col("TopView_Labels")

# ------------------------------------------------------------------ mesh builder
def T(x=0, y=0, z=0, rx=0, ry=0, rz=0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(rz, 4, 'Z') @ Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X')


class B:
    def __init__(self, name):
        self.name = name; self.v = []; self.f = []; self.m = []; self.mats = []

    def mi(self, n):
        if n not in self.mats: self.mats.append(n)
        return self.mats.index(n)

    def face(self, pts, mat):
        base = len(self.v); self.v += [tuple(p) for p in pts]
        self.f.append(tuple(range(base, base + len(pts)))); self.m.append(self.mi(mat))

    def solid(self, bot, top, mat, side_mats=None, bottom=True):
        mats = side_mats or [mat]
        n = len(bot); base = len(self.v)
        self.v += [tuple(p) for p in bot]
        if isinstance(top, Vector):
            self.v.append(tuple(top)); a = base + n
            for i in range(n):
                self.f.append((base + i, base + (i + 1) % n, a)); self.m.append(self.mi(mats[i % len(mats)]))
        else:
            self.v += [tuple(p) for p in top]
            for i in range(n):
                j = (i + 1) % n
                self.f.append((base + i, base + j, base + n + j, base + n + i)); self.m.append(self.mi(mats[i % len(mats)]))
            self.f.append(tuple(base + n + i for i in range(n))); self.m.append(self.mi(mats[0]))
        if bottom:
            self.f.append(tuple(reversed([base + i for i in range(n)]))); self.m.append(self.mi(mats[0]))

    def box(self, M, sx, sy, sz, mat, taper=1.0, jit=0.0, bottom=True):
        hx, hy = sx / 2, sy / 2; t = taper
        bot = [(-hx, -hy, 0), (hx, -hy, 0), (hx, hy, 0), (-hx, hy, 0)]
        top = [(-hx * t, -hy * t, sz), (hx * t, -hy * t, sz), (hx * t, hy * t, sz), (-hx * t, hy * t, sz)]
        j = lambda p: (p[0] + rng.uniform(-jit, jit), p[1] + rng.uniform(-jit, jit), p[2])
        self.solid([M @ Vector(p) for p in bot], [M @ Vector(j(p)) for p in top], mat, bottom=bottom)

    def cyl(self, M, r0, r1, h, n, mat, jit=0.0, rot=0.0, side_mats=None, bottom=True):
        def ring(r, z):
            return [Vector((r * math.cos(rot + 2 * math.pi * i / n) + rng.uniform(-jit, jit),
                            r * math.sin(rot + 2 * math.pi * i / n) + rng.uniform(-jit, jit), z)) for i in range(n)]
        bot = [M @ p for p in ring(r0, 0)]
        if r1 <= 0:
            self.solid(bot, M @ Vector((0, 0, h)), mat, side_mats, bottom)
        else:
            self.solid(bot, [M @ p for p in ring(r1, h)], mat, side_mats, bottom)

    def bip(self, M, rx, ry, hu, hd, n, mats_up, mats_dn=None, jit=0.0, rot=0.0):
        mats_dn = mats_dn or mats_up
        ring = [M @ Vector((rx * math.cos(rot + 2 * math.pi * i / n) + rng.uniform(-jit, jit),
                            ry * math.sin(rot + 2 * math.pi * i / n) + rng.uniform(-jit, jit), rng.uniform(-jit, jit) * .5)) for i in range(n)]
        up = M @ Vector((0, 0, hu)); dn = M @ Vector((0, 0, -hd))
        for i in range(n):
            j = (i + 1) % n
            self.face([ring[i], ring[j], up], mats_up[i % len(mats_up)])
            if hd > 0: self.face([ring[j], ring[i], dn], mats_dn[i % len(mats_dn)])

    def rings(self, rings, mats, apex=None, apex_mat=None, bottom_mat=None):
        """stack of equal-length vertex rings -> side quads (+ optional apex fan, bottom cap)"""
        n = len(rings[0])
        for k in range(len(rings) - 1):
            for i in range(n):
                j = (i + 1) % n
                self.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], mats[(i + k) % len(mats)])
        if apex is not None:
            for i in range(n):
                self.face([rings[-1][i], rings[-1][(i + 1) % n], apex], (apex_mat or mats)[i % len(apex_mat or mats)] if isinstance(apex_mat, list) else (apex_mat or mats[i % len(mats)]))
        if bottom_mat:
            self.face(list(reversed(rings[0])), bottom_mat)

    def mesh(self, recalc=True):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.v, [], self.f)
        me.update()
        for p, mi in zip(me.polygons, self.m):
            p.material_index = mi; p.use_smooth = False
        for n in self.mats: me.materials.append(MATS[n])
        if recalc:
            bm = bmesh.new(); bm.from_mesh(me)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            bm.to_mesh(me); bm.free()
        return me

    def obj(self, collection, loc=(0, 0, 0), rotz=0.0, recalc=True):
        ob = bpy.data.objects.new(self.name, self.mesh(recalc))
        collection.objects.link(ob)
        ob.location = loc; ob.rotation_euler = (0, 0, rotz)
        return ob


def tris_of(me): return sum(len(p.vertices) - 2 for p in me.polygons)


# ------------------------------------------------------------------ terrain model (numpy)
MAP = 380.0
NC = 190                   # cells per side -> 2.0 m (v2: finer terrain)
CS = MAP / NC
es = np.linspace(0, MAP, NC + 1)
EE, NN = np.meshgrid(es, es)          # [j(north), i(east)]


def sm(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


def lerp(a, b, t): return a + (b - a) * t


def catmull(P, sub=8):
    P = np.array(P, float); out = []; n = len(P)
    for i in range(n - 1):
        p0 = P[max(i - 1, 0)]; p1 = P[i]; p2 = P[i + 1]; p3 = P[min(i + 2, n - 1)]
        for k in range(sub):
            t = k / sub
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-1]); return np.array(out)


def poly_query(E, N, poly):
    """poly: K x (2+extra). returns distance to polyline and interpolated extras."""
    E = np.asarray(E, float).reshape(-1); N = np.asarray(N, float).reshape(-1)
    A = poly[:-1]; Bp = poly[1:]
    ab = Bp[:, :2] - A[:, :2]; L2 = (ab ** 2).sum(1) + 1e-9
    ex = E.reshape(-1, 1) - A[:, 0]; ny = N.reshape(-1, 1) - A[:, 1]
    t = np.clip((ex * ab[:, 0] + ny * ab[:, 1]) / L2, 0, 1)
    dx = ex - t * ab[:, 0]; dy = ny - t * ab[:, 1]
    d = np.sqrt(dx * dx + dy * dy)
    idx = d.argmin(1); r = np.arange(len(idx)); tt = t[r, idx]
    ext = A[idx, 2:] + (Bp[idx, 2:] - A[idx, 2:]) * tt[:, None]
    return d[r, idx], ext


def nz(E, N, a=1.0):
    return a * (np.sin(E * 0.11 + 1.3) * np.sin(N * 0.09 + 0.4) + 0.6 * np.sin(E * 0.23 + N * 0.19 + 2.0)
                + 0.35 * np.sin(E * 0.41 - N * 0.37))


# ---- waypoints (E, N, Z, name)
WP = [(25, 25, 0, "Start"), (55, 50, 0, ""), (75, 72, 1, ""), (95, 88, 1, "Bridge 1"), (112, 115, 4, ""),
      (135, 140, 7, "Pine valley"), (160, 165, 9, ""), (185, 190, 12, ""), (205, 215, 18, "Mountain path"),
      (225, 235, 26, ""), (250, 245, 36, ""), (275, 235, 45, ""), (295, 255, 50, "Snowland"),
      (315, 300, 52, ""), (330, 320, 73, ""), (340, 335, 80, "Castle")]
# dense path with zig-zag on the cliff section and a gate node
nodes = []
for i, (e, n, z, _) in enumerate(WP):
    nodes.append((e, n, z))
    if i < 7 and i not in (2, 3):                        # gentle meander (v5: legs touching the bridge stay straight) on the meadow / valley road
        e2, n2, z2, _ = WP[i + 1]
        dx, dy = e2 - e, n2 - n; L = math.hypot(dx, dy)
        s = (6.0 if i % 2 == 0 else -6.0)
        nodes.append(((e + e2) / 2 - dy / L * s, (n + n2) / 2 + dx / L * s, (z + z2) / 2))
    if i in (8, 9, 10) and i + 1 < len(WP):
        e2, n2, z2, _ = WP[i + 1]
        mx, my = (e + e2) / 2, (n + n2) / 2
        dx, dy = e2 - e, n2 - n; L = math.hypot(dx, dy)
        s = 7 if i % 2 == 0 else -7
        nodes.append((mx - dy / L * s, my + dx / L * s, (z + z2) / 2))
    if i == 14:
        nodes.append((331, 330.5, 80))
PATH = catmull(nodes, 10)                       # columns e, n, z
SEG = np.hypot(np.diff(PATH[:, 0]), np.diff(PATH[:, 1]))
PCUM = np.concatenate([[0], np.cumsum(SEG)])


def path_at(s):
    s = min(max(s, 0), PCUM[-1])
    i = min(np.searchsorted(PCUM, s, side="right") - 1, len(PATH) - 2)
    t = (s - PCUM[i]) / max(SEG[i], 1e-6)
    p = PATH[i] + (PATH[i + 1] - PATH[i]) * t
    tang = (PATH[i + 1, :2] - PATH[i, :2]); tang = tang / max(np.hypot(*tang), 1e-6)
    return p[0], p[1], p[2], tang


# v3 village plaza: on the road near waypoint 2 (zone A), flattened so the village sits level
_pp0 = path_at(8.0)                       # v4: village sits at the start of the road
PLAZA = (float(_pp0[0]), float(_pp0[1]), float(_pp0[2]))
PLAZA_R = 9.0

RIV_N = [(0, 100, 5.0), (40, 112, 6.0), (75, 95, 6.0), (95, 88, 6.5), (120, 65, 6.0), (135, 40, 5.5), (150, 0, 5.5)]
RIVER = catmull(RIV_N, 8); RIVER_WL = 0.3
STR_N = [(305, 236, 6.5), (312, 229, 5.0), (322, 218, 3.6), (332, 205, 3.6), (350, 196, 3.6), (381, 190, 3.6)]
STREAM = catmull(STR_N, 8); STREAM_WL = 29.4

ANCH = [  # (E, N, Z) macro elevation anchors
    (0, 0, 1.5), (50, 0, 1), (100, 0, 1.2), (0, 50, 1.5), (50, 50, 0.8), (100, 50, 1.2), (0, 100, 1.5), (40, 80, 1.0), (30, 130, 3.5), (70, 130, 3.5),
    (150, 60, 3), (170, 20, 4.5), (200, 0, 7), (200, 50, 8), (230, 80, 15), (250, 30, 17), (260, 100, 22), (300, 60, 25), (300, 110, 28),
    (250, 140, 27), (350, 60, 28), (380, 0, 25), (380, 100, 28), (330, 30, 26), (250, 0, 19), (310, 0, 23), (380, 150, 30),
    (0, 150, 7), (0, 200, 19), (0, 250, 34), (0, 300, 50), (0, 380, 72), (40, 330, 56), (60, 240, 29), (30, 180, 11), (75, 160, 11),
    (80, 185, 15), (100, 215, 23), (130, 240, 30), (160, 225, 24), (120, 175, 10), (150, 120, 8), (170, 105, 10), (200, 130, 16), (225, 160, 22),
    (60, 300, 55), (120, 330, 60), (180, 350, 62), (240, 350, 66), (300, 365, 70), (100, 280, 42), (160, 280, 40), (210, 290, 48), (250, 300, 56),
    (260, 330, 70), (180, 300, 48), (380, 380, 62), (380, 300, 54), (380, 250, 50), (380, 200, 30), (60, 360, 70), (140, 370, 72),
    (200, 190, 14), (230, 185, 22), (260, 190, 28), (240, 215, 30), (215, 250, 36), (260, 265, 54), (230, 270, 46), (190, 250, 34), (140, 260, 34),
]
for (e, n, z, _) in WP: ANCH.append((e, n, z))
ANCH = np.array(ANCH, float)


def macro(E, N):
    d2 = (E[..., None] - ANCH[:, 0]) ** 2 + (N[..., None] - ANCH[:, 1]) ** 2 + 36.0
    w = d2 ** -1.1
    return (w * ANCH[:, 2]).sum(-1) / w.sum(-1)


CASTLE_XY = (340, 340)


def box_blur(a, r):
    """separable box blur radius r cells, edge-replicated"""
    for ax in (0, 1):
        pad = [(0, 0), (0, 0)]; pad[ax] = (r, r)
        ap = np.pad(a, pad, mode="edge")
        c = np.cumsum(ap, axis=ax, dtype=float)
        c = np.concatenate([np.zeros_like(np.take(c, [0], axis=ax)), c], axis=ax)
        n = a.shape[ax]
        a = (np.take(c, range(2 * r + 1, 2 * r + 1 + n), axis=ax) - np.take(c, range(0, n), axis=ax)) / (2 * r + 1)
    return a


def build_height():
    E, N = EE, NN
    h = macro(E, N)
    h = h + nz(E, N, 0.5 + 0.035 * h)
    de = np.minimum.reduce([E, MAP - E, N, MAP - N])
    h = h + 4.0 * (1 - sm(de / 14.0)) * (h < 20)                # low rim around lowlands
    # C : stepped cliffs (3 terraces 12 -> 45)
    Cm = sm((E - 182) / 10) * (1 - sm((E - 285) / 10)) * sm((N - 160) / 10) * (1 - sm((N - 268) / 10))
    q = np.clip((h - 12) / 11.0, 0, 3.0)
    fl = np.floor(np.minimum(q, 2.999)); fr = q - fl
    ht = 12 + 11 * (fl + sm((fr - 0.72) / 0.28))
    use = Cm * ((h > 11) & (h < 46))
    h = lerp(h, ht, use)
    # D1 / D2 plateaus with one cliff between (waterfall edge)
    wE = sm((E - 282) / 12); wN = sm((N - 120) / 25) * (1 - sm((N - 332) / 12))
    target = 30 + 20 * sm((N - 246.5) / 3.5) + nz(E, N, 0.18)
    h = lerp(h, target, wE * wN)
    # D3 mountain cone + castle crown
    r = np.hypot(E - 340, N - 340)
    zc = np.interp(r, [0, 19, 35, 50, 70], [80, 80, 60, 52, 48]) + nz(E, N, 0.5) * (r > 20)
    h = lerp(h, zc, 1 - sm((r - 50) / 25))
    # v3: softer, gentler terrain on the right (snow) side - D1 low snow plain + D2 plateau.
    # masked box-blur (lower plateau / upper plateau separately so the waterfall cliff N~238-256 stays sharp)
    rc = np.hypot(E - CASTLE_XY[0], N - CASTLE_XY[1])
    for msk in ((N < 240.0).astype(float), ((N > 254.0) & (rc > 55)).astype(float)):
        num = h * msk; den = msk.copy()
        for _ in range(4):
            num = box_blur(num, 3); den = box_blur(den, 3)
        hs = num / np.maximum(den, 1e-6)
        w_sn = sm((E - 266) / 26)
        w_gr = (1 - w_sn) * 0.92 * sm((E - 130) / 90) * (1 - Cm)           # green east side, not the main cliff ascent
        w = np.maximum(w_sn * 0.92, w_gr) * sm((rc - 60) / 16) * msk
        h = lerp(h, hs, w)
    # v4: zone A + B (south / east, up to the snow edge) much flatter and walkable; river banks no longer cliffs
    Rm = (1 - sm((N - 190) / 30)) * (1 - Cm) * (1 - sm((N - 120) / 50) * (1 - sm((E - 70) / 40))) * (1 - sm((E - 275) / 15))
    Hb = h.copy()
    for _ in range(5): Hb = box_blur(Hb, 4)
    h = lerp(h, Hb, 0.9 * Rm)
    dr_, ex_r = poly_query(E, N, RIVER); dr_ = dr_.reshape(E.shape) - ex_r[:, 0].reshape(E.shape)
    hcap = RIVER_WL + 1.1 + 0.11 * np.maximum(dr_, 0)
    h = lerp(h, np.minimum(h, hcap), (1 - sm((dr_ - 40) / 25)) * Rm)
    # path corridor (forces waypoint heights, carves ramps through cliffs)
    d, ext = poly_query(E, N, PATH)
    d = d.reshape(E.shape); pz = ext[:, 0].reshape(E.shape)
    h = lerp(h, pz, 1 - sm((d - 3.4) / 6.0))
    # v3: flat ground for the village (road keeps passing through)
    dd = np.hypot(E - PLAZA[0], N - PLAZA[1])
    h = lerp(h, PLAZA[2], 1 - sm((dd - 13.0) / 9.0))
    # rivers
    for poly, wl, bandw in ((RIVER, RIVER_WL, 6.0), (STREAM, STREAM_WL, 2.5)):
        d, ext = poly_query(E, N, poly)
        d = d.reshape(E.shape); hw = ext[:, 0].reshape(E.shape)
        band = 1 - sm((d - hw - 1.0) / bandw)
        bank = np.clip(h, wl + 0.4, wl + 1.0)
        h = lerp(h, bank, band)
        chan = 1 - sm((d - hw * 0.75) / (hw * 0.45))
        h = lerp(h, wl - 0.9, chan)
    return h


HG = build_height()          # HG[j, i]
def _slope_stats():
    gy, gx = np.gradient(HG, CS)
    sl = np.hypot(gx, gy)
    out = {}
    for nm, m in (("snow E>282 N<238", (EE > 282) & (NN < 238)), ("snow D2 N>256", (EE > 282) & (NN > 256) & (np.hypot(EE - 340, NN - 340) > 80)),
                  ("green E 175-266", (EE > 175) & (EE < 266) & (NN < 160))):
        out[nm] = (round(float(sl[m].mean()), 3), round(float(np.percentile(sl[m], 95)), 3))
    return out
print("SLOPE_STATS mean/p95", _slope_stats())
_gy, _gx = np.gradient(HG, CS); _sl = np.hypot(_gx, _gy)
_mA = (EE < 282) & (NN < 175) & (EE > 8)
print("SLOPE_AB mean/p95/max/frac>0.5", round(float(_sl[_mA].mean()), 3), round(float(np.percentile(_sl[_mA], 95)), 3), round(float(_sl[_mA].max()), 3), round(float((_sl[_mA] > 0.5).mean()), 4))


def Hm(e, n):
    """height of the actual triangulated mesh (diagonal v00-v11)"""
    e = min(max(e, 0), MAP - 1e-6); n = min(max(n, 0), MAP - 1e-6)
    i = int(e / CS); j = int(n / CS); fx = e / CS - i; fy = n / CS - j
    h00, h10, h11, h01 = HG[j, i], HG[j, i + 1], HG[j + 1, i + 1], HG[j + 1, i]
    if fx >= fy: return h00 + fx * (h10 - h00) + fy * (h11 - h10)
    return h00 + fx * (h11 - h01) + fy * (h01 - h00)


def slope_at(e, n):
    return max(abs(Hm(e + 1.5, n) - Hm(e - 1.5, n)), abs(Hm(e, n + 1.5) - Hm(e, n - 1.5))) / 3.0


def dpath(e, n): return float(poly_query([e], [n], PATH)[0][0])


def driver(e, n):
    d, ext = poly_query([e], [n], RIVER); return float(d[0] - ext[0, 0])      # distance to bank edge


def dstream(e, n):
    d, ext = poly_query([e], [n], STREAM); return float(d[0] - ext[0, 0])


def snow_edge(E, N):
    """smooth, coherent grass/snow border field (>0 = snow side) - v2: no per-triangle grain"""
    return E + 0.25 * N - 300 + 10.0 * nz(E * 0.5, N * 0.5)


def snowy_at(e, n, z=None):
    z = Hm(e, n) if z is None else z
    return (z >= 20 and snow_edge(e, n) > 0) or z >= 58


LAKE = (320, 270, 31, 23)
def in_lake(e, n, pad=0.0):
    return ((e - LAKE[0]) / (LAKE[2] + pad)) ** 2 + ((n - LAKE[1]) / (LAKE[3] + pad)) ** 2 < 1
CASTLE = (340, 340)
def r_castle(e, n): return math.hypot(e - CASTLE[0], n - CASTLE[1])

BLOCK = []                                  # (e, n, radius) keep-out circles for scattering
def blocked(e, n, extra=0.0):
    return any(math.hypot(e - a, n - b) < r + extra for a, b, r in BLOCK)


def free(e, n, path=4.6, riv=3.0, extra=0.0):
    if not (3 < e < MAP - 3 and 3 < n < MAP - 3): return False
    if dpath(e, n) < path: return False
    if driver(e, n) < riv or dstream(e, n) < riv: return False
    if in_lake(e, n, 3) or r_castle(e, n) < 24: return False
    if 240 < n < 252 and 292 < e < 318: return False        # waterfall channel
    return not blocked(e, n, extra)


MOVED = []
def settle(e, n, name, rad=14, maxslope=0.30, path=5.2, extra=0.0):
    """snap a requested position to the nearest sensible flat spot"""
    best = None
    for rr in [0, 2, 4, 6, 8, 11, 14, 18]:
        if rr > rad: break
        for k in range(1 if rr == 0 else 10):
            a = 2 * math.pi * k / 10 + rr
            ee, nn = e + rr * math.cos(a), n + rr * math.sin(a)
            if not free(ee, nn, path=path, riv=2.0, extra=extra): continue
            s = slope_at(ee, nn)
            if best is None or s < best[0] - 1e-6: best = (s, ee, nn, rr)
            if s <= maxslope:
                if rr > 0: MOVED.append((name, (e, n), (round(ee, 1), round(nn, 1))))
                return ee, nn
    if best:
        if best[3] > 0: MOVED.append((name, (e, n), (round(best[1], 1), round(best[2], 1))))
        return best[1], best[2]
    return e, n


# ------------------------------------------------------------------ terrain mesh
TM = ["grass_light", "grass_mid", "leaf_light", "pine_dark", "dirt", "stone_light", "stone_mid",
      "snow_light", "snow_shadow", "ice_light", "wood_dark"]
TI = {n: i for i, n in enumerate(TM)}


def build_terrain():
    h00 = HG[:-1, :-1]; h10 = HG[:-1, 1:]; h11 = HG[1:, 1:]; h01 = HG[1:, :-1]
    e0 = EE[:-1, :-1]; n0 = NN[:-1, :-1]
    # triangle A (v00,v10,v11) and B (v00,v11,v01)
    A = dict(z=(h00 + h10 + h11) / 3, de=(h10 - h00) / CS, dn=(h11 - h10) / CS, e=e0 + CS * 2 / 3, n=n0 + CS / 3)
    Bt = dict(z=(h00 + h11 + h01) / 3, de=(h11 - h01) / CS, dn=(h01 - h00) / CS, e=e0 + CS / 3, n=n0 + CS * 2 / 3)
    mats = []
    for t in (A, Bt):
        E_, N_, Z_ = t["e"].ravel(), t["n"].ravel(), t["z"].ravel()
        S = np.hypot(t["de"], t["dn"]).ravel(); SH = (t["de"] + t["dn"]).ravel()
        R, R2 = nrng.rand(len(E_)), nrng.rand(len(E_))
        V = np.clip(0.5 + nz(E_ * 0.55, N_ * 0.55) / 3.9, 0, 1) * 0.8 + R * 0.2      # coherent patches + little grain
        m = np.full(len(E_), TI["grass_mid"])
        regA = (E_ < 150) & (N_ < 130)
        m = np.where(regA, np.select([V < 0.30, V < 0.40], [TI["grass_light"], TI["leaf_light"]], TI["grass_mid"]), m)
        m = np.where(regA & (V > 0.62), TI["grass_light"], m)
        regO = ~regA
        m = np.where(regO, np.select([V < 0.30, V > 0.68, V > 0.58], [TI["grass_light"], TI["pine_dark"], TI["leaf_light"]], TI["grass_mid"]), m)
        m = np.where((Z_ > 30) & (V < 0.34) & ~regA, TI["stone_mid"], m)
        m = np.where(S > 0.55, np.where(R < 0.55, TI["stone_mid"], TI["grass_mid"]), m)
        m = np.where(S > 0.95, np.where(R < 0.5, TI["stone_mid"], TI["stone_light"]), m)
        dr2, er2 = poly_query(E_, N_, RIVER)
        m = np.where((Z_ < 0.05) & (dr2 < er2[:, 0] + 3.0), np.where(R < 0.6, TI["dirt"], TI["stone_mid"]), m)         # river bed (only at the river)
        ds, ex_ = poly_query(E_, N_, STREAM)
        m = np.where((ds < ex_[:, 0] * 0.9) & (Z_ < STREAM_WL + 0.1), np.where(R < 0.6, TI["dirt"], TI["stone_mid"]), m)
        tb = snow_edge(E_, N_)
        snowy = ((Z_ >= 20) & (tb + (R2 - 0.5) * 5.0 > 0)) | (Z_ >= 58)      # smooth border + ~3 m dither only
        sn = np.where(SH < -0.25, TI["snow_shadow"], TI["snow_light"])
        sn = np.where(V < 0.16, TI["snow_shadow"], sn)
        sn = np.where((tb < 6), np.where(R < 0.5, TI["snow_shadow"], TI["snow_light"]), sn)   # soft rim
        sn = np.where(S > 0.62, np.where(R < 0.5, TI["snow_shadow"], TI["stone_light"]), sn)
        sn = np.where(S > 1.0, np.where(R < 0.5, TI["stone_mid"], TI["stone_light"]), sn)
        m = np.where(snowy, sn, m)
        mats.append(m.reshape(t["z"].shape))
    nI = NC
    vid = lambda j, i: j * (NC + 1) + i
    verts = [(EE[j, i], NN[j, i], HG[j, i]) for j in range(NC + 1) for i in range(NC + 1)]
    faces = []; fm = []
    for j in range(NC):
        for i in range(NC):
            faces.append((vid(j, i), vid(j, i + 1), vid(j + 1, i + 1))); fm.append(mats[0][j, i])
            faces.append((vid(j, i), vid(j + 1, i + 1), vid(j + 1, i))); fm.append(mats[1][j, i])
    # skirts: dirt/snow lip, stone bands
    def skirt(idx_list, out_dir):
        for a, b in zip(idx_list[:-1], idx_list[1:]):
            (ea, na, ha), (eb, nb, hb) = verts[a], verts[b]
            lip = "snow_light" if snowy_at((ea + eb) / 2, (na + nb) / 2, (ha + hb) / 2) else "dirt"
            levels = [(0, lip), (2.5, "stone_mid"), (None, "stone_light")]
            tops = [(ha, hb), (ha - 2.5, hb - 2.5), (-6.0, -6.0), (-12.0, -12.0)]
            names = [lip, "stone_mid", "stone_light"]
            for k in range(3):
                base = len(verts)
                verts.extend([(ea, na, tops[k][0]), (eb, nb, tops[k][1]), (eb, nb, tops[k + 1][1]), (ea, na, tops[k + 1][0])])
                f = (base, base + 1, base + 2, base + 3)
                faces.append(f if out_dir else tuple(reversed(f))); fm.append(TI[names[k]])
    south = [vid(0, i) for i in range(NC + 1)]; north = [vid(NC, i) for i in range(NC + 1)]
    west = [vid(j, 0) for j in range(NC + 1)]; east = [vid(j, NC) for j in range(NC + 1)]
    skirt(south, False); skirt(north, True); skirt(west, True); skirt(east, False)
    base = len(verts)
    verts.extend([(0, 0, -12), (MAP, 0, -12), (MAP, MAP, -12), (0, MAP, -12)])
    faces.append((base + 3, base + 2, base + 1, base)); fm.append(TI["stone_mid"])
    me = bpy.data.meshes.new("Terrain")
    me.from_pydata(verts, [], faces); me.update()
    for n in TM: me.materials.append(MATS[n])
    me.polygons.foreach_set("material_index", np.array(fm, dtype=np.int32))
    me.polygons.foreach_set("use_smooth", [False] * len(me.polygons))
    ob = bpy.data.objects.new("Terrain_Ground", me); C_TERR.objects.link(ob)
    return ob


terrain = build_terrain()

# ------------------------------------------------------------------ helpers: ribbons / instances
def ribbon(name, pts, half, zfun, mat, coll, off=0.0):
    """pts: Nx2, half: N half widths, zfun(e,n)->z ; flat quads strip"""
    b = B(name)
    L = []; R = []
    for k in range(len(pts)):
        a = pts[max(k - 1, 0)]; c = pts[min(k + 1, len(pts) - 1)]
        t = np.array([c[0] - a[0], c[1] - a[1]]); t = t / max(np.hypot(*t), 1e-6)
        nrm = np.array([-t[1], t[0]])
        l = pts[k] + nrm * half[k]; r = pts[k] - nrm * half[k]
        z = max(zfun(l[0], l[1]), zfun(pts[k][0], pts[k][1]), zfun(r[0], r[1])) if callable(zfun) else zfun
        L.append((l[0], l[1], z + off)); R.append((r[0], r[1], z + off))
    for k in range(len(pts) - 1):
        m = mat(k) if callable(mat) else mat
        b.face([L[k], R[k], R[k + 1], L[k + 1]], m)
    me = b.mesh(recalc=False)
    # make sure normals point up
    for p in me.polygons:
        if p.normal.z < 0: p.flip()
    ob = bpy.data.objects.new(name, me); coll.objects.link(ob)
    return ob


def resample(poly, step):
    out = [poly[0]]; acc = 0.0
    for a, b in zip(poly[:-1], poly[1:]):
        L = math.hypot(b[0] - a[0], b[1] - a[1]);
        if L < 1e-6: continue
        d = step - acc
        while d <= L:
            t = d / L; out.append(a + (b - a) * t); d += step
        acc = (acc + L) % step if False else L - (d - step)
    out.append(poly[-1]); return np.array(out)


def inst(mesh, name, e, n, rz=0.0, sc=1.0, zoff=0.0, coll=None, z=None):
    ob = bpy.data.objects.new(name, mesh)
    ob.location = (e, n, (Hm(e, n) if z is None else z) + zoff)
    ob.rotation_euler = (0, 0, rz); ob.scale = (sc, sc, sc)
    (coll or C_MISC).objects.link(ob)
    return ob


# ------------------------------------------------------------------ prototypes
# ====================================================================== v2 detailed prototypes (part 1)
def sph(b, M, r, n, mats, lats=(-50, 0, 50), sz=1.0, jit=0.0, rot=0.0, mats_bot=None):
    """faceted blob: latitude rings + pole fans"""
    rings = []
    for k, l in enumerate(lats):
        la = math.radians(l); rr = r * math.cos(la)
        rings.append([M @ Vector((rr * math.cos(rot + 2 * math.pi * i / n + 0.35 * k) + rng.uniform(-jit, jit),
                                  rr * math.sin(rot + 2 * math.pi * i / n + 0.35 * k) + rng.uniform(-jit, jit),
                                  r * sz * math.sin(la) + rng.uniform(-jit, jit) * 0.5)) for i in range(n)])
    top = M @ Vector((0, 0, r * sz)); bot = M @ Vector((0, 0, -r * sz))
    mb = mats_bot or mats
    b.rings(rings, mats)
    for i in range(n):
        j = (i + 1) % n
        b.face([rings[-1][i], rings[-1][j], top], mats[i % len(mats)])
        b.face([rings[0][j], rings[0][i], bot], mb[i % len(mb)])


def arch_tunnel(b, M, w, hs, length, segs, mat_out, mat_frame, mat_in, frame=0.3):
    """arched passage extruded along -Y (from y=0 to y=-length)"""
    def prof(w_, hs_):
        pts = [(-w_, 0.0), (-w_, hs_)]
        for k in range(1, segs):
            a = math.pi - math.pi * k / segs
            pts.append((w_ * math.cos(a), hs_ + w_ * math.sin(a)))
        pts += [(w_, hs_), (w_, 0.0)]
        return pts
    po = prof(w, hs); pi_ = prof(w - frame, hs)
    P = lambda p, y: M @ Vector((p[0], y, p[1]))
    for k in range(len(po) - 1):
        b.face([P(po[k], 0), P(po[k + 1], 0), P(po[k + 1], -length), P(po[k], -length)], mat_out)
        b.face([P(po[k], -length), P(po[k + 1], -length), P(pi_[k + 1], -length), P(pi_[k], -length)], mat_frame)
        b.face([P(pi_[k], -length), P(pi_[k + 1], -length), P(pi_[k + 1], 0), P(pi_[k], 0)], mat_in)
    b.face([P(p, 0) for p in pi_], mat_in)


def roof_gable(b, hx, hy, zr, zt, over, mat, rows=4, lip=0.1):
    """gable roof, ridge along X, shingle rows with small lips; eaves at +-(hx,hy) height zr, ridge zt"""
    for side in (-1, 1):
        def Pt(t, off=0.0):
            return (None, side * (hy + over) * (1 - t) + side * off, zr + (zt - zr) * t)
        for k in range(rows):
            t0, t1 = k / rows, (k + 1) / rows
            ext0 = hx + over * (1 - t0) * 0.0 + 0.2 * t0; ext1 = hx + 0.2 * t1
            y0 = side * (hy + over) * (1 - t0) + side * lip; z0 = zr + (zt - zr) * t0 - lip * 0.8
            y1 = side * (hy + over) * (1 - t1); z1 = zr + (zt - zr) * t1
            yb = side * (hy + over) * (1 - t0); zb = zr + (zt - zr) * t0
            b.face([(-ext0, y0, z0), (ext0, y0, z0), (ext1, y1, z1), (-ext1, y1, z1)][::side], mat)
            b.face([(-ext0, yb, zb), (ext0, yb, zb), (ext0, y0, z0), (-ext0, y0, z0)][::side], mat)
    # ridge cap
    b.box(T(0, 0, zt - 0.02), 2 * hx + 0.5, 0.35, 0.28, "wood_dark")


# ---------------------------------------------------------------- trees
def pine_tier(b, z0, r, h, n, band, top, hd, rot, jit=0.1):
    ring0 = [Vector((r * math.cos(rot + 2 * math.pi * i / n) + rng.uniform(-jit, jit), r * math.sin(rot + 2 * math.pi * i / n) + rng.uniform(-jit, jit), z0 + rng.uniform(-.08, .08))) for i in range(n)]
    ring1 = [Vector((0.58 * r * math.cos(rot + 2 * math.pi * (i + 0.5) / n), 0.58 * r * math.sin(rot + 2 * math.pi * (i + 0.5) / n), z0 + 0.42 * h)) for i in range(n)]
    apex = Vector((0, 0, z0 + h)); dn = Vector((0, 0, z0 - hd))
    for i in range(n):
        j = (i + 1) % n
        b.face([ring0[i], ring0[j], ring1[i]], band[i % len(band)])
        b.face([ring0[j], ring1[j], ring1[i]], band[(i + 1) % len(band)])
        b.face([ring1[i], ring1[j], apex], top[i % len(top)])
        b.face([ring0[j], ring0[i], dn], "pine_dark")


PINE_V = [
    [(1.2, 3.3, 2.7), (2.9, 2.7, 2.6), (4.6, 2.15, 2.5), (6.2, 1.6, 2.4), (7.7, 1.0, 2.3)],
    [(1.0, 3.8, 2.6), (2.6, 3.1, 2.5), (4.2, 2.3, 2.4), (5.7, 1.4, 2.5)],
    [(1.0, 2.4, 2.4), (2.4, 1.9, 2.2), (3.8, 1.4, 2.2), (5.0, 0.8, 2.0)],
]


def proto_pine(snow=False, variant=0):
    b = B(("Pine_Snow" if snow else "Pine_Green") + (str(variant + 1) if variant else ""))
    b.cyl(T(), 0.45, 0.3, 1.9, 5, "wood_dark")
    for k, (z0, r, h) in enumerate(PINE_V[variant]):
        if snow:
            band = ["pine_dark", "grass_mid", "pine_dark", "snow_shadow"]; top = ["snow_light", "snow_shadow", "snow_light", "snow_light"]
        else:
            band = ["pine_dark", "grass_mid"] if variant != 1 else ["grass_mid", "pine_dark"]
            top = ["grass_mid", "leaf_light"] if k >= 2 or variant == 1 else ["pine_dark", "grass_mid"]
        pine_tier(b, z0, r, h, 8, band, top, 0.55, 0.5 * k)
    return b


def proto_dead_tree(snow=True):
    b = B("Tree_Bare_Snow")
    b.cyl(T(), 0.55, 0.22, 5.2, 5, "wood_dark", jit=0.04)
    for (z, a, ln, tilt) in ((2.0, 0.3, 2.6, 0.9), (2.8, 2.5, 2.3, 0.95), (3.7, 4.4, 2.0, 0.8), (4.5, 1.4, 1.5, 0.7), (1.4, 3.7, 1.8, 1.0)):
        b.cyl(T(0, 0, z, rz=a, ry=tilt), 0.17, 0.05, ln, 4, "wood_dark")
        b.cyl(T(0, 0, z + 0.12, rz=a, ry=tilt), 0.13, 0.04, ln * 0.82, 4, "snow_light", bottom=False)
    b.bip(T(0, 0, 5.1), 0.55, 0.55, 0.35, 0.0, 5, ["snow_light", "snow_shadow"])
    b.bip(T(0, 0, 0.1), 1.0, 1.0, 0.3, 0.0, 6, ["snow_light", "snow_shadow"])
    return b


def proto_round_tree(kind="round"):
    b = B({"round": "Tree_Round", "tall": "Tree_Tall", "blossom": "Tree_Blossom", "bush": "Bush"}[kind])
    if kind == "bush":
        for (x, y, z, r) in ((0, 0, 0.7, 1.2), (0.95, 0.35, 0.55, 0.85), (-0.85, -0.3, 0.5, 0.8)):
            sph(b, T(x, y, z), r, 6, ["grass_mid", "leaf_light"], (-40, 20, 60), 0.8, 0.08, mats_bot=["pine_dark"])
        for (x, y, z) in ((0.5, -0.9, 1.0), (-0.3, -0.85, 0.7), (1.3, -0.1, 0.8)):
            b.bip(T(x, y, z), 0.14, 0.14, 0.14, 0.14, 4, ["ember"])
        return b
    if kind == "tall":
        b.cyl(T(), 0.38, 0.22, 4.2, 5, "plaster", jit=0.02)
        for z in (0.8, 1.6, 2.5, 3.3): b.box(T(0.02, 0.0, z), 0.5, 0.5, 0.12, "iron", 0.9)
        b.cyl(T(0, 0, 3.5, ry=0.8, rz=1.0), 0.12, 0.05, 1.5, 4, "plaster")
        for (x, y, z, r, sz) in ((0, 0, 5.2, 1.55, 1.5), (0.5, 0.3, 7.0, 1.2, 1.4), (-0.35, -0.2, 8.4, 0.85, 1.4)):
            sph(b, T(x, y, z), r, 6, ["leaf_light", "grass_mid"], (-55, 0, 55), sz, 0.12, mats_bot=["grass_mid", "pine_dark"])
        return b
    b.cyl(T(), 0.45, 0.28, 2.7, 5, "wood_dark", jit=0.03)
    b.cyl(T(0, 0, 1.9, ry=0.7, rz=0.6), 0.14, 0.07, 1.6, 4, "wood_dark")
    b.cyl(T(0, 0, 2.1, ry=0.6, rz=3.7), 0.13, 0.07, 1.4, 4, "wood_dark")
    cm = ["leaf_light", "grass_mid"] if kind == "round" else ["leaf_light", "plaster", "leaf_light", "flower_y"]
    for (x, y, z, r) in ((0, 0, 3.7, 2.1), (1.0, 0.5, 4.9, 1.55), (-1.0, -0.4, 4.5, 1.5), (0.1, -0.2, 6.0, 1.15)):
        sph(b, T(x, y, z), r, 6, cm, (-50, 0, 50), 0.85, 0.15, mats_bot=["pine_dark", "grass_mid"])
    return b


# ---------------------------------------------------------------- rocks / cliffs / flowers
def proto_rock(name, r, kind=0, snow=False):
    b = B(name)
    n = 6 + (kind % 2)
    up = ["snow_light", "snow_shadow"] if snow else (["stone_light", "stone_mid"] if kind % 2 == 0 else ["stone_mid", "stone_light"])
    side = ["stone_mid", "stone_light", "stone_mid", "stone_light"]
    rr = random.Random(100 + kind)
    prof = [(1.0, 0.0), (0.98, 0.32), (0.66, 0.62)]
    rings = [[Vector((r * rs * math.cos(2 * math.pi * i / n + 0.4 * k + kind) * rr.uniform(0.82, 1.15), r * 0.85 * rs * math.sin(2 * math.pi * i / n + 0.4 * k + kind) * rr.uniform(0.82, 1.15),
                      r * z * 1.1 + rr.uniform(-0.05, 0.05) * r)) for i in range(n)] for k, (rs, z) in enumerate(prof)]
    b.rings(rings, side if not snow else ["stone_mid", "snow_shadow"], bottom_mat="stone_mid")
    apex = Vector((r * 0.1, 0, r * 0.95))
    for i in range(n): b.face([rings[-1][i], rings[-1][(i + 1) % n], apex], up[i % len(up)])
    if kind == 2 and not snow:           # small companion stone
        b.bip(T(r * 1.0, r * 0.3, 0), r * 0.35, r * 0.3, r * 0.3, 0.0, 5, ["stone_light", "stone_mid"])
    return b


def proto_pillar(name, snow=False):
    b = B(name)
    n = 7
    zs = [0, 2.6, 5.0, 7.3, 9.6]; rs = [3.1, 2.5, 2.8, 1.9, 1.1]
    rings = []
    for k, (z, r) in enumerate(zip(zs, rs)):
        rings.append([Vector((r * math.cos(2 * math.pi * i / n + 0.4 * k) + rng.uniform(-.3, .3),
                              r * math.sin(2 * math.pi * i / n + 0.4 * k) + rng.uniform(-.3, .3), z + rng.uniform(-.2, .2))) for i in range(n)])
    sides = ["stone_mid", "stone_light"]
    for k in range(4):
        for i in range(n):
            j = (i + 1) % n
            mm = (["snow_light", "snow_shadow"] if (snow and k >= 2) else sides)
            b.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], mm[(i + k) % 2])
    apex = Vector((0.2, 0.1, 12.2))
    for i in range(n):
        b.face([rings[4][i], rings[4][(i + 1) % n], apex], ("snow_light" if snow else "stone_light") if i % 2 else ("snow_shadow" if snow else "stone_mid"))
    b.face(list(reversed(rings[0])), "stone_mid")
    b.bip(T(2.4, 1.2, 0), 0.9, 0.8, 1.1, 0.0, 5, ["snow_light", "snow_shadow"] if snow else ["stone_light", "stone_mid"])
    return b


def proto_flower(color):
    b = B("Flowers_" + color)
    b.cyl(T(), 0.38, 0, 0.5, 5, "grass_mid", side_mats=["grass_mid", "leaf_light"])
    for (x, y, h, r) in ((0.12, 0, 0.7, 0.2), (-0.17, 0.1, 0.52, 0.17), (0.05, -0.2, 0.42, 0.15)):
        b.cyl(T(x, y, 0.1), 0.03, 0.03, h - 0.1, 3, "grass_mid", bottom=False)
        b.bip(T(x, y, h + 0.02), r, r, 0.1, 0.03, 5, [color])
        b.bip(T(x, y, h + 0.07), r * 0.35, r * 0.35, 0.05, 0.0, 4, ["glow" if color != "flower_y" else "wood_light"])
    return b


# ---------------------------------------------------------------- house
def proto_house():
    b = B("House_RedRoof")
    b.box(T(0, 0, -1.2), 7.0, 6.0, 1.8, "stone_mid", 0.99, 0.03)                       # foundation
    b.box(T(0, 0, 0.6), 6.5, 5.5, 3.0, "plaster", 1.0, 0.0)                              # walls
    for sx in (-1, 1):                                                                    # gable wall triangles
        b.face([(sx * 3.25, -2.75, 3.6), (sx * 3.25, 2.75, 3.6), (sx * 3.25, 0, 6.5)][::sx], "plaster")
    for sx in (-1, 1):
        for sy in (-1, 1): b.box(T(sx * 3.2, sy * 2.7, 0.55), 0.3, 0.3, 3.1, "wood_dark")   # corner posts
    b.box(T(0, -2.76, 3.25), 6.6, 0.16, 0.3, "wood_dark"); b.box(T(0, 2.76, 3.25), 6.6, 0.16, 0.3, "wood_dark")
    b.box(T(0, -2.76, 0.62), 6.6, 0.16, 0.18, "wood_dark")
    for sx in (-1, 1): b.box(T(sx * 3.26, 0, 3.25), 0.16, 5.6, 0.3, "wood_dark")
    for x in (-1.15, 1.15): b.box(T(x, -2.76, 0.62), 0.14, 0.16, 2.7, "wood_dark")        # half-timber studs

    roof_gable(b, 3.95, 3.3, 3.55, 7.0, 0.5, "roof_red", lip=0.2)
    for sd, th_ in ((-1, -0.835), (1, 0.835)):                                              # rafter battens + fascia
        for x in (-3.6, -2.4, -1.2, 0.0, 1.2, 2.4, 3.6):
            b.box(T(x, sd * 3.8, 3.5, rx=th_), 0.14, 0.1, 5.1, "wood_dark", bottom=False)
    for sx in (-1, 1):
        for sd in (-1, 1): b.box(T(sx * 4.05, sd * 3.8, 3.45, rx=sd * 0.835), 0.18, 0.18, 5.25, "wood_dark", bottom=False)
    # front dormer
    b.box(T(0, -2.1, 4.35), 1.5, 1.3, 1.55, "plaster")
    b.box(T(0, -2.78, 4.75), 0.95, 0.12, 0.95, "wood_dark"); b.box(T(0, -2.84, 4.82), 0.7, 0.1, 0.7, "glow")
    for sx in (-1, 1):
        b.face([(sx * 0.75, -2.75, 5.9), (sx * 0.75, -1.45, 5.9), (sx * 0.75, -1.45, 6.6)][::sx], "plaster") if False else None
    b.face([(-0.75, -2.75, 5.9), (0.75, -2.75, 5.9), (0, -2.75, 6.55)], "plaster")
    b.face([(-0.95, -2.95, 5.85), (-0.95, -1.4, 5.85), (0, -1.4, 6.7), (0, -2.95, 6.7)][::-1], "roof_red")
    b.face([(0.95, -2.95, 5.85), (0.95, -1.4, 5.85), (0, -1.4, 6.7), (0, -2.95, 6.7)], "roof_red")
    # chimney + smoke
    b.box(T(2.0, 1.2, 4.3), 0.95, 0.95, 3.75, "stone_light", 0.92)
    b.box(T(2.0, 1.2, 8.0), 1.3, 1.3, 0.25, "stone_mid")
    for (dx, z, r) in ((0.15, 8.9, 0.5), (0.5, 9.9, 0.7), (0.95, 11.1, 0.9)):
        b.bip(T(2.0 + dx, 1.2, z), r, r, r * 0.8, r * 0.7, 5, ["stone_light"], jit=0.05)
    # door
    b.box(T(0, -2.8, 0.6), 1.6, 0.2, 2.5, "wood_dark")
    b.box(T(0, -2.9, 0.6), 1.2, 0.12, 2.25, "wood_light", jit=0.01)
    b.box(T(0.4, -3.0, 1.5), 0.14, 0.1, 0.14, "iron"); b.box(T(0, -2.95, 1.7), 1.2, 0.05, 0.1, "iron")
    b.box(T(0, -3.35, -0.15), 2.0, 1.0, 0.75, "stone_light", 0.95)                         # step
    b.box(T(-1.3, -2.9, 2.85), 0.3, 0.6, 0.18, "wood_dark"); b.box(T(1.3, -2.9, 2.85), 0.3, 0.6, 0.18, "wood_dark")
    b.box(T(0, -3.15, 3.0), 3.0, 0.2, 0.22, "wood_dark")                                   # door canopy beam
    # windows
    for x in (-2.2, 2.2):
        b.box(T(x, -2.78, 1.2), 1.4, 0.16, 1.45, "wood_dark")
        b.box(T(x, -2.87, 1.3), 1.0, 0.1, 1.05, "glow")
        b.box(T(x, -2.86, 1.82), 1.0, 0.12, 0.08, "wood_dark"); b.box(T(x, -2.86, 1.3), 0.08, 0.12, 1.05, "wood_dark")
        b.box(T(x - 0.85, -2.9, 1.2), 0.38, 0.1, 1.4, "wood_light"); b.box(T(x + 0.85, -2.9, 1.2), 0.38, 0.1, 1.4, "wood_light")
        b.box(T(x, -3.0, 0.85), 1.5, 0.35, 0.3, "wood_dark")                                # flower box
        b.bip(T(x - 0.4, -3.0, 1.2), 0.16, 0.16, 0.16, 0.0, 4, ["flower_y"]); b.bip(T(x + 0.1, -3.0, 1.25), 0.16, 0.16, 0.16, 0.0, 4, ["flower_r"])
        b.bip(T(x + 0.5, -3.0, 1.2), 0.16, 0.16, 0.16, 0.0, 4, ["flower_w"])
    for sx in (-1, 1):
        b.box(T(sx * 3.3, 0, 1.2), 0.16, 1.4, 1.45, "wood_dark"); b.box(T(sx * 3.38, 0, 1.3), 0.1, 1.0, 1.05, "glow")
    b.box(T(0, 2.9, 0.9), 1.2, 0.2, 1.2, "wood_dark"); b.box(T(0, 2.98, 1.0), 0.9, 0.08, 0.9, "glow")   # back window
    # woodpile
    for k, z in enumerate((0.0, 0.34, 0.68)):
        for m in range(3 - k): b.box(T(-4.25, -1.0 + m * 0.55 + k * 0.28, 0.0 if False else z - 0.6), 0.5, 0.5, 0.3, "wood_light" if (m + k) % 2 else "wood_dark")
    return b


def proto_windmill():
    b = B("Windmill")
    b.box(T(0, 0, -1.2), 6.6, 6.6, 1.3, "stone_mid", 0.95)
    b.cyl(T(0, 0, -0.5), 3.4, 3.1, 1.6, 8, "stone_mid", jit=0.04, side_mats=["stone_mid", "stone_light"])
    b.cyl(T(0, 0, 1.0), 3.0, 2.6, 3.4, 8, "plaster", jit=0.04, side_mats=["plaster", "stone_light"])
    b.cyl(T(0, 0, 4.3), 2.75, 2.75, 0.3, 8, "wood_dark")
    for k in range(8):                                                                    # balcony posts
        a = 2 * math.pi * k / 8 + 0.2
        b.box(T(3.15 * math.cos(a), 3.15 * math.sin(a), 4.5), 0.14, 0.14, 0.8, "wood_dark")
    b.cyl(T(0, 0, 4.0), 3.3, 3.3, 0.5, 8, "wood_dark")
    b.cyl(T(0, 0, 4.5), 2.55, 2.0, 4.0, 8, "plaster", jit=0.03, side_mats=["plaster", "stone_light"])
    b.cyl(T(0, 0, 8.5), 2.35, 2.35, 0.4, 8, "wood_dark")
    rings = [[Vector((3.1 * math.cos(2 * math.pi * i / 8 + 0.2), 3.1 * math.sin(2 * math.pi * i / 8 + 0.2), 8.9)) for i in range(8)],
             [Vector((2.3 * math.cos(2 * math.pi * i / 8 + 0.2), 2.3 * math.sin(2 * math.pi * i / 8 + 0.2), 9.7)) for i in range(8)],
             [Vector((1.0 * math.cos(2 * math.pi * i / 8 + 0.2), 1.0 * math.sin(2 * math.pi * i / 8 + 0.2), 11.2)) for i in range(8)]]
    b.rings(rings, ["roof_purple", "iron"], apex=Vector((0, 0, 12.6)), apex_mat=["roof_purple", "iron"])
    b.cyl(T(0, 0, 12.5), 0.12, 0, 1.0, 4, "wood_dark")
    # door, windows
    b.box(T(0, -2.9, 0.6), 1.5, 0.2, 2.3, "wood_dark"); b.box(T(0, -3.0, 0.6), 1.1, 0.1, 2.0, "wood_light")
    b.box(T(0, -3.5, -0.2), 2.0, 1.0, 0.8, "stone_light", 0.9)
    for (a, z, s) in ((0.9, 2.6, 0.7), (-0.9 + math.pi, 2.6, 0.7), (math.pi / 2 + 0.5, 5.7, 0.8), (-0.5 - math.pi / 2, 5.7, 0.8), (math.pi, 6.4, 0.8)):
        rr = 2.55 if z < 4 else 2.15
        b.box(T(rr * math.cos(a + math.pi / 2 * 0), rr * math.sin(a), z, rz=a - math.pi / 2), 0.9 * s + 0.2, 0.3, 1.2 * s + 0.2, "wood_dark")
        b.box(T((rr + 0.13) * math.cos(a), (rr + 0.13) * math.sin(a), z + 0.1, rz=a - math.pi / 2), 0.9 * s, 0.1, 1.2 * s, "glow")
    # hub + 4 sails
    yh, zh = -2.75, 8.3
    b.cyl(T(0, yh - 0.3, zh, rx=math.pi / 2), 0.55, 0.45, 0.9, 6, "iron")
    b.cyl(T(0, yh - 0.9, zh, rx=math.pi / 2), 0.2, 0.0, 0.6, 4, "wood_dark")
    b.box(T(0, -1.9, 7.6), 0.5, 1.8, 0.3, "wood_dark")
    for k in range(4):
        a = 0.35 + k * math.pi / 2
        M = T(0, yh - 0.55, zh, ry=a)
        b.box(M @ Matrix.Translation((0, 0, 0.2)), 0.3, 0.26, 7.4, "wood_dark")
        for sx in (0.15, 1.5):
            b.box(M @ Matrix.Translation((sx + 0.1, -0.08, 1.6)), 0.12, 0.12, 5.6, "wood_light", bottom=False)
        for z in (1.6, 2.7, 3.8, 4.9, 6.0, 7.1):
            b.box(M @ Matrix.Translation((0.85, -0.08, z)), 1.5, 0.12, 0.1, "wood_light", bottom=False)
        for z in (1.7, 3.9):
            b.box(M @ Matrix.Translation((0.85, -0.14, z + 0.1)), 1.1, 0.04, 1.0, "plaster", bottom=False)
    b.box(T(0, 3.4, 2.5), 0.3, 0.3, 6.0, "wood_dark", 0.9)
    return b


# ---------------------------------------------------------------- fence / lamp / bridge / signs
def proto_fence():
    b = B("Fence_4m")
    for x in (-1.9, 0.0, 1.9):
        b.box(T(x, 0, 0), 0.24, 0.24, 1.25, "wood_dark", 0.9, 0.02)
        b.cyl(T(x, 0, 1.25), 0.2, 0, 0.3, 4, "wood_dark", rot=math.pi / 4)
    for z in (0.42, 0.88): b.box(T(0, 0.04, z), 4.0, 0.12, 0.17, "wood_light", jit=0.02)
    b.box(T(-0.95, -0.06, 0.1, ry=-0.0), 0.1, 0.07, 0.1, "iron")
    b.box(T(0.95, 0.12, 0.35, ry=-0.78), 0.1, 0.07, 1.1, "wood_dark", bottom=False)
    for (x, z) in ((-1.9, 0.42), (-1.9, 0.88), (1.9, 0.42), (1.9, 0.88), (0, 0.42), (0, 0.88)):
        b.box(T(x, -0.1, z + 0.06), 0.07, 0.05, 0.05, "iron")
    return b


def proto_lamp():
    b = B("Lamp_Post")
    b.box(T(), 0.5, 0.5, 0.4, "stone_mid", 0.85)
    b.box(T(0, 0, 0.4), 0.22, 0.22, 3.4, "wood_dark", 0.85)
    b.box(T(0.4, 0, 3.15), 1.0, 0.14, 0.14, "wood_dark")
    b.box(T(0.25, 0, 2.85, ry=-0.6), 0.1, 0.1, 0.8, "wood_dark", bottom=False)
    b.box(T(0.85, 0, 2.5), 0.05, 0.05, 0.6, "iron")
    for (dx, dy) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        b.box(T(0.85 + dx * 0.21, dy * 0.21, 1.75), 0.06, 0.06, 0.8, "iron")
    b.box(T(0.85, 0, 1.85), 0.34, 0.34, 0.6, "glow")
    b.box(T(0.85, 0, 1.65), 0.5, 0.5, 0.1, "iron"); b.box(T(0.85, 0, 2.55), 0.5, 0.5, 0.08, "iron")
    b.cyl(T(0.85, 0, 2.6), 0.4, 0, 0.35, 4, "iron", rot=math.pi / 4)
    b.cyl(T(0.85, 0, 2.95), 0.07, 0, 0.18, 4, "glow")
    return b


def proto_bridge(name, L, wood_snow=False):
    b = B(name)
    W = 4.0; nseg = max(int(L / 0.6), 6); seg = L / nseg
    arch = lambda x: 0.8 * (1 - (2 * x / L) ** 2)
    for k in range(nseg):
        x = -L / 2 + seg * (k + 0.5)
        dz = (arch(x + seg / 2) - arch(x - seg / 2)) / seg
        b.box(T(x, 0, 0.5 + arch(x), ry=-math.atan(dz)), seg - 0.04, W - 0.55, 0.2, "wood_light" if k % 4 else "wood_dark", jit=0.008)
    ns = 6
    for sy in (-1, 1):
        for k in range(ns):
            x0 = -L / 2 + L * k / ns; x1 = -L / 2 + L * (k + 1) / ns
            xm = (x0 + x1) / 2; dz = (arch(x1) - arch(x0)) / (x1 - x0)
            b.box(T(xm, sy * (W / 2 - 0.55), 0.18 + arch(xm), ry=-math.atan(dz)), x1 - x0 + 0.02, 0.32, 0.36, "wood_dark")        # stringer
            b.box(T(xm, sy * (W / 2 - 0.15), 1.52 + arch(xm), ry=-math.atan(dz)), x1 - x0 + 0.02, 0.14, 0.18, "wood_dark")        # hand rail
            b.box(T(xm, sy * (W / 2 - 0.15), 0.95 + arch(xm), ry=-math.atan(dz)), x1 - x0 + 0.02, 0.1, 0.12, "wood_light")        # mid rail
        for k in range(ns + 1):
            x = -L / 2 + L * k / ns
            b.box(T(x, sy * (W / 2 - 0.15), 0.55 + arch(x)), 0.26, 0.26, 1.2, "wood_dark", 0.9)
            b.cyl(T(x, sy * (W / 2 - 0.15), 1.75 + arch(x)), 0.2, 0, 0.25, 4, "wood_dark", rot=math.pi / 4)
    for sx in (-1, 1):
        b.box(T(sx * (L / 2 + 0.4), 0, -1.0), 1.8, W + 0.8, 1.6, "stone_mid", 0.94)
        b.box(T(sx * (L / 2 + 0.2), 0, 0.2), 1.3, W + 0.2, 0.3, "stone_light", 0.96)
    return b


def proto_sign(kind):
    b = B("Sign_" + kind)
    b.box(T(), 0.5, 0.5, 0.5, "stone_mid", 0.85)
    b.box(T(0, 0, 0.5), 0.36, 0.36, 4.8, "wood_dark", 0.9)
    b.box(T(0, -0.1, 1.6, ry=0.0), 0.15, 0.15, 0.15, "iron")
    b.cyl(T(0, -0.25, 5.4, rx=math.pi / 2), 2.7, 2.7, 0.5, 8, "wood_dark", rot=math.pi / 8)          # rim
    b.cyl(T(0, -0.72, 5.4, rx=math.pi / 2), 2.25, 2.25, 0.12, 8, "plaster", rot=math.pi / 8)        # light face
    f = -0.9
    if kind == "pine":
        for (z0, r, h) in ((4.45, 1.25, 1.5), (5.2, 0.95, 1.4), (5.95, 0.65, 1.3)):
            b.cyl(T(0, f, z0), r, 0, h, 6, "grass_mid", side_mats=["pine_dark", "grass_mid"])
        b.box(T(0, f, 3.95), 0.3, 0.2, 0.55, "wood_dark")
    elif kind == "mountain":
        b.face([(-1.9, f, 4.35), (1.9, f, 4.35), (0.0, f, 6.9)][::-1], "stone_mid")
        b.face([(0.5, f - 0.02, 4.35), (1.9, f - 0.02, 4.35), (0.0, f - 0.02, 6.9)][::-1], "iron")
        b.face([(-0.6, f - 0.04, 5.9), (0.6, f - 0.04, 5.9), (0.0, f - 0.04, 6.9)][::-1], "snow_light")
    elif kind == "snow":
        for a in (0, math.pi / 3, 2 * math.pi / 3):
            b.box(T(0, f, 5.4, ry=a) @ Matrix.Translation((0, 0, -1.65)), 0.3, 0.2, 3.3, "ice_dark")
            for s in (-1, 1):
                for dz in (0.9, -0.9) if False else (0.0,):
                    b.box(T(0, f - 0.05, 5.4, ry=a) @ Matrix.Translation((0.0, 0, s * 1.1)), 0.9, 0.1, 0.12, "ice_dark")
    elif kind == "castle":
        b.box(T(0, f, 4.35), 2.9, 0.22, 1.2, "ice_dark")
        for x, h in ((-1.15, 1.7), (0, 2.5), (1.15, 1.7)):
            b.cyl(T(x, f, 5.55), 0.5, 0, h, 4, "ice_light", rot=math.pi / 4)
        b.box(T(0, f - 0.05, 4.6), 0.5, 0.15, 0.7, "glow")
    return b


def proto_tuft():
    b = B("Grass_Tuft")
    for k, (x, y, h) in enumerate(((0, 0, 0.7), (0.16, 0.06, 0.5), (-0.14, 0.1, 0.55), (0.04, -0.15, 0.45), (-0.08, -0.1, 0.62))):
        b.cyl(T(x, y, 0, rx=0.12 * (k - 2), ry=0.18 * (k % 3 - 1)), 0.07, 0, h, 3, "leaf_light" if k % 2 else "grass_mid", bottom=False)
    return b


def proto_stump():
    b = B("Tree_Stump")
    b.cyl(T(), 0.5, 0.4, 0.55, 6, "wood_dark", jit=0.03)
    b.cyl(T(0, 0, 0.55), 0.39, 0.39, 0.04, 6, "wood_light")
    for a in (0.4, 2.3, 4.2):
        b.box(T(0.38 * math.cos(a), 0.38 * math.sin(a), 0.0, rz=a), 0.5, 0.14, 0.2, "wood_dark")
    return b


def proto_mushroom():
    b = B("Red_Mushrooms")
    for (x, y, h, r) in ((0, 0, 0.3, 0.2), (0.28, 0.1, 0.2, 0.14), (-0.2, 0.2, 0.16, 0.11)):
        b.cyl(T(x, y, 0), 0.05, 0.04, h, 4, "plaster")
        b.bip(T(x, y, h), r, r, r * 0.6, 0.0, 6, ["ember", "ember", "roof_red"])
    return b


def proto_log():
    b = B("Fallen_Log")
    b.cyl(T(0, 0, 0.26, ry=math.pi / 2), 0.26, 0.22, 1.6, 6, "wood_dark")
    b.cyl(T(1.6, 0, 0.26, ry=math.pi / 2), 0.2, 0.2, 0.03, 6, "wood_light")
    return b


def proto_slab_arc():
    b = B("Slab_Arc")
    for k in range(6):
        a = math.radians(-70 + k * 28)
        b.box(T(1.3 * math.cos(a), 1.3 * math.sin(a), 0.0, rz=a), 0.5, 0.75, 0.09, ["stone_light", "stone_mid"][k % 2], 0.95, 0.03)
    return b


def proto_ice_lamp():
    b = B("Ice_Stone_Lamp")
    b.box(T(), 0.8, 0.8, 0.4, "stone_mid", 0.85)
    b.box(T(0, 0, 0.4), 0.45, 0.45, 1.5, "stone_light", 0.8)
    b.box(T(0, 0, 1.9), 0.8, 0.8, 0.14, "stone_mid")
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        b.box(T(sx * 0.27, sy * 0.27, 2.04), 0.08, 0.08, 0.55, "iron")
    b.box(T(0, 0, 2.1), 0.36, 0.36, 0.42, "glow")
    b.cyl(T(0, 0, 2.6), 0.55, 0, 0.5, 4, "ice_light", rot=math.pi / 4)
    b.bip(T(0, 0, 3.05), 0.12, 0.12, 0.14, 0.0, 4, ["snow_light"])
    return b


def proto_chain_post():
    b = B("Chain_Post")
    b.box(T(), 0.5, 0.5, 0.35, "stone_mid", 0.85)
    b.box(T(0, 0, 0.35), 0.3, 0.3, 0.8, "stone_light", 0.85)
    b.cyl(T(0, 0, 0.55, ry=math.pi / 2), 0.1, 0.1, 0.08, 6, "iron")
    b.bip(T(0, 0, 1.15), 0.2, 0.2, 0.15, 0.0, 4, ["snow_light"])
    return b


def proto_barrel():
    b = B("Barrel")
    b.cyl(T(), 0.42, 0.55, 0.45, 8, "wood_dark")
    b.cyl(T(0, 0, 0.45), 0.55, 0.42, 0.45, 8, "wood_light")
    b.cyl(T(0, 0, 0.36), 0.57, 0.57, 0.07, 8, "iron")
    b.cyl(T(0, 0, 0.5), 0.57, 0.57, 0.07, 8, "iron")
    return b


def proto_crate():
    b = B("Crate")
    b.box(T(), 0.9, 0.9, 0.8, "wood_light", 0.97)
    for sx in (-1, 1):
        for sy in (-1, 1): b.box(T(sx * 0.4, sy * 0.4, 0.0), 0.1, 0.1, 0.84, "wood_dark")
    b.box(T(0, 0, 0.8), 0.8, 0.8, 0.06, "wood_dark")
    return b


def proto_stall(awn):
    b = B("Market_Stall")
    b.box(T(0, 0.15, 0), 2.8, 0.9, 0.9, "wood_dark")
    b.box(T(0, 0.15, 0.9), 3.0, 1.05, 0.1, "wood_light")
    for sx in (-1, 1):
        b.box(T(sx * 1.55, -0.6, 0), 0.14, 0.14, 2.3, "wood_dark")
        b.box(T(sx * 1.55, 0.9, 0), 0.14, 0.14, 2.9, "wood_dark")
    # canvas: sloped top + striped front valance
    zf_, zb_, yf, yb, hw = 2.35, 2.95, -1.0, 1.15, 1.9
    b.face([(-hw, yf, zf_), (hw, yf, zf_), (hw, yb, zb_), (-hw, yb, zb_)][::-1], awn)
    b.face([(-hw, yf, zf_ - 0.05), (hw, yf, zf_ - 0.05), (hw, yb, zb_ - 0.05), (-hw, yb, zb_ - 0.05)], awn)
    for k in range(6):
        x0 = -hw + 2 * hw * k / 6; x1 = -hw + 2 * hw * (k + 1) / 6
        b.face([(x0, yf, zf_), (x1, yf, zf_), (x1, yf - 0.05, zf_ - 0.45), (x0, yf - 0.05, zf_ - 0.45)][::-1], awn if k % 2 == 0 else "plaster")
    for k, (x, c) in enumerate(((-0.9, "flower_r"), (-0.3, "flower_y"), (0.35, "leaf_light"), (0.95, "flower_r"))):
        b.bip(T(x, 0.05, 1.12), 0.24, 0.24, 0.2, 0.0, 5, [c])
    b.box(T(1.3, 0.55, 0.9), 0.5, 0.5, 0.4, "wood_light")
    return b


def proto_fountain():
    b = B("Village_Fountain")
    b.cyl(T(), 4.0, 3.7, 0.25, 12, "stone_mid", side_mats=["stone_mid", "stone_light"])
    b.cyl(T(0, 0, 0.25), 3.1, 2.9, 0.65, 12, "stone_light", side_mats=["stone_light", "stone_mid"])
    b.cyl(T(0, 0, 0.9), 3.2, 3.2, 0.12, 12, "stone_mid")
    b.cyl(T(0, 0, 0.7), 2.7, 2.7, 0.06, 12, "water")
    b.box(T(0, 0, 0.7), 1.6, 1.6, 1.4, "stone_mid", 0.85)
    b.box(T(0, 0, 2.1), 1.9, 1.9, 0.18, "stone_light")
    # statue: knight with sword + shield
    b.box(T(0, 0, 2.28), 0.75, 0.5, 1.0, "stone_light", 0.9)
    b.box(T(0, 0, 3.28), 1.0, 0.55, 1.2, "stone_light", 0.85)
    b.bip(T(0, 0, 4.7), 0.34, 0.34, 0.4, 0.3, 6, ["stone_light", "stone_mid"])
    b.cyl(T(0, 0, 4.75), 0.38, 0.0, 0.4, 6, "stone_mid")
    b.box(T(0.66, -0.1, 2.8), 0.12, 0.12, 2.2, "iron"); b.box(T(0.66, -0.1, 3.3), 0.5, 0.12, 0.1, "iron")
    b.box(T(-0.72, -0.28, 3.1), 0.14, 0.6, 0.8, "wood_dark")
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        b.bip(T(2.25 * math.cos(a), 2.25 * math.sin(a), 1.0), 0.18, 0.18, 0.35, 0.0, 4, ["ice_light"])
    return b


def proto_dock(L):
    b = B("Wooden_Dock")
    W = 2.6; n = int(L / 0.5)
    for k in range(n):
        b.box(T(k * L / n + L / n / 2, 0, 0), L / n - 0.05, W, 0.14, "wood_light" if k % 3 else "wood_dark", jit=0.01)
    for sy in (-1, 1):
        b.box(T(L / 2, sy * (W / 2 - 0.2), -0.28), L, 0.22, 0.28, "wood_dark")
        for x in [i * L / 4 for i in range(5)]:
            b.cyl(T(x, sy * (W / 2 - 0.1), -2.4), 0.16, 0.16, 3.4, 5, "wood_dark")
            if x > 0: b.cyl(T(x, sy * (W / 2 - 0.1), 0.0), 0.15, 0, 0.2, 5, "wood_dark") if False else None
        b.box(T(L * 0.55, sy * (W / 2 - 0.1), 0.85), L * 0.9, 0.09, 0.09, "wood_dark")
    b.box(T(L + 0.05, 0, 0.0), 0.3, W, 0.14, "wood_dark")
    return b


def proto_start():
    b = B("Start_Marker")
    b.cyl(T(), 3.0, 2.6, 0.3, 8, "iron")
    b.box(T(), 0.4, 0.4, 3.0, "wood_dark")
    b.bip(T(0, 0, 8.0, rx=math.pi / 2), 2.6, 0.9, 4.2, 4.2, 4, ["marker_green"], rot=math.pi / 4)
    return b


# ---- 8 sample objects
def proto_snowman():
    b = B("SM1_Snowman")
    ms = ["snow_light", "snow_shadow"]
    sph(b, T(0, 0, 0.95), 1.0, 8, ms, (-55, -20, 20, 55), 0.92, 0.03)
    sph(b, T(0, 0, 2.15), 0.75, 8, ms, (-55, -20, 20, 55), 0.92, 0.02)
    sph(b, T(0, 0, 3.1), 0.55, 8, ms, (-50, 0, 50), 0.95, 0.02)
    b.cyl(T(0, 0, 2.95), 0.66, 0.66, 0.2, 8, "ember", rot=0.4)                             # scarf
    b.box(T(0.45, -0.52, 2.35, rx=0.12, rz=0.3), 0.2, 0.12, 0.8, "ember")
    b.box(T(0.62, -0.42, 2.0, rx=0.15, rz=0.3), 0.2, 0.12, 0.35, "ember")
    b.cyl(T(0, 0, 3.55), 0.6, 0.6, 0.08, 8, "iron")                                         # hat
    b.cyl(T(0, 0, 3.6), 0.4, 0.34, 0.6, 8, "iron")
    b.cyl(T(0, 0, 3.7), 0.42, 0.42, 0.14, 8, "ember")                                       # hat band
    b.cyl(T(0, -0.58, 3.12, rx=-math.pi / 2), 0.1, 0, 0.55, 4, "roof_red")                  # carrot nose
    for x in (-0.2, 0.2): b.bip(T(x, -0.5, 3.3), 0.07, 0.07, 0.06, 0.02, 4, ["iron"])      # eyes
    for k in range(5):
        a = (k - 2) * 0.22; b.bip(T(0.5 * math.sin(a), -0.5 * math.cos(a), 2.88 - 0.0 - 0.18 * abs(k - 2) - 0.0 + (-0.05)), 0.05, 0.05, 0.05, 0.02, 4, ["iron"])  # smile
    for z in (1.55, 2.0): b.bip(T(0, -0.7, z), 0.1, 0.1, 0.08, 0.02, 4, ["iron"])         # buttons
    for s in (-1, 1):                                                                       # stick arms
        b.cyl(T(s * 0.7, 0, 2.15, ry=s * 1.2), 0.06, 0.03, 1.5, 3, "wood_dark")
        b.cyl(T(s * 1.95, 0, 2.5, ry=-s * 0.5), 0.04, 0.0, 0.45, 3, "wood_dark")
    return b


def proto_igloo():
    b = B("SM2_Igloo")
    n = 10
    prof = [(3.1, 0.0), (3.05, 0.55), (2.75, 1.25), (2.2, 1.95), (1.5, 2.55), (0.75, 2.95)]
    rings = [[Vector((r * math.cos(2 * math.pi * i / n + k * math.pi / n), r * math.sin(2 * math.pi * i / n + k * math.pi / n), z + rng.uniform(-.03, .03)))
              for i in range(n)] for k, (r, z) in enumerate(prof)]
    b.rings(rings, ["snow_light", "snow_shadow", "snow_light", "ice_light"], apex=Vector((0, 0, 3.15)), apex_mat=["snow_light", "snow_shadow"], bottom_mat="snow_shadow")
    b.cyl(T(0, 0, 3.0), 0.35, 0.28, 0.35, 5, "snow_shadow")
    arch_tunnel(b, T(0, -2.3, 0), 1.15, 0.7, 2.0, 4, "snow_light", "ice_light", "iron", 0.3)
    for (x, y, r) in ((-2.4, -2.9, 0.8), (2.2, -3.2, 0.7), (-2.6, 1.8, 0.9), (2.5, 2.2, 0.8)):
        b.bip(T(x, y, 0), r, r * 0.8, r * 0.45, 0.0, 5, ["snow_light", "snow_shadow"], jit=0.05)
    for (x, y, rz) in ((-0.9, -4.55, 0.1), (1.0, -4.5, -0.1)):
        b.box(T(x, y, 0, rz=rz), 0.55, 0.45, 0.5, "ice_light", 0.9)
    return b


def proto_crystal():
    b = B("SM3_IceCrystal")
    spec = ((0, 0, 6.2, 0.85, 0.0, 0.05), (1.2, 0.5, 3.8, 0.58, 0.32, 0.5), (-1.0, 0.7, 4.6, 0.62, -0.35, -0.3),
            (0.3, -1.0, 2.8, 0.45, 0.45, -0.1), (-1.5, -0.6, 2.4, 0.4, -0.5, 0.2), (1.9, -0.7, 1.9, 0.33, 0.6, 0.35), (-0.4, 1.4, 1.6, 0.3, -0.1, 0.9))
    for (x, y, h, r, rx, ry) in spec:
        M = T(x, y, 0, rx=rx, ry=ry)
        b.cyl(M, r, r * 0.9, h * 0.6, 6, "ice_light", side_mats=["ice_light", "ice_dark", "ice_light", "snow_shadow", "ice_dark", "ice_light"], bottom=False)
        b.cyl(M @ Matrix.Translation((0, 0, h * 0.6)), r * 0.9, 0, h * 0.4, 6, "ice_light", side_mats=["ice_dark", "ice_light", "snow_shadow"], bottom=False)
    b.bip(T(0, 0, 0), 1.9, 1.7, 0.35, 0.0, 7, ["snow_light", "snow_shadow"], jit=0.1)
    b.bip(T(-1.3, 0.2, 0), 0.7, 0.6, 0.5, 0.0, 5, ["stone_light", "stone_mid"])
    return b


def proto_sled():
    b = B("SM5_Sled")
    for sy in (-0.62, 0.62):
        b.box(T(-0.45, sy, 0.02), 1.9, 0.14, 0.2, "wood_dark")
        b.box(T(0.4, sy, 0.02, ry=0.0), 0.5, 0.14, 0.2, "wood_dark")
        for k, (dx, dz, ry) in enumerate(((0.85, 0.08, -0.35), (1.15, 0.28, -0.7), (1.3, 0.55, -1.1))):
            b.box(T(dx + 0.12 * k, sy, dz, ry=ry), 0.45, 0.14, 0.2, "wood_dark", bottom=False)
        for x in (-1.1, -0.2, 0.55): b.box(T(x, sy, 0.2), 0.12, 0.12, 0.34, "iron")
        b.box(T(-1.45, sy, 0.1, ry=0.5), 0.3, 0.14, 0.2, "wood_dark", bottom=False)
    for x in (-1.2, -0.65, -0.1, 0.45, 0.95): b.box(T(x, 0, 0.5), 0.46, 1.65, 0.09, "wood_light", jit=0.012)
    b.box(T(-0.2, 0, 0.59), 0.8, 1.2, 0.14, "roof_red")                                       # cushion
    b.box(T(-1.45, 0, 0.55), 0.1, 1.5, 0.55, "wood_dark")                                     # backrest
    for sy in (-1, 1):
        b.box(T(1.25, sy * 0.5, 0.78), 0.9, 0.05, 0.05, "wood_light", bottom=False)           # rope
    b.box(T(1.75, 0, 0.8), 0.06, 1.0, 0.06, "wood_light")
    return b


def proto_campfire():
    b = B("SM6_Campfire")
    b.cyl(T(), 1.5, 1.2, 0.2, 10, "snow_light", jit=0.06, side_mats=["snow_light", "snow_shadow"])
    b.bip(T(1.3, 0.8, 0.1), 0.6, 0.5, 0.3, 0.0, 5, ["snow_light", "snow_shadow"])
    for i in range(9):
        a = 2 * math.pi * i / 9
        b.bip(T(0.95 * math.cos(a), 0.95 * math.sin(a), 0.14, rz=a), 0.3 + 0.05 * (i % 3), 0.24, 0.25 + 0.04 * (i % 2), 0.0, 5, ["stone_light", "stone_mid"], jit=0.03)
    for k in range(6):
        a = k * math.pi / 3 + 0.4
        b.cyl(T(0, 0, 0.2, rz=a, ry=-0.55 - 0.1 * (k % 2)), 0.1, 0.09, 1.25, 5, "wood_dark" if k % 2 else "wood_light", bottom=False)
    b.cyl(T(0.0, 0.0, 0.3), 0.62, 0, 1.4, 6, "ember", bottom=False)
    b.cyl(T(0.05, 0.05, 0.35), 0.44, 0, 1.15, 5, "glow", bottom=False)
    b.cyl(T(-0.3, -0.2, 0.3), 0.3, 0, 0.8, 4, "ember", bottom=False)
    b.cyl(T(0.32, 0.18, 0.3), 0.26, 0, 0.7, 4, "glow", bottom=False)
    b.cyl(T(0.0, 0.0, 1.4), 0.12, 0, 0.5, 4, "glow")
    return b


def proto_lake():
    b = B("SM7_IceLake")
    rr = random.Random(5)
    n = 30; ro = []; rm = []; ri = []
    for i in range(n):
        a = 2 * math.pi * i / n; k = rr.uniform(0.88, 1.1) if i % 2 else rr.uniform(0.96, 1.04)
        ro.append((30 * k * math.cos(a), 22 * k * math.sin(a)))
        km = rr.uniform(0.9, 1.1); rm.append((21 * km * math.cos(a + 0.05), 15.5 * km * math.sin(a + 0.05)))
        ri.append((11 * math.cos(a + 0.1) * rr.uniform(.85, 1.15), 8 * math.sin(a + 0.1) * rr.uniform(.85, 1.15)))
    top = 0.3
    for i in range(n):
        j = (i + 1) % n
        b.face([(0, 0, top), (ri[j][0], ri[j][1], top), (ri[i][0], ri[i][1], top)], "ice_dark" if i % 7 == 0 else "ice_light")
        b.face([(ri[i][0], ri[i][1], top), (ri[j][0], ri[j][1], top), (rm[j][0], rm[j][1], top), (rm[i][0], rm[i][1], top)], "ice_dark" if (i * 5) % 11 == 2 else "ice_light")
        b.face([(rm[i][0], rm[i][1], top), (rm[j][0], rm[j][1], top), (ro[j][0], ro[j][1], top), (ro[i][0], ro[i][1], top)], "ice_dark" if i % 4 == 1 else ("snow_light" if i % 9 == 4 else "ice_light"))
        b.face([(ro[i][0], ro[i][1], top), (ro[j][0], ro[j][1], top), (ro[j][0], ro[j][1], -0.4), (ro[i][0], ro[i][1], -0.4)], "snow_shadow")
        b.face([(ro[j][0], ro[j][1], top + 0.0), (ro[i][0], ro[i][1], top), (ro[i][0] * 1.04, ro[i][1] * 1.04, top - 0.1), (ro[j][0] * 1.04, ro[j][1] * 1.04, top - 0.1)][::-1], "snow_light")
    for i in range(0, n, 3):                                                   # shore ice shards
        x, y = ro[i][0] * 0.98, ro[i][1] * 0.98
        b.cyl(T(x, y, top - 0.05, rx=rr.uniform(-.3, .3), ry=rr.uniform(-.3, .3)), 0.45, 0, rr.uniform(0.9, 1.6), 4, "ice_light", side_mats=["ice_light", "ice_dark"])
    for (x0, y0, x1, y1) in ((-12, -3, 8, 4), (3, 8, 14, -2), (-4, -10, 5, -6)):   # cracks
        L = math.hypot(x1 - x0, y1 - y0); a = math.atan2(y1 - y0, x1 - x0)
        b.box(T((x0 + x1) / 2, (y0 + y1) / 2, top + 0.02, rz=a), L, 0.28, 0.03, "ice_dark", bottom=False)
    return b


def proto_cave():
    b = B("SM8_IceCave")
    n = 12
    prof = [(5.0, 0.0), (4.9, 1.3), (4.2, 2.7), (3.2, 3.9), (2.0, 4.8), (1.0, 5.3)]
    rings = [[Vector((r * math.cos(2 * math.pi * i / n + k * math.pi / n), 0.9 * r * math.sin(2 * math.pi * i / n + k * math.pi / n), z + rng.uniform(-.05, .05)))
              for i in range(n)] for k, (r, z) in enumerate(prof)]
    b.rings(rings, ["ice_light", "ice_dark", "ice_light", "snow_shadow"], apex=Vector((0, 0, 5.6)), apex_mat=["ice_light", "snow_light"], bottom_mat="ice_dark")
    arch_tunnel(b, T(0, -3.2, 0), 2.0, 1.7, 2.2, 5, "ice_dark", "ice_light", "iron", 0.35)
    for x in (-1.6, -0.7, 0.2, 1.1, 1.8):                                           # icicles in the mouth
        b.cyl(T(x, -5.35, 3.6 - abs(x) * 0.22, rx=math.pi), 0.16, 0, 0.9 + 0.15 * (x % 3), 4, "ice_light")
    for (x, y, h, tilt) in ((-2.9, -4.4, 3.2, -0.2), (2.9, -4.4, 3.2, 0.2), (-3.8, -3.3, 2.2, -0.4), (3.8, -3.3, 2.0, 0.4)):
        b.cyl(T(x, y, 0, ry=tilt), 0.55, 0.0, h, 5, "ice_light", side_mats=["ice_light", "ice_dark"])
    for (x, y, s) in ((-1.5, 1.2, 0.7), (1.8, 0.6, 0.9), (0.2, -0.5, 1.1)):
        b.cyl(T(x, y, 4.3 - abs(x) * 0.25, ry=0.2 * x), 0.5 * s, 0, 2.6 * s, 5, "ice_light", side_mats=["ice_light", "ice_dark"], bottom=False)
    b.bip(T(0, -5.6, 0), 1.1, 0.8, 0.4, 0.0, 5, ["snow_light", "snow_shadow"])
    return b


# ---- castle (v2)
def build_castle():
    b = B("Castle_Ice")
    HW = 13.0
    b.cyl(T(0, 0, -9), 21, 17, 9, 8, "stone_mid", rot=math.pi / 8, side_mats=["stone_mid", "stone_light", "snow_shadow", "stone_mid"])
    for k in range(8):                                                       # ice spurs around the plinth
        a = 2 * math.pi * k / 8 + 0.2
        if abs(a - 3.9) < 0.6: continue
        b.cyl(T(19.5 * math.cos(a), 19.5 * math.sin(a), -6, ry=0.0), 1.2, 0, 4.5 + (k % 3), 4, "ice_light", rot=a, side_mats=["ice_light", "ice_dark"])

    def win(x, y, z, a, w=0.8, h=1.5):
        b.box(T(x, y, z - 0.15, rz=a - math.pi / 2), w + 0.35, 0.3, h + 0.3, "ice_light", bottom=False)
        o = 0.12
        b.box(T(x + o * math.cos(a), y + o * math.sin(a), z, rz=a - math.pi / 2), w, 0.25, h, "glow", bottom=False)

    def tower(x, y, z0, r, h1, h2, roof_h, n, wins=3, wz=None, flag=True):
        b.cyl(T(x, y, z0 - 0.6), r * 1.15, r, h1 + 0.6, n, "ice_dark", side_mats=["ice_dark", "ice_light"], rot=0.0)
        b.cyl(T(x, y, z0 + h1), r * 1.2, r * 1.2, 0.55, n, "ice_light")
        b.cyl(T(x, y, z0 + h1 + 0.55), r * 0.95, r * 0.85, h2, n, "ice_dark", side_mats=["ice_dark", "ice_light"])
        zt = z0 + h1 + 0.55 + h2
        b.cyl(T(x, y, zt), r * 1.3, r * 1.3, 0.5, n, "ice_light")
        rin = [[Vector((x + rr_ * math.cos(2 * math.pi * i / n), y + rr_ * math.sin(2 * math.pi * i / n), zz)) for i in range(n)] for (rr_, zz) in ((r * 1.45, zt + 0.35), (r * 0.95, zt + 0.5 + roof_h * 0.25), (r * 0.5, zt + 0.5 + roof_h * 0.62))]
        b.rings(rin, ["ice_light", "ice_dark", "snow_light"], apex=Vector((x, y, zt + 0.5 + roof_h)), apex_mat=["snow_light", "ice_light"])
        b.cyl(T(x, y, zt + 0.5 + roof_h - 0.2), 0.14, 0, 1.4, 4, "glow")
        for k in range(wins):
            a = (k + 0.5) * 2 * math.pi / wins - math.pi * 0.75
            z = (wz or [z0 + h1 * 0.55, z0 + h1 + 0.55 + h2 * 0.45, z0 + h1 + 1.0])[k % 3]
            win(x + r * 0.97 * math.cos(a), y + r * 0.97 * math.sin(a), z, a, 0.7, 1.3)
        return zt

    # courtyard walls (gate gap on -Y) with battlements
    wh = 4.2; th = 1.6
    def wall(xc, yc, ln, ang, merlons=True, skip=()):
        b.box(T(xc, yc, 0, rz=ang), ln, th, wh, "ice_dark", 0.97)
        b.box(T(xc, yc, wh, rz=ang), ln, th * 1.15, 0.25, "ice_light")
        if merlons:
            c = max(int((ln - 1.0) / 2.2), 1)
            for k in range(c):
                u = -ln / 2 + (k + 0.5) * ln / c
                b.box(T(xc + u * math.cos(ang), yc + u * math.sin(ang), wh + 0.25, rz=ang), 1.0, th * 1.1, 0.9, "ice_light", bottom=False)
        if wh > 3:                                                           # arrow slit
            b.box(T(xc + 1.0 * math.cos(ang), yc + 1.0 * math.sin(ang) - 0.0, 1.8, rz=ang), 0.3, th + 0.2, 1.1, "iron", bottom=False)
    sx_ = (HW - 3.3 - 2.5) / 2
    wall(-(2.5 + sx_ + 0.0), -HW, HW - 3.3 - 2.5 + 0.0, 0.0); wall((2.5 + sx_), -HW, HW - 3.3 - 2.5, 0.0)
    wall(0, HW, 2 * HW - 6.6, 0.0); wall(-HW, 0, 2 * HW - 6.6, math.pi / 2); wall(HW, 0, 2 * HW - 6.6, math.pi / 2)
    # gate: flanking square towers, lintel, portcullis
    for gx in (-3.9, 3.9):
        b.box(T(gx, -HW, 0, rz=0), 3.2, 3.0, 8.0, "ice_dark", 0.95)
        b.box(T(gx, -HW, 8.0), 3.8, 3.6, 0.45, "ice_light")
        b.cyl(T(gx, -HW, 8.4), 2.9, 0, 3.4, 4, "ice_light", rot=math.pi / 4, side_mats=["ice_light", "snow_light"])
        win(gx, -HW - 1.52, 5.2, -math.pi / 2, 0.6, 1.2)
    b.box(T(0, -HW, 4.0), 5.0, 1.8, 1.6, "ice_light")
    b.box(T(0, -HW, 5.6), 5.0, 1.9, 0.4, "ice_dark")
    for x in (-1.9, -0.95, 0.0, 0.95, 1.9): b.box(T(x, -HW - 0.4, 0.0), 0.12, 0.14, 4.0, "iron", bottom=False)
    for z in (1.2, 2.6): b.box(T(0, -HW - 0.42, z), 4.4, 0.12, 0.12, "iron", bottom=False)
    win(0, -HW - 1.0, 6.2, -math.pi / 2, 0.9, 1.0)
    b.box(T(0, -HW - 4.0, -0.15), 4.5, 6.0, 0.3, "stone_light", 0.98)                          # gate approach
    # corner towers
    for x in (-HW, HW):
        for y in (-HW, HW):
            tower(x, y, 0, 3.2, 8.0, 3.5, 7.5, 6, 3)
    # keep
    kx, ky = 2, 4
    zt = tower(kx, ky, 0, 5.4, 9.0, 8.0, 12.5, 8, 5, wz=[3.5, 12.5, 7.2, 14.5, 9.0])
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        tx, ty = kx + 5.1 * math.cos(a), ky + 5.1 * math.sin(a)
        zb = 9.8
        b.cyl(T(tx, ty, zb - 2.4), 0.25, 1.35, 2.4, 5, "ice_light")
        b.cyl(T(tx, ty, zb), 1.25, 1.0, 3.2, 5, "ice_dark", side_mats=["ice_dark", "ice_light"])
        rin = [[Vector((tx + rr_ * math.cos(2 * math.pi * i / 5), ty + rr_ * math.sin(2 * math.pi * i / 5), zz)) for i in range(5)] for (rr_, zz) in ((1.55, zb + 3.2), (0.8, zb + 3.9))]
        b.rings(rin, ["ice_light", "ice_dark"], apex=Vector((tx, ty, zb + 6.0)), apex_mat=["snow_light", "ice_light"])
        win(tx + 1.0 * math.cos(a), ty + 1.0 * math.sin(a), zb + 1.0, a, 0.5, 1.1)
    b.cyl(T(kx, ky, zt + 6.0), 0.3, 0, 3.0, 4, "glow")
    b.face([(kx, ky, zt + 6.4 + 2.2), (kx + 0.05, ky, zt + 7.4 + 2.2), (kx + 2.2, ky, zt + 7.0 + 2.2)], "ice_dark")        # pennant
    # side towers
    tower(-6, 6, 0, 3.0, 6.5, 3.0, 6.5, 6, 2)
    tower(9, 8, 0, 2.8, 5.5, 2.5, 6.0, 6, 2)
    # chapel / hall with gable roof
    b.box(T(7, -3.5, 0), 5.0, 4.2, 5.6, "ice_dark", 0.97)
    for sx in (-1, 1): b.face([(7 + sx * 2.5, -3.5 - 2.1, 5.6), (7 + sx * 2.5, -3.5 + 2.1, 5.6), (7 + sx * 2.5, -3.5, 7.6)][::sx], "ice_dark")
    for sy in (-1, 1):
        b.face([(7 - 2.9, -3.5 + sy * 2.3, 5.5), (7 + 2.9, -3.5 + sy * 2.3, 5.5), (7 + 2.9, -3.5, 8.0), (7 - 2.9, -3.5, 8.0)][::sy], "ice_light" if sy > 0 else "snow_light")
    win(7 - 1.2, -3.5 - 2.12, 2.5, -math.pi / 2, 0.7, 1.7); win(7 + 1.2, -3.5 - 2.12, 2.5, -math.pi / 2, 0.7, 1.7)
    return b


def proto_mountain(name, seed):
    r = random.Random(seed); b = B(name)
    n = 9
    prof = [(1.0, 0.0), (0.82, 0.16), (0.6, 0.4), (0.4, 0.65), (0.22, 0.84)]
    rows = [["stone_mid", "stone_light"], ["stone_mid", "stone_light", "snow_shadow"], ["snow_shadow", "stone_light", "snow_light"], ["snow_light", "snow_shadow"]]

    def peak(cx, cy, s, hs, rot0, base=True):
        rings = [[Vector((cx + s * rs * math.cos(2 * math.pi * i / n + 0.4 * k + rot0) * r.uniform(0.85, 1.15),
                          cy + s * rs * math.sin(2 * math.pi * i / n + 0.4 * k + rot0) * r.uniform(0.85, 1.15),
                          hs * (z + r.uniform(-0.035, 0.035)))) for i in range(n)] for k, (rs, z) in enumerate(prof)]
        for k in range(4):
            for i in range(n):
                j = (i + 1) % n
                b.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], rows[k][(i + k) % len(rows[k])])
        apex = Vector((cx + r.uniform(-.05, .05) * s, cy, hs))
        for i in range(n): b.face([rings[4][i], rings[4][(i + 1) % n], apex], "snow_light" if i % 2 else "snow_shadow")
        if base: b.face(list(reversed(rings[0])), "stone_mid")
    peak(0, 0, 1.0, 1.0, 0.0)
    peak(0.62, 0.2, 0.5, 0.58, 1.1, False)
    peak(-0.58, -0.25, 0.46, 0.5, 0.4, False)
    return b


# build prototype meshes ---------------------------------------------------
P = {}
def reg(key, builder, recalc=True):
    P[key] = builder.mesh(recalc)
    return P[key]

_RNG_STATE = rng.getstate()          # prototypes must not shift the scatter random sequence
for v in range(3):
    reg("pine" + ("" if v == 0 else str(v + 1)), proto_pine(False, v)); reg("pine_snow" + ("" if v == 0 else str(v + 1)), proto_pine(True, v))
reg("dead_snow", proto_dead_tree())
for kd in ("round", "tall", "blossom", "bush"): reg("tree_" + kd, proto_round_tree(kd))
P["tree"] = P["tree_round"]
for k in range(3):
    reg(f"rock{k}", proto_rock(f"Rock_{k}", [0.9, 1.3, 1.8][k], k))
    reg(f"rocksnow{k}", proto_rock(f"RockSnow_{k}", [0.9, 1.3, 1.8][k], k, True))
for k in range(3):
    reg(f"pillar{k}", proto_pillar(f"CliffPillar_{k}")); reg(f"pillarsnow{k}", proto_pillar(f"CliffPillarSnow_{k}", True))
for c in ("flower_y", "flower_w", "flower_p", "flower_r"): reg(c, proto_flower(c))
reg("tuft", proto_tuft()); reg("stump", proto_stump()); reg("mushroom", proto_mushroom()); reg("log", proto_log()); reg("slab_arc", proto_slab_arc())
reg("ice_lamp", proto_ice_lamp()); reg("chain_post", proto_chain_post())
reg("barrel", proto_barrel()); reg("crate", proto_crate()); reg("fountain", proto_fountain())
reg("stall0", proto_stall("roof_red")); reg("stall1", proto_stall("plaster")); reg("stall2", proto_stall("roof_purple"))
reg("dock", proto_dock(8.0))
reg("house", proto_house()); reg("windmill", proto_windmill()); reg("fence", proto_fence()); reg("lamp", proto_lamp())
reg("bridge16", proto_bridge("Bridge_S1", 16)); reg("bridge12", proto_bridge("Bridge_S2", 12)); reg("bridge8", proto_bridge("Bridge_S3", 8))
for k in ("pine", "mountain", "snow", "castle"): reg("sign_" + k, proto_sign(k))
reg("snowman", proto_snowman()); reg("igloo", proto_igloo()); reg("crystal", proto_crystal()); reg("sled", proto_sled())
reg("campfire", proto_campfire()); reg("lake", proto_lake()); reg("cave", proto_cave())
reg("castle", build_castle())
for k in range(4): reg(f"mtn{k}", proto_mountain(f"Mountain_{k}", 40 + k))
rng.setstate(_RNG_STATE)

# ------------------------------------------------------------------ water, path
rv = resample(RIVER[:, :2], 3.0)
rv_hw = np.interp(np.hypot(rv[:, 0][:, None] - RIVER[:, 0], rv[:, 1][:, None] - RIVER[:, 1]).argmin(1), range(len(RIVER)), RIVER[:, 2])
ribbon("Water_River", rv, rv_hw + 0.5, RIVER_WL, "water", C_WATER)
sv = resample(STREAM[:, :2], 3.0)
sv_hw = np.interp(np.hypot(sv[:, 0][:, None] - STREAM[:, 0], sv[:, 1][:, None] - STREAM[:, 1]).argmin(1), range(len(STREAM)), STREAM[:, 2])
ribbon("Water_Stream", sv, sv_hw + 0.5, STREAM_WL, "water", C_WATER)
# pool at the foot of the fall (octagon)
pb = B("Water_Pool")
for i in range(8):
    a0, a1 = 2 * math.pi * i / 8, 2 * math.pi * (i + 1) / 8
    pb.face([(305, 238, STREAM_WL), (305 + 8.2 * math.cos(a1), 238 + 7.0 * math.sin(a1), STREAM_WL), (305 + 8.2 * math.cos(a0), 238 + 7.0 * math.sin(a0), STREAM_WL)], "water")
pm = pb.mesh(False)
for p in pm.polygons:
    if p.normal.z < 0: p.flip()
C_WATER.objects.link(bpy.data.objects.new("Water_Pool", pm))

# ice lake (sample #7)
lake_ob = inst(P["lake"], "SM7_IceLake_1", 320, 270, 0, 1.0, z=50.0 + 0.05, coll=C_SNOW)
# channel lake -> fall
ch = np.array([[309, 255.0], [308, 250.0], [306.5, 247.0]])
ribbon("Water_LakeOutlet", ch, np.array([6.0, 6.5, 6.5]), 50.2, "water", C_WATER)

# waterfall: layered strips following the cliff
rows = np.arange(252.0, 238.0, -0.9)
for k in range(4):
    half = 7.0 - 0.9 * k
    b = B(f"Waterfall_L{k}")
    cols = np.linspace(305 - half, 305 + half, 4)
    for r0, r1 in zip(rows[:-1], rows[1:]):
        for c0, c1 in zip(cols[:-1], cols[1:]):
            def z(e, n): return max(Hm(e, n), STREAM_WL) + 0.32 + 0.2 * k
            pts = [(c0, r0, z(c0, r0)), (c1, r0, z(c1, r0)), (c1, r1, z(c1, r1)), (c0, r1, z(c0, r1))]
            ri = int((r0 - 238) / 0.9)
            b.face(pts[::-1], ["water", "ice_light"][(ri + k) % 2 if k % 2 == 0 else (ri + k + 1) % 2])
    me = b.mesh(False)
    for p in me.polygons:
        if p.normal.z < 0: p.flip()
    C_WATER.objects.link(bpy.data.objects.new(f"Waterfall_L{k}", me))

# ---- v4 road: 4-6 m wide, ragged edges, sand base, dirt edge/patches, grass patches, slab clusters, full-width slab stretches
pp = resample(PATH[:, :2], 1.0)
_pr = random.Random(77)
_ice_k = next(k for k in range(len(pp)) if snowy_at(pp[k][0], pp[k][1]) and Hm(pp[k][0], pp[k][1]) >= 20)
S_ICE0 = 0.0     # filled after arc length is known
_sc = np.concatenate([[0], np.cumsum(np.hypot(*np.diff(pp, axis=0).T))])
_iA = int(np.argmin(np.hypot(PATH[:, 0] - WP[7][0], PATH[:, 1] - WP[7][1]))); _iB = int(np.argmin(np.hypot(PATH[:, 0] - WP[8][0], PATH[:, 1] - WP[8][1])))
S_ICE0 = float(_sc[_ice_k])
SLAB_RANGES = [(0.0, 34.0), (float(PCUM[_iA]) - 6.0, float(PCUM[_iB]))]
_nl = np.array([1.0, 0.6, 0.2, -0.2, -0.6, -1.0])
Lv = []                                              # lateral vertex rows
for k in range(len(pp)):
    a_ = pp[max(k - 1, 0)]; c_ = pp[min(k + 1, len(pp) - 1)]
    t_ = np.array([c_[0] - a_[0], c_[1] - a_[1]]); t_ = t_ / max(np.hypot(*t_), 1e-6); nr_ = np.array([-t_[1], t_[0]])
    sk = _sc[k]
    wf = sm((sk - (S_ICE0 - 8.0)) / 16.0)                           # v5: road widens to 5-7 m on the ice stretch
    hl = np.clip(2.5 + 0.35 * math.sin(sk * 0.071 + 1.0) + 0.25 * math.sin(sk * 0.23) + _pr.uniform(-0.12, 0.12) + 0.6 * wf, 2.0, 3.5)
    hr = np.clip(2.5 + 0.35 * math.sin(sk * 0.059 + 3.1) + 0.25 * math.sin(sk * 0.19 + 1.0) + _pr.uniform(-0.12, 0.12) + 0.6 * wf, 2.0, 3.5)
    row = []
    for f in _nl:
        lat = f * (hl if f > 0 else hr) * 1.0
        if abs(f) == 1: lat += _pr.uniform(-0.35, 0.35)
        q = pp[k] + nr_ * lat
        zq = max(Hm(q[0] + dx, q[1] + dy) for dx, dy in ((0, 0), (0.8, 0), (-0.8, 0), (0, 0.8), (0, -0.8))) + (0.2 if abs(f) < 1 else 0.16)
        row.append((q[0], q[1], zq))
    Lv.append(row)
pb_ = B("Path_Surface")
for k in range(len(pp) - 1):
    sm_ = 0.5 * (_sc[k] + _sc[k + 1])
    if math.hypot(pp[k][0] - PLAZA[0], pp[k][1] - PLAZA[1]) < PLAZA_R + 0.4: continue        # plaza paving covers it
    if sm_ >= S_ICE0 + 0.5: continue                                                          # ice stretch is built below
    slab = any(a <= sm_ <= b for a, b in SLAB_RANGES)
    for j in range(5):
        pts = [Lv[k][j], Lv[k][j + 1], Lv[k + 1][j + 1], Lv[k + 1][j]]
        cx_ = sum(v[0] for v in pts) / 4; cy_ = sum(v[1] for v in pts) / 4
        edge = (j == 0 or j == 4)
        v = min(max(0.5 + nz(np.array(cx_ * 0.8), np.array(cy_ * 0.8)) / 3.9, 0), 1)
        sn_ = snowy_at(cx_, cy_)
        if slab:
            m = ["stone_light", "stone_mid", "stone_light", "plaster"][(k // 1 + j * 2 + (k // 2) % 2) % 4] if not edge else ["stone_mid", "stone_light"][(k + j) % 2]
            if edge and v > 0.7: m = "sand"
        else:
            m = "sand"
            if edge and v < 0.42: m = "dirt"                                # worn dirt edge, not a straight line
            elif (not edge) and 0.47 < v < 0.52: m = "dirt"
            if edge and v > 0.6: m = "snow_light" if sn_ else "grass_mid"   # grass (or snow) creeping over the road
            elif (not edge) and v > 0.8: m = "snow_shadow" if sn_ else "grass_light"
            elif (not edge) and v < 0.18: m = "dirt"
        pb_.face(pts[::-1], m)
pm_ = pb_.mesh(False)
for p_ in pm_.polygons:
    if p_.normal.z < 0: p_.flip()
C_PATH.objects.link(bpy.data.objects.new("Path_Surface", pm_))
# ---- ice stretch: stepped pale-blue treads, jagged edge, ice-block rim
ib_ = B("Path_Ice_Steps")
_nk = len(pp)
_bounds = [_ice_k]
while _bounds[-1] < _nk - 1: _bounds.append(min(_bounds[-1] + _pr.randint(3, 5), _nk - 1))
_tz = []
for t in range(len(_bounds) - 1):
    a_, b_ = _bounds[t], _bounds[t + 1]
    zt = max(Lv[k][j][2] for k in range(a_, b_ + 1) for j in range(6)) + 0.04 + (0.14 if t % 2 else 0.0)
    _tz.append(zt)
    poly_ = [Lv[a_][j] for j in range(6)] + [Lv[k][5] for k in range(a_ + 1, b_)] + [Lv[b_][j] for j in range(5, -1, -1)] + [Lv[k][0] for k in range(b_ - 1, a_, -1)]
    mat_ = ["ice_light", "ice_light", "snow_light", "ice_light", "snow_shadow", "ice_light"][_pr.randint(0, 5)]
    ib_.face([(q[0], q[1], zt) for q in poly_], mat_)
    for k in range(a_, b_):                                                                   # side walls
        for j in (0, 5):
            lo = min(Lv[k][j][2], Lv[k + 1][j][2]) - 1.0
            ib_.face([(Lv[k][j][0], Lv[k][j][1], zt), (Lv[k + 1][j][0], Lv[k + 1][j][1], zt), (Lv[k + 1][j][0], Lv[k + 1][j][1], lo), (Lv[k][j][0], Lv[k][j][1], lo)][::(1 if j == 0 else -1)],
                     ["ice_dark", "snow_shadow", "ice_dark", "stone_mid"][(k + j) % 4])
    if t > 0:                                                                                 # riser at the boundary with the previous tread
        top = max(zt, _tz[t - 1])
        for j in range(5):
            lo = min(Lv[a_][j][2], Lv[a_][j + 1][2]) - 1.0
            ib_.face([(Lv[a_][j][0], Lv[a_][j][1], top), (Lv[a_][j + 1][0], Lv[a_][j + 1][1], top), (Lv[a_][j + 1][0], Lv[a_][j + 1][1], lo), (Lv[a_][j][0], Lv[a_][j][1], lo)],
                     ["ice_dark", "snow_shadow", "ice_dark"][(t + j) % 3])
im_ = ib_.mesh(False)
for p_ in im_.polygons:
    if p_.normal.z < 0 and abs(p_.normal.z) > 0.7: p_.flip()
C_PATH.objects.link(bpy.data.objects.new("Path_Ice_Steps", im_))
ch_ = B("Path_Ice_Chunks")
for k in range(_ice_k + 2, _nk - 2, 3):
    for j in (0, 5):
        if _pr.random() < 0.55:
            q = Lv[k][j]; c_ = pp[k]; ou = np.array([q[0] - c_[0], q[1] - c_[1]]); ou = ou / max(np.hypot(*ou), 1e-6)
            r_ = _pr.uniform(0.45, 0.95)
            ch_.bip(T(q[0] + ou[0] * 0.35, q[1] + ou[1] * 0.35, q[2] - 0.35, rz=_pr.uniform(0, 3)), r_, r_ * _pr.uniform(0.6, 1.0), _pr.uniform(0.5, 1.4), 0.0, _pr.choice((5, 6)),
                    ["ice_light", "ice_dark", "snow_light", "ice_light"], jit=0.08)
ch_.obj(C_PATH)
# clustered flagstones set into the road (stone palette)
sb_ = B("Path_Stones"); _s = 14.0
while _s < S_ICE0 - 2:
    k0 = min(int(_s), len(pp) - 1)
    for _m in range(_pr.randint(3, 7)):
        kk = min(max(k0 + _pr.randint(-3, 3), 0), len(pp) - 1)
        cx, cy, _z = Lv[kk][_pr.randint(1, 4)]
        if math.hypot(cx - PLAZA[0], cy - PLAZA[1]) < PLAZA_R + 1.5: continue
        sb_.bip(T(cx + _pr.uniform(-0.4, 0.4), cy + _pr.uniform(-0.4, 0.4), _z + 0.02, rz=_pr.uniform(0, 3)), _pr.uniform(0.35, 0.85), _pr.uniform(0.3, 0.6), 0.1, 0.0, 6,
                ["stone_mid", "stone_light"], jit=0.05)
    _s += _pr.uniform(9, 20)
sb_.obj(C_PATH)

# ------------------------------------------------------------------ placing of fixed objects
HOUSES = [(70, 60), (50, 95), (40, 50), (110, 25), (60, 120)]
BRIDGES = ((95, 88), (128, 48), (330, 205))
MOVED_BLD = []


def bld_ok(e, n, others, pmin=9.5, gap=13.0):
    if not free(e, n, path=pmin, riv=7.0): return False
    if any(math.hypot(e - a, n - b) < 12 for a, b in BRIDGES): return False
    if any(math.hypot(e - a, n - b) < gap for a, b in others): return False
    return slope_at(e, n) <= 0.35


# river bank nearest the plaza -> dock direction
_rd = np.hypot(RIVER[:, 0] - PLAZA[0], RIVER[:, 1] - PLAZA[1]) + np.where(RIVER[:, 0] < 24, 1e3, 0); _rk = int(_rd.argmin())   # bank nearest the village (not on the map edge)
_u = np.array([RIVER[_rk, 0] - PLAZA[0], RIVER[_rk, 1] - PLAZA[1]]); _dist = float(np.hypot(*_u)); _u = _u / _dist
DOCK_ANG = math.atan2(_u[1], _u[0]); DOCK_BANK = _dist - float(RIVER[_rk, 2])      # plaza centre -> bank edge distance
HOUSES = []; VILLAGE_LOG = []
_cand = []
for _r in (15.0, 17.0, 19.0, 21.0, 23.0, 26.0):
    for _a in range(0, 360, 10):
        ar = math.radians(_a)
        if not (9 < PLAZA[0] + _r * math.cos(ar) < 370 and 9 < PLAZA[1] + _r * math.sin(ar) < 370): continue
        _cand.append((_r, ar))
for _k in range(5):
    pick = None
    for (_r, ar) in _cand:
        ee, nn = PLAZA[0] + _r * math.cos(ar), PLAZA[1] + _r * math.sin(ar)
        if bld_ok(ee, nn, HOUSES):
            pick = (ee, nn); break
    if pick is None: break
    HOUSES.append(pick)
    VILLAGE_LOG.append((round(pick[0], 1), round(pick[1], 1)))
while len(HOUSES) < 5:                              # fallback (should not happen)
    HOUSES.append(settle(70, 60, "House", rad=18, maxslope=0.35, path=10.0))
_wm = settle(141, 56, "Windmill", rad=8, maxslope=0.4)
WINDMILL = _wm if bld_ok(_wm[0], _wm[1], [], pmin=11.0, gap=0.0) else _wm
for (e, n) in HOUSES: BLOCK.append((e, n, 7.5))
BLOCK.append((WINDMILL[0], WINDMILL[1], 7.5))
BLOCK.append((PLAZA[0], PLAZA[1], PLAZA_R + 3.5))

n_house = 0
for (e, n) in HOUSES:
    e2, n2 = e, n
    rng.uniform(-0.45, 0.45)
    rz_ = math.atan2(PLAZA[0] - e, -(PLAZA[1] - n)) + rng.uniform(-0.12, 0.12)         # door faces the plaza
    inst(P["house"], f"House_RedRoof_{n_house}", e2, n2, rz_, 1.0, 0.9, coll=C_BLD,
         z=min(Hm(e2 - 3, n2 - 3), Hm(e2 + 3, n2 - 3), Hm(e2, n2)) + 0.9)
    n_house += 1
e2, n2 = WINDMILL
inst(P["windmill"], "Windmill_0", e2, n2, 0.0, 1.0, 0.9, coll=C_BLD)

# bridges
def bridge(key, e, n, ang, wl, name):
    ob = inst(P[key], name, e, n, ang, 1.0, coll=C_BLD, z=wl + 0.72)
def _dir_at(poly, k):
    a = poly[max(k - 2, 0)]; b = poly[min(k + 2, len(poly) - 1)]
    return math.atan2(b[1] - a[1], b[0] - a[0])


BRIDGE_LOG = []
_k1 = int(np.argmin(np.hypot(PATH[:, 0] - 95, PATH[:, 1] - 88)))                 # S1 sits on the road, axis = road tangent
_a1 = _dir_at(PATH, _k1); bridge("bridge16", float(PATH[_k1, 0]), float(PATH[_k1, 1]), _a1, RIVER_WL, "Bridge_S1")
BRIDGE_LOG.append(("Bridge_S1", round(float(PATH[_k1, 0]), 1), round(float(PATH[_k1, 1]), 1), round(math.degrees(_a1), 1), "road tangent"))
for (key, poly, e0, n0, wl, nm) in (("bridge12", RIVER, 128, 48, RIVER_WL, "Bridge_S2"), ("bridge8", STREAM, 330, 205, STREAM_WL, "Bridge_S3")):
    _k = int(np.argmin(np.hypot(poly[:, 0] - e0, poly[:, 1] - n0)))                # no road here: axis perpendicular to the water flow
    _a = _dir_at(poly, _k) + math.pi / 2
    bridge(key, float(poly[_k, 0]), float(poly[_k, 1]), _a, wl, nm)
    BRIDGE_LOG.append((nm, round(float(poly[_k, 0]), 1), round(float(poly[_k, 1]), 1), round(math.degrees(_a), 1), "perpendicular to flow"))

# ---- village props (v3): paved plaza, fountain + statue, market stalls, dock, lamps, barrels, crates
PZ = PLAZA[2] + 0.30
_pb = B("Village_Plaza_Paving")
_rings = [(3.6, 5.6), (5.6, 7.4), (7.4, PLAZA_R)]; _ns = 20
for kr, (r0, r1) in enumerate(_rings):
    for i in range(_ns):
        a0_, a1_ = 2 * math.pi * i / _ns, 2 * math.pi * (i + 1) / _ns
        pts = [(PLAZA[0] + r * math.cos(a), PLAZA[1] + r * math.sin(a), PZ) for r, a in ((r0, a0_), (r0, a1_), (r1, a1_), (r1, a0_))]
        _pb.face(pts[::-1], ["stone_light", "stone_mid", "plaster"][(i + kr) % 3] if kr < 2 else ["stone_mid", "stone_light"][(i + kr) % 2])
_pm = _pb.mesh(False)
for _p in _pm.polygons:
    if _p.normal.z < 0: _p.flip()
C_BLD.objects.link(bpy.data.objects.new("Village_Plaza_Paving", _pm))
inst(P["fountain"], "Village_Fountain", PLAZA[0], PLAZA[1], 0.3, 1.0, z=PZ - 0.02, coll=C_BLD)
# market stalls on the plaza rim, facing the fountain (skip where the road crosses)
_si = 0; _stall_pos = []
for _a in range(20, 360, 20):
    if _si >= 4: break
    ar = math.radians(_a)
    sx, sy = PLAZA[0] + 6.6 * math.cos(ar), PLAZA[1] + 6.6 * math.sin(ar)
    if dpath(sx, sy) < 5.5: continue
    if any(math.hypot(sx - a, sy - b) < 6 for a, b in _stall_pos): continue
    _stall_pos.append((sx, sy))
    inst(P[f"stall{_si % 3}"], f"Market_Stall_{_si}", sx, sy, ar - math.pi / 2, 1.0, z=PZ, coll=C_BLD)
    ax_, ay_ = sx + 1.8 * math.cos(ar + 1.5708), sy + 1.8 * math.sin(ar + 1.5708)
    inst(P["barrel"], f"Barrel_stall{_si}", ax_ + 1.2 * math.cos(ar), ay_ + 1.2 * math.sin(ar), 0, 1.0, z=PZ, coll=C_MISC)
    inst(P["crate"], f"Crate_stall{_si}", sx - 2.2 * math.sin(ar), sy + 2.2 * math.cos(ar), ar, 1.0, z=PZ, coll=C_MISC)
    _si += 1
# lamps around the plaza
for k, _a in enumerate((50, 140, 230, 320)):
    ar = math.radians(_a); lx, ly = PLAZA[0] + (PLAZA_R + 0.6) * math.cos(ar), PLAZA[1] + (PLAZA_R + 0.6) * math.sin(ar)
    if dpath(lx, ly) < 3.0: continue
    inst(P["lamp"], f"Plaza_Lamp_{k}", lx, ly, ar + math.pi, 1.0, z=PZ - 0.1, coll=C_MISC)
# wooden dock at the river bank nearest to the village (river is far from the new start, ~70 m)
_hw0 = float(RIVER[_rk, 2])
dx0, dy0 = RIVER[_rk, 0] - _u[0] * (_hw0 + 1.5), RIVER[_rk, 1] - _u[1] * (_hw0 + 1.5)
inst(P["dock"], "Wooden_Dock", dx0, dy0, DOCK_ANG, 1.0, z=RIVER_WL + 1.15, coll=C_BLD)
for k, off_ in enumerate((2.2, 5.5)):
    bx, by = dx0 + (off_) * _u[0] - 0.9 * _u[1], dy0 + (off_) * _u[1] + 0.9 * _u[0]
    inst(P["barrel" if k == 0 else "crate"], f"Dock_Prop_{k}", bx, by, 0.4, 1.0, z=RIVER_WL + 1.3, coll=C_MISC)
BLOCK.append((dx0 + 4 * _u[0], dy0 + 4 * _u[1], 5.5))

# zone signs
for idx, kind in ((5, "pine"), (8, "mountain"), (12, "snow"), (15, "castle")):
    e, n, z, _ = WP[idx]
    px, py, pz, tg = path_at(float(PCUM[np.abs(PATH[:, 0] - e).argmin() + 0]) if False else min(range(len(PATH)), key=lambda q: math.hypot(PATH[q, 0] - e, PATH[q, 1] - n)) and PCUM[min(range(len(PATH)), key=lambda q: math.hypot(PATH[q, 0] - e, PATH[q, 1] - n))])
    sx, sy = e - tg[1] * -4.0 * -1 + 0, n + tg[0] * -4.0 * -1
    sx, sy = e + (-tg[1]) * 5.5, n + tg[0] * 5.5
    if kind == "castle":          # v2: v1 put it inside the courtyard (hidden by walls) -> beside the approach, outside the gate
        cpx, cpy, cpz, ctg = path_at(float(PCUM[min(range(len(PATH)), key=lambda q: math.hypot(PATH[q, 0] - 331, PATH[q, 1] - 330.5))]) - 9.0)
        sx, sy = cpx + ctg[1] * 4.0, cpy - ctg[0] * 4.0
    inst(P["sign_" + kind], f"Sign_{kind}", sx, sy, 0.0, 1.0, -0.2, coll=C_MISC)
    BLOCK.append((sx, sy, 3))

# lamps along path near start
for s in (28, 44, 60, 76):
    px, py, pz, tg = path_at(s)
    inst(P["lamp"], f"Lamp_{s}", px - tg[1] * 4.2, py + tg[0] * 4.2, 0.0, 1.0, -0.1, coll=C_MISC)
    BLOCK.append((px - tg[1] * 4.2, py + tg[0] * 4.2, 1.5))

# fences along the first path stretch
fence_pos = []
s_end = float(PCUM[np.argmin(np.abs(PATH[:, 0] - 95) + np.abs(PATH[:, 1] - 88))]) - 12
s = 6.0
while s < s_end:
    px, py, pz, tg = path_at(s)
    for side in (1, -1):
        fe, fn = px - tg[1] * 4.4 * side, py + tg[0] * 4.4 * side
        if rng.random() < 0.72 and not blocked(fe, fn, -3) and driver(fe, fn) > 1.5:
            fence_pos.append((fe, fn, math.atan2(tg[1], tg[0])))
    s += 4.0
rng.shuffle(fence_pos)
for k, (fe, fn, ang) in enumerate(fence_pos[:40]):
    inst(P["fence"], f"Fence_{k:02d}", fe, fn, ang, 1.0, -0.05, coll=C_BLD)
    BLOCK.append((fe, fn, 1.2))

# ---- v5 roadside details along the whole road + ice-road furniture
_rd8 = random.Random(88)
DET = {"tuft": 0, "flower": 0, "rock": 0, "stump": 0, "mushroom": 0, "log": 0, "arc": 0, "lamp": 0, "ice_lamp": 0, "chain_post": 0, "chain_link": 0, "ice_rock": 0}


def _near_bridge(e, n, r=11): return any(math.hypot(e - a, n - b) < r for a, b in BRIDGES)


def _det_ok(e, n):
    if math.hypot(e - PLAZA[0], n - PLAZA[1]) < PLAZA_R + 4.5 or _near_bridge(e, n): return False
    return free(e, n, path=3.3, riv=1.6) and slope_at(e, n) < 0.6


for k in range(2, _nk - 2, 1):
    c_ = pp[k]
    for j in (0, 5):
        q = Lv[k][j]; ou = np.array([q[0] - c_[0], q[1] - c_[1]]); ou = ou / max(np.hypot(*ou), 1e-6)
        off = _rd8.uniform(0.7, 3.0); e, n = q[0] + ou[0] * off, q[1] + ou[1] * off
        r_ = _rd8.random(); ice = _sc[k] >= S_ICE0 - 4
        if not _det_ok(e, n): continue
        rz = _rd8.uniform(0, 6.28)
        if ice:
            if r_ < 0.14:
                inst(P[f"rocksnow{_rd8.randint(0, 2)}"], f"Road_IceRock_{DET['ice_rock']}", e, n, rz, _rd8.uniform(0.5, 1.1), -0.1, coll=C_ROCK); DET["ice_rock"] += 1
                BLOCK.append((e, n, 1.0))
            continue
        if r_ < 0.40:
            inst(P["tuft"], f"Road_Tuft_{DET['tuft']}", e, n, rz, _rd8.uniform(0.8, 1.4), 0.0, coll=C_PLANT); DET["tuft"] += 1
        elif r_ < 0.52:
            inst(P[_rd8.choice(["flower_y", "flower_w", "flower_p", "flower_r"])], f"Road_Flowers_{DET['flower']}", e, n, rz, _rd8.uniform(0.9, 1.4), 0.0, coll=C_PLANT); DET["flower"] += 1
        elif r_ < 0.64:
            inst(P[f"rock{_rd8.randint(0, 2)}"], f"Road_Rock_{DET['rock']}", e, n, rz, _rd8.uniform(0.4, 0.95), -0.08, coll=C_ROCK); DET["rock"] += 1
            BLOCK.append((e, n, 1.0))
        elif r_ < 0.68:
            inst(P["stump"], f"Road_Stump_{DET['stump']}", e, n, rz, _rd8.uniform(0.8, 1.2), -0.05, coll=C_PLANT); DET["stump"] += 1
            BLOCK.append((e, n, 1.0))
        elif r_ < 0.75:
            inst(P["mushroom"], f"Road_Mushrooms_{DET['mushroom']}", e, n, rz, _rd8.uniform(0.9, 1.3), 0.0, coll=C_PLANT); DET["mushroom"] += 1
        elif r_ < 0.77:
            inst(P["log"], f"Road_Log_{DET['log']}", e, n, rz, 1.0, -0.05, coll=C_PLANT); DET["log"] += 1
            BLOCK.append((e, n, 1.2))
        elif r_ < 0.80:
            inst(P["slab_arc"], f"Road_SlabArc_{DET['arc']}", e - ou[0] * 0.2, n - ou[1] * 0.2, math.atan2(-ou[1], -ou[0]), 1.0, 0.0, coll=C_PLANT); DET["arc"] += 1
# lamps at intervals (grass road) and stone ice-lamps (ice road), alternating sides
_s = 95.0; _side = 0
while _s < _sc[-1] - 2:
    k = min(int(_s), _nk - 1); c_ = pp[k]; q = Lv[k][0 if _side == 0 else 5]
    ou = np.array([q[0] - c_[0], q[1] - c_[1]]); ou = ou / max(np.hypot(*ou), 1e-6)
    e, n = q[0] + ou[0] * 1.3, q[1] + ou[1] * 1.3
    if _det_ok(e, n):
        if _s >= S_ICE0:
            inst(P["ice_lamp"], f"Ice_Lamp_{DET['ice_lamp']}", e, n, 0.0, 1.0, -0.1, coll=C_MISC); DET["ice_lamp"] += 1; BLOCK.append((e, n, 1.0))
        elif _s < S_ICE0 - 10:
            inst(P["lamp"], f"Road_Lamp_{DET['lamp']}", e, n, math.atan2(c_[1] - n, c_[0] - e), 1.0, -0.1, coll=C_MISC); DET["lamp"] += 1; BLOCK.append((e, n, 1.2))
    _s += 46.0 if _s < S_ICE0 else 22.0; _side = 1 - _side
# iron chains along the cliff side of the ice road
for j in (0, 5):
    prev = None
    for k in range(_ice_k + 2, _nk - 3, 6):
        c_ = pp[k]; q = Lv[k][j]; ou = np.array([q[0] - c_[0], q[1] - c_[1]]); ou = ou / max(np.hypot(*ou), 1e-6)
        e, n = q[0] + ou[0] * 0.8, q[1] + ou[1] * 0.8
        drop = Hm(q[0], q[1]) - Hm(q[0] + ou[0] * 4.5, q[1] + ou[1] * 4.5)
        if drop < 1.6 or in_lake(e, n, 1) or r_castle(e, n) < 30 or driver(e, n) < 1 or dstream(e, n) < 1:
            prev = None; continue
        z0 = Hm(e, n)
        inst(P["chain_post"], f"Chain_Post_{DET['chain_post']}", e, n, 0.0, 1.0, -0.05, coll=C_MISC); DET["chain_post"] += 1
        top = (e, n, z0 + 1.0)
        if prev is not None and math.hypot(top[0] - prev[0], top[1] - prev[1]) < 8.0:
            cb = B(f"Chain_{DET['chain_link']}")
            nseg = 5; L0 = np.array(prev); L1 = np.array(top)
            pts_ = [L0 + (L1 - L0) * (i / nseg) - np.array([0, 0, 0.45 * math.sin(math.pi * i / nseg)]) for i in range(nseg + 1)]
            for i in range(nseg):
                d_ = pts_[i + 1] - pts_[i]; ln = float(np.linalg.norm(d_)); mid = (pts_[i] + pts_[i + 1]) / 2
                cb.box(T(mid[0], mid[1], mid[2] - 0.04, rz=math.atan2(d_[1], d_[0]), ry=-math.atan2(d_[2], math.hypot(d_[0], d_[1]))), ln, 0.07, 0.08, "iron")
            cb.obj(C_MISC); DET["chain_link"] += 1
        prev = top
# thin mist banks beside the ice road
_ml = 0
for k in range(_ice_k + 10, _nk - 10, 28):
    for off in (1, -1):
        q = Lv[k][0 if off > 0 else 5]; c_ = pp[k]; ou = np.array([q[0] - c_[0], q[1] - c_[1]]); ou = ou / max(np.hypot(*ou), 1e-6)
        if _rd8.random() < 0.55:
            cb = B(f"Mist_{_ml}"); e, n = q[0] + ou[0] * _rd8.uniform(3, 7), q[1] + ou[1] * _rd8.uniform(3, 7)
            for (dx, dy, rx_, ry_) in ((0, 0, 4.5, 2.6), (3.0, 1.0, 3.2, 2.0), (-2.5, -1.2, 3.0, 1.8)):
                cb.face([(e + dx + rx_ * math.cos(a), n + dy + ry_ * math.sin(a), Hm(e, n) + 1.5 + 0.2 * (dx > 0)) for a in np.linspace(0, 2 * math.pi, 11)[:-1]], "cloud")
            me = cb.mesh(False)
            for p_ in me.polygons:
                if p_.normal.z < 0: p_.flip()
            ob = bpy.data.objects.new(f"Mist_{_ml}", me); C_MISC.objects.link(ob); ob.visible_shadow = False; _ml += 1
DET["mist"] = _ml
print("ROAD_DETAIL", DET, "ice_start_s", round(S_ICE0, 1))

# ------------------------------------------------------------------ sample objects in the world
SAMPLE_LOG = []
def put(key, nm, pts, sc=1.0, coll=C_SNOW, rotz=None, maxs=0.45, zoff=-0.05):
    for k, (e, n) in enumerate(pts):
        e2, n2 = settle(e, n, nm, rad=14, maxslope=maxs, path=5.2)
        inst(P[key], f"{nm}_{k + 1}", e2, n2, rng.uniform(-0.6, 0.6) if rotz is None else rotz, sc, zoff, coll=coll)
        SAMPLE_LOG.append((nm, k + 1, round(e2, 1), round(n2, 1)))

BLOCK_BACKUP = list(BLOCK)
BLOCK[:] = [b_ for b_ in BLOCK if not (b_[2] == 5.5)]       # sample keep-outs removed while placing the samples themselves
put("snowman", "SM1_Snowman", [(285, 251), (300, 285), (335, 262), (270, 275), (345, 300)])
put("igloo", "SM2_Igloo", [(280, 290), (305, 310), (355, 285)], rotz=0.0)
put("sled", "SM5_Sled", [(290, 268), (326, 251), (350, 320)])
put("campfire", "SM6_Campfire", [(283, 287), (308, 307), (358, 282), (287, 262)])
put("cave", "SM8_IceCave", [(355, 255), (313, 318)], maxs=0.6)
BLOCK[:] = BLOCK_BACKUP

# ------------------------------------------------------------------ scatter helpers
def scatter(count, key_fn, name, coll, region, tries=60, sc=(0.8, 1.3), zoff=-0.25, path=4.8, extra=0.0, maxs=0.55, sink=True, rot=True, out=None):
    placed = 0; k = 0
    while placed < count and k < count * tries:
        k += 1
        e, n = region()
        if not free(e, n, path=path, riv=3.0, extra=extra): continue
        if slope_at(e, n) > maxs: continue
        key = key_fn(e, n)
        if key is None: continue
        s = rng.uniform(*sc)
        inst(P[key], f"{name}_{placed:03d}", e, n, rng.uniform(0, 6.28) if rot else 0, s, zoff, coll=coll)
        BLOCK.append((e, n, 1.8 * s))
        placed += 1
        if out is not None: out.append((e, n))
    return placed

U = rng.uniform
vrng = random.Random(77)            # variant picker (separate stream, keeps scatter positions stable)
def pick(keys, w):
    return vrng.choices(keys, w)[0]
CLEAR = [(125, 165, 15), (170, 200, 12)]
def not_clear(e, n): return not any(math.hypot(e - a, n - b) < r for a, b, r in CLEAR)

# ---- green pines (300)
def green_ok(e, n): return (not snowy_at(e, n)) and Hm(e, n) < 40 and not_clear(e, n)
cnt = {}
cnt["pine_B"] = scatter(180, lambda e, n: pick(["pine", "pine2", "pine3"], [5, 3, 2]) if green_ok(e, n) else None, "Pine", C_PLANT, lambda: (U(88, 235), U(98, 238)), sc=(0.85, 1.35))
cnt["pine_A"] = scatter(45, lambda e, n: pick(["pine", "pine2", "pine3"], [5, 3, 2]) if green_ok(e, n) else None, "Pine_A", C_PLANT, lambda: (U(4, 150), U(4, 128)), sc=(0.75, 1.2), path=5.0)
cnt["pine_hill"] = scatter(45, lambda e, n: pick(["pine", "pine2", "pine3"], [5, 3, 2]) if (green_ok(e, n) and Hm(e, n) > 5) else None, "Pine_Hill", C_PLANT,
                           lambda: (U(4, 300), U(4, 300)), sc=(0.8, 1.4))
def edge_pt():
    s = rng.randrange(4); t = U(4, 376); d = U(3, 13)
    return [(t, d), (t, 380 - d), (d, t), (380 - d, t)][s]
cnt["pine_edge"] = scatter(30, lambda e, n: pick(["pine", "pine2", "pine3"], [5, 3, 2]) if (green_ok(e, n) and Hm(e, n) > 0.8) else None, "Pine_Edge", C_PLANT, edge_pt, sc=(0.9, 1.5), extra=-1.0)
# ---- round trees (40)
cnt["tree"] = scatter(40, lambda e, n: pick(["tree_round", "tree_tall", "tree_blossom", "tree_bush"], [4, 3, 2, 3]) if (not snowy_at(e, n) and Hm(e, n) < 8) else None, "Tree_Round", C_PLANT,
                      lambda: (U(4, 150), U(4, 130)), sc=(0.8, 1.25), path=5.0, extra=2.0)
# ---- snow pines (140)
def snow_ok(e, n): return snowy_at(e, n) and Hm(e, n) < 70 and r_castle(e, n) > 26 and e > 250
cnt["pine_snow"] = scatter(140, lambda e, n: pick(["pine_snow", "pine_snow2", "pine_snow3", "dead_snow"], [5, 3, 2, 1]) if snow_ok(e, n) else None, "PineSnow", C_SNOW,
                           lambda: (U(250, 378), U(60, 375) if rng.random() < 0.8 else U(2, 60)), sc=(0.8, 1.3), maxs=0.65)
# ---- crystals (25)  ring around lake + castle base
crys = []
for k in range(17):
    a = 2 * math.pi * k / 17 + rng.uniform(-0.15, 0.15); rr = rng.uniform(1.12, 1.3)
    crys.append((LAKE[0] + LAKE[2] * rr * math.cos(a), LAKE[1] + LAKE[3] * rr * math.sin(a)))
for k in range(8):
    a = 2 * math.pi * k / 8 + 0.3; rr = rng.uniform(24, 29)
    if abs(a - 3.9) < 0.5: continue
    crys.append((340 + rr * math.cos(a), 340 + rr * math.sin(a)))
cn = 0
for (e, n) in crys:
    if not (5 < e < 375 and 5 < n < 375) or dpath(e, n) < 4 or (240 < n < 252 and 292 < e < 318): continue
    inst(P["crystal"], f"IceCrystal_{cn:02d}", e, n, rng.uniform(0, 6.28), rng.uniform(0.55, 1.05), -0.1, coll=C_SNOW); cn += 1
extra = 0
while cn < 25 and extra < 400:
    extra += 1
    e, n = U(255, 378), U(255, 330)
    if snowy_at(e, n) and free(e, n, extra=1.0) and in_lake(e, n, 12) is False and slope_at(e, n) < 0.5 and r_castle(e, n) > 26:
        inst(P["crystal"], f"IceCrystal_{cn:02d}", e, n, rng.uniform(0, 6.28), rng.uniform(0.55, 1.0), -0.1, coll=C_SNOW); cn += 1
cnt["crystal"] = cn
# ---- rocks (120)
def rock_key(e, n):
    z = Hm(e, n); sn = snowy_at(e, n)
    return ("rocksnow" if sn else "rock") + str(rng.randrange(3))
rk = []
for k in range(55):
    for _ in range(20):
        t = rng.randrange(len(RIVER) - 1)
        e, n = RIVER[t, 0] + U(-1, 1) * 14, RIVER[t, 1] + U(-1, 1) * 14
        if 0.5 < driver(e, n) < 3.5 and 3 < e < 377 and 3 < n < 377 and dpath(e, n) > 4 and not blocked(e, n, -2):
            inst(P["rock" + str(rng.randrange(3))], f"Rock_Bank_{k:02d}", e, n, U(0, 6.28), U(0.8, 1.6), -0.12, coll=C_ROCK)
            rk.append(1); break
cnt["rock_bank"] = len(rk)
cnt["rock"] = scatter(40, lambda e, n: rock_key(e, n) if (not snowy_at(e, n)) else None, "Rock", C_ROCK,
                      lambda: (U(4, 376), U(4, 376)), sc=(0.7, 1.7), zoff=-0.12, maxs=0.9)
cnt["rock_snow"] = scatter(25, lambda e, n: rock_key(e, n) if snowy_at(e, n) else None, "RockSnow", C_ROCK,
                           lambda: (U(255, 376), U(135, 376)), sc=(0.8, 1.8), zoff=-0.12, maxs=0.9)
# ---- cliff pillars (25) on steep ground
pillars = []
tries = 0
while len(pillars) < 25 and tries < 20000:
    tries += 1
    e, n = U(178, 378), U(140, 335)
    if slope_at(e, n) < 0.85 or not free(e, n, path=5.0, riv=0, extra=0.5): continue
    if any(math.hypot(e - a, n - b) < 14 for a, b in pillars): continue
    sn = snowy_at(e, n)
    inst(P[("pillarsnow" if sn else "pillar") + str(rng.randrange(3))], f"CliffPillar_{len(pillars):02d}", e, n, U(0, 6.28), U(1.0, 1.8), -0.5, coll=C_ROCK)
    pillars.append((e, n))
cnt["pillar"] = len(pillars)
# ---- flowers (60)
FL = ["flower_y", "flower_w", "flower_p", "flower_r"]
fl = 0
def put_flower(e, n):
    global fl
    if dpath(e, n) < 3.4 or driver(e, n) < 1.5 or blocked(e, n, -3): return False
    inst(P[FL[fl % 4]], f"Flowers_{fl:02d}", e, n, U(0, 6.28), U(1.0, 1.7), -0.02, coll=C_PLANT); fl += 1; return True
t = 0
while fl < 24 and t < 2000:
    t += 1; a = U(0, 6.28); r = U(5, 48); put_flower(25 + r * math.cos(a), 25 + r * math.sin(a))
t = 0
while fl < 44 and t < 3000:
    t += 1; s = U(0, float(PCUM[np.argmin(np.abs(PATH[:, 0] - 112) + np.abs(PATH[:, 1] - 115))])); px, py, pz, tg = path_at(s)
    sd = rng.choice((-1, 1)) * U(3.5, 9); put_flower(px - tg[1] * sd, py + tg[0] * sd)
t = 0
while fl < 60 and t < 4000:
    t += 1; k = rng.randrange(len(RIVER)); e, n = RIVER[k, 0] + U(-14, 14), RIVER[k, 1] + U(-14, 14)
    if 2 < driver(e, n) < 12 and 3 < e < 150 and 3 < n < 128: put_flower(e, n)
cnt["flowers"] = fl

# ---- backdrop mountains (18)
mt = []
north_x = np.linspace(14, 292, 11)
for k, e in enumerate(north_x): mt.append((e + U(-6, 6), U(338, 372), U(34, 56), U(30, 44)))
for k, n in enumerate(np.linspace(150, 320, 6)): mt.append((U(8, 24), n + U(-8, 8), U(30, 48), U(26, 38)))
mt.append((374, 372, 40, 20)); mt.append((296, 374, 46, 24))
rng.shuffle(mt)
mt = [(e, n, h * rng.uniform(0.75, 1.25), r * rng.uniform(0.75, 1.15)) for (e, n, h, r) in mt]
for k, (e, n, h, r) in enumerate(mt):
    ob = inst(P[f"mtn{k % 4}"], f"Mountain_{k:02d}", e, n, U(0, 6.28), 1.0, -4.0, coll=C_MTN)
    ob.scale = (r, r, h)
cnt["mountains"] = len(mt)
# ---- castle
inst(P["castle"], "Castle_Ice_K", CASTLE[0], CASTLE[1], -math.pi / 4, 1.0, 0, coll=C_CASTLE, z=80.0)
# ---- fog planes (clouds)
cl = 0
for (e, n) in ((215, 195), (262, 218), (300, 150), (345, 150), (268, 168), (200, 168), (365, 175), (320, 120)):
    b = B(f"Cloud_{cl}")
    pts = [(U(10, 30) * math.cos(a), U(6, 17) * math.sin(a), U(-.4, .4)) for a in np.linspace(0, 2 * math.pi, 11)[:-1]]
    b.face(pts, "cloud")
    me = b.mesh(False)
    for p in me.polygons:
        if p.normal.z < 0: p.flip()
    ob = bpy.data.objects.new(f"Cloud_{cl}", me); C_MISC.objects.link(ob)
    ob.location = (e, n, Hm(e, n) + 2.8); ob.rotation_euler.z = U(0, 3)
    ob.visible_shadow = False
    cl += 1
cnt["cloud"] = cl

# ------------------------------------------------------------------ top-view labels
mk_mat("label_plate", "#EEF3F8", emis=1.0)


def label(txt, e, n, size=9, z=160, rot=0.0, hexc=None, plate=True):
    """v2: dark text on a light plate so it stays readable over mountains / snow / castle"""
    cu = bpy.data.curves.new("L_" + txt, 'FONT'); cu.body = txt; cu.size = size
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'; cu.extrude = 0.0
    ob = bpy.data.objects.new("Label_" + txt[:20], cu); C_LABEL.objects.link(ob)
    cu.materials.append(MATS["label"])
    ob.location = (e, n, z); ob.rotation_euler.z = rot; ob.visible_shadow = False
    if plate:
        bpy.context.view_layer.update()
        w = ob.dimensions.x / 2 + size * 0.45; h = size * 0.8
        pb = B("LabelPlate_" + txt[:16])
        c = math.cos(rot); s = math.sin(rot)
        pts = [(-w, -h), (w, -h), (w, h), (-w, h)]
        pb.face([(e + x * c - y * s, n + x * s + y * c, z - 2) for (x, y) in pts], "label_plate")
        pm = pb.mesh(False)
        for p in pm.polygons:
            if p.normal.z < 0: p.flip()
        po = bpy.data.objects.new("LabelPlate_" + txt[:16], pm); C_LABEL.objects.link(po); po.visible_shadow = False
    return ob


label("A  MEADOW", 80, 12, 8); label("B  PINE VALLEY", 160, 128, 8, rot=0.0)
label("C  CLIFF ASCENT", 218, 168, 7.5); label("D1  LOW SNOW + FALLS", 335, 175, 8)
label("D2  ICE LAKE PLATEAU", 322, 308, 7); label("D3  ICE CASTLE", 282, 361, 9)
label("RIVER", 50, 100, 5, rot=0.0)
# north arrow + scale bar
ar = B("NorthArrow"); ar.face([(-5, 0, 0), (5, 0, 0), (0, 22, 0)][::-1], "label"); ar.face([(-5, 0, 0), (0, 4, 0), (0, 22, 0)], "stone_light")
aob = ar.obj(C_LABEL, (28, 335, 160)); aob.visible_shadow = False
label("N", 28, 330, 11)
sb = B("ScaleBar"); sb.face([(0, -2, 0), (100, -2, 0), (100, 2, 0), (0, 2, 0)], "label"); sob = sb.obj(C_LABEL, (240, 15, 160)); sob.visible_shadow = False
label("100 m", 290, 24, 8)

# ------------------------------------------------------------------ stats
def count_tris(coll_filter=None):
    tot = 0; per = {}
    for ob in scene.objects:
        if ob.type != 'MESH': continue
        if ob.users_collection[0] == C_LABEL: continue
        t = tris_of(ob.data)
        tot += t; per[ob.users_collection[0].name] = per.get(ob.users_collection[0].name, 0) + t
    return tot, per


# ------------------------------------------------------------------ sample sheet scene (v2: 16 slots)
sheet = bpy.data.scenes.new("ObjectsSheet")
SC_COL = sheet.collection
items = [("SM1 Snowman", ["snowman"], 4.6), ("SM2 Igloo", ["igloo"], 6.5), ("SM3 Ice crystal", ["crystal"], 6.5), ("SM4 Snow pine", ["pine_snow"], 7.5),
         ("SM5 Sled", ["sled"], 4.0), ("SM6 Campfire", ["campfire"], 4.0), ("SM7 Ice lake", ["lake"], 8.0), ("SM8 Ice cave", ["cave"], 7.0),
         ("House", ["house"], 7.5), ("Windmill", ["windmill"], 8.0), ("Ice castle", ["castle"], 8.2), ("Bridge", ["bridge8"], 8.0),
         ("Pines (3 variants)", ["pine", "pine2", "pine3"], 4.0), ("Trees (4 variants)", ["tree_round", "tree_tall", "tree_blossom", "tree_bush"], 3.6),
         ("Snow pines + bare tree", ["pine_snow2", "pine_snow3", "dead_snow"], 4.0), ("Fence, lamp, sign", ["fence", "lamp", "sign_castle"], 4.0)]
ONLY = [a.split("=")[1] for a in sys.argv if a.startswith("--only=")]     # debug: render a closer sheet of chosen prototype keys
if ONLY:
    items = [(kk, [kk], 9.0) for kk in ONLY[0].split(",")]
COLS_N = 4 if not ONLY else 2
for k, (nm, keys, tgt) in enumerate(items):
    ci, ri = k % COLS_N, k // COLS_N
    cx, cy = (ci - (COLS_N - 1) / 2) * 12.5, ((len(items) - 1) // COLS_N / 2 - ri) * 12.5
    for m, key in enumerate(keys):
        vs = np.array([v.co[:] for v in P[key].vertices]); dim = vs.max(0) - vs.min(0)
        sc_ = tgt / max(dim)
        ob = bpy.data.objects.new("Sheet_" + key, P[key]); SC_COL.objects.link(ob)
        ob.location = (cx + (m - (len(keys) - 1) / 2) * (tgt * 1.05 if len(keys) > 1 else 0), cy, 0.0)
        ob.scale = (sc_, sc_, sc_)
        if key == "lake": ob.location.z = 0.0
    cu = bpy.data.curves.new("T_" + nm, 'FONT'); cu.body = nm; cu.size = 0.8; cu.align_x = 'CENTER'
    cu.materials.append(MATS["label"])
    to = bpy.data.objects.new("T_" + nm, cu); SC_COL.objects.link(to)
    to.location = (cx, cy - 5.0, 0.02); to.visible_shadow = False
fb = B("SheetFloor"); fb.face([(-60, -60, -0.02), (60, -60, -0.02), (60, 60, -0.02), (-60, 60, -0.02)][::-1], "sheet_floor")
SC_COL.objects.link(bpy.data.objects.new("SheetFloor", fb.mesh(False)))
for p in SC_COL.objects["SheetFloor"].data.polygons:
    if p.normal.z < 0: p.flip()

# ------------------------------------------------------------------ world / light / camera / render
def make_world(name, sky=True):
    w = bpy.data.worlds.new(name); w.use_nodes = True
    nt = w.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld"); bg = nt.nodes.new("ShaderNodeBackground")
    if sky:
        tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        nt.links.new(tc.outputs["Window"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
        ramp.color_ramp.elements[0].color = lin("#D5BFC3"); ramp.color_ramp.elements[1].color = lin("#B6C4DE")
        ramp.color_ramp.elements[0].position = 0.55; ramp.color_ramp.elements[1].position = 1.0
        nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    else:
        bg.inputs["Color"].default_value = lin("#B6C4DE")
    bg.inputs["Strength"].default_value = 1.0
    nt.links.new(bg.outputs[0], out.inputs[0])
    return w


W_SKY = make_world("World_Sky"); W_TOP = make_world("World_Top", sky=False)
W_SHEET = make_world("World_Sheet", sky=False)
W_SHEET.node_tree.nodes["Background"].inputs["Color"].default_value = lin("#DAD3C6")


def add_sun(sc, energy, color, direction, angle=5):
    d = bpy.data.lights.new("Sun_" + sc.name, 'SUN'); d.energy = energy; d.color = color; d.angle = math.radians(angle)
    ob = bpy.data.objects.new("Sun_" + sc.name, d); sc.collection.objects.link(ob)
    ob.rotation_euler = Vector(direction).to_track_quat('-Z', 'Y').to_euler()
    return ob


sun = add_sun(scene, 3.6, (1.0, 0.84, 0.66), (0.55, 0.45, -0.62), 5)        # from WSW-ish, low, casting toward NE
sun_s = add_sun(sheet, 3.0, (1.0, 0.9, 0.78), (0.5, 0.6, -0.7), 6)
cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam)
cam_s = bpy.data.objects.new("CamSheet", cam_data); sheet.collection.objects.link(cam_s)


def setup(sc, w, x, y, pct=100):
    sc.world = w; sc.render.resolution_x = x; sc.render.resolution_y = y; sc.render.resolution_percentage = pct
    try: sc.render.engine = 'BLENDER_EEVEE'
    except Exception: sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.view_settings.view_transform = 'Standard'
    try: sc.eevee.taa_render_samples = 24 if LOW else 64
    except Exception: pass
    try: sc.eevee.use_raytracing = False
    except Exception: pass
    sc.render.image_settings.file_format = 'PNG'


CAM_POS = (180, -330, 250)   # spec: (110,-140,190)->(230,230,25) cannot show Start + castle in 16:9; pulled back
CAM_TGT = (195, 200, 30)
pct = 40 if LOW else 100
tot, per = count_tris()
print("TRIS_TOTAL", tot, per)
print("COUNTS", cnt)
print("MOVED", MOVED)
print("BRIDGES", BRIDGE_LOG)
print("VILLAGE plaza", PLAZA, "houses", VILLAGE_LOG, "dock", round(dx0, 1), round(dy0, 1))
print("BLD_POS", HOUSES, WINDMILL)
print("PROTO_TRIS", {k: tris_of(m) for k, m in P.items()})
print("OBJECT_COUNT", len(scene.objects))

if not NORENDER and not ONLY:
    # --- top
    setup(scene, W_TOP, 2048, 2048, pct); scene.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 400; cam.data.clip_end = 2000; cam.data.clip_start = 1
    cam.location = (190, 190, 500); cam.rotation_euler = (0, 0, 0)
    C_LABEL.hide_render = False
    scene.render.filepath = os.path.join(OUT, "preview_top.png"); bpy.ops.render.render(write_still=True)
    # --- perspective
    setup(scene, W_SKY, 2560, 1440, pct)
    C_LABEL.hide_render = True
    cam.data.type = 'PERSP'; cam.data.lens = 38; cam.location = CAM_POS
    cam.rotation_euler = (Vector(CAM_TGT) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(OUT, "preview.png"); bpy.ops.render.render(write_still=True)
if not NORENDER:
    # --- objects sheet
    setup(sheet, W_SHEET, 2400, 2400, pct); sheet.camera = cam_s
    cam_s.data = bpy.data.cameras.new("CamSheetData"); cam_s.data.type = 'ORTHO'; cam_s.data.ortho_scale = 44 if not ONLY else 26; cam_s.data.clip_end = 400
    cam_s.location = (0, -55, 60); cam_s.rotation_euler = (Vector((0, 0, 2)) - cam_s.location).to_track_quat('-Z', 'Y').to_euler()
    sheet.render.filepath = os.path.join(OUT, "preview_objects.png") if not ONLY else os.environ.get("DBG_OUT", os.path.join(OUT, "debug_only.png"))
    bpy.ops.render.render(write_still=True, scene=sheet.name)

if ONLY: sys.exit(0)
# restore main scene camera for saving
cam.data.type = 'PERSP'; cam.data.lens = 35
C_LABEL.hide_render = True
bpy.context.window.scene = scene if bpy.context.window else None
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "world_map.blend"))
print("DONE")
