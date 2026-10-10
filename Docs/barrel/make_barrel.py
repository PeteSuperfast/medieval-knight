"""Wooden barrel (octagonal) for medieval-knight. Run:
  blender --background --factory-startup --python make_barrel.py
Writes barrel.blend, preview.png, compare.png next to this script. No FBX export. Does not modify Art_kit/style_kit.py.
"""
import bpy, math, os, random, sys, inspect
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Art_kit"))
import style_kit as K

K.PAL.update({"barrel_wood": "#A1773D", "barrel_groove": "#715332", "barrel_iron": "#3A3A3A"})

H = 0.90            # height
A_MID = 0.320       # stave base apothem (flat-to-centre) at the middle, before relief -> ~0.65 m across flats
THICK = 0.030       # stave thickness
TAPER = 0.15        # ends are 15% narrower
HOOP_Z = (0.13, 0.77)
HOOP_H = 0.04
HOOP_OUT = 0.018    # hoop sticks out past the highest stave (a bit more so the tilted hoop still clears the staves)
RELIEF_MAX = 0.022  # per-stave outward relief, was 0.010 (v1)
TOP_DROP_MAX = 0.030  # per-stave top height variation, was 0.012 (v1); stays above the lid (lid is 0.035 down)
HOOP_TILT_DEG = 4.0  # each hoop tilted a little, in opposite directions on top / bottom
LID_DROP = 0.035
T225 = math.tan(math.radians(22.5))
C225 = math.cos(math.radians(22.5))


def f(z):
    t = z / H
    return 1 - TAPER * (2 * t - 1) ** 2


def pol(theta, u, a, z):
    """point at apothem a along direction theta, u along tangent."""
    c, s = math.cos(theta), math.sin(theta)
    return (a * c - u * s, a * s + u * c, z)


def clip(poly, axis, val, keep_ge):
    out = []
    n = len(poly)
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        ip = (p[axis] >= val) if keep_ge else (p[axis] <= val)
        iq = (q[axis] >= val) if keep_ge else (q[axis] <= val)
        if ip: out.append(p)
        if ip != iq:
            t = (val - p[axis]) / (q[axis] - p[axis])
            out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return out


def make_barrel(name, seed=5):
    rng = random.Random(seed)
    b = K.B(name)
    W, G, I = b.mi("barrel_wood"), b.mi("barrel_groove"), b.mi("barrel_iron")

    def add(verts, faces, m):
        base = len(b.v); b.v.extend(verts)
        for fc in faces:
            b.f.append(tuple(base + i for i in fc)); b.m.append(m)

    zs = (0.0, H / 2, H)
    # core (groove colour): octagon prism with bulge, top lowered to lid level
    lid_z = H - LID_DROP
    core_top = lid_z - 0.015
    a_core = lambda z: (A_MID - THICK + 0.001) * f(z)
    core_z = (0.0, H / 2, core_top)
    verts = []
    for z in core_z:
        a = a_core(z)
        for i in range(8):
            verts.append(pol(math.radians(45 * i + 22.5), 0, a / C225, z))
    faces = []
    for r in range(2):
        for i in range(8):
            j = (i + 1) % 8
            faces.append((r * 8 + i, r * 8 + j, (r + 1) * 8 + j, (r + 1) * 8 + i))
    faces.append(tuple(range(7, -1, -1)))
    faces.append(tuple(16 + i for i in range(8)))
    add(verts, faces, G)

    # staves (light wood), each with its own relief and top height
    max_relief = 0.0
    for i in range(8):
        th = math.radians(45 * i)
        relief = rng.uniform(0.0, RELIEF_MAX)
        top = H - rng.uniform(0.0, TOP_DROP_MAX)
        max_relief = max(max_relief, relief)
        zl = (0.0, H / 2, top)
        vs = []
        for z in zl:
            fz = f(z)
            ao = (A_MID + relief) * fz
            ai = (A_MID - THICK) * fz
            wo = ao * T225 - 0.008
            wi = ai * T225 - 0.008
            vs += [pol(th, -wo, ao, z), pol(th, wo, ao, z), pol(th, wi, ai, z), pol(th, -wi, ai, z)]
        # per ring: 0 outerL, 1 outerR, 2 innerR, 3 innerL
        fcs = []
        for r in range(2):
            o, n = r * 4, (r + 1) * 4
            fcs += [(o + 0, o + 1, n + 1, n + 0),   # outer
                    (o + 1, o + 2, n + 2, n + 1),   # right side
                    (o + 2, o + 3, n + 3, n + 2),   # inner
                    (o + 3, o + 0, n + 0, n + 3)]   # left side
        fcs += [(3, 2, 1, 0), (8, 9, 10, 11)]
        add(vs, fcs, W)

    # lid: 4 planks inside the mouth, slightly uneven heights
    a_lid = a_core(lid_z) * 1.0
    octo = [pol(math.radians(45 * i + 22.5), 0, a_lid / C225, 0)[:2] for i in range(8)]
    R = a_lid / C225
    edges = [-R, -R / 2, 0.0, R / 2, R]
    for k in range(4):
        x0, x1 = edges[k] + 0.004, edges[k + 1] - 0.004
        p = clip(clip(octo, 0, x0, True), 0, x1, False)
        if len(p) < 3: continue
        zt = lid_z + rng.uniform(-0.006, 0.004)
        zb = core_top - 0.002
        n = len(p)
        vs = [(x, y, zb) for x, y in p] + [(x, y, zt) for x, y in p]
        fcs = [tuple(range(n - 1, -1, -1)), tuple(n + i for i in range(n))]
        for i in range(n):
            j = (i + 1) % n
            fcs.append((i, j, n + j, n + i))
        add(vs, fcs, G)

    # iron hoops: octagonal prism poking out past the staves
    from mathutils import Euler
    for hi, hz in enumerate(HOOP_Z):
        z0, z1 = hz - HOOP_H / 2, hz + HOOP_H / 2
        r0 = (A_MID + 0.010 + HOOP_OUT) * f(z0) + 0.0
        r1 = (A_MID + 0.010 + HOOP_OUT) * f(z1) + 0.0
        # keep a constant stick-out above the local stave surface
        r0 = max(r0, (A_MID + max_relief) * f(z0) + HOOP_OUT * 0.9)
        r1 = max(r1, (A_MID + max_relief) * f(z1) + HOOP_OUT * 0.9)
        vs = [pol(math.radians(45 * i + 22.5), 0, r0 / C225, z0) for i in range(8)] + \
             [pol(math.radians(45 * i + 22.5), 0, r1 / C225, z1) for i in range(8)]
        # tilt the hoop about its own centre: tilt axis direction differs per hoop
        sgn = 1 if hi == 0 else -1
        rot = Euler((math.radians(HOOP_TILT_DEG) * sgn, math.radians(HOOP_TILT_DEG * 0.6) * -sgn, 0)).to_matrix()
        vs = [tuple(rot @ Vector((x, y, z - hz)) + Vector((0, 0, hz))) for x, y, z in vs]
        fcs = [tuple(range(7, -1, -1)), tuple(8 + i for i in range(8))]
        for i in range(8):
            j = (i + 1) % 8
            fcs.append((i, j, 8 + j, 8 + i))
        add(vs, fcs, I)

    # keep material order stable
    return b.build()


