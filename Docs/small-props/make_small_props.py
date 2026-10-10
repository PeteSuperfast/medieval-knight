"""Small props set 2 for medieval-knight: red mushroom cluster, grass tuft, stepping stone, tree stump.
Run:  blender --background --factory-startup --python make_small_props.py
Writes small_props.blend, preview.png, compare.png next to this script. No FBX export.
Does not modify Art_kit/style_kit.py (imports it for palette, light, sky, camera).
Fixed seeds. Flat colour materials, flat shading, no textures. 1 unit = 1 m, origin = centre of base, Z up.
"""
import bpy, math, os, random, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Art_kit"))
import style_kit as K

# colours: all from Docs/style.md palette
K.PAL.update({"mushroom_red": "#C8483A", "plaster_cream": "#D8CCB0", "wood_light": "#A1773D"})
# existing kit keys used: grass_mid #6C8535, grass_light #8DB04A, wood_dark #715332, rock_top #ADABB7, rock_side #958A80

TRI_LIMIT = 120


class P:
    """Per-face mesh builder. Every face is oriented away from a reference interior point c."""

    def __init__(self, name):
        self.name = name; self.v = []; self.f = []; self.m = []; self.mats = []

    def face(self, pts, mat, c):
        pts = [Vector(p) for p in pts]
        n = Vector((0, 0, 0))
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
        cen = sum(pts, Vector()) / len(pts)
        if n.dot(cen - Vector(c)) < 0: pts.reverse()
        base = len(self.v)
        self.v += [tuple(p) for p in pts]
        self.f.append(tuple(range(base, base + len(pts))))
        if mat not in self.mats: self.mats.append(mat)
        self.m.append(self.mats.index(mat))

    def strip(self, a, b, mat, c):
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            self.face([a[i], a[j], b[j], b[i]], mat, c)

    def fan(self, ring, apex, mat, c):
        n = len(ring)
        for i in range(n):
            self.face([ring[i], ring[(i + 1) % n], apex], mat, c)

    def tris(self):
        return sum(len(f) - 2 for f in self.f)

    def build(self):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.v, [], self.f); me.update()
        for p, mi in zip(me.polygons, self.m):
            p.material_index = mi; p.use_smooth = False
        for n in self.mats: me.materials.append(K.MATS[n])
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def ring(n, r, z, rot=0.0, cx=0.0, cy=0.0, rng=None, rj=0.0, rscale=None):
    out = []
    for i in range(n):
        a = rot + 2 * math.pi * i / n
        rr = (rscale[i % len(rscale)] if rscale else r) * (1 + (rng.uniform(-rj, rj) if rng else 0))
        out.append(Vector((cx + rr * math.cos(a), cy + rr * math.sin(a), z)))
    return out


