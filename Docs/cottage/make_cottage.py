"""Cottage (main massing only) for medieval-knight. Run:
  blender --background --factory-startup --python make_cottage.py
Writes cottage.blend, preview_front.png, preview_back.png, compare.png next to this script.
No FBX export. Does not modify Art_kit/style_kit.py. Seeded (fixed).
Axes: Z up, front (door) faces -Y, chimney on +X side, ridge runs along X. Origin = centre of base at ground.
"""
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Art_kit"))
import style_kit as K

# roof colour as ordered by owner; opening_dark = palette "iron dark" #3A3A3A (same hex as barrel_iron)
K.PAL.update({"roof_purple": "#5E5878", "opening_dark": "#3A3A3A"})

# ---- dimensions (m)
BASE_X, BASE_Y, BASE_H = 5.0, 4.0, 0.80          # stone plinth
WALL_H = 2.50                                    # plaster wall above plinth
WALL_BOT = (4.70, 3.70)                          # wall footprint at the bottom (plinth sticks out 0.15 m)
WALL_TOP = (4.50, 3.50)                          # leans inward 0.1 m per side
ROOF_RIDGE_Z = 6.50
ROOF_OVER = 0.50
ROOF_T = 0.20                                    # perpendicular thickness
ROOF_SAG = 0.30                                  # ridge dips this much at the middle
CH_TOP_Z = 7.40
Z_WALL_TOP = BASE_H + WALL_H                     # 3.30
DOOR = dict(x0=-0.55, x1=0.55, z0=-0.2, z1=2.10)
WINDOWS = [dict(x0=-1.8, x1=-1.0, z0=1.55, z1=2.45), dict(x0=1.0, x1=1.8, z0=1.55, z1=2.45)]
POCKET_BACK_Y = -1.55


def box_mesh(name, x0, x1, y0, y1, z0, z1):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector(((x1 if v.co.x > 0 else x0), (y1 if v.co.y > 0 else y0), (z1 if v.co.z > 0 else z0)))
    bm.to_mesh(me); bm.free()
    return me


def cut(solid_ob, cutters):
    """Boolean difference (exact). Returns evaluated mesh copy."""
    for c in cutters:
        md = solid_ob.modifiers.new("cut", 'BOOLEAN')
        md.operation = 'DIFFERENCE'; md.solver = 'EXACT'; md.object = c
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    return bpy.data.meshes.new_from_object(solid_ob.evaluated_get(dg))


def in_box(p, bb, e=0.02):
    return (bb[0] - e <= p.x <= bb[1] + e and bb[2] - e <= p.y <= bb[3] + e and bb[4] - e <= p.z <= bb[5] + e)


def loft_roof(b, rng):
    """Gable roof slab, ridge along Y (gable faces front/back), sagging ridge, lofted over N stations."""
    half_x = WALL_TOP[0] / 2 + ROOF_OVER      # across the ridge
    half_y = WALL_TOP[1] / 2 + ROOF_OVER      # along the ridge
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
    return half_x, half_y, slope


