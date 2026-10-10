"""Medieval Knight art kit: approved asset builders (Blender, no add-ons).

Use:  blender -b -P Art_kit/style_kit.py -- <asset> [<asset> ...]
      assets: pine   (writes Assets/Models/<name>.fbx)
From another bpy script:  import style_kit as K; K.reset_scene(); ob = K.make_pine()

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

if __name__ == "__main__":
    want = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(ASSETS)
    for key in want:
        reset_scene()
        ob = ASSETS[key]()
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        zs = [(ob.matrix_world @ Vector(c)).z for c in ob.bound_box]
        print("EXPORTED", export_fbx(ob), "tris", tris, "height", round(max(zs) - min(zs), 3))
