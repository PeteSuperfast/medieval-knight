"""Medieval Knight art kit: approved asset builders (Blender, no add-ons).

Use:  blender -b -P Art_kit/style_kit.py -- <asset> [<asset> ...]
      assets: pine   (writes Assets/Models/Tree_Pine.fbx)
              rocks  (writes Assets/Models/Rocks/Rock_{Small,Medium,Large}_{A,B,C}.fbx
                      + Art_kit/previews/Rocks_variants.png)
              barrels (writes Assets/Models/Props/Barrel_{Upright,Lying}.fbx + previews/Barrels.png)
              fence   (writes Assets/Models/Props/Fence_Straight_2m.fbx, Fence_CornerPost.fbx
                       + previews/Fence.png)
              props   (writes Assets/Models/Props/Mushroom_Red_Cluster, Grass_Tuft, Stepping_Stone,
                       Tree_Stump .fbx + previews/Small_props.png)
              round_trees (writes Assets/Models/Tree_Round_Slender.fbx, Tree_Round_Wide.fbx
                       + previews/Round_trees.png)
From another bpy script:  import style_kit as K; K.reset_scene(); ob = K.make_pine()
                          ob = K.make_rock("medium", seed=123)   # any seed -> new rock

Palette comes from Docs/style.md. Flat colour materials, flat shading, no textures/UVs.
Origin of every asset = centre of its base, Z up, 1 unit = 1 m.
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, Matrix, Euler

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, "Assets", "Models")

PAL = {
    "grass_light": "#8DB04A", "grass_mid": "#6C8535", "pine_dark": "#465820", "leaf_light": "#A0B85B",
    "wood_dark": "#715332",
    "rock_top": "#ADABB7", "rock_side": "#958A80",
    "barrel_wood": "#A1773D", "barrel_groove": "#715332", "barrel_iron": "#3A3A3A",
    "fence_wood": "#A1773D", "fence_side": "#715332", "fence_peg": "#3A3A3A",
    "mushroom_red": "#C8483A", "plaster_cream": "#D8CCB0", "wood_light": "#A1773D",
    "leaf_under": "#6C8535",
}
MATS = {}


def lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]


def reset_scene():
    """Empty scene + the kit's flat materials (MK_<name>)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    for k, v in PAL.items():
        m = bpy.data.materials.new("MK_" + k)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = lin(v)
        b.inputs["Roughness"].default_value = 0.9
        MATS[k] = m


class B:
    """Tiny mesh builder: solids from rings, flat shaded, per-face material."""

    def __init__(self, name):
        self.name = name; self.v = []; self.f = []; self.m = []; self.mats = []

    def mi(self, n):
        if n not in self.mats: self.mats.append(n)
        return self.mats.index(n)

    def solid(self, bot, top, mat):
        """bot: ring of points; top: ring of points or a single apex Vector."""
        n = len(bot); base = len(self.v)
        self.v += [tuple(p) for p in bot]
        if isinstance(top, Vector):
            self.v.append(tuple(top)); a = base + n
            for i in range(n):
                self.f.append((base + i, base + (i + 1) % n, a)); self.m.append(self.mi(mat))
        else:
            self.v += [tuple(p) for p in top]
            for i in range(n):
                j = (i + 1) % n
                self.f.append((base + i, base + j, base + n + j, base + n + i)); self.m.append(self.mi(mat))
            self.f.append(tuple(base + n + i for i in range(n))); self.m.append(self.mi(mat))
        self.f.append(tuple(reversed([base + i for i in range(n)]))); self.m.append(self.mi(mat))

    def cyl(self, M, r0, r1, h, n, mat, jit=0.0, rot=0.0):
        def ring(r, z):
            return [Vector((r * math.cos(rot + 2 * math.pi * i / n) + random.uniform(-jit, jit),
                            r * math.sin(rot + 2 * math.pi * i / n) + random.uniform(-jit, jit), z)) for i in range(n)]
        self.solid([M @ p for p in ring(r0, 0)], [M @ p for p in ring(r1, h)], mat)

    def build(self):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.v, [], self.f)
        me.update()
        for p, mi in zip(me.polygons, self.m):
            p.material_index = mi; p.use_smooth = False
        for n in self.mats: me.materials.append(MATS[n])
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        bm = bmesh.new(); bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free()
        return ob