def run():
    rng = random.Random(7)
    K.reset_scene()
    sc = bpy.context.scene

    # ---- stone plinth (slightly irregular frustum), wall frustum (leans inward)
    pb = K.B("tmp_base")
    bx, by = BASE_X / 2, BASE_Y / 2
    pb.solid([Vector(p) for p in [(-bx, -by, -0.05), (bx, -by, -0.05), (bx, by, -0.05), (-bx, by, -0.05)]],
             [Vector(p) for p in [(-bx + 0.05, -by + 0.04, BASE_H), (bx - 0.04, -by + 0.04, BASE_H),
                                   (bx - 0.04, by - 0.05, BASE_H), (-bx + 0.05, by - 0.05, BASE_H)]], "rock_side")
    base_ob = pb.build()
    wb = K.B("tmp_wall")
    w0x, w0y = WALL_BOT[0] / 2, WALL_BOT[1] / 2
    w1x, w1y = WALL_TOP[0] / 2, WALL_TOP[1] / 2
    sx, sy = 0.04, -0.02    # top shifted a little: not perfectly vertical
    wb.solid([Vector(p) for p in [(-w0x, -w0y, BASE_H - 0.05), (w0x, -w0y, BASE_H - 0.05),
                                   (w0x, w0y, BASE_H - 0.05), (-w0x, w0y, BASE_H - 0.05)]],
             [Vector(p) for p in [(-w1x + sx, -w1y + sy, Z_WALL_TOP), (w1x + sx, -w1y + sy, Z_WALL_TOP),
                                   (w1x + sx, w1y + sy, Z_WALL_TOP), (-w1x + sx, w1y + sy, Z_WALL_TOP)]], "plaster_cream")
    wall_ob = wb.build()

    # ---- openings: cutters
    door_c = bpy.data.objects.new("c_door", box_mesh("c_door", DOOR["x0"], DOOR["x1"], -2.4, POCKET_BACK_Y, DOOR["z0"], DOOR["z1"]))
    win_c = [bpy.data.objects.new("c_win%d" % i, box_mesh("c_win%d" % i, w["x0"], w["x1"], -2.4, POCKET_BACK_Y - 0.05, w["z0"], w["z1"]))
             for i, w in enumerate(WINDOWS)]
    for c in [door_c] + win_c: sc.collection.objects.link(c)
    cut_boxes_door = (DOOR["x0"], DOOR["x1"], -2.4, POCKET_BACK_Y, DOOR["z0"], DOOR["z1"])
    cut_boxes_win = [(w["x0"], w["x1"], -2.4, POCKET_BACK_Y - 0.05, w["z0"], w["z1"]) for w in WINDOWS]

    base_me = cut(base_ob, [door_c])
    wall_me = cut(wall_ob, [door_c] + win_c)

    # ---- assemble into one mesh with kit materials
    b = K.B("Cottage")
    def absorb(me, painter):
        off = len(b.v)
        b.v += [tuple(v.co) for v in me.vertices]
        for p in me.polygons:
            b.f.append(tuple(off + i for i in p.vertices))
            b.m.append(b.mi(painter(p)))

    allcut = [cut_boxes_door] + cut_boxes_win
    def is_pocket(p, me):
        # pocket faces: every vertex inside a cutter box, and not the outer front plane
        if abs(p.normal.y) > 0.9 and p.center.y < POCKET_BACK_Y - 0.1:
            return False
        for bb in allcut:
            if all(in_box(me.vertices[i].co, bb, 0.03) for i in p.vertices): return True
        return False
    def paint_base(p):
        if is_pocket(p, base_me): return "opening_dark"
        return "rock_top" if p.normal.z > 0.5 else "rock_side"
    def paint_wall(p):
        if is_pocket(p, wall_me): return "opening_dark"
        return "plaster_cream"
    absorb(base_me, paint_base)
    absorb(wall_me, paint_wall)

    # gable fills (plain plaster triangles under the roof, at front and back ends)
    for sgn in (-1, 1):
        y_out = sgn * (w1y - 0.01) + sy
        y_in = sgn * (w1y - 0.31) + sy
        prof = [(-w1x + 0.01 + sx, Z_WALL_TOP - 0.1), (w1x - 0.01 + sx, Z_WALL_TOP - 0.1), (0.0 + sx, 5.80)]
        base = len(b.v)
        for y in (y_in, y_out):
            for (x, z) in prof: b.v.append((x, y, z))
        m = b.mi("plaster_cream")
        b.f.append((base + 2, base + 1, base + 0)); b.m.append(m)
        b.f.append((base + 3, base + 4, base + 5)); b.m.append(m)
        for i in range(3):
            j = (i + 1) % 3
            b.f.append((base + i, base + j, base + 3 + j, base + 3 + i)); b.m.append(m)

    # ---- chimney: stone, +X side, tapered, with a wider cap, rises well above the ridge
    ch = K.B("tmp_chimney")
    cj = lambda: rng.uniform(-0.02, 0.02)
    bot = [Vector(p) for p in [(2.20, -0.50, 0.0), (3.15, -0.50 + cj(), 0.0), (3.15, 0.50, 0.0), (2.20, 0.50, 0.0)]]
    top = [Vector(p) for p in [(2.20, -0.38, 7.0), (2.92, -0.38 + cj(), 7.0), (2.92 + 0.03, 0.40, 7.0), (2.20, 0.40, 7.0)]]
    cap0 = [Vector(p) for p in [(2.12, -0.46, 7.0), (3.02, -0.46, 7.0), (3.02, 0.48, 7.0), (2.12, 0.48, 7.0)]]
    cap1 = [Vector(p) for p in [(2.14, -0.44, CH_TOP_Z), (3.00, -0.44, CH_TOP_Z), (3.00, 0.46, CH_TOP_Z), (2.14, 0.46, CH_TOP_Z)]]
    ch.solid(bot, top, "rock_side")
    ch.solid(cap0, cap1, "rock_side")
    ch_ob = ch.build()
    def paint_ch(p): return "rock_top" if p.normal.z > 0.5 else "rock_side"
    absorb(ch_ob.data, paint_ch)

    # ---- roof
    half_x, half_y, slope = loft_roof(b, rng)

    # clean up temp objects, build final
    for o in (base_ob, wall_ob, ch_ob, door_c, *win_c):
        bpy.data.objects.remove(o)
    house = b.build()
    house.name = "Cottage"
    for p in house.data.polygons: p.use_smooth = False

    vs = house.data.vertices
    ext = lambda a: (min(getattr(v.co, a) for v in vs), max(getattr(v.co, a) for v in vs))
    tris = sum(len(p.vertices) - 2 for p in house.data.polygons)
    print("RESULT tris", tris)
    print("RESULT bbox x", [round(t, 3) for t in ext("x")], "y", [round(t, 3) for t in ext("y")], "z", [round(t, 3) for t in ext("z")])
    roof_z = [v.co.z for v in vs if v.co.z > 2.0 and abs(v.co.x) < 2.8 and False]
    print("RESULT roof half_x/half_y", round(half_x, 3), round(half_y, 3), "slope deg", round(math.degrees(math.atan(slope)), 1))
    print("RESULT materials", [m.name for m in house.data.materials])

    # ---- renders with kit light/sky/camera; only dist/target_z tuned for the house, house yawed per view
    house.rotation_euler = (0, 0, math.radians(-20))
    pf = os.path.join(HERE, "preview_front.png")
    K.render_lineup([house], pf, cell=1, cols=1, res=(1024, 768), dist=21, target_z=3.4)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "cottage.blend"))

    for o in list(sc.objects):
        if o.name.startswith(("Ground_Flat", "Sun_Warm", "Cam")): bpy.data.objects.remove(o)
    house.rotation_euler = (0, 0, math.radians(240))
    pb_ = os.path.join(HERE, "preview_back.png")
    K.render_lineup([house], pb_, cell=1, cols=1, res=(1024, 768), dist=21, target_z=3.4)
    house.rotation_euler = (0, 0, math.radians(-20))

    # ---- compare.png: front | back | reference tower
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
    img = bpy.data.images.new("compare", out.shape[1], oh, alpha=False)
    img.pixels = out.ravel()
    img.filepath_raw = os.path.join(HERE, "compare.png")
    img.file_format = 'PNG'
    img.save()
    print("DONE")


run()
