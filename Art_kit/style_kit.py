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
              cottage (writes Assets/Models/Buildings/Cottage.fbx = ONE file: root empty "Cottage" +
                       Cottage_Plinth/_Walls/_Roof/_Door/_Windows/_Chimney meshes;
                       + previews/Cottage_front.png, Cottage_back.png. Smoke + decor are NOT in the FBX)
From another bpy script:  import style_kit as K; K.reset_scene(); ob = K.make_pine()
                          ob = K.make_rock("medium", seed=123)   # any seed -> new rock
                          root = K.make_cottage()   # whole house; or K.cottage_roof() etc. one piece

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
    "roof_purple": "#5E5878", "opening_dark": "#3A3A3A", "window_glow": "#F5C25A",   # cottage
}
EMISSIVE = {"window_glow": 1.6}   # material -> emission strength (colour = its PAL colour). Blender only, not in FBX
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
    for k, strength in EMISSIVE.items():
        g = MATS[k].node_tree.nodes["Principled BSDF"]
        g.inputs["Emission Color"].default_value = lin(PAL[k])
        g.inputs["Emission Strength"].default_value = strength


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


# ---------------------------------------------------------------- cottage
# Axes: Z up, door faces -Y, chimney on +X, roof ridge along Y. Origin = centre of the base, z = 0 = ground.
# Pieces share these constants, so each cottage_* function builds alone yet fits the others.
# Approved shape (Docs/cottage/make_cottage_detail.py): 2,822 tris, 7.4 m tall, 5.6 x 4.6 m with eaves.
COTTAGE_SEED = 7                    # roof sag jitter + chimney jitter (approved)
COTTAGE_BASE_X, COTTAGE_BASE_Y = 5.0, 4.0
COTTAGE_BASE_H = 0.80               # plinth top
COTTAGE_BASE_Z0 = -0.05             # plinth bottom (sunk 5 cm)
COTTAGE_WALL_H = 2.50
COTTAGE_WALL_BOT = (4.70, 3.70)     # wall frustum footprint at the bottom / top
COTTAGE_WALL_TOP = (4.50, 3.50)
COTTAGE_SHIFT = (0.04, -0.02)       # wall top ring shift (x, y) = the lean
COTTAGE_Z_WALL_TOP = COTTAGE_BASE_H + COTTAGE_WALL_H
COTTAGE_ROOF_RIDGE_Z = 6.50
COTTAGE_ROOF_OVER = 0.50
COTTAGE_ROOF_T = 0.20
COTTAGE_ROOF_SAG = 0.30
COTTAGE_CH_TOP_Z = 7.40
COTTAGE_CHIMNEY_DRAWS = 2           # rng draws chimney uses before the roof (the roof continues the same stream)
COTTAGE_POCKET_BACK_Y = -1.55       # door pocket back
COTTAGE_WIN_POCKET_BACK_Y = COTTAGE_POCKET_BACK_Y - 0.05
COTTAGE_WALL_Z0 = 0.75              # wall surface functions start here
# wall face -> (y or x at z=WALL_Z0, change by the wall top): front, back, left(-X), right(+X)
COTTAGE_FACE = {"F": (-1.85, 0.08), "B": (1.85, -0.12), "L": (-2.35, 0.14), "R": (2.35, -0.06)}
COTTAGE_PLINTH_LEAN = 0.04          # plinth side slope: inset 4 cm over COTTAGE_PLINTH_RUN
COTTAGE_PLINTH_RUN = 0.85
# openings: x half width, bottom z, spring z, apex z (arched)
COTTAGE_DOOR = dict(cx=0.0, xh=0.55, z0=-0.2, zsp=1.70, zap=2.10)
COTTAGE_WINS = [dict(cx=-1.4, xh=0.40, z0=1.55, zsp=2.15, zap=2.45), dict(cx=1.4, xh=0.40, z0=1.55, zsp=2.15, zap=2.45)]
COTTAGE_GABLE_Y = (-1.76, 1.72)     # gable timber plane (front, back)
COTTAGE_SEEDS = dict(quoins=1234, door=99, smoke=77, decor=55)


def _ct_tz(z): return (z - COTTAGE_WALL_Z0) / (COTTAGE_Z_WALL_TOP - COTTAGE_WALL_Z0)
def _ct_yF(z): return COTTAGE_FACE["F"][0] + COTTAGE_FACE["F"][1] * _ct_tz(z)
def _ct_yB(z): return COTTAGE_FACE["B"][0] + COTTAGE_FACE["B"][1] * _ct_tz(z)
def _ct_xL(z): return COTTAGE_FACE["L"][0] + COTTAGE_FACE["L"][1] * _ct_tz(z)
def _ct_xR(z): return COTTAGE_FACE["R"][0] + COTTAGE_FACE["R"][1] * _ct_tz(z)
def _ct_plinth_inset(z): return COTTAGE_PLINTH_LEAN * (z - COTTAGE_BASE_Z0) / COTTAGE_PLINTH_RUN
def _ct_plinth_front(z): return -COTTAGE_BASE_Y / 2 + _ct_plinth_inset(z)
def _ct_unit(*a): return Vector(a).normalized()


