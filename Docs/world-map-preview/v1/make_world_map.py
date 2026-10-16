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
    "wood_light": "#A1773D", "wood_dark": "#715332", "dirt": "#8A5A32",
    "stone_light": "#ADABB7", "stone_mid": "#958A80", "plaster": "#D8CCB0",
    "roof_purple": "#5E5878", "roof_red": "#B5523B", "iron": "#3A3A3A",
    "glow": "#F5C25A", "ember": "#C8483A", "water": "#1594BB",
    "flower_y": "#F5C25A", "flower_w": "#D8CCB0", "flower_p": "#5E5878", "flower_r": "#C8483A",
    # TEMPORARY snow / ice colours (decisions.md #3, not in style.md palette yet)
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
mk_mat("marker_white", "#EEF3F8", emis=1.2)          # waypoint discs / guide line (preview helper)
mk_mat("marker_green", "#8DB04A", emis=1.0)          # start marker
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
NC = 152                   # cells per side -> 2.5 m
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
    if i < 7:                                            # gentle meander on the meadow / valley road
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
    # path corridor (forces waypoint heights, carves ramps through cliffs)
    d, ext = poly_query(E, N, PATH)
    d = d.reshape(E.shape); pz = ext[:, 0].reshape(E.shape)
    h = lerp(h, pz, 1 - sm((d - 3.0) / 6.0))
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


def snowy_at(e, n, z=None):
    z = Hm(e, n) if z is None else z
    return (z >= 20 and e + 0.25 * n > 300) or z >= 58


LAKE = (320, 270, 31, 23)
def in_lake(e, n, pad=0.0):
    return ((e - LAKE[0]) / (LAKE[2] + pad)) ** 2 + ((n - LAKE[1]) / (LAKE[3] + pad)) ** 2 < 1
CASTLE = (340, 340)
def r_castle(e, n): return math.hypot(e - CASTLE[0], n - CASTLE[1])

BLOCK = []                                  # (e, n, radius) keep-out circles for scattering
def blocked(e, n, extra=0.0):
    return any(math.hypot(e - a, n - b) < r + extra for a, b, r in BLOCK)


def free(e, n, path=3.5, riv=3.0, extra=0.0):
    if not (3 < e < MAP - 3 and 3 < n < MAP - 3): return False
    if dpath(e, n) < path: return False
    if driver(e, n) < riv or dstream(e, n) < riv: return False
    if in_lake(e, n, 3) or r_castle(e, n) < 24: return False
    if 240 < n < 252 and 292 < e < 318: return False        # waterfall channel
    return not blocked(e, n, extra)


MOVED = []
def settle(e, n, name, rad=14, maxslope=0.30, path=4.0, extra=0.0):
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
        m = np.full(len(E_), TI["grass_mid"])
        regA = (E_ < 150) & (N_ < 130)
        m = np.where(regA, np.select([R < 0.5, R < 0.8], [TI["grass_light"], TI["leaf_light"]], TI["grass_mid"]), m)
        regO = ~regA
        m = np.where(regO, np.select([R < 0.62, R < 0.86, R < 0.95], [TI["grass_mid"], TI["grass_light"], TI["leaf_light"]], TI["pine_dark"]), m)
        m = np.where((Z_ > 30) & (R < 0.30) & ~regA, TI["stone_mid"], m)
        m = np.where(S > 0.55, np.where(R < 0.55, TI["stone_mid"], TI["grass_mid"]), m)
        m = np.where(S > 0.95, np.where(R < 0.5, TI["stone_mid"], TI["stone_light"]), m)
        m = np.where(Z_ < 0.05, np.where(R < 0.6, TI["dirt"], TI["stone_mid"]), m)         # river bed
        ds, ex_ = poly_query(E_, N_, STREAM)
        m = np.where((ds < ex_[:, 0] * 0.9) & (Z_ < STREAM_WL + 0.1), np.where(R < 0.6, TI["dirt"], TI["stone_mid"]), m)
        snowy = ((Z_ >= 20) & (E_ + 0.25 * N_ + (R2 - 0.5) * 36 > 300)) | (Z_ >= 58)
        sn = np.where(SH < -0.25, TI["snow_shadow"], TI["snow_light"])
        sn = np.where(R < 0.12, TI["snow_shadow"], sn)
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
def proto_pine(snow=False):
    b = B("Pine_Snow" if snow else "Pine_Green")
    b.cyl(T(), 0.42, 0.3, 1.9, 4, "wood_dark")
    tiers = [(1.3, 3.1, 3.4), (3.3, 2.5, 3.1), (5.2, 1.85, 2.8), (7.0, 1.2, 2.5)]
    for k, (z0, r, h) in enumerate(tiers):
        if snow:
            sides = ["pine_dark", "snow_light", "pine_dark", "snow_shadow", "snow_light", "pine_dark"] if k < 3 else ["snow_light", "snow_shadow"]
        else:
            sides = ["pine_dark", "grass_mid"] if k < 2 else ["grass_mid", "leaf_light"]
        b.cyl(T(0, 0, z0, rz=0.5 * k), r, 0, h, 6, "pine_dark", jit=0.12, side_mats=sides)
    return b