def T(x=0, y=0, z=0, rx=0, ry=0, rz=0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(rz, 4, 'Z') @ Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X')


def pine_tier(b, z, r, h, tilt_x, tilt_y, rot, mat):
    """Jagged skirt cone: alternating long/short rim points, tilted, apex slightly off-centre."""
    n = 10
    M = T(random.uniform(-.04, .04), random.uniform(-.04, .04), z, rx=tilt_x, ry=tilt_y)
    bot = []
    for i in range(n):
        a = rot + 2 * math.pi * i / n
        rr = r * (1.0 if i % 2 == 0 else 0.78) * random.uniform(0.93, 1.05)
        bot.append(M @ Vector((rr * math.cos(a), rr * math.sin(a), (0 if i % 2 == 0 else 0.12))))
    b.solid(bot, M @ Vector((0, 0, h)), mat)


def make_pine(seed=7):
    """Tree_Pine: ~4.0 m tall, 92 tris. Approved. Do not change numbers without owner OK."""
    random.seed(seed)
    b = B("Tree_Pine")
    b.cyl(T(), 0.26, 0.15, 2.0, 6, "wood_dark", jit=0.015)
    # (base z, radius, height, tilt_x, tilt_y, material): 4 tiers shrinking upward, each tilted a little
    tiers = [(1.10, 1.30, 1.20, 0.07, -0.04, "pine_dark"),
             (1.80, 1.05, 1.15, -0.05, 0.08, "grass_mid"),
             (2.50, 0.80, 1.05, 0.06, 0.05, "pine_dark"),
             (3.15, 0.50, 0.85, -0.04, -0.06, "grass_mid")]
    for z, r, h, tx, ty, m in tiers:
        pine_tier(b, z, r, h, tx, ty, random.uniform(0, 1), m)
    return b.build()


# ---------------------------------------------------------------- rocks
# kind -> visible height m, footprint m, tris budget for the whole asset. Height / footprint jitter +-15% by seed.
ROCK_KINDS = {
    "small": dict(height=0.30, size=(0.50, 0.42), budget=60),
    "medium": dict(height=1.00, size=(1.30, 1.05), budget=150),
    "large": dict(height=2.50, size=(2.30, 1.90), budget=500),  # cluster of 2-3 blocks pressed together
}
ROCK_SINK = 0.12  # fraction of total height buried below ground (origin z = 0 is the ground)
ROCK_SEEDS = {"small": (3, 14, 27), "medium": (11, 42, 77), "large": (21, 58, 90)}  # variants A, B, C
ROCK_VARIANTS = "ABC"


def _rock_block(rng, height, size, budget, subdiv):
    """One faceted block: icosphere -> uneven squash -> box-ish push -> seeded jitter -> tilted flat top ->
    planar dissolve with the loosest angle that fits the tri budget."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    sx, sy = rng.uniform(0.9, 1.1), rng.uniform(0.9, 1.1)
    for v in bm.verts:
        x, y, z = v.co
        x = math.copysign(abs(x) ** 0.62, x); y = math.copysign(abs(y) ** 0.62, y)  # toward a box: steep sides
        z = math.copysign(abs(z) ** 0.8, z)
        x *= sx; y *= sy
        x += rng.uniform(-.14, .14); y += rng.uniform(-.14, .14); z += rng.uniform(-.12, .12)
        x += 0.12 * z; y -= 0.06 * z  # asymmetric skew
        ztop = 0.55 + 0.10 * x / max(sx, .1) - 0.06 * y / max(sy, .1)  # slightly tilted flat top
        v.co = Vector((x, y, max(min(z, ztop), -0.7)))
    me = bpy.data.meshes.new("rock_block"); bm.to_mesh(me); bm.free()
    total = height / (1 - ROCK_SINK)
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    ex, ey, ez = max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)
    zmin = min(zs)
    for v in me.vertices:
        v.co.x = (v.co.x - cx) * size[0] / ex
        v.co.y = (v.co.y - cy) * size[1] / ey
        v.co.z = (v.co.z - zmin) * total / ez - total * ROCK_SINK
    ob = bpy.data.objects.new("rock_tmp", me); bpy.context.scene.collection.objects.link(ob)
    md = ob.modifiers.new("Planar", 'DECIMATE'); md.decimate_type = 'DISSOLVE'
    dg = bpy.context.evaluated_depsgraph_get()
    best = 70
    for ang in range(4, 72, 2):
        md.angle_limit = math.radians(ang); dg.update()
        ev = ob.evaluated_get(dg); m2 = ev.to_mesh()
        b2 = bmesh.new(); b2.from_mesh(m2)
        n = sum(len(f.verts) - 2 for f in b2.faces)
        b2.free(); ev.to_mesh_clear()
        if n <= budget:
            best = ang; break
    md.angle_limit = math.radians(best); dg.update()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    return out


def make_rock(kind, seed, name=None):
    """kind: 'small' | 'medium' | 'large' (cluster). Same (kind, seed) -> same rock.
    Budgets 60 / 150 / 500 tris. Faces with normal.z > .5 use rock_top, the rest rock_side."""
    k = ROCK_KINDS[kind]
    rng = random.Random(seed * 1009 + len(kind))
    j = lambda: rng.uniform(0.85, 1.15)
    h = k["height"] * j(); size = (k["size"][0] * j(), k["size"][1] * j())
    blocks = []  # (mesh, x, y, rotz)
    if kind == "large":
        n = rng.choice((2, 3))
        blocks.append((_rock_block(rng, h, size, 250 if n == 3 else 330, 2), 0, 0, rng.uniform(0, 6.28)))
        a = rng.uniform(0, 6.28)
        for i in range(n - 1):
            hh = h * rng.uniform(0.45, 0.7)
            ss = (size[0] * rng.uniform(0.55, 0.7), size[1] * rng.uniform(0.55, 0.7))
            reach = (size[0] + ss[0]) * 0.36  # overlap so they read as one pile
            blocks.append((_rock_block(rng, hh, ss, 130 if n == 3 else 170, 2),
                           reach * math.cos(a), reach * math.sin(a), rng.uniform(0, 6.28)))
            a += rng.uniform(1.8, 3.4)
    else:
        blocks.append((_rock_block(rng, h, size, k["budget"], 1 if kind == "small" else 2), 0, 0, rng.uniform(0, 6.28)))
    bm = bmesh.new()
    for m, x, y, rz in blocks:
        t = bmesh.new(); t.from_mesh(m); t.transform(T(x, y, 0, rz=rz))
        t2 = bpy.data.meshes.new("t"); t.to_mesh(t2); t.free()
        bm.from_mesh(t2); bpy.data.meshes.remove(t2); bpy.data.meshes.remove(m)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2  # origin = centre of footprint
    for v in bm.verts: v.co.x -= cx; v.co.y -= cy
    me = bpy.data.meshes.new(name or "Rock"); bm.to_mesh(me); bm.free()
    me.materials.append(MATS["rock_top"]); me.materials.append(MATS["rock_side"])
    for p in me.polygons:
        p.use_smooth = False
        p.material_index = 0 if p.normal.z > 0.5 else 1
    ob = bpy.data.objects.new(name or "Rock", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def rock_name(kind, i):
    return "Rock_%s_%s" % (kind.capitalize(), ROCK_VARIANTS[i])


def render_lineup(objs, path, cell=4.5, cols=3, res=(1280, 960), dist=None, target_z=0.8):
    """Spread objects on a grass plate and render with the kit's light / sky / camera
    (warm sun, #B6C4DE -> #D5BFC3 sky, 32 deg elevation, 50 mm)."""
    sc = bpy.context.scene
    rows = (len(objs) + cols - 1) // cols
    for i, ob in enumerate(objs):
        ob.location = ((i % cols - (cols - 1) / 2) * cell, -((i // cols) - (rows - 1) / 2) * cell, 0)
    g = B("Ground_Flat"); g.cyl(T(0, 0, -0.06), 60, 60, 0.06, 24, "grass_light"); g.build()
    w = bpy.data.worlds.new("Sky"); sc.world = w; w.use_nodes = True
    nt = w.node_tree; nt.nodes.clear()
    tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(tc.outputs["Window"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].color = lin("#B6C4DE"); ramp.color_ramp.elements[1].color = lin("#D5BFC3")
    ramp.color_ramp.elements[0].position = 0.15; ramp.color_ramp.elements[1].position = 0.9
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"]); nt.links.new(bg.outputs[0], out.inputs[0])
    sd = bpy.data.lights.new("Sun_Warm", 'SUN'); sd.energy = 4.0; sd.color = (1.0, 0.82, 0.62); sd.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun_Warm", sd); sc.collection.objects.link(sun)
    sun.rotation_euler = Vector((12, 8, -8)).to_track_quat('-Z', 'Y').to_euler()
    cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam)
    sc.camera = cam; cd.type = 'PERSP'; cd.lens = 50
    t = Vector((0, 0, target_z)); e_, a_ = math.radians(32), math.radians(18)
    dist = dist or 7.5 + cell * max(cols, rows) * 1.45
    cam.location = t + Vector((dist * math.cos(e_) * math.sin(a_), -dist * math.cos(e_) * math.cos(a_), dist * math.sin(e_)))
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = res
    try: sc.render.engine = 'BLENDER_EEVEE'
    except Exception: sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def build_rocks():
    """Export 3 variants of each size to Assets/Models/Rocks and render a 3x3 contact sheet."""
    folder = os.path.join(MODELS, "Rocks")
    for kind in ("small", "medium", "large"):
        for i, seed in enumerate(ROCK_SEEDS[kind]):
            reset_scene()
            ob = make_rock(kind, seed, rock_name(kind, i))
            tris = len(ob.data.polygons)
            zs = [v.co.z for v in ob.data.vertices]
            xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]
            ok = "OK" if tris <= ROCK_KINDS[kind]["budget"] else "OVER_BUDGET"
            print("ROCK", ob.name, "seed", seed, "tris", tris, "/", ROCK_KINDS[kind]["budget"], ok,
                  "visible_h", round(max(zs), 2), "w", round(max(xs) - min(xs), 2), "d", round(max(ys) - min(ys), 2))
            print("EXPORTED", export_fbx(ob, folder=folder))
    reset_scene()
    objs = [make_rock(k, s, rock_name(k, i)) for k in ("small", "medium", "large") for i, s in enumerate(ROCK_SEEDS[k])]
    prev = os.path.join(ROOT, "Art_kit", "previews")
    os.makedirs(prev, exist_ok=True)
    render_lineup(objs, os.path.join(prev, "Rocks_variants.png"))
    print("PREVIEW", os.path.join(prev, "Rocks_variants.png"))


# ---------------------------------------------------------------- barrel
BARREL_H = 0.90          # height
BARREL_A_MID = 0.320     # stave apothem (flat-to-centre) at the middle before relief -> ~0.65 m across flats
BARREL_THICK = 0.030     # stave thickness
BARREL_TAPER = 0.15      # ends are 15% narrower
BARREL_HOOP_Z = (0.13, 0.77)
BARREL_HOOP_H = 0.04
BARREL_HOOP_OUT = 0.018  # hoop sticks out past the highest stave
BARREL_LID_DROP = 0.035  # lid sunk below the rim
BARREL_RELIEF_MAX = 0.022   # per-stave outward relief
BARREL_TOP_DROP_MAX = 0.030  # per-stave top height variation (stays above the lid)
BARREL_HOOP_TILT_DEG = 4.0   # hoops tilted a little, opposite directions top / bottom
BARREL_SEEDS = {"upright": 5, "lying": 9}  # approved
_T225 = math.tan(math.radians(22.5))
_C225 = math.cos(math.radians(22.5))


def _barrel_f(z):
    t = z / BARREL_H
    return 1 - BARREL_TAPER * (2 * t - 1) ** 2


def _pol(theta, u, a, z):
    c, s = math.cos(theta), math.sin(theta)
    return (a * c - u * s, a * s + u * c, z)


def _clip(poly, axis, val, keep_ge):
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


def make_barrel(name="Barrel", seed=5, lying=False):
    """Octagonal wooden barrel: 8 separate staves (hand-made relief / height jitter by seed), 2 tilted iron hoops,
    lid sunk 3.5 cm and split into 4 planks. 324 tris. Origin = centre of the base.
    lying=True: rolled onto a flat side, resting on the ground, footprint centred, no yaw."""
    H, A, TH = BARREL_H, BARREL_A_MID, BARREL_THICK
    rng = random.Random(seed)
    b = B(name)
    W, G, I = b.mi("barrel_wood"), b.mi("barrel_groove"), b.mi("barrel_iron")

    def add(verts, faces, m):
        base = len(b.v); b.v.extend(verts)
        for fc in faces:
            b.f.append(tuple(base + i for i in fc)); b.m.append(m)

    # core (groove colour): octagon prism with bulge, top below the lid
    lid_z = H - BARREL_LID_DROP
    core_top = lid_z - 0.015
    a_core = lambda z: (A - TH + 0.001) * _barrel_f(z)
    verts = []
    for z in (0.0, H / 2, core_top):
        a = a_core(z)
        for i in range(8):
            verts.append(_pol(math.radians(45 * i + 22.5), 0, a / _C225, z))
    faces = []
    for r in range(2):
        for i in range(8):
            j = (i + 1) % 8
            faces.append((r * 8 + i, r * 8 + j, (r + 1) * 8 + j, (r + 1) * 8 + i))
    faces.append(tuple(range(7, -1, -1)))
    faces.append(tuple(16 + i for i in range(8)))
    add(verts, faces, G)

    # staves, each with its own relief and top height
    max_relief = 0.0
    for i in range(8):
        th = math.radians(45 * i)
        relief = rng.uniform(0.0, BARREL_RELIEF_MAX)
        top = H - rng.uniform(0.0, BARREL_TOP_DROP_MAX)
        max_relief = max(max_relief, relief)
        vs = []
        for z in (0.0, H / 2, top):
            fz = _barrel_f(z)
            ao, ai = (A + relief) * fz, (A - TH) * fz
            wo, wi = ao * _T225 - 0.008, ai * _T225 - 0.008
            vs += [_pol(th, -wo, ao, z), _pol(th, wo, ao, z), _pol(th, wi, ai, z), _pol(th, -wi, ai, z)]
        fcs = []
        for r in range(2):
            o, n = r * 4, (r + 1) * 4
            fcs += [(o + 0, o + 1, n + 1, n + 0), (o + 1, o + 2, n + 2, n + 1),
                    (o + 2, o + 3, n + 3, n + 2), (o + 3, o + 0, n + 0, n + 3)]
        fcs += [(3, 2, 1, 0), (8, 9, 10, 11)]
        add(vs, fcs, W)

    # lid: 4 planks inside the mouth, slightly uneven heights
    R = a_core(lid_z) / _C225
    octo = [_pol(math.radians(45 * i + 22.5), 0, R, 0)[:2] for i in range(8)]
    edges = [-R, -R / 2, 0.0, R / 2, R]
    for k in range(4):
        p = _clip(_clip(octo, 0, edges[k] + 0.004, True), 0, edges[k + 1] - 0.004, False)
        if len(p) < 3: continue
        zt = lid_z + rng.uniform(-0.006, 0.004); zb = core_top - 0.002
        n = len(p)
        vs = [(x, y, zb) for x, y in p] + [(x, y, zt) for x, y in p]
        fcs = [tuple(range(n - 1, -1, -1)), tuple(n + i for i in range(n))]
        for i in range(n):
            j = (i + 1) % n
            fcs.append((i, j, n + j, n + i))
        add(vs, fcs, G)

    # iron hoops: octagonal prisms poking out past the staves, tilted
    for hi, hz in enumerate(BARREL_HOOP_Z):
        z0, z1 = hz - BARREL_HOOP_H / 2, hz + BARREL_HOOP_H / 2
        r0 = max((A + 0.010 + BARREL_HOOP_OUT) * _barrel_f(z0), (A + max_relief) * _barrel_f(z0) + BARREL_HOOP_OUT * 0.9)
        r1 = max((A + 0.010 + BARREL_HOOP_OUT) * _barrel_f(z1), (A + max_relief) * _barrel_f(z1) + BARREL_HOOP_OUT * 0.9)
        vs = [_pol(math.radians(45 * i + 22.5), 0, r0 / _C225, z0) for i in range(8)] + \
             [_pol(math.radians(45 * i + 22.5), 0, r1 / _C225, z1) for i in range(8)]
        sgn = 1 if hi == 0 else -1
        rot = Euler((math.radians(BARREL_HOOP_TILT_DEG) * sgn, math.radians(BARREL_HOOP_TILT_DEG * 0.6) * -sgn, 0)).to_matrix()
        vs = [tuple(rot @ Vector((x, y, z - hz)) + Vector((0, 0, hz))) for x, y, z in vs]
        fcs = [tuple(range(7, -1, -1)), tuple(8 + i for i in range(8))]
        for i in range(8):
            j = (i + 1) % 8
            fcs.append((i, j, 8 + j, 8 + i))
        add(vs, fcs, I)

    ob = b.build()
    if lying:  # bake: roll 90 deg about Y, rest on ground, centre the footprint
        me = ob.data
        me.transform(Matrix.Rotation(math.radians(90), 4, 'Y'))
        zmin = min(v.co.z for v in me.vertices)
        xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
        cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
        for v in me.vertices:
            v.co.x -= cx; v.co.y -= cy; v.co.z -= zmin
        me.update()
    return ob


def build_barrels():
    """Export upright + lying barrel to Assets/Models/Props."""
    folder = os.path.join(MODELS, "Props")
    for key, lying in (("upright", False), ("lying", True)):
        reset_scene()
        ob = make_barrel("Barrel_" + key.capitalize(), BARREL_SEEDS[key], lying=lying)
        zs = [v.co.z for v in ob.data.vertices]
        print("BARREL", ob.name, "tris", sum(len(p.vertices) - 2 for p in ob.data.polygons), "height", round(max(zs) - min(zs), 3))
        print("EXPORTED", export_fbx(ob, folder=folder))
    reset_scene()
    objs = [make_barrel("Barrel_Upright", BARREL_SEEDS["upright"]),
            make_barrel("Barrel_Lying", BARREL_SEEDS["lying"], lying=True)]
    prev = os.path.join(ROOT, "Art_kit", "previews")
    os.makedirs(prev, exist_ok=True)
    render_lineup(objs, os.path.join(prev, "Barrels.png"), cell=1.5, cols=2, res=(1280, 960), dist=5.2, target_z=0.35)
    print("PREVIEW", os.path.join(prev, "Barrels.png"))


# ---------------------------------------------------------------- fence
FENCE_POST_H = 1.10
FENCE_RAIL_T = 0.06       # rail thickness
FENCE_RAIL_H = 0.13       # rail board height
FENCE_RAIL_Z = (0.35, 0.78)
FENCE_LEN = 2.0
FENCE_POST_X = (-0.85, 0.0, 0.85)
FENCE_HW = 0.06           # post half width (0.12 m square)
FENCE_CORNER_HW = 0.075
FENCE_CORNER_ARM = 0.45
FENCE_SEEDS = {"straight": 21, "corner": 34}  # approved
_FWOOD, _FSIDE, _FPEG = 0, 1, 2   # material slots (order of mi calls)


def _fence_builder(name):
    b = B(name)
    b.mi("fence_wood"); b.mi("fence_side"); b.mi("fence_peg")
    return b


def _fence_prism(b, prof, v0, v1, P, M):
    """Extrude a 2D profile (u,z) from v0 to v1 through mapping P(u,z,v) -> xyz, then matrix M."""
    n = len(prof)
    base = len(b.v)
    for v in (v0, v1):
        for u, z in prof:
            b.v.append(tuple(M @ Vector(P(u, z, v))))
    b.f.append(tuple(base + i for i in range(n))); b.m.append(_FWOOD)
    b.f.append(tuple(base + n + i for i in range(n - 1, -1, -1))); b.m.append(_FWOOD)
    for i in range(n):
        j = (i + 1) % n
        b.f.append((base + i, base + j, base + n + j, base + n + i)); b.m.append(_FWOOD)


def _fence_peg(b, P, M, pu, pz, v0, rng):
    """Small iron pin: 4-sided frustum sticking out of the rail face (no bottom face)."""
    a = rng.uniform(0.3, 1.2)

    def ring(r, v):
        return [tuple(M @ Vector(P(pu + r * math.cos(a + math.pi / 2 * k + math.pi / 4),
                                   pz + r * math.sin(a + math.pi / 2 * k + math.pi / 4), v))) for k in range(4)]
    base = len(b.v)
    b.v += ring(0.032, v0) + ring(0.020, v0 + 0.026)
    for i in range(4):
        j = (i + 1) % 4
        b.f.append((base + i, base + j, base + 4 + j, base + 4 + i)); b.m.append(_FPEG)
    b.f.append(tuple(base + 4 + i for i in range(4))); b.m.append(_FPEG)


def _fence_post(b, rng, x, y, hw):
    """Square post, slanted top (direction random per post), leaning a degree or two."""
    zl = FENCE_POST_H - rng.uniform(0.07, 0.11)
    zh = FENCE_POST_H
    if rng.random() < 0.5:
        zl, zh = zh, zl
    prof = [(-hw, 0.0), (hw, 0.0), (hw * 0.97, zh), (-hw * 0.97, zl)]
    M = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(rng.uniform(-0.03, 0.03), 4, 'X') \
        @ Matrix.Rotation(rng.uniform(-0.03, 0.03), 4, 'Y') @ Matrix.Rotation(rng.uniform(-0.12, 0.12), 4, 'Z')
    _fence_prism(b, prof, -hw, hw, lambda u, z, v: (u, v, z), M)


def _fence_rail_profile(rng, u0, u1, h):
    """Board silhouette with chipped / notched ends (seeded)."""
    j = lambda s=0.005: rng.uniform(-s, s)
    return [(u0 + 0.03, j(0.003)), (u1 - 0.02, j(0.003)), (u1 - 0.012, 0.42 * h), (u1, 0.60 * h),
            (u1 - 0.05 + j(), h + j(0.003)), (u0 + 0.05, h + j(0.003)), (u0, 0.72 * h),
            (u0 + 0.025, 0.36 * h), (u0 + 0.005, 0.14 * h)]


def _fence_paint(ob, wood_normals):
    """Wood colour on big board faces (+-Y, top, extra normals), dark colour on sides/chips/undersides."""
    me = ob.data
    for p in me.polygons:
        if p.material_index == _FPEG:
            continue
        n = p.normal
        wood = abs(n.y) > 0.72 or n.z > 0.72 or any(n.dot(Vector(w)) > 0.72 for w in wood_normals)
        p.material_index = _FWOOD if wood else _FSIDE
    me.update()


def make_fence_straight(name="Fence_Straight_2m", seed=FENCE_SEEDS["straight"]):
    """2 m fence section: 3 posts + 2 uneven rails with iron pegs on the -Y side. 160 tris, ~1.1 m tall.
    Origin = centre of the base (middle post)."""
    rng = random.Random(seed)
    b = _fence_builder(name)
    for x in FENCE_POST_X:
        _fence_post(b, rng, x + rng.uniform(-0.01, 0.01), rng.uniform(-0.008, 0.008), FENCE_HW)
    P = lambda u, z, v: (u, -(FENCE_HW - 0.005) - v, z)
    for k, zc in enumerate(FENCE_RAIL_Z):
        zc += rng.uniform(-0.015, 0.015)
        h = FENCE_RAIL_H + rng.uniform(-0.01, 0.01)
        u0, u1 = -FENCE_LEN / 2 + rng.uniform(0, 0.02), FENCE_LEN / 2 - rng.uniform(0, 0.02)
        ang = 0.026 if k == 0 else -0.021
        M = Matrix.Translation((0, 0, zc - h / 2)) @ Matrix.Rotation(ang, 4, 'Y')
        _fence_prism(b, _fence_rail_profile(rng, u0, u1, h), 0.0, FENCE_RAIL_T, P, M)
        for x in FENCE_POST_X:
            _fence_peg(b, P, M, x + rng.uniform(-0.004, 0.004), h / 2 + rng.uniform(-0.01, 0.01), FENCE_RAIL_T, rng)
    ob = b.build()
    _fence_paint(ob, [])
    return ob


def make_fence_corner(name="Fence_CornerPost", seed=FENCE_SEEDS["corner"]):
    """Corner post with two short rail stubs (toward -X and +Y). 180 tris. Origin = centre of the post base."""
    rng = random.Random(seed)
    hw = FENCE_CORNER_HW
    b = _fence_builder(name)
    _fence_post(b, rng, 0, 0, hw)
    L = FENCE_CORNER_ARM
    mapA = lambda u, z, v: (u, -(hw - 0.005) - v, z)
    mapB = lambda u, z, v: (hw - 0.005 + v, u, z)
    for k, zc in enumerate(FENCE_RAIL_Z):
        for arm, (P, u0, u1) in enumerate(((mapA, -L, hw + 0.02), (mapB, -hw + 0.02, L))):
            h = FENCE_RAIL_H + rng.uniform(-0.008, 0.008)
            zz = zc + rng.uniform(-0.012, 0.012)
            ang = (1 if (k + arm) % 2 == 0 else -1) * rng.uniform(0.02, 0.03)
            M = Matrix.Translation((0, 0, zz - h / 2)) @ Matrix.Rotation(ang, 4, 'Y' if arm == 0 else 'X')
            _fence_prism(b, _fence_rail_profile(rng, u0, u1, h), 0.0, FENCE_RAIL_T, P, M)
            _fence_peg(b, P, M, 0.0, h / 2, FENCE_RAIL_T, rng)
    ob = b.build()
    _fence_paint(ob, [(1, 0, 0)])
    return ob


# ---------------------------------------------------------------- small props
PROPS_SEEDS = {"mushroom": 21, "grass": 33, "stone": 45, "stump": 57}  # approved


class _FaceBuilder:
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

    def build(self):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.v, [], self.f); me.update()
        for p, mi in zip(me.polygons, self.m):
            p.material_index = mi; p.use_smooth = False
        for n in self.mats: me.materials.append(MATS[n])
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def _props_ring(n, r, z, rot=0.0, cx=0.0, cy=0.0, rng=None, rj=0.0, rscale=None):
    out = []
    for i in range(n):
        a = rot + 2 * math.pi * i / n
        rr = (rscale[i % len(rscale)] if rscale else r) * (1 + (rng.uniform(-rj, rj) if rng else 0))
        out.append(Vector((cx + rr * math.cos(a), cy + rr * math.sin(a), z)))
    return out


def _add_mushroom(b, rng, x, y, h, lean, stem_n, spots):
    """h = total height (m). Stem n-sided (cream), 6-sided faceted cap (red) with spot triangles (cream)."""
    rot = rng.uniform(0, 6.28)
    ang = rng.uniform(0, 6.28)
    lx, ly = math.cos(ang) * lean, math.sin(ang) * lean
    sh = 0.62 * h
    ch = h - sh
    Rc = 0.40 * h
    def at(z): return (x + lx * z, y + ly * z)
    sr0, sr1 = 0.115 * h, 0.085 * h
    bot = _props_ring(stem_n, sr0, 0.0, rot, x, y, rng, 0.06)
    top = _props_ring(stem_n, sr1, sh, rot, *at(sh), rng=rng, rj=0.06)
    cs = Vector((*at(sh * 0.5), sh * 0.5))
    b.strip(bot, top, "plaster_cream", cs)
    b.face(bot, "plaster_cream", cs)
    b.face(top, "plaster_cream", cs)
    cz = sh - 0.04 * h
    r0 = _props_ring(6, Rc, cz, rot + 0.3, *at(cz), rng=rng, rj=0.07)
    z1 = sh + 0.38 * ch
    r1 = _props_ring(6, Rc * 0.80, z1, rot + 0.3, *at(z1), rng=rng, rj=0.05)
    apex = Vector((*at(h), h))
    cc = Vector((*at(sh + 0.3 * ch), sh + 0.3 * ch))
    b.strip(r0, r1, "mushroom_red", cc)
    b.fan(r1, apex, "mushroom_red", cc)
    b.face(r0, "mushroom_red", cc)
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


def make_mushroom_cluster(name="Mushroom_Red_Cluster", seed=PROPS_SEEDS["mushroom"]):
    """3 red-cap mushrooms (25 / 17 / 10 cm). 119 tris. Origin = centre of the base."""
    rng = random.Random(seed)
    b = _FaceBuilder(name)
    _add_mushroom(b, rng, 0.00, 0.00, 0.25, 0.10, 5, [("q", 0), ("q", 2), ("q", 4), ("t", 3)])
    _add_mushroom(b, rng, 0.15, -0.07, 0.17, -0.18, 5, [("q", 1), ("q", 3), ("q", 5)])
    _add_mushroom(b, rng, -0.10, -0.12, 0.10, 0.22, 4, [("q", 0), ("q", 3)])
    return b.build()


def make_grass_tuft(name="Grass_Tuft", seed=PROPS_SEEDS["grass"]):
    """7 leaning blades, ~0.2 m. 70 tris. Origin = centre of the base."""
    rng = random.Random(seed)
    b = _FaceBuilder(name)
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
    return b.build()


def make_stepping_stone(name="Stepping_Stone", seed=PROPS_SEEDS["stone"]):
    """Flat 7-sided stone slab ~38 x 31 cm, 5-6 cm tall. 40 tris. Origin = centre of the base."""
    rng = random.Random(seed)
    b = _FaceBuilder(name)
    n = 7
    angs = [2 * math.pi * i / n + rng.uniform(-0.22, 0.22) for i in range(n)]
    pts0, pts1, pts2 = [], [], []
    for a in angs:
        r = rng.uniform(0.15, 0.19)
        x, y = r * 1.12 * math.cos(a), r * 0.92 * math.sin(a)
        pts0.append(Vector((x, y, 0.0)))
        pts1.append(Vector((x * 0.97, y * 0.97, 0.034 + rng.uniform(-0.004, 0.004))))
        pts2.append(Vector((x * 0.84, y * 0.84, 0.050 + rng.uniform(-0.006, 0.006) + 0.008 * math.cos(a))))
    c = Vector((0, 0, 0.02))
    apex = Vector((0.01, -0.005, 0.058))
    b.strip(pts0, pts1, "rock_side", c)
    b.strip(pts1, pts2, "rock_top", c)
    b.fan(pts2, apex, "rock_top", c)
    b.face(pts0, "rock_side", c)
    return b.build()


def make_tree_stump(name="Tree_Stump", seed=PROPS_SEEDS["stump"]):
    """8-sided stump with root flare and a ringed cut face, ~0.4 m tall. 78 tris. Origin = centre of the base."""
    rng = random.Random(seed)
    b = _FaceBuilder(name)
    n = 8
    rot = 0.2
    bot = _props_ring(n, 0, 0.0, rot, rng=rng, rj=0.05, rscale=[0.255, 0.205])
    mid = _props_ring(n, 0.170, 0.11, rot, rng=rng, rj=0.05)
    top = []
    for i in range(n):
        a = rot + 2 * math.pi * i / n
        r = 0.150 * (1 + rng.uniform(-0.05, 0.05))
        top.append(Vector((r * math.cos(a), r * math.sin(a), 0.394 + 0.006 * math.cos(a - 0.8))))
    c = Vector((0, 0, 0.2))
    b.strip(bot, mid, "wood_dark", c)
    b.strip(mid, top, "wood_dark", c)
    b.face(bot, "wood_dark", c)
    def shrink(ringp, f, dz):
        return [Vector((p.x * f, p.y * f, p.z + dz)) for p in ringp]
    r1 = shrink(top, 0.88, -0.003)
    r2 = shrink(top, 0.58, -0.007)
    ctr = Vector((0, 0, 0.384))
    b.strip(top, r1, "wood_dark", c)
    b.strip(r1, r2, "wood_light", c)
    b.fan(r2, ctr, "plaster_cream", c)
    return b.build()


# ---------------------------------------------------------------- round trees
ROUND_TREE_TOP_NZ = 0.3   # leaf faces with normal.z above this get leaf_light, the rest leaf_under
ROUND_TREE_SEGMENTS = 6   # trunk sides
# kind -> params. blobs: (centre xyz, radii xyz); branches: (t on trunk, blob index)
ROUND_TREES = {
    "slender": dict(name="Tree_Round_Slender", seed=3, trunk_h=3.4, r0=0.22, r1=0.11, lean=(0.45, 0.2), bend=0.22,
                    branches=[(0.45, 0), (0.65, 1)],
                    blobs=[((-0.45, 0.10, 2.75), (1.25, 1.15, 0.95)),
                           ((0.7, -0.1, 3.4), (1.0, 0.95, 0.8)),
                           ((0.1, 0.25, 4.1), (0.8, 0.78, 0.65)),
                           ((0.95, 0.4, 2.6), (0.65, 0.6, 0.5))]),
    "wide": dict(name="Tree_Round_Wide", seed=8, trunk_h=2.3, r0=0.36, r1=0.2, lean=(-0.35, 0.3), bend=-0.2,
                 branches=[(0.5, 0), (0.7, 1), (0.9, 2)],
                 blobs=[((-1.5, -0.2, 2.5), (1.5, 1.35, 1.0)),
                        ((1.4, 0.35, 2.55), (1.45, 1.3, 0.95)),
                        ((-0.2, 1.0, 3.0), (1.35, 1.2, 0.95)),
                        ((-0.35, -0.9, 3.3), (1.25, 1.1, 0.9)),
                        ((-0.1, 0.1, 3.75), (1.1, 1.0, 0.8))]),
}


def _rt_tube(b, pts, radii, n, rng, mat="wood_dark"):
    """Closed tapered tube along pts (list of Vector)."""
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


def _rt_blob(b, c, radii, rng, jit=0.12):
    """Faceted ellipsoid chunk: icosphere(subdiv 2, 80 tris) with per-vertex jitter, random yaw, slight tilt."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    R = (Matrix.Rotation(rng.uniform(0, 6.28), 3, 'Z') @ Matrix.Rotation(rng.uniform(-.2, .2), 3, 'X')
         @ Matrix.Rotation(rng.uniform(-.2, .2), 3, 'Y'))
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


def _rt_curve_pt(base, lean, h, bend, t):
    off = lean * (t * t) + Vector((-lean.y, lean.x)) * (bend * math.sin(t * math.pi))
    return Vector((base.x + off.x, base.y + off.y, h * t))


def make_round_tree(kind, seed=None, name=None):
    """kind: 'slender' (428 tris, ~4.7 m) | 'wide' (534 tris, ~4.6 m). Bent 6-sided trunk + branches + 4-5 faceted
    leaf blobs (light on top, #6C8535 underneath). Origin = trunk base. Same (kind, seed) -> same tree."""
    k = ROUND_TREES[kind]
    rng = random.Random(k["seed"] if seed is None else seed)
    b = B(name or k["name"])
    b.mi("wood_dark"); b.mi("leaf_light"); b.mi("leaf_under")
    lean = Vector(k["lean"]); trunk_h = k["trunk_h"]; r0, r1 = k["r0"], k["r1"]; bend = k["bend"]; blobs = k["blobs"]
    N = 4
    pts = [_rt_curve_pt(Vector((0, 0)), lean, trunk_h, bend, i / N) for i in range(N + 1)]
    radii = [r0 + (r1 - r0) * i / N for i in range(N + 1)]
    _rt_tube(b, pts, radii, ROUND_TREE_SEGMENTS, rng)
    for t, bi in k["branches"]:
        p = _rt_curve_pt(Vector((0, 0)), lean, trunk_h, bend, t)
        tgt = Vector(blobs[bi][0]); tgt.z -= blobs[bi][1][2] * 0.25
        mid = (p + tgt) / 2 + Vector((rng.uniform(-.12, .12), rng.uniform(-.12, .12), 0.12))
        rb = r1 + (r0 - r1) * (1 - t) * 0.6
        _rt_tube(b, [p, mid, tgt], [rb * 0.7, rb * 0.5, 0.07], 5, rng)
    for c, r in blobs:
        _rt_blob(b, Vector(c), r, rng)
    ob = b.build()
    for p in ob.data.polygons:
        if ob.data.materials[p.material_index].name == "MK_wood_dark": continue
        p.material_index = b.mats.index("leaf_light" if p.normal.z > ROUND_TREE_TOP_NZ else "leaf_under")
    return ob


# ---------------------------------------------------------------- batches
def _report(tag, ob):
    vs = ob.data.vertices
    ext = [round(max(getattr(v.co, a) for v in vs) - min(getattr(v.co, a) for v in vs), 3) for a in "xyz"]
    print(tag, ob.name, "tris", sum(len(p.vertices) - 2 for p in ob.data.polygons),
          "size xyz", ext, "min_z", round(min(v.co.z for v in vs), 3))


def _build_set(tag, makers, folder, preview, **lineup):
    """makers: zero-arg callables returning an object. Export each alone (own scene), then render all together."""
    for mk in makers:
        reset_scene()
        ob = mk()
        _report(tag, ob)
        print("EXPORTED", export_fbx(ob, folder=folder))
    reset_scene()
    objs = [mk() for mk in makers]
    prev = os.path.join(ROOT, "Art_kit", "previews")
    os.makedirs(prev, exist_ok=True)
    for ob in objs:   # presentation yaw only (never in the FBX): shows both arms of the corner post
        if ob.name == "Fence_CornerPost": ob.rotation_euler = (0, 0, math.radians(-45))
    render_lineup(objs, os.path.join(prev, preview), **lineup)
    print("PREVIEW", os.path.join(prev, preview))


def build_fence():
    _build_set("FENCE", [make_fence_straight, make_fence_corner], os.path.join(MODELS, "Props"), "Fence.png",
               cell=2.5, cols=2, res=(1024, 768), dist=6.4, target_z=0.5)


def build_props():
    _build_set("PROP", [make_mushroom_cluster, make_grass_tuft, make_stepping_stone, make_tree_stump],
               os.path.join(MODELS, "Props"), "Small_props.png",
               cell=0.7, cols=4, res=(1280, 640), dist=4.1, target_z=0.1)


def build_round_trees():
    _build_set("ROUND_TREE", [lambda: make_round_tree("slender"), lambda: make_round_tree("wide")], MODELS,
               "Round_trees.png", cell=7.0, cols=2, res=(1280, 960), dist=24, target_z=2.3)


def export_fbx(ob, name=None, folder=MODELS):
    """Export one object to <folder>/<name>.fbx for Unity (Y up, 1 unit = 1 m, flat faces)."""
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, (name or ob.name) + ".fbx")
    for o in bpy.context.scene.objects: o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    return path


ASSETS = {"pine": make_pine}
BATCH = {"rocks": lambda: build_rocks(), "barrels": lambda: build_barrels(), "fence": lambda: build_fence(),
         "props": lambda: build_props(), "round_trees": lambda: build_round_trees()}  # assets that export their own files

if __name__ == "__main__":
    want = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(ASSETS)
    for key in want:
        if key in BATCH:
            BATCH[key](); continue
        reset_scene()
        ob = ASSETS[key]()
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        zs = [(ob.matrix_world @ Vector(c)).z for c in ob.bound_box]
        print("EXPORTED", export_fbx(ob), "tris", tris, "height", round(max(zs) - min(zs), 3))