_CT_FACES = {
    "F": (lambda a, z: Vector((a, _ct_yF(z), z)), _ct_unit(0, -1, 0.0314)),
    "B": (lambda a, z: Vector((a, _ct_yB(z), z)), _ct_unit(0, 1, 0.0471)),
    "L": (lambda a, z: Vector((_ct_xL(z), a, z)), _ct_unit(-1, 0, 0.0549)),
    "R": (lambda a, z: Vector((_ct_xR(z), a, z)), _ct_unit(1, 0, 0.0235)),
}


def _ct_prism_mesh(name, poly_xz, y0, y1):
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


def _ct_cut(solid_ob, cutters):
    """Boolean-difference cutters (objects) out of solid_ob, return the new mesh."""
    for c in cutters:
        md = solid_ob.modifiers.new("cut", 'BOOLEAN')
        md.operation = 'DIFFERENCE'; md.solver = 'EXACT'; md.object = c
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    return bpy.data.meshes.new_from_object(solid_ob.evaluated_get(dg))


def _ct_in_box(p, bb, e=0.02):
    return (bb[0] - e <= p.x <= bb[1] + e and bb[2] - e <= p.y <= bb[3] + e and bb[4] - e <= p.z <= bb[5] + e)


def _ct_arch_pts(cx, xh, zsp, zap, n=6, grow=0.0):
    """Points of a segmental arch from the right spring over the top to the left spring (x, z)."""
    r = zap - zsp
    R = (xh * xh + r * r) / (2 * r)
    zc = zap - R
    th0 = math.asin(xh / R)
    return [(cx + (R + grow) * math.sin(th0 - 2 * th0 * k / n), zc + (R + grow) * math.cos(th0 - 2 * th0 * k / n))
            for k in range(n + 1)], R, zc, th0


def _ct_opening_poly(o, shrink=0.0, bottom=None, n=6):
    pts, R, zc, th0 = _ct_arch_pts(o["cx"], o["xh"], o["zsp"], o["zap"], n, -shrink)
    zb = o["z0"] if bottom is None else bottom
    return [(pts[-1][0], zb), (pts[0][0], zb)] + pts


def _ct_box(b, x0, x1, y0, y1, z0, z1, mat, j=0.0, rng=None):
    def p(x, y, z):
        if j:
            return Vector((x + rng.uniform(-j, j), y + rng.uniform(-j, j), z + rng.uniform(-j, j)))
        return Vector((x, y, z))
    x0, x1 = min(x0, x1), max(x0, x1)
    y0, y1 = min(y0, y1), max(y0, y1)
    b.solid([p(x0, y0, z0), p(x1, y0, z0), p(x1, y1, z0), p(x0, y1, z0)],
            [p(x0, y0, z1), p(x1, y0, z1), p(x1, y1, z1), p(x0, y1, z1)], mat)


def _ct_arch_prism(b, poly, y0, y1, mat):
    b.solid([Vector((x, y0, z)) for x, z in poly], [Vector((x, y1, z)) for x, z in poly], mat)


def _ct_bar(b, p0, p1, n, w, t, mat, embed=0.02):
    """Timber bar lying on a surface with outward normal n, from p0 to p1, width w, sticking out t."""
    d = (p1 - p0).normalized()
    s = d.cross(n).normalized() * (w / 2)

    def ring(p):
        return [p - s + n * (-embed), p + s + n * (-embed), p + s + n * t, p - s + n * t]
    b.solid(ring(p0), ring(p1), mat)


def _ct_face_bar(b, face, a0, z0, a1, z1, w, t, mat="wood_dark"):
    P, n = _CT_FACES[face]
    _ct_bar(b, P(a0, z0), P(a1, z1), n, w, t, mat)


def _ct_loft(b, rings, mat, closed=False, caps=True):
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


def _ct_ico_blob(b, c, radii, rng, subdiv, mats, jit=0.14, up_mat=None):
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


def _ct_stone_chunk(b, c, n, a, depth, h, rng):
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


def _ct_absorb(b, me, painter):
    """Copy mesh me into builder b; painter(polygon) -> material key."""
    off = len(b.v)
    b.v += [tuple(v.co) for v in me.vertices]
    for p in me.polygons:
        b.f.append(tuple(off + i for i in p.vertices))
        b.m.append(b.mi(painter(p)))


def _ct_cutter(name, o, back_y):
    ob = bpy.data.objects.new(name, _ct_prism_mesh(name, _ct_opening_poly(o), -2.4, back_y))
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _ct_cut_boxes():
    d, pb = COTTAGE_DOOR, COTTAGE_POCKET_BACK_Y
    return [(d["cx"] - d["xh"], d["cx"] + d["xh"], -2.4, pb, d["z0"], d["zap"])] + \
           [(w["cx"] - w["xh"], w["cx"] + w["xh"], -2.4, COTTAGE_WIN_POCKET_BACK_Y, w["z0"], w["zap"])
            for w in COTTAGE_WINS]