def proto_round_tree():
    b = B("Tree_Round")
    b.cyl(T(), 0.42, 0.28, 2.6, 4, "wood_dark")
    b.bip(T(0, 0, 3.4), 2.0, 2.0, 1.5, 1.4, 5, ["grass_mid", "leaf_light"], ["pine_dark", "grass_mid"], jit=0.2)
    b.bip(T(0.9, 0.4, 4.6, rz=1), 1.5, 1.5, 1.3, 1.1, 5, ["leaf_light", "grass_mid"], ["grass_mid", "pine_dark"], jit=0.15)
    b.bip(T(-0.9, -0.3, 4.1, rz=2), 1.4, 1.4, 1.2, 1.0, 5, ["grass_mid", "leaf_light"], ["pine_dark", "grass_mid"], jit=0.15)
    return b


def proto_rock(name, r, kind=0, snow=False):
    b = B(name)
    n = 5 + (kind % 2)
    up = ["snow_light", "snow_shadow"] if snow else (["stone_light", "stone_mid"] if kind % 2 == 0 else ["stone_mid", "stone_light"])
    dn = ["stone_mid", "stone_light"]
    b.bip(T(rz=kind), r, r * 0.85, r * 0.7, r * 0.15, n, up, dn, jit=r * 0.2)
    return b


def proto_pillar(name, snow=False):
    b = B(name)
    n = 6
    zs = [0, 3.4, 6.8, 9.6]; rs = [3.0, 2.6, 2.0, 1.2]
    rings = []
    for k, (z, r) in enumerate(zip(zs, rs)):
        rings.append([Vector((r * math.cos(2 * math.pi * i / n + 0.4 * k) + rng.uniform(-.35, .35),
                              r * math.sin(2 * math.pi * i / n + 0.4 * k) + rng.uniform(-.35, .35), z + rng.uniform(-.2, .2))) for i in range(n)])
    sides = ["stone_mid", "stone_light"]
    for k in range(3):
        for i in range(n):
            j = (i + 1) % n
            mm = (["snow_light", "snow_shadow"] if (snow and k == 2) else sides)
            b.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], mm[(i + k) % 2])
    apex = Vector((0.2, 0.1, 12.0))
    for i in range(n):
        b.face([rings[3][i], rings[3][(i + 1) % n], apex], ("snow_light" if snow else "stone_light") if i % 2 else ("snow_shadow" if snow else "stone_mid"))
    b.face(list(reversed(rings[0])), "stone_mid")
    return b


def proto_flower(color):
    b = B("Flowers_" + color)
    b.cyl(T(), 0.35, 0, 0.45, 4, "grass_mid")
    b.bip(T(0.12, 0, 0.62), 0.16, 0.16, 0.12, 0.05, 4, [color])
    b.bip(T(-0.15, 0.1, 0.46), 0.14, 0.14, 0.1, 0.04, 4, [color])
    return b


def proto_house():
    b = B("House_RedRoof")
    b.box(T(0, 0, -1.2), 7.0, 6.0, 1.8, "stone_mid", 0.99, 0.02)
    b.box(T(0, 0, 0.6), 6.5, 5.5, 3.0, "plaster", 0.97, 0.02)
    hx, hy = 3.9, 3.3; zr = 3.6; zt = 7.2
    tl = [(-hx, -hy, zr), (hx, -hy, zr), (hx, hy, zr), (-hx, hy, zr)]
    ridge = [(-hx - 0.2, -0.2, zt), (hx + 0.2, -0.2, zt)]
    b.face([tl[0], tl[1], ridge[1], ridge[0]], "roof_red")
    b.face([tl[3], ridge[0], ridge[1], tl[2]], "roof_red")
    b.face([tl[0], ridge[0], tl[3]], "roof_red"); b.face([tl[1], tl[2], ridge[1]], "roof_red")
    b.face([tl[3], tl[2], tl[1], tl[0]], "roof_red")
    b.box(T(2.0, 1.2, 4.0), 0.9, 0.9, 3.6, "stone_light", 0.9)            # chimney
    b.box(T(0, -2.8, 0.6), 1.3, 0.2, 2.1, "wood_dark")                     # door
    for x in (-2.2, 2.2): b.box(T(x, -2.78, 1.7), 0.9, 0.12, 0.9, "glow")   # windows
    return b


def proto_windmill():
    b = B("Windmill")
    b.cyl(T(), 3.0, 1.9, 8.0, 6, "plaster", jit=0.05, side_mats=["plaster", "stone_light"])
    b.cyl(T(0, 0, 8.0), 2.4, 0, 3.4, 6, "roof_purple", side_mats=["roof_purple", "iron"])
    b.box(T(0, -2.2, 6.4), 0.9, 1.0, 0.9, "iron")
    for a in (0.35, 0.35 + math.pi / 2, 0.35 + math.pi, 0.35 + 3 * math.pi / 2):
        b.box(T(0, -2.9, 6.8, ry=a), 0.3, 0.25, 7.0, "wood_dark")
        b.box(T(0, -3.15, 6.8, ry=a) @ Matrix.Translation((0.7, 0, 2.2)), 1.2, 0.08, 4.2, "plaster")
    b.box(T(0, -2.9, -0.3), 1.6, 0.2, 2.2, "wood_dark")
    b.box(T(0, 0, -1.2), 6.6, 6.6, 1.3, "stone_mid", 0.95)
    return b


