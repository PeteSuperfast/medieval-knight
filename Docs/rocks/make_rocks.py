import bpy, bmesh, math, random, os
import numpy as np
from mathutils import Vector, Matrix

OUT = r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\rocks"
REF = r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\ref"


def lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1]


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def mat(name, hexc):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(hexc); b.inputs["Roughness"].default_value = 0.9
    return m


M_TOP = mat("MK_rock_light", "#ADABB7")
M_SIDE = mat("MK_rock_mid", "#958A80")
M_GRASS = mat("MK_grass_light", "#8DB04A")

SINK = 0.12  # fraction of total height buried below ground


def make_rock(name, seed, height, size_xy, budget, subdiv=2):
    # icosphere -> uneven squash -> seeded jitter -> flat top + steep sides -> planar decimate
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    sx, sy = rng.uniform(0.9, 1.1), rng.uniform(0.9, 1.1)
    for v in bm.verts:
        x, y, z = v.co
        p = 0.62  # push toward a box so the sides go steep
        x = math.copysign(abs(x) ** p, x); y = math.copysign(abs(y) ** p, y)
        z = math.copysign(abs(z) ** 0.8, z)
        x *= sx; y *= sy
        x += rng.uniform(-.14, .14); y += rng.uniform(-.14, .14); z += rng.uniform(-.12, .12)
        x += 0.12 * z; y -= 0.06 * z  # asymmetric skew
        ztop = 0.55 + 0.10 * x / max(sx, .1) - 0.06 * y / max(sy, .1)  # slightly tilted flat top
        z = min(z, ztop); z = max(z, -0.7)
        v.co = Vector((x, y, z))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    total = height / (1 - SINK)
    ext = [max(v.co[i] for v in me.vertices) - min(v.co[i] for v in me.vertices) for i in range(3)]
    cx = (max(v.co.x for v in me.vertices) + min(v.co.x for v in me.vertices)) / 2
    cy = (max(v.co.y for v in me.vertices) + min(v.co.y for v in me.vertices)) / 2
    for v in me.vertices:
        v.co.x = (v.co.x - cx) * size_xy[0] / ext[0]
        v.co.y = (v.co.y - cy) * size_xy[1] / ext[1]
        v.co.z *= total / ext[2]
    zmin = min(v.co.z for v in me.vertices)
    for v in me.vertices: v.co.z += -zmin - total * SINK
    dg = bpy.context.evaluated_depsgraph_get()
    md = ob.modifiers.new("Planar", 'DECIMATE'); md.decimate_type = 'DISSOLVE'
    best = None
    for ang in range(4, 70, 2):
        md.angle_limit = math.radians(ang)
        dg.update()
        ev = ob.evaluated_get(dg); m2 = ev.to_mesh()
        b2 = bmesh.new(); b2.from_mesh(m2); bmesh.ops.triangulate(b2, faces=b2.faces[:])
        n = len(b2.faces); b2.free(); ev.to_mesh_clear()
        if n <= budget:
            best = ang; break
    md.angle_limit = math.radians(best if best else 70)
    dg.update()
    ev = ob.evaluated_get(dg); m2 = bpy.data.meshes.new_from_object(ev)
    ob.modifiers.remove(md)
    scene.collection.objects.unlink(ob); bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    return m2, best


def finalize(name, meshes_xf):
    bm = bmesh.new()
    for m, xf in meshes_xf:
        tmp = bmesh.new(); tmp.from_mesh(m); tmp.transform(xf)
        t2 = bpy.data.meshes.new("t"); tmp.to_mesh(t2); tmp.free()
        bm.from_mesh(t2); bpy.data.meshes.remove(t2)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(M_TOP); me.materials.append(M_SIDE)
    for p in me.polygons:
        p.use_smooth = False
        p.material_index = 0 if p.normal.z > 0.5 else 1
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    return ob


