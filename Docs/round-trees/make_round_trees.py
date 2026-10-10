"""Round-bush trees (2 variants) for medieval-knight. Run:
  blender --background --factory-startup --python make_round_trees.py
Writes round_trees.blend, preview.png, compare.png next to this script. No FBX export.
Does not modify Art_kit/style_kit.py. Fixed seeds.
"""
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Art_kit"))
import style_kit as K

K.PAL.update({"leaf_under": "#6C8535"})   # leaf_light #A0B85B (top), leaf_under #6C8535, wood_dark #715332 already in kit
TOP_NZ = 0.3   # leaf faces with normal.z above this get the light colour


def tube(b, pts, radii, n, rng, mat="wood_dark"):
    """Closed tapered tube along pts (list of Vector). Returns nothing; appends to builder b."""
    base = len(b.v)
    for k, (p, r) in enumerate(zip(pts, radii)):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        ref = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
        u = d.cross(ref).normalized(); w = d.cross(u).normalized()
        rot = rng.uniform(0, 1)
        for i in range(n):
            a = 2 * math.pi * i / n + rot * 0.3
            rr = r * rng.uniform(0.9, 1.08)
            b.v.append(tuple(p + u * (rr * math.cos(a)) + w * (rr * math.sin(a))))
    m = b.mi(mat)
    for k in range(len(pts) - 1):
        for i in range(n):
            j = (i + 1) % n
            o, q = base + k * n, base + (k + 1) * n
            b.f.append((o + i, o + j, q + j, q + i)); b.m.append(m)
    b.f.append(tuple(base + i for i in range(n - 1, -1, -1))); b.m.append(m)
    last = base + (len(pts) - 1) * n
    b.f.append(tuple(last + i for i in range(n))); b.m.append(m)


