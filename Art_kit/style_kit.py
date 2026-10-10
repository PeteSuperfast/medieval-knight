"""Medieval Knight art kit: approved asset builders (Blender, no add-ons).

Use:  blender -b -P Art_kit/style_kit.py -- <asset> [<asset> ...]
      assets: pine   (writes Assets/Models/Tree_Pine.fbx)
              rocks  (writes Assets/Models/Rocks/Rock_{Small,Medium,Large}_{A,B,C}.fbx
                      + Art_kit/previews/Rocks_variants.png)
From another bpy script:  import style_kit as K; K.reset_scene(); ob = K.make_pine()
                          ob = K.make_rock("medium", seed=123)   # any seed -> new rock

Palette comes from Docs/style.md. Flat colour materials, flat shading, no textures/UVs.
Origin of every asset = centre of its base, Z up, 1 unit = 1 m.
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, "Assets", "Models")

PAL = {
    "grass_light": "#8DB04A", "grass_mid": "#6C8535", "pine_dark": "#465820", "leaf_light": "#A0B85B",
    "wood_dark": "#715332",
    "rock_top": "#ADABB7", "rock_side": "#958A80",
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


def render_lineup(objs, path, cell=4.5, cols=3, res=(1280, 960)):
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
    t = Vector((0, 0, 0.8)); e_, a_ = math.radians(32), math.radians(18)
    dist = 7.5 + cell * max(cols, rows) * 1.45
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
BATCH = {"rocks": lambda: build_rocks()}  # assets that export their own files

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