def T(x=0, y=0, z=0, rz=0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(rz, 4, 'Z')


ms, a1 = make_rock("s", 3, 0.30, (0.5, 0.4), 60, subdiv=1)
small = finalize("Rock_Small", [(ms, T(-3.4, 0.3, 0, 0.5))])
mm, a2 = make_rock("m", 11, 1.0, (1.3, 1.0), 150)
mid = finalize("Rock_Medium", [(mm, T(-1.0, 0, 0, 0.3))])
l1, a3 = make_rock("l1", 21, 2.5, (2.3, 1.9), 250)
l2, a4 = make_rock("l2", 5, 1.7, (1.5, 1.3), 130)
l3, a5 = make_rock("l3", 8, 1.3, (1.2, 1.1), 100)
large = finalize("Rock_Large_Cluster", [(l1, T(3.0, 0.0, 0, 0.2)), (l2, T(4.3, -0.5, 0, 1.0)), (l3, T(2.3, -1.3, 0, 2.2))])

gm = bpy.data.meshes.new("Ground_Flat")
bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=60, radius2=60, depth=0.06)
bm.to_mesh(gm); bm.free()
gm.materials.append(M_GRASS)
for p in gm.polygons: p.use_smooth = False
ground = bpy.data.objects.new("Ground_Flat", gm); scene.collection.objects.link(ground); ground.location.z = -0.03

for ob in (small, mid, large):
    vs = ob.data.vertices
    zs = [v.co.z for v in vs]
    print("ROCK", ob.name, "TRIS", len(ob.data.polygons), "TOTAL_H", round(max(zs) - min(zs), 3),
          "VISIBLE_H", round(max(zs), 3), "MINZ", round(min(zs), 3),
          "X", round(max(v.co.x for v in vs) - min(v.co.x for v in vs), 2),
          "Y", round(max(v.co.y for v in vs) - min(v.co.y for v in vs), 2))
print("ANGLES", a1, a2, a3, a4, a5)

# ---------------- world / light / camera (same setup as pine tree) ----------------
w = bpy.data.worlds.new("Sky"); scene.world = w; w.use_nodes = True
nt = w.node_tree; nt.nodes.clear()
tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
ramp = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(tc.outputs["Window"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
ramp.color_ramp.elements[0].color = lin("#B6C4DE"); ramp.color_ramp.elements[1].color = lin("#D5BFC3")
ramp.color_ramp.elements[0].position = 0.15; ramp.color_ramp.elements[1].position = 0.9
nt.links.new(ramp.outputs["Color"], bg.inputs["Color"]); nt.links.new(bg.outputs[0], out.inputs[0])
bg.inputs["Strength"].default_value = 1.0

sd = bpy.data.lights.new("Sun_Warm", 'SUN'); sd.energy = 4.0; sd.color = (1.0, 0.82, 0.62); sd.angle = math.radians(8)
sun = bpy.data.objects.new("Sun_Warm", sd); scene.collection.objects.link(sun)
sun.rotation_euler = Vector((12, 8, -8)).to_track_quat('-Z', 'Y').to_euler()

cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); scene.collection.objects.link(cam)
scene.camera = cam
scene.render.resolution_x = 1024; scene.render.resolution_y = 768
try: scene.render.engine = 'BLENDER_EEVEE'
except Exception: scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
try: scene.eevee.taa_render_samples = 64
except Exception: pass
try: scene.eevee.use_raytracing = True
except Exception: pass

t = Vector((1.1, 0, 0.9))
e_, a_, dist = math.radians(32), math.radians(22), 13.5
cam.location = t + Vector((dist * math.cos(e_) * math.sin(a_), -dist * math.cos(e_) * math.cos(a_), dist * math.sin(e_)))
cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
cd.type = 'PERSP'; cd.lens = 50
scene.render.filepath = os.path.join(OUT, "preview.png")
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "rocks.blend"))


def load(p):
    im = bpy.data.images.load(p)
    w_, h_ = im.size; nh = 480; nw = int(round(w_ * nh / h_))
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
c = bpy.data.images.new("compare", W, 480, alpha=False)
c.pixels.foreach_set(canvas.ravel())
c.filepath_raw = os.path.join(OUT, "compare.png"); c.file_format = 'PNG'; c.save()