# ------------------------------------------------------------------ mushrooms
def add_mushroom(b, rng, x, y, h, lean, stem_n, spots):
    """h = total height (m). Stem n-sided (cream), 6-sided faceted cap (red) with spot triangles (cream)."""
    rot = rng.uniform(0, 6.28)
    ang = rng.uniform(0, 6.28)
    lx, ly = math.cos(ang) * lean, math.sin(ang) * lean   # lateral shift per metre of height
    sh = 0.62 * h
    ch = h - sh
    Rc = 0.40 * h
    def at(z): return (x + lx * z, y + ly * z)
    sr0, sr1 = 0.115 * h, 0.085 * h
    bot = ring(stem_n, sr0, 0.0, rot, x, y, rng, 0.06)
    top = ring(stem_n, sr1, sh, rot, *at(sh), rng=rng, rj=0.06)
    cs = Vector((*at(sh * 0.5), sh * 0.5))
    b.strip(bot, top, "plaster_cream", cs)
    b.face(bot, "plaster_cream", cs)
    b.face(top, "plaster_cream", cs)
    # cap
    cz = sh - 0.04 * h
    r0 = ring(6, Rc, cz, rot + 0.3, *at(cz), rng=rng, rj=0.07)
    z1 = sh + 0.38 * ch
    r1 = ring(6, Rc * 0.80, z1, rot + 0.3, *at(z1), rng=rng, rj=0.05)
    apex = Vector((*at(h), h))
    cc = Vector((*at(sh + 0.3 * ch), sh + 0.3 * ch))
    b.strip(r0, r1, "mushroom_red", cc)
    b.fan(r1, apex, "mushroom_red", cc)
    b.face(r0, "mushroom_red", cc)
    # spots: small triangles slightly proud of cap facets
    for kind, idx in spots:
        if kind == "q":
            q = [r0[idx], r0[(idx + 1) % 6], r1[(idx + 1) % 6], r1[idx]]
        else:
            q = [r1[idx], r1[(idx + 1) % 6], apex]
        cen = sum(q, Vector()) / len(q)
        nrm = (q[1] - q[0]).cross(q[2] - q[0])
        if nrm.dot(cen - cc) < 0: nrm = -nrm
        nrm.normalize()
        u = (q[1] - q[0]).normalized()
        w = nrm.cross(u)
        s = 0.30 * Rc
        p = cen + nrm * (0.004 * h / 0.25 + 0.002)
        tri = [p + u * s, p - u * s * 0.5 + w * s * 0.87, p - u * s * 0.5 - w * s * 0.87]
        b.face(tri, "plaster_cream", cc)


def make_mushrooms():
    rng = random.Random(21)
    b = P("Mushroom_Red_Cluster")
    add_mushroom(b, rng, 0.00, 0.00, 0.25, 0.10, 5, [("q", 0), ("q", 2), ("q", 4), ("t", 3)])   # large, 25 cm
    add_mushroom(b, rng, 0.15, -0.07, 0.17, -0.18, 5, [("q", 1), ("q", 3), ("q", 5)])           # medium, 17 cm
    add_mushroom(b, rng, -0.10, -0.12, 0.10, 0.22, 4, [("q", 0), ("q", 3)])                     # small, 10 cm
    return b.build(), b


# ------------------------------------------------------------------ grass tuft
def make_grass():
    rng = random.Random(33)
    b = P("Grass_Tuft")
    n = 7
    heights = [0.20, 0.17, 0.19, 0.14, 0.18, 0.15, 0.13]
    for i in range(n):
        a = 2 * math.pi * i / n + rng.uniform(-0.25, 0.25)
        rd = Vector((math.cos(a), math.sin(a), 0))
        td = Vector((-rd.y, rd.x, 0))
        base = rd * rng.uniform(0.010, 0.030)
        h = heights[i]
        lean = rng.uniform(0.045, 0.085) + (0.2 - h) * 0.2
        w = 0.060
        d = 0.022
        def sect(sc, z, off):
            c = base + rd * off + Vector((0, 0, z))
            return [c - td * (w * sc / 2), c + td * (w * sc / 2), c + rd * (d * sc)]
        s0 = sect(1.0, 0.0, 0.0)
        s1 = sect(0.62, h * 0.5, lean * 0.28)
        tip = base + rd * lean + Vector((0, 0, h))
        cen = base + rd * (lean * 0.15) + Vector((0, 0, h * 0.4))
        b.strip(s0, s1, "grass_mid", cen)
        b.fan(s1, tip, "grass_light", cen)
        b.face(s0, "grass_mid", cen)
    return b.build(), b


# ------------------------------------------------------------------ stepping stone
def make_stone():
    rng = random.Random(45)
    b = P("Stepping_Stone")
    n = 7
    angs = [2 * math.pi * i / n + rng.uniform(-0.22, 0.22) for i in range(n)]
    pts0, pts1, pts2 = [], [], []
    for a in angs:
        r = rng.uniform(0.15, 0.19)
        x, y = r * 1.12 * math.cos(a), r * 0.92 * math.sin(a)   # ~38 x 31 cm
        pts0.append(Vector((x, y, 0.0)))
        pts1.append(Vector((x * 0.97, y * 0.97, 0.034 + rng.uniform(-0.004, 0.004))))
        pts2.append(Vector((x * 0.84, y * 0.84, 0.050 + rng.uniform(-0.006, 0.006) + 0.008 * math.cos(a))))
    c = Vector((0, 0, 0.02))
    apex = Vector((0.01, -0.005, 0.058))
    b.strip(pts0, pts1, "rock_side", c)
    b.strip(pts1, pts2, "rock_top", c)
    b.fan(pts2, apex, "rock_top", c)
    b.face(pts0, "rock_side", c)
    return b.build(), b