def proto_fence():
    b = B("Fence_4m")
    for x in (-1.9, 0.0, 1.9): b.box(T(x, 0, 0), 0.22, 0.22, 1.3, "wood_dark", 0.9, 0.02)
    for z in (0.45, 0.92): b.box(T(0, 0.02, z), 4.0, 0.12, 0.16, "wood_light", jit=0.015)
    return b


def proto_lamp():
    b = B("Lamp_Post")
    b.box(T(), 0.22, 0.22, 3.6, "wood_dark", 0.85)
    b.box(T(0.35, 0, 3.3), 0.9, 0.14, 0.14, "wood_dark")
    b.box(T(0.7, 0, 2.55), 0.42, 0.42, 0.6, "glow")
    b.cyl(T(0.7, 0, 3.15), 0.35, 0, 0.35, 4, "iron", rot=math.pi / 4)
    return b


def proto_bridge(name, L, wood_snow=False):
    b = B(name)
    W = 4.0; nseg = 6; seg = L / nseg
    arch = lambda x: 0.8 * (1 - (2 * x / L) ** 2)
    for k in range(nseg):
        x = -L / 2 + seg * (k + 0.5)
        dz = (arch(x + seg / 2) - arch(x - seg / 2)) / seg
        b.box(T(x, 0, 0.55 + arch(x), ry=-math.atan(dz)), seg + 0.12, W - 0.4, 0.32, "wood_light", jit=0.01)
    for sy in (-1, 1):
        xs = np.linspace(-L / 2 + 0.5, L / 2 - 0.5, 5)
        for x in xs: b.box(T(x, sy * (W / 2 - 0.2), 0.7 + arch(x)), 0.24, 0.24, 1.2, "wood_dark", 0.9)
        b.box(T(0, sy * (W / 2 - 0.2), 1.55 + 0.35), L - 0.8, 0.16, 0.18, "wood_dark")
    for sx in (-1, 1): b.box(T(sx * (L / 2 + 0.2), 0, -0.8), 1.6, W + 0.6, 1.6, "stone_mid", 0.92)
    return b


def proto_sign(kind):
    b = B("Sign_" + kind)
    b.box(T(), 0.4, 0.4, 5.0, "wood_dark", 0.9)
    b.cyl(T(0, -0.25, 5.3, rx=math.pi / 2), 2.5, 2.5, 0.5, 8, "iron", rot=math.pi / 8)
    f = -0.8      # icon plane y
    if kind == "pine":
        b.cyl(T(0, f, 4.1, rx=0), 1.2, 0, 2.0, 6, "grass_mid", side_mats=["grass_mid", "leaf_light"])
        b.cyl(T(0, f, 5.3, rx=0), 0.9, 0, 1.6, 6, "leaf_light", side_mats=["leaf_light", "grass_mid"]) if False else None
        b.cyl(T(0, f, 5.2), 0.9, 0, 1.7, 6, "leaf_light")
    elif kind == "mountain":
        b.face([(-1.8, f, 4.2), (1.8, f, 4.2), (0, f, 6.8)], "stone_light")
        b.face([(0.4, f - 0.01, 4.2), (1.8, f - 0.01, 4.2), (0, f - 0.01, 6.8)], "stone_mid")
        b.face([(-0.5, f - 0.02, 5.9), (0.5, f - 0.02, 5.9), (0, f - 0.02, 6.8)], "snow_light")
    elif kind == "snow":
        for a in (0, math.pi / 3, 2 * math.pi / 3):
            b.box(T(0, f, 5.3, ry=a) @ Matrix.Translation((0, 0, -1.7)), 0.24, 0.2, 3.4, "snow_light")
    elif kind == "castle":
        b.box(T(0, f, 4.2), 2.6, 0.25, 1.3, "ice_dark")
        for x in (-1.1, 0, 1.1): b.cyl(T(x, f, 5.4), 0.4, 0, 1.4 if x == 0 else 1.0, 4, "ice_light")
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
    b.bip(T(0, 0, 0.85), 0.9, 0.9, 0.85, 0.85, 8, ms, jit=0.04)
    b.bip(T(0, 0, 1.85), 0.65, 0.65, 0.6, 0.6, 8, ms, jit=0.03)
    b.bip(T(0, 0, 2.6), 0.46, 0.46, 0.46, 0.4, 8, ms, jit=0.02)
    b.cyl(T(0, 0, 2.55), 0.58, 0.58, 0.22, 8, "ember", rot=0.4)                       # scarf
    b.box(T(0.38, -0.34, 1.95, rx=0.1, rz=0.3), 0.18, 0.1, 0.65, "ember")
    b.cyl(T(0, 0, 3.0), 0.52, 0.52, 0.07, 8, "iron")                                   # hat brim
    b.cyl(T(0, 0, 3.05), 0.32, 0.28, 0.5, 8, "iron")
    b.cyl(T(0, -0.46, 2.65, rx=-math.pi / 2 + 0.0), 0.08, 0, 0.45, 4, "roof_red")      # nose
    return b


