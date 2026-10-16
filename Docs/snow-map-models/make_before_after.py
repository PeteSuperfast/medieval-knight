"""before_after.png: blockout (world-map-preview/world_map.blend, read-only) vs new snow_map.blend, village corner + castle corner.
Run: blender --background --factory-startup --python make_before_after.py"""
import bpy, os, math
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OLD = os.path.join(HERE, "..", "world-map-preview", "world_map.blend")
NEW = os.path.join(HERE, "snow_map.blend")
VIEWS = {"village": ((-6, -22, 24), (30, 36, 3), 40), "castle": ((300, 262, 112), (340, 338, 90), 40)}
W, H = 1280, 720
tmp = os.path.join(HERE, "_tmp_ba")
os.makedirs(tmp, exist_ok=True)


def render(blend, tag):
    bpy.ops.wm.open_mainfile(filepath=blend)
    sc = bpy.data.scenes["WorldMap"]; bpy.context.window.scene = sc if bpy.context.window else None
    cam = sc.objects["Cam"]; sc.camera = cam
    cam.data.type = 'PERSP'; cam.data.clip_start = 0.5; cam.data.clip_end = 3000
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = W, H, 100
    sc.eevee.taa_render_samples = 48
    out = {}
    for nm, (pos, tgt, lens) in VIEWS.items():
        cam.data.lens = lens; cam.location = pos
        cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
        fp = os.path.join(tmp, f"{tag}_{nm}.png"); sc.render.filepath = fp
        bpy.ops.render.render(write_still=True, scene=sc.name); out[nm] = fp
    return out


def load(fp):
    im = bpy.data.images.load(fp); a = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4); return a


a = render(OLD, "old"); b = render(NEW, "new")
top = np.concatenate([load(a["village"]), load(b["village"])], axis=1)
bot = np.concatenate([load(a["castle"]), load(b["castle"])], axis=1)
sheet = np.concatenate([bot, top], axis=0)       # pixel rows are bottom-up: castle row ends up below the village row
bpy.ops.wm.read_factory_settings(use_empty=True)
im = bpy.data.images.new("ba", 2 * W, 2 * H, alpha=True); im.pixels = sheet.ravel().tolist()
im.filepath_raw = os.path.join(HERE, "before_after.png"); im.file_format = 'PNG'; im.save()
for f in os.listdir(tmp): os.remove(os.path.join(tmp, f))
os.rmdir(tmp)
print("BA DONE")
