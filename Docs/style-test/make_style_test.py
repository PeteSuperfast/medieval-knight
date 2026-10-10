import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix

OUT = r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\style-test"
random.seed(7)

PAL = {
    "grass_light": "#8DB04A", "grass_mid": "#6C8535", "pine_dark": "#465820", "leaf_light": "#A0B85B",
    "wood_light": "#A1773D", "wood_dark": "#715332", "dirt": "#8A5A32",
    "stone_light": "#ADABB7", "stone_mid": "#958A80", "plaster": "#D8CCB0",
    "roof_purple": "#5E5878", "roof_red": "#B5523B", "iron": "#3A3A3A",
    "glow": "#F5C25A", "mushroom_red": "#C8483A", "water": "#1594BB",
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
    if k == "glow":
        b.inputs["Emission Color"].default_value = lin(v)
        b.inputs["Emission Strength"].default_value = 4.0
    MATS[k] = m


class B:
    def __init__(self, name):
        self.name = name; self.v = []; self.f = []; self.m = []; self.mats = []

    def mi(self, n):
        if n not in self.mats: self.mats.append(n)
        return self.mats.index(n)

    def solid(self, bot, top, mat, side_mats=None):
        """bot: ring of points; top: ring of points or single point (apex)."""
        mats = side_mats or [mat]
        n = len(bot); base = len(self.v)
        self.v += [tuple(p) for p in bot]
        if isinstance(top, Vector) or (len(top) == 3 and isinstance(top[0], float)):
            self.v.append(tuple(top)); a = base + n
            for i in range(n):
                self.f.append((base + i, base + (i + 1) % n, a)); self.m.append(self.mi(mats[i % len(mats)]))
        else:
            self.v += [tuple(p) for p in top]
            for i in range(n):
                j = (i + 1) % n
                self.f.append((base + i, base + j, base + n + j, base + n + i)); self.m.append(self.mi(mats[i % len(mats)]))
            self.f.append(tuple(base + n + i for i in range(n))); self.m.append(self.mi(mat if len(mats) == 1 else mats[0]))
        self.f.append(tuple(reversed([base + i for i in range(n)]))); self.m.append(self.mi(mat if len(mats) == 1 else mats[0]))

    def tri(self, pts, mat):
        base = len(self.v); self.v += [tuple(p) for p in pts]
        self.f.append(tuple(range(base, base + len(pts)))); self.m.append(self.mi(mat))

    def box(self, M, sx, sy, sz, mat, taper=1.0, jit=0.0):
        hx, hy = sx / 2, sy / 2
        bot = [(-hx, -hy, 0), (hx, -hy, 0), (hx, hy, 0), (-hx, hy, 0)]
        t = taper
        top = [(-hx * t, -hy * t, sz), (hx * t, -hy * t, sz), (hx * t, hy * t, sz), (-hx * t, hy * t, sz)]
        def j(p): return (p[0] + random.uniform(-jit, jit), p[1] + random.uniform(-jit, jit), p[2])
        self.solid([M @ Vector(p) for p in bot], [M @ Vector(j(p)) for p in top], mat)

    def cyl(self, M, r0, r1, h, n, mat, jit=0.0, rot=0.0, side_mats=None):
        def ring(r, z, jj):
            return [Vector((r * math.cos(rot + 2 * math.pi * i / n) + random.uniform(-jj, jj),
                            r * math.sin(rot + 2 * math.pi * i / n) + random.uniform(-jj, jj), z)) for i in range(n)]
        bot = [M @ p for p in ring(r0, 0, jit)]
        if r1 <= 0:
            top = M @ Vector((0, 0, h))
            self.solid(bot, top, mat, side_mats)
        else:
            self.solid(bot, [M @ p for p in ring(r1, h, jit)], mat, side_mats)

    def build(self, loc=(0, 0, 0), rotz=0.0):
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
        ob.location = loc; ob.rotation_euler = (0, 0, rotz)
        return ob


def T(x=0, y=0, z=0, rx=0, ry=0, rz=0):
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(rz, 4, 'Z') @ Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X')

def bevel(ob, w=0.025):
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    md = ob.modifiers.new("Bevel", 'BEVEL')
    md.width = w; md.segments = 1; md.limit_method = 'ANGLE'; md.angle_limit = math.radians(35)
    bpy.ops.object.modifier_apply(modifier="Bevel")
    ob.select_set(False)
    for p in ob.data.polygons: p.use_smooth = False

# ---------------- HOUSE ----------------
def make_house():
    b = B("House_Medieval")
    b.box(T(), 6.4, 5.4, 0.55, "stone_mid", 0.98, 0.02)                       # stone base
    for x in (-2.2, -0.9, 1.5, 2.4):                                           # base blocks
        b.box(T(x, -2.72, 0.12), 0.7, 0.16, 0.3, "stone_light")
    b.box(T(0, 0, 0.55), 6.0, 5.0, 2.45, "plaster", 0.96, 0.02)               # walls
    # gable body (triangle prism along X)
    gx = 2.88
    tri_a = [(-gx, -2.4, 3.0), (-gx, 2.4, 3.0), (-gx, 0, 5.8)]
    tri_b = [(gx, -2.4, 3.0), (gx, 2.4, 3.0), (gx, 0, 5.8)]
    b.solid([Vector(p) for p in tri_a], [Vector(p) for p in tri_b], "plaster")
    # timber frame
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box(T(sx * 2.95, sy * 2.45, 0.4), 0.26, 0.26, 2.75, "wood_dark", 0.97)
    for y in (-2.5, 2.5):
        b.box(T(0, y, 2.85), 6.2, 0.2, 0.22, "wood_dark")
        b.box(T(0, y, 1.55), 5.8, 0.14, 0.16, "wood_dark")
    for x in (-2.95, 2.95):
        b.box(T(x, 0, 2.85), 0.2, 5.2, 0.22, "wood_dark")
    for sx in (-1, 1):                                                         # diagonals on front
        b.box(T(sx * 2.1, -2.52, 1.6, ry=0).__matmul__(Matrix.Rotation(sx * 0.7, 4, 'Y')), 0.14, 0.12, 1.5, "wood_dark")
    # door
    b.box(T(0, -2.5, 0.5), 1.05, 0.14, 1.85, "wood_light")
    for sx in (-1, 1):
        b.box(T(sx * 0.7, -2.54, 0.5), 0.28, 0.2, 1.95, "stone_light")
    b.box(T(0, -2.54, 2.25), 1.7, 0.2, 0.32, "stone_light")
    b.box(T(0.3, -2.6, 1.3), 0.12, 0.05, 0.12, "iron")
    b.box(T(0, -3.1, 0), 1.7, 0.8, 0.15, "stone_mid", 0.95)
    # windows (emissive)
    def window_front(x, z):
        b.box(T(x, -2.5, z - 0.08), 1.0, 0.1, 1.0, "wood_dark")
        b.box(T(x, -2.55, z), 0.72, 0.1, 0.72, "glow")
        b.box(T(x, -2.6, z + 0.34), 0.76, 0.04, 0.05, "wood_dark")
        b.box(T(x, -2.6, z + 0.0), 0.05, 0.04, 0.74, "wood_dark")
    window_front(-1.9, 1.15); window_front(1.9, 1.15)
    for sx in (-1, 1):
        b.box(T(sx * 2.85, 0, 1.0), 0.1, 1.0, 1.0, "wood_dark")
        b.box(T(sx * 2.9, 0, 1.08), 0.1, 0.7, 0.7, "glow")
        b.box(T(sx * 2.92, 0, 3.55), 0.1, 0.55, 0.65, "wood_dark")
        b.box(T(sx * 2.96, 0, 3.6), 0.08, 0.38, 0.5, "glow")
    # roof: two slabs, steep, big overhang
    th = 0.24; x0, x1 = -3.7, 3.7
    ridge = 6.4; eave_z = 2.7; eave_y = 3.15
    for s in (-1, 1):
        A = Vector((0, s * eave_y, eave_z)) - Vector((0, 0, ridge))
        nrm = Vector((0, -s * math.cos(0), 0))
        d = Vector((0, s * eave_y, eave_z - ridge)); d.normalize()
        n = Vector((0, -d.z * -1 * 0, 0))
        n = Vector((0, d.z, -d.y)) if s < 0 else Vector((0, -d.z, d.y))
        n.normalize()
        if n.z < 0: n = -n
        top = [Vector((x0, 0.0, ridge)), Vector((x0 - 0.1, s * eave_y, eave_z)),
               Vector((x1 + 0.1, s * eave_y, eave_z)), Vector((x1, 0.0, ridge))]
        bot = [p - n * th for p in top]
        # curl the eave edge up slightly (stylized)
        b.solid(bot, top, "roof_red")
    b.box(T(0, 0, ridge - 0.08), 7.6, 0.4, 0.3, "wood_dark", 0.9)              # ridge beam
    for sx in (-1, 1):                                                         # upturned ridge tips
        b.box(T(sx * 3.75, 0, ridge + 0.02, ry=-sx * 0.55), 0.7, 0.34, 0.26, "wood_dark", 0.8)
    # chimney
    b.box(T(1.75, 0.9, 4.4), 0.85, 0.85, 2.5, "stone_mid", 0.88, 0.02)
    b.box(T(1.75, 0.9, 6.85), 1.05, 1.05, 0.22, "stone_light")
    ob = b.build()
    bevel(ob, 0.03)
    return ob

def make_pine():
    b = B("Tree_Pine_Dark")
    b.cyl(T(), 0.34, 0.22, 1.5, 6, "wood_dark", jit=0.02)
    tiers = [(1.0, 1.7, 2.2, "pine_dark"), (2.3, 1.45, 2.1, "grass_mid"), (3.6, 1.2, 2.0, "pine_dark"),
             (4.9, 0.95, 1.9, "grass_mid"), (6.1, 0.7, 1.8, "pine_dark"), (7.2, 0.42, 1.7, "grass_mid")]
    for z, r, h, m in tiers:
        b.cyl(T(random.uniform(-.04, .04), random.uniform(-.04, .04), z), r, 0, h, 7, m, jit=0.08, rot=random.uniform(0, 1))
    ob = b.build()
    return ob

def make_fence():
    b = B("Fence_Wood_Section")
    L = 3.2
    for sx, lean in ((-1, 0.03), (1, -0.04)):
        b.box(T(sx * L / 2, 0, 0, rx=lean), 0.2, 0.2, 1.25, "wood_dark", 0.85, 0.01)
        b.cyl(T(sx * L / 2, 0, 1.25, rx=lean), 0.17, 0, 0.2, 4, "wood_dark", rot=math.pi / 4)
    for z, tw in ((0.38, 0.03), (0.82, -0.04)):
        b.box(T(0, -0.15, z, rx=tw, rz=random.uniform(-.02, .02)), L + 0.3, 0.09, 0.2, "wood_light", 0.97, 0.01)
    b.box(T(0, 0.12, 0.35, ry=0.0, rx=0).__matmul__(Matrix.Rotation(0.5, 4, 'Y')), 0.1, 0.07, 1.4, "wood_light") if False else None
    # diagonal brace behind
    b.box(T(0, 0.14, 0.2).__matmul__(Matrix.Rotation(-0.72, 4, 'Y')), 0.12, 0.08, 1.9, "wood_light")
    for sx in (-1, 1):                                                         # iron nails
        for z in (0.45, 0.9):
            b.box(T(sx * (L / 2 - 0.02), -0.22, z - 0.03), 0.06, 0.04, 0.06, "iron")
    ob = b.build()
    bevel(ob, 0.02)
    return ob

def make_barrel():
    b = B("Barrel_Wood")
    n = 10
    sm = ["wood_light", "wood_dark"]
    b.cyl(T(), 0.34, 0.43, 0.45, n, "wood_light", side_mats=sm)
    b.cyl(T(0, 0, 0.45), 0.43, 0.34, 0.45, n, "wood_light", side_mats=sm)
    b.cyl(T(0, 0, 0.9), 0.31, 0.31, 0.03, n, "wood_dark")
    for z in (0.1, 0.72):
        b.cyl(T(0, 0, z), 0.4 + (0.02 if z < 0.5 else 0.02), 0.4 + 0.02, 0.1, n, "iron")
    b.cyl(T(0, 0, 0.36), 0.455, 0.455, 0.1, n, "iron")
    ob = b.build()
    return ob

# ---------------- build ----------------
house = make_house(); house.location = (-3.0, 1.9, 0); house.rotation_euler = (0, 0, math.radians(-8))
pine = make_pine(); pine.location = (4.8, 2.0, 0); pine.rotation_euler = (0, 0, 0.5)
fence = make_fence(); fence.location = (0.6, -2.0, 0); fence.rotation_euler = (0, 0, math.radians(7))
barrel = make_barrel(); barrel.location = (3.0, -1.2, 0)

# ground island
g = B("Ground_GrassIsland")
n = 16
rx_, ry_ = 8.6, 6.4
ring0 = []; ring1 = []; ring2 = []
for i in range(n):
    a = 2 * math.pi * i / n
    k = random.uniform(0.92, 1.05)
    ring0.append(Vector((rx_ * k * math.cos(a), ry_ * k * math.sin(a), random.uniform(-0.04, 0.04))))
    ring1.append(Vector((rx_ * k * 0.97 * math.cos(a), ry_ * k * 0.97 * math.sin(a), -1.1 + random.uniform(-.1, .1))))
    ring2.append(Vector((rx_ * k * 0.55 * math.cos(a + 0.2), ry_ * k * 0.55 * math.sin(a + 0.2), -2.7 + random.uniform(-.2, .2))))
c = Vector((0, 0, 0)); apex = Vector((0.3, 0.2, -4.4))
inner = [[], []]
for i in range(n):
    for lvl, f in enumerate((0.36, 0.68)):
        pnt = ring0[i] * f
        pnt.z = random.uniform(-0.05, 0.12) if lvl == 0 else random.uniform(-0.05, 0.08)
        pnt.x += random.uniform(-0.4, 0.4); pnt.y += random.uniform(-0.4, 0.4)
        inner[lvl].append(pnt)
GR = ["grass_light", "grass_light", "grass_mid", "leaf_light"]
for i in range(n):
    j = (i + 1) % n
    g.tri([c, inner[0][i], inner[0][j]], random.choice(GR))
    g.tri([inner[0][i], inner[1][i], inner[1][j], inner[0][j]], random.choice(GR))
    g.tri([inner[1][i], ring0[i], ring0[j], inner[1][j]], random.choice(GR))
for i in range(n):
    j = (i + 1) % n
    g.tri([ring0[i], ring1[i], ring1[j], ring0[j]], "dirt")
    g.tri([ring1[i], ring2[i], ring2[j], ring1[j]], random.choice(["dirt", "wood_dark"]))
    g.tri([ring2[j], ring2[i], apex], random.choice(["dirt", "wood_dark", "stone_mid"]))
ground = g.build()

# extras (small details)
e = B("Details_Small")
def rock(x, y, s, m):
    e.cyl(T(x, y, -0.02, rz=random.uniform(0, 3)), s, s * 0.45, s * 0.8, 6, m, jit=s * 0.18)
rock(-6.0, -2.0, 0.7, "stone_mid"); rock(-5.3, -2.6, 0.4, "stone_light"); rock(6.0, -1.6, 0.55, "stone_light")
rock(6.2, 3.6, 0.7, "stone_mid"); rock(0.8, 4.2, 0.45, "stone_light")
for gx_, gy_, k in ((-0.6, 0.0, 1), (3.3, 1.0, .8), (3.6, 3.2, 1), (-5.6, 2.2, 1), (2.5, -3.2, .7)):   # mushrooms
    for dx, dy, s in ((0, 0, 1.0), (0.3, -0.12, 0.65)):
        x, y = gx_ + dx * k, gy_ + dy * k
        e.cyl(T(x, y, 0), 0.08 * s, 0.07 * s, 0.26 * s, 5, "plaster")
        e.cyl(T(x, y, 0.24 * s), 0.27 * s, 0, 0.2 * s, 6, "mushroom_red", jit=0.01)
for i in range(18):                                                               # grass tufts
    x, y = random.uniform(-8, 8), random.uniform(-5.5, 5.5)
    if (x / 8.2) ** 2 + (y / 6.0) ** 2 > 1: continue
    for k in range(3):
        e.cyl(T(x + k * 0.07, y + random.uniform(-.06, .06), 0, rx=random.uniform(-.3, .3), ry=random.uniform(-.3, .3)),
              0.08, 0, random.uniform(0.35, 0.6), 3, random.choice(["leaf_light", "grass_mid", "pine_dark"]))
path = [(-2.7, -1.2), (-2.2, -2.3), (-1.4, -3.2), (-0.4, -3.9), (0.9, -4.3)]       # stepping stones
for (x, y) in path:
    e.cyl(T(x, y, 0, rz=random.uniform(0, 3)), 0.4, 0.34, 0.08, 6, "stone_light", jit=0.04)
# chimney smoke puffs (octahedral low-poly)
for z, r, dx in ((7.7, 0.28, 0.0), (8.35, 0.38, 0.3), (9.15, 0.46, 0.2)):
    pass
details = e.build()

smoke = B("Chimney_Smoke")
for z, r, dx in ((7.55, 0.3, 0.0), (8.2, 0.4, 0.35), (9.0, 0.5, 0.25)):
    ico = bmesh.new()
    bmesh.ops.create_icosphere(ico, subdivisions=1, radius=r)
    base = len(smoke.v)
    for v in ico.verts: smoke.v.append((v.co.x + 1.75 + dx, v.co.y + 0.9, v.co.z + z))
    for f in ico.faces:
        smoke.f.append(tuple(base + v.index for v in f.verts)); smoke.m.append(smoke.mi("plaster"))
    ico.free()
sm = smoke.build()
# rotate smoke with the house (parent)
for ob in (sm,):
    ob.parent = house
    ob.location = (0, 0, 0)

# ---------------- stats ----------------
stats = {}
for ob in (house, pine, fence, barrel, ground, details, sm):
    stats[ob.name] = sum(len(p.vertices) - 2 for p in ob.data.polygons)

# ---------------- world / light / render ----------------
w = bpy.data.worlds.new("Sky"); scene.world = w; w.use_nodes = True
nt = w.node_tree; nt.nodes.clear()
tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
ramp = nt.nodes.new("ShaderNodeValToRGB"); bg = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
mp = nt.nodes.new("ShaderNodeMapRange")
nt.links.new(tc.outputs["Window"], sep.inputs[0]); nt.links.new(sep.outputs["Y"], ramp.inputs["Fac"])
ramp.color_ramp.elements[0].color = lin("#B6C4DE"); ramp.color_ramp.elements[1].color = lin("#D5BFC3")
ramp.color_ramp.elements[0].position = 0.15; ramp.color_ramp.elements[1].position = 0.9
nt.links.new(ramp.outputs["Color"], bg.inputs["Color"]); nt.links.new(bg.outputs[0], out.inputs[0])
bg.inputs["Strength"].default_value = 1.0

sun_data = bpy.data.lights.new("Sun_Warm", 'SUN'); sun_data.energy = 4.0; sun_data.color = (1.0, 0.82, 0.62); sun_data.angle = math.radians(8)
sun = bpy.data.objects.new("Sun_Warm", sun_data); scene.collection.objects.link(sun)
d = Vector((12, 8, -8)); sun.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()   # light from left-front, low

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

def shot(path, el, az, dist, lens=None, ortho=None):
    t = Vector((0, 0.2, 1.4))
    e_, a_ = math.radians(el), math.radians(az)
    cam.location = t + Vector((dist * math.cos(e_) * math.sin(a_), -dist * math.cos(e_) * math.cos(a_), dist * math.sin(e_)))
    if el >= 89:
        cam.rotation_euler = (0, 0, 0)
    else:
        cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        cam_data.type = 'ORTHO'; cam_data.ortho_scale = ortho
    else:
        cam_data.type = 'PERSP'; cam_data.lens = lens or 45
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

shot(os.path.join(OUT, "preview.png"), 56, 22, 33, lens=50)
shot(os.path.join(OUT, "preview_top.png"), 90, 0, 40, ortho=20)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "style_test.blend"))
print("TRIS", stats)