def proto_igloo():
    b = B("SM2_Igloo")
    n = 8
    prof = [(3.0, 0.0), (2.75, 1.1), (2.0, 2.15), (1.05, 2.95)]
    rings = [[Vector((r * math.cos(2 * math.pi * i / n + 0.2 * k), r * math.sin(2 * math.pi * i / n + 0.2 * k), z + rng.uniform(-.04, .04)))
              for i in range(n)] for k, (r, z) in enumerate(prof)]
    b.rings(rings, ["snow_light", "ice_light", "snow_shadow", "snow_light"], apex=Vector((0, 0, 3.5)), apex_mat=["snow_light", "snow_shadow"], bottom_mat="snow_shadow")
    b.box(T(0, -3.0, 0), 2.2, 2.6, 1.7, "snow_light", 0.85)
    b.face([(-0.55, -4.36, 0.02), (0.55, -4.36, 0.02), (0.5, -4.36, 1.1), (0, -4.36, 1.45), (-0.5, -4.36, 1.1)], "iron")
    return b


def proto_crystal():
    b = B("SM3_IceCrystal")
    for (x, y, h, r, rx, ry) in ((0, 0, 6.0, 0.8, 0.0, 0.08), (1.1, 0.5, 3.6, 0.55, 0.3, 0.5), (-0.9, 0.7, 4.4, 0.6, -0.35, -0.3)):
        M = T(x, y, 0, rx=rx, ry=ry)
        b.cyl(M, r, r * 0.85, h * 0.62, 5, "ice_light", side_mats=["ice_light", "ice_dark"], bottom=False)
        b.cyl(M @ Matrix.Translation((0, 0, h * 0.62)), r * 0.85, 0, h * 0.38, 5, "ice_light", side_mats=["ice_dark", "ice_light"], bottom=False)
    return b


def proto_sled():
    b = B("SM5_Sled")
    for sy in (-0.62, 0.62):
        b.box(T(-0.35, sy, 0.02), 2.0, 0.14, 0.2, "wood_dark")
        b.box(T(0.85, sy, 0.1, ry=-0.65), 0.95, 0.14, 0.2, "wood_dark")
        for x in (-1.0, 0.1): b.box(T(x, sy, 0.2), 0.12, 0.12, 0.3, "wood_dark")
    for x in (-1.1, -0.4, 0.3, 0.9): b.box(T(x, 0, 0.5), 0.5, 1.5, 0.09, "wood_light", jit=0.01)
    return b


def proto_campfire():
    b = B("SM6_Campfire")
    b.cyl(T(), 1.25, 1.1, 0.14, 8, "snow_light")
    for i in range(8):
        a = 2 * math.pi * i / 8
        b.bip(T(0.9 * math.cos(a), 0.9 * math.sin(a), 0.12, rz=a), 0.28, 0.22, 0.24, 0.0, 4, ["stone_light", "stone_mid"])
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        b.box(T(0, 0, 0.12, rz=a, ry=-0.45) @ Matrix.Translation((0.0, 0, 0.0)), 0.16, 0.16, 1.1, "wood_dark" if k % 2 else "wood_light", bottom=False)
    b.cyl(T(0, 0, 0.3), 0.38, 0, 1.1, 5, "glow", bottom=False)
    b.cyl(T(0.05, 0, 0.3), 0.24, 0, 0.7, 5, "ember", bottom=False)
    return b


def proto_lake():
    b = B("SM7_IceLake")
    rr = random.Random(5)
    n = 18; ro = []; ri = []
    for i in range(n):
        a = 2 * math.pi * i / n; k = rr.uniform(0.88, 1.1) if i % 2 else rr.uniform(0.97, 1.03)
        ro.append((30 * k * math.cos(a), 22 * k * math.sin(a))); ri.append((17 * math.cos(a + 0.1), 12.5 * math.sin(a + 0.1)))
    top = 0.3
    for i in range(n):
        j = (i + 1) % n
        b.face([(0, 0, top), (ri[i][0], ri[i][1], top), (ri[j][0], ri[j][1], top)][::-1], "ice_dark" if i % 6 == 0 else "ice_light")
        b.face([(ri[i][0], ri[i][1], top), (ro[i][0], ro[i][1], top), (ro[j][0], ro[j][1], top), (ri[j][0], ri[j][1], top)][::-1] if False else
               [(ri[j][0], ri[j][1], top), (ro[j][0], ro[j][1], top), (ro[i][0], ro[i][1], top), (ri[i][0], ri[i][1], top)], "ice_dark" if i % 5 == 2 else "ice_light")
        b.face([(ro[i][0], ro[i][1], top), (ro[j][0], ro[j][1], top), (ro[j][0], ro[j][1], -0.4), (ro[i][0], ro[i][1], -0.4)], "snow_shadow")
    return b


def proto_cave():
    b = B("SM8_IceCave")
    n = 10
    prof = [(5.0, 0.0), (4.7, 2.0), (3.5, 3.9), (1.9, 5.2)]
    rings = [[Vector((r * math.cos(2 * math.pi * i / n + 0.15 * k), 0.9 * r * math.sin(2 * math.pi * i / n + 0.15 * k), z + rng.uniform(-.06, .06)))
              for i in range(n)] for k, (r, z) in enumerate(prof)]
    b.rings(rings, ["ice_light", "ice_dark", "ice_light", "snow_shadow"], apex=Vector((0, 0, 6.0)), apex_mat=["ice_light", "snow_light"], bottom_mat="ice_dark")
    yf = -4.85
    b.face([(-1.9, yf, 0.02), (1.9, yf, 0.02), (1.9, yf, 2.5), (0, yf, 4.1), (-1.9, yf, 2.5)], "iron")
    b.box(T(-2.4, yf + 0.3, 0), 1.0, 0.9, 2.6, "ice_dark", 0.9); b.box(T(2.4, yf + 0.3, 0), 1.0, 0.9, 2.6, "ice_dark", 0.9)
    b.box(T(0, yf + 0.3, 3.6), 3.2, 0.9, 0.9, "ice_light", 0.9)
    for (x, y, s) in ((-1.5, 1.5, 0.6), (1.8, 0.8, 0.8)):
        b.cyl(T(x, y, 4.2 - abs(x) * 0.3, ry=0.2 * x), 0.5 * s, 0, 2.4 * s, 5, "ice_light", side_mats=["ice_light", "ice_dark"], bottom=False)
    return b