def blob(b, c, radii, rng, jit=0.12):
    """Faceted ellipsoid chunk: icosphere(subdiv 2, 80 tris) with per-vertex jitter, random yaw, slight tilt."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    R = (Matrix.Rotation(rng.uniform(0, 6.28), 3, 'Z') @ Matrix.Rotation(rng.uniform(-.2, .2), 3, 'X')
         @ Matrix.Rotation(rng.uniform(-.2, .2), 3, 'Y'))
    base = len(b.v)
    idx = {}
    for v in bm.verts:
        s = 1 + rng.uniform(-jit, jit)
        p = Vector((v.co.x * radii[0] * s, v.co.y * radii[1] * s, v.co.z * radii[2] * s))
        idx[v.index] = len(b.v)
        b.v.append(tuple(c + R @ p))
    m = b.mi("leaf_light")
    for f in bm.faces:
        b.f.append(tuple(idx[v.index] for v in f.verts)); b.m.append(m)
    bm.free()


def curve_pt(base, lean, h, bend, t):
    """point on a gently curved trunk: leans toward 'lean' (xy vector) with an S-ish bend."""
    off = lean * (t * t) + Vector((-lean.y, lean.x)) * (bend * math.sin(t * math.pi))
    return Vector((base.x + off.x, base.y + off.y, h * t))


def make_tree(name, seed, trunk_h, r0, r1, lean, bend, branches, blobs):
    """branches: list of (t_on_trunk, blob_index). blobs: list of (centre xyz, radii xyz)."""
    rng = random.Random(seed)
    b = K.B(name)
    b.mi("wood_dark"); b.mi("leaf_light"); b.mi("leaf_under")
    lean = Vector(lean)
    N = 4
    pts = [curve_pt(Vector((0, 0)), lean, trunk_h, bend, k / N) for k in range(N + 1)]
    radii = [r0 + (r1 - r0) * k / N for k in range(N + 1)]
    tube(b, pts, radii, 6, rng)
    for t, bi in branches:
        p = curve_pt(Vector((0, 0)), lean, trunk_h, bend, t)
        tgt = Vector(blobs[bi][0]); tgt.z -= blobs[bi][1][2] * 0.25
        mid = (p + tgt) / 2 + Vector((rng.uniform(-.12, .12), rng.uniform(-.12, .12), 0.12))
        rb = r1 + (r0 - r1) * (1 - t) * 0.6
        tube(b, [p, mid, tgt], [rb * 0.7, rb * 0.5, 0.07], 5, rng)
    for c, r in blobs:
        blob(b, Vector(c), r, rng)
    ob = b.build()
    for p in ob.data.polygons:
        if ob.data.materials[p.material_index].name == "MK_wood_dark": continue
        nm = "leaf_light" if p.normal.z > TOP_NZ else "leaf_under"
        p.material_index = b.mats.index(nm)
    return ob


def run():
    K.reset_scene()
    # (a) slender: 4 blobs stacked, shrinking upward, drifting a bit
    a = make_tree("Tree_Round_Slender", 3, trunk_h=3.4, r0=0.22, r1=0.11, lean=(0.45, 0.2), bend=0.22,
                  branches=[(0.45, 0), (0.65, 1)],
                  blobs=[((-0.45, 0.10, 2.75), (1.25, 1.15, 0.95)),
                         ((0.7, -0.1, 3.4), (1.0, 0.95, 0.8)),
                         ((0.1, 0.25, 4.1), (0.8, 0.78, 0.65)),
                         ((0.95, 0.4, 2.6), (0.65, 0.6, 0.5))])
    # (b) low wide: short thick trunk, 3 branches, 5 wide blobs
    w = make_tree("Tree_Round_Wide", 8, trunk_h=2.3, r0=0.36, r1=0.2, lean=(-0.35, 0.3), bend=-0.2,
                  branches=[(0.5, 0), (0.7, 1), (0.9, 2)],
                  blobs=[((-1.5, -0.2, 2.5), (1.5, 1.35, 1.0)),
                         ((1.4, 0.35, 2.55), (1.45, 1.3, 0.95)),
                         ((-0.2, 1.0, 3.0), (1.35, 1.2, 0.95)),
                         ((-0.35, -0.9, 3.3), (1.25, 1.1, 0.9)),
                         ((-0.1, 0.1, 3.75), (1.1, 1.0, 0.8))])
    objs = [a, w]
    for o in objs:
        zs = [v.co.z for v in o.data.vertices]
        leaf = {v for p in o.data.polygons if o.data.materials[p.material_index].name != "MK_wood_dark" for v in p.vertices}
        xs = [o.data.vertices[i].co.x for i in leaf]; ys = [o.data.vertices[i].co.y for i in leaf]
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        print("RESULT", o.name, "tris", tris, "height", round(max(zs) - min(zs), 2),
              "canopy_x", round(max(xs) - min(xs), 2), "canopy_y", round(max(ys) - min(ys), 2), "min_z", round(min(zs), 3))
    K.render_lineup(objs, os.path.join(HERE, "preview.png"), cell=7.0, cols=2, res=(1280, 960), dist=24, target_z=2.3)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "round_trees.blend"))

    def load(p):
        im = bpy.data.images.load(p); w_, h_ = im.size
        return np.array(im.pixels[:], dtype=np.float32).reshape(h_, w_, 4)
    def fit(img, H):
        h_, w_ = img.shape[:2]; s = H / h_; nw = max(1, int(w_ * s))
        return img[(np.arange(H) / s).astype(int).clip(0, h_ - 1)][:, (np.arange(nw) / s).astype(int).clip(0, w_ - 1)]
    H = 960
    parts = [load(os.path.join(HERE, "preview.png")),
             load(os.path.join(ROOT, "Docs", "ref", "env_nature_kit.jpg")),
             load(os.path.join(ROOT, "Docs", "ref", "env_forest_scene.jpg"))]
    row = []
    for i, p in enumerate(parts):
        if i: row.append(np.ones((H, 12, 4), np.float32))
        row.append(fit(p, H))
    out = np.concatenate(row, axis=1)
    img = bpy.data.images.new("compare", out.shape[1], H, alpha=False)
    img.pixels = out.ravel()
    img.filepath_raw = os.path.join(HERE, "compare.png"); img.file_format = 'PNG'; img.save()
    print("DONE")


run()