def stats(ob):
    me = ob.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    return tris


def run():
    K.reset_scene()
    up = make_barrel("Barrel_Upright", seed=5)
    lying = make_barrel("Barrel_Lying", seed=9)
    for o in (up, lying):
        o.data.update()
    # report geometry before placing
    def extent(ob, lo, hi):
        pts = [v.co for v in ob.data.vertices if lo <= v.co.z <= hi]
        return (max(p.x for p in pts) - min(p.x for p in pts), max(p.y for p in pts) - min(p.y for p in pts))
    zs = [v.co.z for v in up.data.vertices]
    print("RESULT up tris", stats(up), "lying tris", stats(lying))
    print("RESULT height", round(max(zs) - min(zs), 3))
    # stave-only widths at given z by interpolating the stave faces (use W material faces)
    def width_at(ob, z):
        xs = []
        for p in ob.data.polygons:
            if ob.data.materials[p.material_index].name != "MK_barrel_wood": continue
            vs = [ob.data.vertices[i].co for i in p.vertices]
            if len(vs) != 4: continue
            for a, c in zip(vs, vs[1:] + vs[:1]):
                if (a.z - z) * (c.z - z) <= 0 and a.z != c.z:
                    t = (z - a.z) / (c.z - a.z)
                    xs.append((a.x + t * (c.x - a.x), a.y + t * (c.y - a.y)))
        return (max(p[0] for p in xs) - min(p[0] for p in xs), max(p[1] for p in xs) - min(p[1] for p in xs))
    print("RESULT width_mid(x,y)", [round(v, 3) for v in width_at(up, H / 2)])
    print("RESULT width_mouth(x,y)", [round(v, 3) for v in width_at(up, H - 0.015)])
    print("RESULT width_base(x,y)", [round(v, 3) for v in width_at(up, 0.0005)])
    print("RESULT hoop_outer_width_x", round(max(v.co.x for v in up.data.vertices if v.co.z > 0.1 and v.co.z < 0.2) * 2, 3))

    # lying: roll onto its side (axis along X), flat face down, yaw a bit
    lying.rotation_euler = (0, math.radians(90), 0)
    bpy.context.view_layer.update()
    mn = min((lying.matrix_world @ v.co).z for v in lying.data.vertices)
    lying.data.transform(lying.matrix_world)
    lying.rotation_euler = (0, 0, 0)
    for v in lying.data.vertices:
        v.co.z -= mn
    # origin to bottom centre of the footprint
    xs = [v.co.x for v in lying.data.vertices]; ys = [v.co.y for v in lying.data.vertices]
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    for v in lying.data.vertices:
        v.co.x -= cx; v.co.y -= cy
    lying.data.update()
    lying.rotation_euler = (0, 0, math.radians(-28))
    print("RESULT lying height", round(max(v.co.z for v in lying.data.vertices), 3))

    # render with kit light/sky/camera (only camera distance/target tuned for a close-up)
    src = inspect.getsource(K.render_lineup)
    src = src.replace("dist = 7.5 + cell * max(cols, rows) * 1.45", "dist = 5.2")
    src = src.replace("Vector((0, 0, 0.8))", "Vector((0, 0, 0.35))")
    ns = K.__dict__
    exec(src, ns)
    prev = os.path.join(HERE, "preview.png")
    ns["render_lineup"]([up, lying], prev, cell=1.5, cols=2)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "barrel.blend"))

    # compare.png: ours | reference crop (barrels only)
    ours = bpy.data.images.load(prev)
    ref = bpy.data.images.load(os.path.join(ROOT, "Docs", "ref", "prop_fence_barrel.jpg"))
    ow, oh = ours.size
    a = np.array(ours.pixels[:], dtype=np.float32).reshape(oh, ow, 4)
    rw, rh = ref.size
    r = np.array(ref.pixels[:], dtype=np.float32).reshape(rh, rw, 4)
    # crop barrels: image-top coords x 250..475, y 280..480 -> bottom-up rows
    x0, x1, y0, y1 = 250, 475, 280, 480
    crop = r[rh - y1:rh - y0, x0:x1]
    ch, cw = crop.shape[:2]
    # nearest-neighbour upscale to height oh
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