# ---- castle
def build_castle():
    b = B("Castle_Ice")
    HW = 13.0
    # plinth rock
    b.cyl(T(0, 0, -9), 21, 17, 9, 8, "stone_mid", rot=math.pi / 8, side_mats=["stone_mid", "stone_light", "snow_shadow", "stone_mid"])
    # courtyard walls (gate gap on -Y)
    wh = 4.2; th = 1.6
    b.box(T(-HW / 2 - 2.0, -HW, 0), HW - 4.0, th, wh, "ice_dark", 0.96)
    b.box(T(HW / 2 + 2.0, -HW, 0), HW - 4.0, th, wh, "ice_dark", 0.96)
    b.box(T(0, -HW, 3.2), 5.0, th, 1.0, "ice_light")           # gate lintel
    b.box(T(0, HW, 0), 2 * HW, th, wh, "ice_dark", 0.96)
    b.box(T(-HW, 0, 0), th, 2 * HW, wh, "ice_dark", 0.96)
    b.box(T(HW, 0, 0), th, 2 * HW, wh, "ice_dark", 0.96)
    for x in (-HW, HW):                                          # gate towers / corner towers
        for y in (-HW, HW):
            b.cyl(T(x, y, -0.5), 3.4, 2.8, 12.0, 6, "ice_dark", jit=0.1, side_mats=["ice_dark", "ice_light"])
            b.cyl(T(x, y, 11.5), 3.8, 0, 8.0, 6, "ice_light", side_mats=["ice_light", "snow_light"])
    for x in (-3.0, 3.0): b.cyl(T(x, -HW, 0), 1.2, 1.0, 5.5, 5, "ice_light")
    # keep
    b.cyl(T(2, 4, 0), 6.4, 4.8, 17.0, 8, "ice_dark", jit=0.1, side_mats=["ice_dark", "ice_light", "ice_dark", "snow_shadow"])
    b.cyl(T(2, 4, 17.0), 5.6, 0, 13.0, 8, "ice_light", side_mats=["ice_light", "snow_light", "ice_light", "snow_shadow"])
    b.cyl(T(2, 4, 29.0), 0.5, 0, 3.5, 4, "glow")
    b.cyl(T(-6, 6, 0), 3.4, 2.6, 11.0, 6, "ice_dark"); b.cyl(T(-6, 6, 10.5), 3.8, 0, 7.5, 6, "ice_light")
    b.cyl(T(9, 8, 0), 3.0, 2.4, 9.0, 6, "ice_dark"); b.cyl(T(9, 8, 8.5), 3.4, 0, 6.5, 6, "ice_light")
    # windows (emission) 6
    for (x, y, z) in ((2, -2.5, 8), (-1.0, -2.4, 12), (5.4, -1.0, 14), (-6, 3.0, 6.5), (-13 + 0.0, -HW - 1.0, 7.5), (13, -HW - 1.0, 7.5)):
        b.box(T(x, y, z), 1.1, 0.35, 1.9, "glow")
    return b


def proto_mountain(name, seed):
    r = random.Random(seed); b = B(name)
    n = 7
    prof = [(1.0, 0.0), (0.62, 0.42), (0.32, 0.76)]
    rings = []
    for k, (rs, z) in enumerate(prof):
        rings.append([Vector((rs * math.cos(2 * math.pi * i / n + 0.5 * k) * r.uniform(0.85, 1.15), rs * math.sin(2 * math.pi * i / n + 0.5 * k) * r.uniform(0.85, 1.15),
                              z + r.uniform(-0.04, 0.04))) for i in range(n)])
    mats_rows = [["stone_mid", "stone_light"], ["snow_shadow", "stone_light", "snow_light"], ["snow_light", "snow_shadow"]]
    for k in range(2):
        for i in range(n):
            j = (i + 1) % n
            b.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], mats_rows[k][(i + k) % len(mats_rows[k])])
    apex = Vector((0.05, 0.0, 1.0))
    for i in range(n):
        b.face([rings[2][i], rings[2][(i + 1) % n], apex], "snow_light" if i % 2 else "snow_shadow")
    b.face(list(reversed(rings[0])), "stone_mid")
    return b


# build prototype meshes ---------------------------------------------------
P = {}
def reg(key, builder, recalc=True):
    P[key] = builder.mesh(recalc)
    return P[key]

reg("pine", proto_pine()); reg("pine_snow", proto_pine(True)); reg("tree", proto_round_tree())
for k in range(3):
    reg(f"rock{k}", proto_rock(f"Rock_{k}", [0.9, 1.3, 1.8][k], k))
    reg(f"rocksnow{k}", proto_rock(f"RockSnow_{k}", [0.9, 1.3, 1.8][k], k, True))
