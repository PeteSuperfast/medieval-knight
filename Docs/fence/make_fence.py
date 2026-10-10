"""Wooden fence set for medieval-knight: Fence_Straight_2m + Fence_CornerPost. Run:
  blender --background --factory-startup --python make_fence.py
Writes fence.blend, preview.png, compare.png next to this script. No FBX export.
Does not modify Art_kit/style_kit.py (imports it only).
"""
import bpy, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Art_kit"))
import style_kit as K

K.PAL.update({"fence_wood": "#A1773D", "fence_side": "#715332", "fence_peg": "#3A3A3A"})

POST_H = 1.10
RAIL_T = 0.06        # rail thickness (m)
RAIL_H = 0.13        # rail board height
RAIL_Z = (0.35, 0.78)
LEN = 2.0
POST_X = (-0.85, 0.0, 0.85)
HW = 0.06            # post half width (0.12 m square)
WOOD, SIDE, PEG = 0, 1, 2   # material slots (order of b.mi calls)


def new_builder(name):
    b = K.B(name)
    b.mi("fence_wood"); b.mi("fence_side"); b.mi("fence_peg")
    return b


def prism(b, prof, v0, v1, P, M):
    """Extrude a 2D profile (u,z) from v0 to v1 through mapping P(u,z,v) -> xyz, then matrix M."""
    n = len(prof)
    base = len(b.v)
    for v in (v0, v1):
        for u, z in prof:
            b.v.append(tuple(M @ Vector(P(u, z, v))))
    b.f.append(tuple(base + i for i in range(n))); b.m.append(WOOD)
    b.f.append(tuple(base + n + i for i in range(n - 1, -1, -1))); b.m.append(WOOD)
    for i in range(n):
        j = (i + 1) % n
        b.f.append((base + i, base + j, base + n + j, base + n + i)); b.m.append(WOOD)


def peg(b, P, M, pu, pz, v0, rng):
    """Small iron pin: 4-sided frustum sticking out of the rail face (no bottom face)."""
    a = rng.uniform(0.3, 1.2)

    def ring(r, v):
        return [tuple(M @ Vector(P(pu + r * math.cos(a + math.pi / 2 * k + math.pi / 4),
                                   pz + r * math.sin(a + math.pi / 2 * k + math.pi / 4), v))) for k in range(4)]
    base = len(b.v)
    b.v += ring(0.032, v0) + ring(0.020, v0 + 0.026)
    for i in range(4):
        j = (i + 1) % 4
        b.f.append((base + i, base + j, base + 4 + j, base + 4 + i)); b.m.append(PEG)
    b.f.append(tuple(base + 4 + i for i in range(4))); b.m.append(PEG)


def post(b, rng, x, y, hw):
    """Square post, slanted top (direction random per post), leaning a degree or two."""
    zl = POST_H - rng.uniform(0.07, 0.11)
    zh = POST_H
    if rng.random() < 0.5:
        zl, zh = zh, zl
    prof = [(-hw, 0.0), (hw, 0.0), (hw * 0.97, zh), (-hw * 0.97, zl)]
    M = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(rng.uniform(-0.03, 0.03), 4, 'X') \
        @ Matrix.Rotation(rng.uniform(-0.03, 0.03), 4, 'Y') @ Matrix.Rotation(rng.uniform(-0.12, 0.12), 4, 'Z')
    prism(b, prof, -hw, hw, lambda u, z, v: (u, v, z), M)


def rail_profile(rng, u0, u1, h):
    """Board silhouette with chipped / notched ends (seeded)."""
    j = lambda s=0.005: rng.uniform(-s, s)
    return [(u0 + 0.03, j(0.003)), (u1 - 0.02, j(0.003)), (u1 - 0.012, 0.42 * h), (u1, 0.60 * h),
            (u1 - 0.05 + j(), h + j(0.003)), (u0 + 0.05, h + j(0.003)), (u0, 0.72 * h),
            (u0 + 0.025, 0.36 * h), (u0 + 0.005, 0.14 * h)]