def _ct_is_pocket(p, me):
    if abs(p.normal.y) > 0.9 and p.center.y < COTTAGE_POCKET_BACK_Y - 0.1:
        return False
    for bb in _ct_cut_boxes():
        if all(_ct_in_box(me.vertices[i].co, bb, 0.03) for i in p.vertices): return True
    return False


def _ct_quoins_and_stones(b_quoin, b_stone):
    """Wall-corner quoin blocks -> b_quoin (walls), stones pressed into the plinth -> b_stone (plinth).
    One shared random stream (stones continue after quoins), so both pieces call this and keep their half."""
    rng = random.Random(COTTAGE_SEEDS["quoins"])
    b = b_quoin
    zs = [COTTAGE_BASE_H + i * (COTTAGE_Z_WALL_TOP - COTTAGE_BASE_H) / 6 for i in range(7)]
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for ci, (sx_, sy_) in enumerate(corners):
        for i in range(6):
            z0, z1 = zs[i] + 0.012, zs[i + 1] - 0.012
            zm = (z0 + z1) / 2
            cx = _ct_xR(zm) if sx_ > 0 else _ct_xL(zm)
            cy = _ct_yB(zm) if sy_ > 0 else _ct_yF(zm)
            long_x = (i + ci) % 2 == 0
            Lx, Ly = (0.36 + rng.uniform(-0.03, 0.03), 0.17 + rng.uniform(-0.02, 0.02)) if long_x else \
                     (0.17 + rng.uniform(-0.02, 0.02), 0.36 + rng.uniform(-0.03, 0.03))
            out = 0.035
            _ct_box(b, cx - sx_ * Lx, cx + sx_ * out, cy - sy_ * Ly, cy + sy_ * out, z0, z1,
                    "rock_top" if rng.random() < 0.5 else "rock_side", j=0.006, rng=rng)
    placed = []
    sides = [("F", 9, lambda r: r.uniform(-2.35, 2.35)), ("B", 8, lambda r: r.uniform(-2.35, 2.35)),
             ("L", 7, lambda r: r.uniform(-1.85, 1.85)), ("R", 6, lambda r: r.uniform(-1.85, 1.85))]
    hx, hy = COTTAGE_BASE_X / 2, COTTAGE_BASE_Y / 2
    for face, count, pick in sides:
        got, tries = 0, 0
        while got < count and tries < 400:
            tries += 1
            a = pick(rng)
            z = rng.uniform(0.14, 0.66)
            if face == "F" and abs(a) < 0.98: continue          # door jamb stones are there
            if face == "R" and abs(a) < 0.70: continue          # chimney
            if any(f == face and abs(a - a2) < 0.5 and abs(z - z2) < 0.28 for f, a2, z2 in placed): continue
            placed.append((face, a, z)); got += 1
            if face == "F":
                p = Vector((a, _ct_plinth_front(z), z)); n = _ct_unit(0, -1, 0.05)
            elif face == "B":
                p = Vector((a, -_ct_plinth_front(z), z)); n = _ct_unit(0, 1, 0.05)
            elif face == "L":
                p = Vector((-(hx - _ct_plinth_inset(z)), a, z)); n = _ct_unit(-1, 0, 0.05)
            else:
                p = Vector((hx - _ct_plinth_inset(z), a, z)); n = _ct_unit(1, 0, 0.05)
            _ct_stone_chunk(b_stone, p, n, rng.uniform(0.13, 0.26), rng.uniform(0.045, 0.075),
                            rng.uniform(0.08, 0.15), rng)