for k in range(3):
    reg(f"pillar{k}", proto_pillar(f"CliffPillar_{k}")); reg(f"pillarsnow{k}", proto_pillar(f"CliffPillarSnow_{k}", True))
for c in ("flower_y", "flower_w", "flower_p", "flower_r"): reg(c, proto_flower(c))
reg("house", proto_house()); reg("windmill", proto_windmill()); reg("fence", proto_fence()); reg("lamp", proto_lamp())
reg("bridge16", proto_bridge("Bridge_S1", 16)); reg("bridge12", proto_bridge("Bridge_S2", 12)); reg("bridge8", proto_bridge("Bridge_S3", 8))
for k in ("pine", "mountain", "snow", "castle"): reg("sign_" + k, proto_sign(k))
reg("start", proto_start())
reg("snowman", proto_snowman()); reg("igloo", proto_igloo()); reg("crystal", proto_crystal()); reg("sled", proto_sled())
reg("campfire", proto_campfire()); reg("lake", proto_lake()); reg("cave", proto_cave())
reg("castle", build_castle())
for k in range(4): reg(f"mtn{k}", proto_mountain(f"Mountain_{k}", 40 + k))

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

# path ribbons (dirt / stone / packed snow) + white guide line
pp = resample(PATH[:, :2], 1.5)
p_hw = np.full(len(pp), 1.8)
zf = lambda e, n: Hm(e, n)
def pmat(k):
    z = Hm(*pp[k])
    return "dirt" if z < 14 else ("stone_light" if z < 44 else "snow_shadow")
ribbon("Path_Surface", pp, p_hw, zf, pmat, C_PATH, off=0.22)
ribbon("Path_GuideLine", pp, np.full(len(pp), 0.3), zf, "marker_white", C_PATH, off=0.36)
# waypoint discs
wp_info = []
for idx, (e, n, z, nm) in enumerate(WP):
    zz = Hm(e, n)
    wp_info.append((idx, e, n, zz, nm))
    d = B(f"Waypoint_{idx:02d}")
    d.cyl(T(), 1.0, 1.0, 0.1, 10, "marker_white")
    d.obj(C_PATH, (e, n, zz + 0.5))

# ------------------------------------------------------------------ placing of fixed objects
HOUSES = [(70, 60), (50, 95), (40, 50), (110, 25), (60, 120)]
for (e, n) in HOUSES: BLOCK.append((e, n, 7.5))
BLOCK.append((141, 56, 7.5))
BLOCK.append((25, 25, 6))
for sx, sy in ((95, 88), (128, 48), (330, 205)): BLOCK.append((sx, sy, 3))
for (e, n) in ((285, 251), (300, 285), (335, 262), (270, 275), (345, 300), (280, 290), (305, 310), (355, 285), (355, 255), (313, 318),
               (290, 268), (326, 251), (350, 320), (283, 287), (308, 307), (358, 282), (287, 262)):
    BLOCK.append((e, n, 5.5))

n_house = 0
for (e, n) in HOUSES:
    e2, n2 = settle(e, n, "House", rad=6, maxslope=0.35, path=7.0)
    inst(P["house"], f"House_RedRoof_{n_house}", e2, n2, rng.uniform(-0.45, 0.45), 1.0, 0.9, coll=C_BLD,
         z=min(Hm(e2 - 3, n2 - 3), Hm(e2 + 3, n2 - 3), Hm(e2, n2)) + 0.9)
    n_house += 1
e2, n2 = settle(141, 56, "Windmill", rad=8, maxslope=0.4)
inst(P["windmill"], "Windmill_0", e2, n2, 0.0, 1.0, 0.9, coll=C_BLD)
inst(P["start"], "Start_Marker", 25, 25, 0, 1.6, 0.1, coll=C_MISC)

# bridges
def bridge(key, e, n, ang, wl, name):
    ob = inst(P[key], name, e, n, ang, 1.0, coll=C_BLD, z=wl + 0.72)
for (key, e, n, ang, wl, nm) in (("bridge16", 95, 88, math.atan2(43, 37), RIVER_WL, "Bridge_S1"),
                                 ("bridge12", 128, 48, math.atan2(15, 25), RIVER_WL, "Bridge_S2"),
                                 ("bridge8", 330, 205, math.atan2(18, 9), STREAM_WL, "Bridge_S3")):
    bridge(key, e, n, ang, wl, nm)

# zone signs
for idx, kind in ((5, "pine"), (8, "mountain"), (12, "snow"), (15, "castle")):
    e, n, z, _ = WP[idx]
    px, py, pz, tg = path_at(float(PCUM[np.abs(PATH[:, 0] - e).argmin() + 0]) if False else min(range(len(PATH)), key=lambda q: math.hypot(PATH[q, 0] - e, PATH[q, 1] - n)) and PCUM[min(range(len(PATH)), key=lambda q: math.hypot(PATH[q, 0] - e, PATH[q, 1] - n))])
    sx, sy = e - tg[1] * -4.0 * -1 + 0, n + tg[0] * -4.0 * -1
    sx, sy = e + (-tg[1]) * 4.5, n + tg[0] * 4.5
    if kind == "castle": sx, sy = e + 6, n - 2
    inst(P["sign_" + kind], f"Sign_{kind}", sx, sy, 0.0, 1.0, -0.2, coll=C_MISC)
    BLOCK.append((sx, sy, 3))