def make_fence(rng):
    b = new_builder("Fence_Straight_2m")
    for x in POST_X:
        post(b, rng, x + rng.uniform(-0.01, 0.01), rng.uniform(-0.008, 0.008), HW)
    P = lambda u, z, v: (u, -(HW - 0.005) - v, z)       # rails sit on the -Y (camera) side of the posts
    for k, zc in enumerate(RAIL_Z):
        zc += rng.uniform(-0.015, 0.015)
        h = RAIL_H + rng.uniform(-0.01, 0.01)
        u0, u1 = -LEN / 2 + rng.uniform(0, 0.02), LEN / 2 - rng.uniform(0, 0.02)
        ang = 0.026 if k == 0 else -0.021               # not parallel
        M = Matrix.Translation((0, 0, zc - h / 2)) @ Matrix.Rotation(ang, 4, 'Y')
        prism(b, rail_profile(rng, u0, u1, h), 0.0, RAIL_T, P, M)
        for x in POST_X:
            peg(b, P, M, x + rng.uniform(-0.004, 0.004), h / 2 + rng.uniform(-0.01, 0.01), RAIL_T, rng)
    return b.build()


def make_corner(rng):
    hw = 0.075
    b = new_builder("Fence_CornerPost")
    post(b, rng, 0, 0, hw)
    L = 0.45
    mapA = lambda u, z, v: (u, -(hw - 0.005) - v, z)      # arm A: toward -X on the -Y face
    mapB = lambda u, z, v: (hw - 0.005 + v, u, z)         # arm B: toward +Y on the +X face
    for k, zc in enumerate(RAIL_Z):
        for arm, (P, u0, u1) in enumerate(((mapA, -L, hw + 0.02), (mapB, -hw + 0.02, L))):
            h = RAIL_H + rng.uniform(-0.008, 0.008)
            zz = zc + rng.uniform(-0.012, 0.012)
            ang = (1 if (k + arm) % 2 == 0 else -1) * rng.uniform(0.02, 0.03)
            M = Matrix.Translation((0, 0, zz - h / 2)) @ Matrix.Rotation(ang, 4, 'Y' if arm == 0 else 'X')
            prism(b, rail_profile(rng, u0, u1, h), 0.0, RAIL_T, P, M)
            peg(b, P, M, 0.0, h / 2, RAIL_T, rng)
    return b.build()


def paint(ob, wood_normals):
    """Wood colour on big board faces (+-Y, top, extra normals), dark colour on sides/chips/undersides."""
    me = ob.data
    for p in me.polygons:
        if p.material_index == PEG:
            continue
        n = p.normal
        wood = abs(n.y) > 0.72 or n.z > 0.72 or any(n.dot(Vector(w)) > 0.72 for w in wood_normals)
        p.material_index = WOOD if wood else SIDE
    me.update()


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def bbox(ob):
    vs = [v.co for v in ob.data.vertices]
    return [round(max(getattr(v, a) for v in vs) - min(getattr(v, a) for v in vs), 3) for a in "xyz"]


def run():
    K.reset_scene()
    fence = make_fence(random.Random(21))
    corner = make_corner(random.Random(34))
    paint(fence, [])
    paint(corner, [(1, 0, 0)])
    print("RESULT fence tris", tris(fence), "bbox xyz", bbox(fence))
    print("RESULT corner tris", tris(corner), "bbox xyz", bbox(corner))
    prev = os.path.join(HERE, "preview.png")
    corner.rotation_euler = (0, 0, math.radians(-45))   # presentation yaw only: shows both arms' outer faces
    K.render_lineup([fence, corner], prev, cell=2.5, cols=2, res=(1024, 768), dist=6.4, target_z=0.5)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "fence.blend"))

    ours = bpy.data.images.load(prev)
    ref = bpy.data.images.load(os.path.join(ROOT, "Docs", "ref", "prop_fence_barrel.jpg"))
    ow, oh = ours.size
    a = np.array(ours.pixels[:], dtype=np.float32).reshape(oh, ow, 4)
    rw, rh = ref.size
    r = np.array(ref.pixels[:], dtype=np.float32).reshape(rh, rw, 4)
    x0, x1, y0, y1 = 228, 492, 8, 300     # fences only (image-top coords)
    crop = r[rh - y1:rh - y0, x0:x1]
    ch, cw = crop.shape[:2]
    sc = oh / ch
    nw = int(cw * sc)
    yi = (np.arange(oh) / sc).astype(int).clip(0, ch - 1)
    xi = (np.arange(nw) / sc).astype(int).clip(0, cw - 1)
    big = crop[yi][:, xi]
    out = np.concatenate([a, np.ones((oh, 12, 4), np.float32), big], axis=1)
    img = bpy.data.images.new("compare", out.shape[1], oh, alpha=False)
    img.pixels = out.ravel()
    img.filepath_raw = os.path.join(HERE, "compare.png")
    img.file_format = 'PNG'
    img.save()
    print("DONE")


run()