# ------------------------------------------------------------------ tree stump
def make_stump():
    rng = random.Random(57)
    b = P("Tree_Stump")
    n = 8
    rot = 0.2
    bot = ring(n, 0, 0.0, rot, rng=rng, rj=0.05, rscale=[0.255, 0.205])      # roots flare out on alternate sides
    mid = ring(n, 0.170, 0.11, rot, rng=rng, rj=0.05)
    top = []
    for i in range(n):
        a = rot + 2 * math.pi * i / n
        r = 0.150 * (1 + rng.uniform(-0.05, 0.05))
        top.append(Vector((r * math.cos(a), r * math.sin(a), 0.394 + 0.006 * math.cos(a - 0.8))))
    c = Vector((0, 0, 0.2))
    b.strip(bot, mid, "wood_dark", c)
    b.strip(mid, top, "wood_dark", c)
    b.face(bot, "wood_dark", c)
    # cut face: rim lip (dark), light growth ring, pale core
    def shrink(ringp, f, dz):
        return [Vector((p.x * f, p.y * f, p.z + dz)) for p in ringp]
    r1 = shrink(top, 0.88, -0.003)
    r2 = shrink(top, 0.58, -0.007)
    ctr = Vector((0, 0, 0.384))
    b.strip(top, r1, "wood_dark", c)
    b.strip(r1, r2, "wood_light", c)
    b.fan(r2, ctr, "plaster_cream", c)
    return b.build(), b


def bbox(ob):
    xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]; zs = [v.co.z for v in ob.data.vertices]
    return (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs), min(zs))


def run():
    K.reset_scene()
    items = [make_mushrooms(), make_grass(), make_stone(), make_stump()]
    objs = []
    for ob, b in items:
        t = b.tris()
        w, d, h, z0 = bbox(ob)
        print("RESULT %-22s tris %3d  size x%.3f y%.3f z%.3f m  min_z %.3f  %s" %
              (ob.name, t, w, d, h, z0, "OK" if t <= TRI_LIMIT else "OVER"))
        objs.append(ob)
    K.render_lineup(objs, os.path.join(HERE, "preview.png"), cell=0.7, cols=4, res=(1280, 640), dist=4.1, target_z=0.1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "small_props.blend"))

    # compare.png: ours | env_rocks_trees | env_tower (all scaled to the same height)
    def load(path):
        im = bpy.data.images.load(path)
        w, h = im.size
        return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    def fit(a, th):
        h, w = a.shape[:2]
        nw = max(1, int(w * th / h))
        yi = (np.arange(th) * h / th).astype(int).clip(0, h - 1)
        xi = (np.arange(nw) * w / nw).astype(int).clip(0, w - 1)
        return a[yi][:, xi]
    th = 768
    parts = [load(os.path.join(HERE, "preview.png")),
             load(os.path.join(ROOT, "Docs", "ref", "env_rocks_trees.jpg")),
             load(os.path.join(ROOT, "Docs", "ref", "env_tower.jpg"))]
    gap = np.ones((th, 12, 4), np.float32)
    out = None
    for p in parts:
        q = fit(p, th)
        out = q if out is None else np.concatenate([out, gap, q], axis=1)
    img = bpy.data.images.new("compare", out.shape[1], th, alpha=False)
    img.pixels = out.ravel()
    img.filepath_raw = os.path.join(HERE, "compare.png")
    img.file_format = 'PNG'
    img.save()
    print("DONE")


run()