# lamps along path near start
for s in (10, 26, 42, 58):
    px, py, pz, tg = path_at(s)
    inst(P["lamp"], f"Lamp_{s}", px - tg[1] * 3.2, py + tg[0] * 3.2, 0.0, 1.0, -0.1, coll=C_MISC)
    BLOCK.append((px - tg[1] * 3.2, py + tg[0] * 3.2, 1.5))

# fences along the first path stretch
fence_pos = []
s_end = float(PCUM[np.argmin(np.abs(PATH[:, 0] - 95) + np.abs(PATH[:, 1] - 88))]) - 12
s = 6.0
while s < s_end:
    px, py, pz, tg = path_at(s)
    for side in (1, -1):
        fe, fn = px - tg[1] * 3.2 * side, py + tg[0] * 3.2 * side
        if rng.random() < 0.72 and not blocked(fe, fn, -3) and driver(fe, fn) > 1.5:
            fence_pos.append((fe, fn, math.atan2(tg[1], tg[0])))
    s += 4.0
rng.shuffle(fence_pos)
for k, (fe, fn, ang) in enumerate(fence_pos[:40]):
    inst(P["fence"], f"Fence_{k:02d}", fe, fn, ang, 1.0, -0.05, coll=C_BLD)
    BLOCK.append((fe, fn, 1.2))

# ------------------------------------------------------------------ sample objects in the world
SAMPLE_LOG = []
def put(key, nm, pts, sc=1.0, coll=C_SNOW, rotz=None, maxs=0.45, zoff=-0.05):
    for k, (e, n) in enumerate(pts):
        e2, n2 = settle(e, n, nm, rad=14, maxslope=maxs, path=4.0)
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
def scatter(count, key_fn, name, coll, region, tries=60, sc=(0.8, 1.3), zoff=-0.25, path=3.5, extra=0.0, maxs=0.55, sink=True, rot=True, out=None):
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
CLEAR = [(125, 165, 15), (170, 200, 12)]
def not_clear(e, n): return not any(math.hypot(e - a, n - b) < r for a, b, r in CLEAR)

# ---- green pines (300)
def green_ok(e, n): return (not snowy_at(e, n)) and Hm(e, n) < 40 and not_clear(e, n)
cnt = {}
cnt["pine_B"] = scatter(180, lambda e, n: "pine" if green_ok(e, n) else None, "Pine", C_PLANT, lambda: (U(88, 235), U(98, 238)), sc=(0.85, 1.35))
cnt["pine_A"] = scatter(45, lambda e, n: "pine" if green_ok(e, n) else None, "Pine_A", C_PLANT, lambda: (U(4, 150), U(4, 128)), sc=(0.75, 1.2), path=5.0)
cnt["pine_hill"] = scatter(45, lambda e, n: "pine" if (green_ok(e, n) and Hm(e, n) > 5) else None, "Pine_Hill", C_PLANT,
                           lambda: (U(4, 300), U(4, 300)), sc=(0.8, 1.4))
def edge_pt():
    s = rng.randrange(4); t = U(4, 376); d = U(3, 13)
    return [(t, d), (t, 380 - d), (d, t), (380 - d, t)][s]
cnt["pine_edge"] = scatter(30, lambda e, n: "pine" if (green_ok(e, n) and Hm(e, n) > 0.8) else None, "Pine_Edge", C_PLANT, edge_pt, sc=(0.9, 1.5), extra=-1.0)
# ---- round trees (40)
cnt["tree"] = scatter(40, lambda e, n: "tree" if (not snowy_at(e, n) and Hm(e, n) < 8) else None, "Tree_Round", C_PLANT,
                      lambda: (U(4, 150), U(4, 130)), sc=(0.8, 1.25), path=5.0, extra=2.0)