def _ct_door_and_windows(b_door, b_win):
    """Door leaf + stone frame -> b_door; arched window frames, sills, glowing panes, muntins -> b_win.
    One shared random stream (window sills continue after the door jambs)."""
    rng = random.Random(COTTAGE_SEEDS["door"])
    b = b_door
    leaf = dict(cx=0.0, xh=0.50, z0=0.0, zsp=1.69, zap=2.06)
    poly = _ct_opening_poly(leaf, n=6)
    y_face, y_back = -1.70, -1.555
    _ct_arch_prism(b, poly, y_face, y_back, "wood_light")
    _, Rl, zcl, _ = _ct_arch_pts(0, 0.5, 1.69, 2.06)
    for x in (-0.30, -0.10, 0.10, 0.30):
        ztop = zcl + math.sqrt(max(Rl * Rl - x * x, 0)) - 0.10
        _ct_box(b, x - 0.011, x + 0.011, y_face - 0.012, y_face + 0.02, 0.06, ztop, "wood_dark")
    for zc, lx in ((0.45, 0.46), (1.32, 0.46)):                     # iron strap hinges from the left edge
        _ct_box(b, -0.50, -0.50 + lx, y_face - 0.022, y_face + 0.01, zc - 0.045, zc + 0.045, "barrel_iron")
    _ct_box(b, 0.24, 0.36, y_face - 0.03, y_face + 0.01, 1.12, 1.24, "barrel_iron")   # ring backplate
    R, tw, cz, cx_ = 0.075, 0.014, 1.04, 0.30
    rings = []
    for i in range(8):
        a = 2 * math.pi * i / 8
        c = Vector((cx_ + R * math.cos(a), y_face - 0.045, cz + R * math.sin(a)))
        rr = Vector((math.cos(a), 0, math.sin(a)))
        yy = Vector((0, 1, 0))
        rings.append([c + rr * tw + yy * tw, c - rr * tw + yy * tw, c - rr * tw - yy * tw, c + rr * tw - yy * tw])
    _ct_loft(b, rings, "barrel_iron", closed=True, caps=False)
    # stone frame: jamb blocks + arch voussoirs
    xi, xo = 0.54, 0.72
    zb_list = [-0.05, 0.40, 0.82, 1.26, 1.70]
    for side in (-1, 1):
        for i in range(4):
            z0, z1 = zb_list[i] + 0.01, zb_list[i + 1] - 0.01
            zm = (z0 + z1) / 2
            yf = (_ct_plinth_front(zm) if zm < 0.8 else _ct_yF(zm)) - 0.05 + rng.uniform(-0.012, 0.012)
            w = xo - xi + (0.03 if i % 2 else 0.0)
            _ct_box(b, side * xi, side * (xi + w), yf, -1.50, max(z0, 0.0) if i == 0 else z0, z1,
                    "rock_top" if (i + (side > 0)) % 2 else "rock_side", j=0.005, rng=rng)
    d = COTTAGE_DOOR
    _, R, zc, th0 = _ct_arch_pts(0.0, d["xh"], d["zsp"], d["zap"])
    nW = 5
    for k in range(nW):
        ta = th0 - 2 * th0 * k / nW
        tb = th0 - 2 * th0 * (k + 1) / nW
        key = (k == nW // 2)
        ro = R + 0.17 + (0.05 if key else 0.0)
        pts = lambda th, r: Vector((r * math.sin(th), 0, zc + r * math.cos(th)))
        zm = zc + R * math.cos((ta + tb) / 2)
        yf = _ct_yF(zm) - (0.075 if key else 0.052)
        r_in = R - 0.01
        ring_f = [pts(ta, r_in), pts(ta, ro), pts(tb, ro), pts(tb, r_in)]
        bot = [Vector((p.x, yf, p.z)) for p in ring_f]
        top = [Vector((p.x, -1.50, p.z)) for p in ring_f]
        b.solid(bot, top, "rock_top" if k % 2 == 0 else "rock_side")
    # windows
    b = b_win
    for w in COTTAGE_WINS:
        inner, _, _, _ = _ct_arch_pts(w["cx"], w["xh"], w["zsp"], w["zap"], 6, -0.015)
        outer, _, _, _ = _ct_arch_pts(w["cx"], w["xh"], w["zsp"], w["zap"], 6, 0.075)
        pin = [(inner[-1][0], w["z0"] + 0.015), (inner[0][0], w["z0"] + 0.015)] + inner
        pout = [(outer[-1][0], w["z0"] - 0.075), (outer[0][0], w["z0"] - 0.075)] + outer
        yf = _ct_yF(2.0) - 0.035
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
        _ct_box(b, cxw - 0.50, cxw + 0.50, _ct_yF(1.45) - 0.085, -1.76, 1.40, 1.475, "wood_light", j=0.004, rng=rng)  # sill
        pane = dict(cx=cxw, xh=0.395, z0=1.56, zsp=2.15, zap=2.445)
        _ct_arch_prism(b, _ct_opening_poly(pane), -1.665, -1.605, "window_glow")
        _ct_box(b, cxw - 0.022, cxw + 0.022, -1.725, -1.655, 1.56, 2.40, "wood_dark")
        _ct_box(b, cxw - 0.395, cxw + 0.395, -1.725, -1.655, 1.96, 2.00, "wood_dark")


def _ct_timber(b):
    H, V, D = 0.13, 0.12, 0.10        # widths: horizontals, posts, diagonals
    tH, tV, tD = 0.030, 0.035, 0.025  # stand-off from wall
    zl, zm, zt = 1.25, 2.70, 3.17     # low rail, mid rail (above windows), top plate
    fb = lambda face, a0, z0, a1, z1, w, t: _ct_face_bar(b, face, a0, z0, a1, z1, w, t)
    for z, segs in ((zl, [(-2.12, -0.75), (0.75, 2.12)]), (zm, [(-2.12, 2.12)]), (zt, [(-2.12, 2.12)])):
        for a0, a1 in segs: fb("F", a0, z, a1, z, H, tH)
    for a in (-1.4, 1.4):
        fb("F", a, 0.80, a, zl, V, tV)
    for a in (-1.4, 0.0, 1.4):
        fb("F", a, zm, a, zt, V, tV)
    fb("F", -2.08, zm, -1.44, zt - 0.04, D, tD)
    fb("F", 2.08, zm, 1.44, zt - 0.04, D, tD)
    fb("F", -1.36, zt - 0.04, -0.06, zm + 0.04, D, tD)
    fb("F", 1.36, zt - 0.04, 0.06, zm + 0.04, D, tD)
    for z in (zl, zm, zt): fb("B", -2.12, z, 2.12, z, H, tH)
    for a in (-1.2, 0.0, 1.2): fb("B", a, 0.80, a, zt, V, tV)
    pan = [(-2.08, -1.24), (-1.16, -0.04), (0.04, 1.16), (1.24, 2.08)]
    for k, (a0, a1) in enumerate(pan):
        if k % 2 == 0: fb("B", a0, zl + 0.08, a1, zm - 0.08, D, tD)
        else: fb("B", a1, zl + 0.08, a0, zm - 0.08, D, tD)
    for face, chim in (("L", False), ("R", True)):
        for z in (zl, zm, zt):
            if chim:
                fb(face, -1.67, z, -0.56, z, H, tH); fb(face, 0.56, z, 1.67, z, H, tH)
            else:
                fb(face, -1.67, z, 1.67, z, H, tH)
        posts = (-0.95, 0.95) if chim else (-0.9, 0.0, 0.9)
        for a in posts: fb(face, a, 0.80, a, zt, V, tV)
        pans = [(-1.62, -1.0), (0.98, 1.62)] if chim else [(-1.62, -0.96), (-0.84, -0.06), (0.06, 0.84), (0.96, 1.62)]
        for k, (a0, a1) in enumerate(pans):
            if (k + (1 if chim else 0)) % 2 == 0: fb(face, a0, zl + 0.08, a1, zm - 0.08, D, tD)
            else: fb(face, a1, zl + 0.08, a0, zm - 0.08, D, tD)
    # gable triangles: centre post, collar, two V braces, upper collar
    for sgn, yg in zip((-1, 1), COTTAGE_GABLE_Y):
        n = Vector((0, sgn, 0))
        gp = lambda x, z: Vector((COTTAGE_SHIFT[0] + x, yg, z))
        _ct_bar(b, gp(0, 3.28), gp(0, 5.50), n, V, tV, "wood_dark")
        _ct_bar(b, gp(-1.30, 4.15), gp(1.30, 4.15), n, H, tH, "wood_dark")
        _ct_bar(b, gp(-1.95, 3.30), gp(-0.05, 4.10), n, D, tD, "wood_dark")
        _ct_bar(b, gp(1.95, 3.30), gp(0.05, 4.10), n, D, tD, "wood_dark")
        _ct_bar(b, gp(-0.62, 4.95), gp(0.62, 4.95), n, H * 0.85, tH, "wood_dark")
        _ct_bar(b, gp(-1.15, 4.20), gp(-0.10, 4.88), n, D, tD, "wood_dark")
        _ct_bar(b, gp(1.15, 4.20), gp(0.10, 4.88), n, D, tD, "wood_dark")


def _ct_new_mesh_obj(b, name):
    ob = b.build()
    ob.name = name; ob.data.name = name
    for p in ob.data.polygons: p.use_smooth = False
    return ob


def cottage_plinth(name="Cottage_Plinth"):
    """Stone plinth (frustum, door notch cut out, dark pocket) + 30 faceted stones pressed into its sides."""
    sc = bpy.context.scene
    pb = B("tmp_base")
    bx, by = COTTAGE_BASE_X / 2, COTTAGE_BASE_Y / 2
    z0, h = COTTAGE_BASE_Z0, COTTAGE_BASE_H
    pb.solid([Vector(p) for p in [(-bx, -by, z0), (bx, -by, z0), (bx, by, z0), (-bx, by, z0)]],
             [Vector(p) for p in [(-bx + 0.05, -by + 0.04, h), (bx - 0.04, -by + 0.04, h),
                                   (bx - 0.04, by - 0.05, h), (-bx + 0.05, by - 0.05, h)]], "rock_side")
    base_ob = pb.build()
    door_c = _ct_cutter("c_door", COTTAGE_DOOR, COTTAGE_POCKET_BACK_Y)
    me = _ct_cut(base_ob, [door_c])
    b = B(name)
    _ct_absorb(b, me, lambda p: "opening_dark" if _ct_is_pocket(p, me) else ("rock_top" if p.normal.z > 0.5 else "rock_side"))
    _ct_quoins_and_stones(B("tmp_quoins"), b)
    bpy.data.objects.remove(base_ob); bpy.data.objects.remove(door_c)
    return _ct_new_mesh_obj(b, name)


def cottage_walls(name="Cottage_Walls"):
    """Plaster wall frustum with arched door + 2 window openings cut out (dark pockets), gable fills,
    timber frame, corner quoin stones."""
    wb = B("tmp_wall")
    w0x, w0y = COTTAGE_WALL_BOT[0] / 2, COTTAGE_WALL_BOT[1] / 2
    w1x, w1y = COTTAGE_WALL_TOP[0] / 2, COTTAGE_WALL_TOP[1] / 2
    SX, SY = COTTAGE_SHIFT
    zb, zt = COTTAGE_BASE_H - 0.05, COTTAGE_Z_WALL_TOP
    wb.solid([Vector(p) for p in [(-w0x, -w0y, zb), (w0x, -w0y, zb), (w0x, w0y, zb), (-w0x, w0y, zb)]],
             [Vector(p) for p in [(-w1x + SX, -w1y + SY, zt), (w1x + SX, -w1y + SY, zt),
                                   (w1x + SX, w1y + SY, zt), (-w1x + SX, w1y + SY, zt)]], "plaster_cream")
    wall_ob = wb.build()
    cutters = [_ct_cutter("c_door", COTTAGE_DOOR, COTTAGE_POCKET_BACK_Y)] + \
              [_ct_cutter("c_win%d" % i, w, COTTAGE_WIN_POCKET_BACK_Y) for i, w in enumerate(COTTAGE_WINS)]
    me = _ct_cut(wall_ob, cutters)
    b = B(name)
    _ct_absorb(b, me, lambda p: "opening_dark" if _ct_is_pocket(p, me) else "plaster_cream")
    for sgn in (-1, 1):                                   # gable fills
        y_out = sgn * (w1y - 0.01) + SY
        y_in = sgn * (w1y - 0.31) + SY
        prof = [(-w1x + 0.01 + SX, zt - 0.1), (w1x - 0.01 + SX, zt - 0.1), (0.0 + SX, 5.80)]
        base = len(b.v)
        for y in (y_in, y_out):
            for (x, z) in prof: b.v.append((x, y, z))
        m = b.mi("plaster_cream")
        b.f.append((base + 2, base + 1, base + 0)); b.m.append(m)
        b.f.append((base + 3, base + 4, base + 5)); b.m.append(m)
        for i in range(3):
            j = (i + 1) % 3
            b.f.append((base + i, base + j, base + 3 + j, base + 3 + i)); b.m.append(m)
    _ct_timber(b)
    _ct_quoins_and_stones(b, B("tmp_stones"))
    for o in [wall_ob] + cutters: bpy.data.objects.remove(o)
    return _ct_new_mesh_obj(b, name)


def cottage_chimney(seed=COTTAGE_SEED, name="Cottage_Chimney"):
    """Stone chimney on the +X side, 7.4 m to the cap top (runs from z=0 up past the roof)."""
    rng = random.Random(seed)
    ch = B("tmp_chimney")
    cj = lambda: rng.uniform(-0.02, 0.02)
    top_z = COTTAGE_CH_TOP_Z
    bot = [Vector(p) for p in [(2.20, -0.50, 0.0), (3.15, -0.50 + cj(), 0.0), (3.15, 0.50, 0.0), (2.20, 0.50, 0.0)]]
    top = [Vector(p) for p in [(2.20, -0.38, 7.0), (2.92, -0.38 + cj(), 7.0), (2.92 + 0.03, 0.40, 7.0), (2.20, 0.40, 7.0)]]
    cap0 = [Vector(p) for p in [(2.12, -0.46, 7.0), (3.02, -0.46, 7.0), (3.02, 0.48, 7.0), (2.12, 0.48, 7.0)]]
    cap1 = [Vector(p) for p in [(2.14, -0.44, top_z), (3.00, -0.44, top_z), (3.00, 0.46, top_z), (2.14, 0.46, top_z)]]
    ch.solid(bot, top, "rock_side")
    ch.solid(cap0, cap1, "rock_side")
    tmp = ch.build()
    b = B(name)
    _ct_absorb(b, tmp.data, lambda p: "rock_top" if p.normal.z > 0.5 else "rock_side")
    bpy.data.objects.remove(tmp)
    return _ct_new_mesh_obj(b, name)


def cottage_roof(seed=COTTAGE_SEED, name="Cottage_Roof"):
    """Purple sagging gable roof (loft) + wooden eave fascias + rake boards + the +X-slope dormer
    (dormer window frame and its glowing pane are part of the roof piece)."""
    rng = random.Random(seed)
    for _ in range(COTTAGE_CHIMNEY_DRAWS):   # same stream as the chimney used first
        rng.uniform(-0.02, 0.02)
    b = B(name)
    half_x = COTTAGE_WALL_TOP[0] / 2 + COTTAGE_ROOF_OVER
    half_y = COTTAGE_WALL_TOP[1] / 2 + COTTAGE_ROOF_OVER
    N = 10
    slope = (COTTAGE_ROOF_RIDGE_Z - 2.85) / half_x
    vt = COTTAGE_ROOF_T / math.cos(math.atan(slope))
    m = b.mi("roof_purple")
    rings = []
    for i in range(N + 1):
        y = -half_y + 2 * half_y * i / N
        sag = COTTAGE_ROOF_SAG * (1 - (y / half_y) ** 2)
        hr = COTTAGE_ROOF_RIDGE_Z - sag
        he = COTTAGE_ROOF_RIDGE_Z - slope * half_x - sag * 0.5
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
    # eave fascia boards (long sides) lofted along the roof stations
    for side in (1, -1):
        st = []
        for r in rings:
            top, bot = (r[1], r[2]) if side > 0 else (r[5], r[4])
            xe = top[0]
            st.append([Vector((xe - side * 0.03, top[1], top[2] + 0.03)), Vector((xe + side * 0.055, top[1], top[2] + 0.03)),
                       Vector((xe + side * 0.055, top[1], bot[2] - 0.055)), Vector((xe - side * 0.03, top[1], bot[2] - 0.055))])
        _ct_loft(b, st, "wood_dark")
    # rake boards on both gable ends
    for idx, sgn in ((0, -1), (-1, 1)):
        prof = [Vector(p) for p in rings[idx]]
        lift = [0.03, 0.03, -0.055, -0.055, -0.055, 0.03]
        ring0 = [Vector((p.x, p.y, p.z + dz)) for p, dz in zip(prof, lift)]
        ring1 = [Vector((p.x, p.y + sgn * 0.055, p.z)) for p in ring0]
        b.solid(ring0, ring1, "wood_dark")
    # dormer on the +X slope (toward the front, clear of the chimney)
    yc, hw = -1.05, 0.46
    xf, xb = 1.95, 1.02
    sag = COTTAGE_ROOF_SAG * (1 - (yc / half_y) ** 2)
    zf = COTTAGE_ROOF_RIDGE_Z - sag - (slope - sag * 0.5 / half_x) * abs(xf)
    zw0, zw1 = zf - 0.16, zf + 1.07
    _ct_box(b, xb, xf, yc - hw, yc + hw, zw0, zw1, "plaster_cream")
    fe = [Vector((xf + 0.14, yc - hw - 0.08, zw1)), Vector((xf + 0.14, yc, zw1 + 0.56)), Vector((xf + 0.14, yc + hw + 0.08, zw1))]
    be = [Vector((xb, yc - hw - 0.08, zw1 - 0.42)), Vector((xb, yc, zw1 + 0.14)), Vector((xb, yc + hw + 0.08, zw1 - 0.42))]
    b.solid(fe, be, "roof_purple")
    wy0, wy1, wz0, wz1 = yc - 0.21, yc + 0.21, zw0 + 0.40, zw0 + 0.40 + 0.56
    _ct_box(b, xf - 0.02, xf + 0.012, wy0, wy1, wz0, wz1, "window_glow")
    t = 0.06
    _ct_box(b, xf - 0.02, xf + 0.045, wy0 - t, wy1 + t, wz1, wz1 + t, "wood_dark")
    _ct_box(b, xf - 0.02, xf + 0.045, wy0 - t, wy1 + t, wz0 - t, wz0, "wood_dark")
    _ct_box(b, xf - 0.02, xf + 0.045, wy0 - t, wy0, wz0, wz1, "wood_dark")
    _ct_box(b, xf - 0.02, xf + 0.045, wy1, wy1 + t, wz0, wz1, "wood_dark")
    _ct_box(b, xf, xf + 0.035, yc - 0.015, yc + 0.015, wz0, wz1, "wood_dark")
    _ct_box(b, xf, xf + 0.035, wy0, wy1, (wz0 + wz1) / 2 - 0.015, (wz0 + wz1) / 2 + 0.015, "wood_dark")
    return _ct_new_mesh_obj(b, name)


def cottage_door(name="Cottage_Door"):
    """Arched plank door (planks, 2 iron strap hinges, iron ring) + stone frame (jambs + voussoirs)."""
    door, wins = _ct_door_parts(name, "tmp_windows")
    bpy.data.objects.remove(wins)
    return door


def cottage_windows(name="Cottage_Windows"):
    """Two arched wooden window frames + sills + glowing panes (#F5C25A emission) + muntins.
    The dormer window is part of cottage_roof."""
    door, wins = _ct_door_parts("tmp_door", name)
    bpy.data.objects.remove(door)
    return wins


def _ct_door_parts(door_name, win_name):
    """Door and windows share one random stream, so they are always built together."""
    bd, bw = B(door_name), B(win_name)
    _ct_door_and_windows(bd, bw)
    return _ct_new_mesh_obj(bd, door_name), _ct_new_mesh_obj(bw, win_name)


def cottage_smoke(name="Cottage_Smoke"):
    """3 faceted smoke puffs above the chimney. Decoration only, never exported."""
    sm = B(name)
    srng = random.Random(COTTAGE_SEEDS["smoke"])
    cxs, cys = 2.57, 0.01
    z = COTTAGE_CH_TOP_Z
    _ct_ico_blob(sm, Vector((cxs + 0.05, cys, z + 0.45)), (0.30, 0.28, 0.26), srng, 2, "rock_top")
    _ct_ico_blob(sm, Vector((cxs + 0.35, cys - 0.10, z + 1.00)), (0.42, 0.40, 0.36), srng, 2, "plaster_cream")
    _ct_ico_blob(sm, Vector((cxs + 0.80, cys - 0.20, z + 1.70)), (0.56, 0.52, 0.46), srng, 2, "plaster_cream")
    return _ct_new_mesh_obj(sm, name)


COTTAGE_PIECES = ("Plinth", "Walls", "Roof", "Door", "Windows", "Chimney")


def make_cottage(seed=COTTAGE_SEED, name="Cottage"):
    """Whole house: root empty "Cottage" (origin = base centre at ground level) with 6 child meshes
    Cottage_Plinth / _Walls / _Roof / _Door / _Windows / _Chimney. 2,822 tris, 7.4 m tall."""
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = 'PLAIN_AXES'
    bpy.context.scene.collection.objects.link(root)
    door, wins = _ct_door_parts(name + "_Door", name + "_Windows")
    parts = [cottage_plinth(name + "_Plinth"), cottage_walls(name + "_Walls"), cottage_roof(seed, name + "_Roof"),
             door, wins, cottage_chimney(seed, name + "_Chimney")]
    for ob in parts:
        ob.parent = root
    return root


def build_cottage():
    """Export the whole cottage as ONE FBX (root empty + 6 meshes), re-import it to check, render 2 views.
    Decor (barrels, stones, mushrooms, grass, smoke) is only in the preview, never in the FBX."""
    folder = os.path.join(MODELS, "Buildings")
    tri = lambda o: sum(len(p.vertices) - 2 for p in o.data.polygons)
    reset_scene()
    root = make_cottage()
    total, zs = 0, []
    for ob in root.children:
        ws = [ob.matrix_world @ v.co for v in ob.data.vertices]
        zs += [w.z for w in ws]
        total += tri(ob)
        print("COTTAGE", ob.name, "tris", tri(ob), "z", round(min(w.z for w in ws), 3), round(max(w.z for w in ws), 3),
              "materials", [m.name for m in ob.data.materials])
    print("COTTAGE total tris", total, "height", round(max(zs), 3), "min_z", round(min(zs), 3))
    path = export_fbx_group(root, folder=folder)
    print("EXPORTED", path)
    reset_scene()
    bpy.ops.import_scene.fbx(filepath=path)
    ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    zz = [(o.matrix_world @ v.co).z for o in ms for v in o.data.vertices]
    print("REIMPORT objects", sorted(o.name for o in bpy.context.scene.objects), "meshes", len(ms),
          "tris", sum(tri(o) for o in ms), "z", round(min(zz), 3), round(max(zz), 3))
    # preview (decor parented to the root so it turns with the house)
    reset_scene()
    root = make_cottage()
    drng = random.Random(COTTAGE_SEEDS["decor"])

    def put(ob, x, y, rz=0.0, s=1.0, name=None):
        ob.parent = root
        ob.location = (x, y, 0.0); ob.rotation_euler = (0, 0, rz); ob.scale = (s, s, s)
        if name: ob.name = name
        return ob
    put(make_barrel("Barrel_Upright", BARREL_SEEDS["upright"]), -1.55, -2.75, 0.3, name="Deco_Barrel_Upright")
    put(make_barrel("Barrel_Lying", BARREL_SEEDS["lying"], lying=True), 1.95, -2.75, 0.65, name="Deco_Barrel_Lying")
    for i, (x, y) in enumerate([(0.05, -2.50), (-0.20, -3.30), (0.15, -4.10), (-0.10, -4.95), (0.22, -5.75)]):
        put(make_stepping_stone("Stepping_Stone"), x, y, drng.uniform(0, 3.1), 1.9, name="Deco_StepStone_%d" % i)
    put(make_mushroom_cluster("Mushroom_Cluster"), 2.60, -2.35, 0.5, 1.7, name="Deco_Mushrooms_A")
    put(make_mushroom_cluster("Mushroom_Cluster"), -2.75, 1.10, 2.2, 1.5, name="Deco_Mushrooms_B")
    for i, (x, y) in enumerate([(-2.70, -2.35), (0.95, -2.65), (-0.95, -3.55), (2.15, -3.35), (-2.95, -0.60),
                                (-2.80, 2.35), (2.95, 1.60), (1.20, 2.50), (-1.30, 2.60), (3.4, -1.3)]):
        put(make_grass_tuft("Grass_Tuft"), x, y, drng.uniform(0, 6.28), drng.uniform(1.6, 2.1), name="Deco_Grass_%d" % i)
    cottage_smoke().parent = root
    prev = os.path.join(ROOT, "Art_kit", "previews")
    os.makedirs(prev, exist_ok=True)
    root.rotation_euler = (0, 0, math.radians(-20))
    render_lineup([root], os.path.join(prev, "Cottage_front.png"), cell=1, cols=1, res=(1024, 768), dist=21, target_z=3.4)
    for o in list(bpy.context.scene.objects):
        if o.name.startswith(("Ground_Flat", "Sun_Warm", "Cam")): bpy.data.objects.remove(o)
    root.rotation_euler = (0, 0, math.radians(240))
    render_lineup([root], os.path.join(prev, "Cottage_back.png"), cell=1, cols=1, res=(1024, 768), dist=21, target_z=3.4)
    print("PREVIEW", os.path.join(prev, "Cottage_front.png"), os.path.join(prev, "Cottage_back.png"))


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


def export_fbx_group(root, name=None, folder=MODELS):
    """Export root (an empty) plus all its descendants into ONE <folder>/<name>.fbx, same settings as export_fbx.
    The hierarchy is kept (Unity imports the empty as the parent GameObject)."""
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, (name or root.name) + ".fbx")
    for o in bpy.context.scene.objects: o.select_set(False)
    for o in [root] + list(root.children_recursive): o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    return path


ASSETS = {"pine": make_pine}
BATCH = {"rocks": lambda: build_rocks(), "barrels": lambda: build_barrels(), "fence": lambda: build_fence(),
         "props": lambda: build_props(), "round_trees": lambda: build_round_trees(),
         "cottage": lambda: build_cottage()}  # assets that export their own files

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
