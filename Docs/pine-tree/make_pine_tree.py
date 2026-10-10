import bpy, bmesh, math, random, os
import numpy as np
from mathutils import Vector, Matrix

OUT = r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\pine-tree"
REF = r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\ref"
random.seed(7)

PAL = {
    "grass_light": "#8DB04A", "grass_mid": "#6C8535", "pine_dark": "#465820", "leaf_light": "#A0B85B",
    "wood_dark": "#715332",
}

def lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
MATS = {}
for k, v in PAL.items():
    m = bpy.data.materials.new("MK_" + k)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(v)
    b.inputs["Roughness"].default_value = 0.9
    MATS[k] = m


class B:
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
        scene.collection.objects.link(ob)
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


def make_pine():
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


pine = make_pine()
gb = B("Ground_Flat")
gb.cyl(T(0, 0, -0.06), 60, 60, 0.06, 24, "grass_light")
ground = gb.build()

tris = sum(len(p.vertices) - 2 for p in pine.data.polygons)
bb = [pine.matrix_world @ Vector(c) for c in pine.bound_box]
print("PINE_TRIS", tris, "HEIGHT", max(v.z for v in bb) - min(v.z for v in bb),
      "XY", max(v.x for v in bb) - min(v.x for v in bb), max(v.y for v in bb) - min(v.y for v in bb))

# ---------------- world / light / render (same as style test) ----------------
w = bpy.data.worlds.new("Sky"); scene.world = w; w.use_nodes = True
nt = w.node_tree; nt.nodes.clear()
tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
ramp = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(tc.outputs["Window"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
ramp.color_ramp.elements[0].color = lin("#B6C4DE"); ramp.color_ramp.elements[1].color = lin("#D5BFC3")
ramp.color_ramp.elements[0].position = 0.15; ramp.color_ramp.elements[1].position = 0.9
nt.links.new(ramp.outputs["Color"], bg.inputs["Color"]); nt.links.new(bg.outputs[0], out.inputs[0])
bg.inputs["Strength"].default_value = 1.0

sun_data = bpy.data.lights.new("Sun_Warm", 'SUN'); sun_data.energy = 4.0; sun_data.color = (1.0, 0.82, 0.62); sun_data.angle = math.radians(8)
sun = bpy.data.objects.new("Sun_Warm", sun_data); scene.collection.objects.link(sun)
d = Vector((12, 8, -8)); sun.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

cam_data = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam)
scene.camera = cam
scene.render.resolution_x = 1024; scene.render.resolution_y = 768
try: scene.render.engine = 'BLENDER_EEVEE'
except Exception: scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
try: scene.eevee.taa_render_samples = 64
except Exception: pass
try: scene.eevee.use_raytracing = True
except Exception: pass

# style test used el 56 / az 22 / lens 50; here a lower elevation and closer target so a 4 m tree fills the frame
t = Vector((0, 0, 1.9))
e_, a_, dist = math.radians(32), math.radians(22), 12.5
cam.location = t + Vector((dist * math.cos(e_) * math.sin(a_), -dist * math.cos(e_) * math.cos(a_), dist * math.sin(e_)))
cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_data.type = 'PERSP'; cam_data.lens = 50
scene.render.filepath = os.path.join(OUT, "preview.png")
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "pine_tree.blend"))

# ---------------- compare.png: ours | env_nature_kit | env_rocks_trees ----------------
def load(p):
    im = bpy.data.images.load(p)
    w_, h_ = im.size
    nh = 480; nw = int(round(w_ * nh / h_))
    im.scale(nw, nh)
    a = np.empty(nw * nh * 4, dtype=np.float32); im.pixels.foreach_get(a)
    return a.reshape(nh, nw, 4)

pics = [load(os.path.join(OUT, "preview.png")), load(os.path.join(REF, "env_nature_kit.jpg")),
        load(os.path.join(REF, "env_rocks_trees.jpg"))]
gap = 8
W = sum(p.shape[1] for p in pics) + gap * 2
canvas = np.ones((480, W, 4), dtype=np.float32)
x = 0
for p in pics:
    canvas[:, x:x + p.shape[1], :] = p; canvas[:, x:x + p.shape[1], 3] = 1; x += p.shape[1] + gap
cmp_ = bpy.data.images.new("compare", W, 480, alpha=False)
cmp_.pixels.foreach_set(canvas.ravel())
cmp_.filepath_raw = os.path.join(OUT, "compare.png"); cmp_.file_format = 'PNG'; cmp_.save()