# ---- snow pines (140)
def snow_ok(e, n): return snowy_at(e, n) and Hm(e, n) < 70 and r_castle(e, n) > 26 and e > 250
cnt["pine_snow"] = scatter(140, lambda e, n: "pine_snow" if snow_ok(e, n) else None, "PineSnow", C_SNOW,
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
    if not (5 < e < 375 and 5 < n < 375) or dpath(e, n) < 3 or (240 < n < 252 and 292 < e < 318): continue
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
        if 0.5 < driver(e, n) < 3.5 and 3 < e < 377 and 3 < n < 377 and dpath(e, n) > 3 and not blocked(e, n, -2):
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
    if slope_at(e, n) < 0.85 or not free(e, n, path=4.0, riv=0, extra=0.5): continue
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
    if dpath(e, n) < 2.2 or driver(e, n) < 1.5 or blocked(e, n, -3): return False
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
def label(txt, e, n, size=9, z=160, rot=0.0, hexc=None):
    cu = bpy.data.curves.new("L_" + txt, 'FONT'); cu.body = txt; cu.size = size
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    ob = bpy.data.objects.new("Label_" + txt[:20], cu); C_LABEL.objects.link(ob)
    cu.materials.append(MATS["label"])
    ob.location = (e, n, z); ob.rotation_euler.z = rot; ob.visible_shadow = False
    cu2 = bpy.data.curves.new("H_" + txt, 'FONT'); cu2.body = txt; cu2.size = size
    cu2.align_x = 'CENTER'; cu2.align_y = 'CENTER'; cu2.materials.append(MATS["marker_white"])
    cu2.extrude = 0.0
    h = bpy.data.objects.new("LabelHalo_" + txt[:20], cu2); C_LABEL.objects.link(h)
    h.location = (e + size * 0.07, n - size * 0.07, z - 1); h.rotation_euler.z = rot; h.visible_shadow = False
    cu2.offset = 0
    return ob


label("A  MEADOW + START", 80, 12, 8); label("B  PINE VALLEY", 160, 128, 8, rot=0.0)
label("C  CLIFF ASCENT", 218, 168, 7.5); label("D1  LOW SNOW + FALLS", 335, 175, 8)
label("D2  ICE LAKE PLATEAU", 322, 308, 7); label("D3  ICE CASTLE", 300, 372, 8)
label("RIVER", 50, 100, 5, rot=0.0)
for (idx, e, n, zz, nm) in wp_info:
    label(str(idx) if idx else "0 START", e + 6, n + 5, 5.5 if idx else 7)
# north arrow + scale bar
ar = B("NorthArrow"); ar.face([(-5, 0, 0), (5, 0, 0), (0, 22, 0)][::-1], "label"); ar.face([(-5, 0, 0), (0, 4, 0), (0, 22, 0)], "stone_light")
aob = ar.obj(C_LABEL, (28, 335, 160)); aob.visible_shadow = False
label("N", 28, 330, 11)
sb = B("ScaleBar"); sb.face([(0, -2, 0), (100, -2, 0), (100, 2, 0), (0, 2, 0)], "label"); sob = sb.obj(C_LABEL, (240, 15, 160)); sob.visible_shadow = False
label("100 m", 290, 24, 8)
label("(temp snow/ice colours)", 190, 4, 5)

# ------------------------------------------------------------------ stats
def count_tris(coll_filter=None):
    tot = 0; per = {}
    for ob in scene.objects:
        if ob.type != 'MESH': continue
        if ob.users_collection[0] == C_LABEL: continue
        t = tris_of(ob.data)
        tot += t; per[ob.users_collection[0].name] = per.get(ob.users_collection[0].name, 0) + t
    return tot, per


# ------------------------------------------------------------------ sample sheet scene
sheet = bpy.data.scenes.new("ObjectsSheet")
SC_COL = sheet.collection
items = [("SM1 Snowman", "snowman", 2.0), ("SM2 Igloo", "igloo", 6.0), ("SM3 Ice crystal", "crystal", 4.5), ("SM4 Snow pine", "pine_snow", 10.0),
         ("SM5 Sled", "sled", 3.0), ("SM6 Campfire", "campfire", 2.4), ("SM7 Ice lake", "lake", 60.0), ("SM8 Ice cave", "cave", 10.0)]
for k, (nm, key, size) in enumerate(items):
    ci, ri = k % 4, k // 4
    sc_ = 4.6 / size if key in ("lake", "cave", "pine_snow", "igloo") else (4.2 / size if size < 4.5 else 1.0)
    if key == "snowman": sc_ = 1.8
    if key == "crystal": sc_ = 1.0
    if key == "sled": sc_ = 1.5
    if key == "campfire": sc_ = 1.7
    ob = bpy.data.objects.new("Sheet_" + key, P[key]); SC_COL.objects.link(ob)
    ob.location = ((ci - 1.5) * 8.5, (0.5 - ri) * -9.0 + 0.0, 0.0 if key != "lake" else 0.0)
    ob.location.y = (0.5 - ri) * 11.0
    ob.scale = (sc_, sc_, sc_)
    cu = bpy.data.curves.new("T_" + nm, 'FONT'); cu.body = nm; cu.size = 0.55; cu.align_x = 'CENTER'
    cu.materials.append(MATS["label"])
    to = bpy.data.objects.new("T_" + nm, cu); SC_COL.objects.link(to)
    to.location = (ob.location.x, ob.location.y - 4.3, 0.02); to.visible_shadow = False
fb = B("SheetFloor"); fb.face([(-40, -40, -0.02), (40, -40, -0.02), (40, 40, -0.02), (-40, 40, -0.02)][::-1], "sheet_floor")
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


CAM_POS = (200, -330, 250)   # spec: (110,-140,190)->(230,230,25) cannot show Start + castle in 16:9; pulled back
CAM_TGT = (210, 200, 30)
pct = 40 if LOW else 100
tot, per = count_tris()
print("TRIS_TOTAL", tot, per)
print("COUNTS", cnt)
print("MOVED", MOVED)
print("OBJECT_COUNT", len(scene.objects))

if not NORENDER:
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
    # --- objects sheet
    setup(sheet, W_SHEET, 2048, 1024, pct); sheet.camera = cam_s
    cam_s.location = (0, -48, 24); cam_s.rotation_euler = (Vector((0, -0.5, 1.0)) - cam_s.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data_s = cam_s.data
    sheet.render.filepath = os.path.join(OUT, "preview_objects.png")
    # sheet camera shares data with main cam -> make persp for sheet
    cam_s.data = bpy.data.cameras.new("CamSheetData"); cam_s.data.lens = 50
    bpy.ops.render.render(write_still=True, scene=sheet.name)

# restore main scene camera for saving
cam.data.type = 'PERSP'; cam.data.lens = 35
C_LABEL.hide_render = True
bpy.context.window.scene = scene if bpy.context.window else None
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "world_map.blend"))
print("DONE")
