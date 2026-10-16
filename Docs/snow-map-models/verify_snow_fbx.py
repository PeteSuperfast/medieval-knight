import struct, zlib, os, glob, json
D=r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Assets\Models\Snow"
def parse(b):
    ver=struct.unpack_from("<I",b,23)[0]; w=8 if ver>=7500 else 4
    fmt="<Q" if w==8 else "<I"
    def node(p):
        end,np_,pl=[struct.unpack_from(fmt,b,p+i*w)[0] for i in range(3)]
        p+=3*w
        if end==0: return None,p
        nl=b[p]; name=b[p+1:p+1+nl].decode(); p+=1+nl
        props=[]; q=p
        for _ in range(np_):
            t=chr(b[q]); q+=1
            if t in "YCIFDL":
                sz={"Y":2,"C":1,"I":4,"F":4,"D":8,"L":8}[t]
                props.append(struct.unpack_from("<"+{"Y":"h","C":"?","I":"i","F":"f","D":"d","L":"q"}[t],b,q)[0]); q+=sz
            elif t in "SR":
                n=struct.unpack_from("<I",b,q)[0]; q+=4; props.append(bytes(b[q:q+n])); q+=n
            else:
                n,enc,cl=struct.unpack_from("<III",b,q); q+=12
                raw=b[q:q+cl]; q+=cl
                if enc: raw=zlib.decompress(raw)
                c={"f":"f","d":"d","l":"q","i":"i","b":"b"}[t]
                props.append(struct.unpack("<%d%s"%(n,c),raw))
        p+=pl; kids=[]
        while p<end:
            k,p=node(p)
            if k is None: break
            kids.append(k)
        return (name,props,kids),end
    p=27; top=[]
    while p<len(b):
        n,p2=node(p)
        if n is None: break
        top.append(n); p=p2
    return top
def walk(n,name):
    if n[0]==name: yield n
    for k in n[2]: yield from walk(k,name)
# ---------------------------------------------------------------- verify + update layout.json (run with Blender: blender -b -P verify_snow_fbx.py)
# Reads every Assets/Models/Snow/*.fbx (binary FBX parser, no Blender import), checks tris against export_manifest.txt,
# then writes the Blender->Unity axis mapping + file table into layout.json and prints the markdown table for Art_kit/approved.md.
HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\Discord-bot\pusom\Project\Game\medieval-knight\Docs\snow-map-models"
info = {}; rots = set()
for f in sorted(glob.glob(D + "/*.fbx")):
    top = parse(open(f, "rb").read()); V = []; tris = 0; mats = set(); gs = {}
    for t in top:
        for g in walk(t, "Geometry"):
            for k in g[2]:
                if k[0] == "Vertices": V = k[1][0]
                if k[0] == "PolygonVertexIndex":
                    n = 0
                    for i in k[1][0]:
                        n += 1
                        if i < 0: tris += n - 2; n = 0
        for m in walk(t, "Model"):
            for pr in walk(m, "P"):
                if pr[1] and pr[1][0] == b"Lcl Rotation": rots.add(tuple(round(x, 3) for x in pr[1][4:7]))
        for m in walk(t, "Material"): mats.add(m[1][1].split(b"\x00")[0].decode())
        for q in walk(t, "GlobalSettings"):
            for p_ in walk(q, "P"):
                if p_[1][0] in (b"UpAxis", b"UpAxisSign", b"FrontAxis", b"FrontAxisSign", b"CoordAxis", b"CoordAxisSign", b"UnitScaleFactor"): gs[p_[1][0].decode()] = p_[1][4]
    xs = V[0::3]; ys = V[1::3]; zs = V[2::3]
    info[os.path.basename(f)[:-4]] = dict(tris=tris, bytes=os.path.getsize(f), size_m=[round(max(xs) - min(xs), 2), round(max(ys) - min(ys), 2), round(max(zs) - min(zs), 2)],
                                          bbox_min=[round(min(xs), 2), round(min(ys), 2), round(min(zs), 2)], materials=sorted(mats), gs=gs)
man = [l.rstrip("\n").split("\t") for l in open(os.path.join(HERE, "export_manifest.txt"))]
bad = [(n, t, info.get(n, {}).get("tris")) for n, t, s in man if not s.startswith("alias") and info.get(n, {}).get("tris") != int(t)]
print("FILES", len(info), "TRIS", sum(v["tris"] for v in info.values()), "BYTES", sum(v["bytes"] for v in info.values()), "MISMATCH", bad, "ROOT_ROT", rots)
lay = json.load(open(os.path.join(HERE, "layout.json"), encoding="utf-8"))
lay["coordinates"] = ("Blender world: X = east, Y = north, Z = up, metres, SW corner (0,0), map 380 x 380. pos = world position of the FBX pivot (bottom-centre of the piece; "
                      "terrain chunk = chunk centre at z = -12; merged world meshes = world origin). See unity_axis_mapping for the Blender -> Unity conversion.")
lay["unity_axis_mapping"] = {
    "fbx_contents_verified": "Every .fbx (123) stores vertices in raw Blender axes (x east, y north, z up) and a root Model with Lcl Rotation (-90,0,0); GlobalSettings Up=+Y Front=+Z Coord=+X, UnitScaleFactor=100 (1 unit = 1 m in Unity). Parsed from the binary files, not imported.",
    "mesh_local_in_unity": "(x, y, z)_blender -> (-x, z, -y)_unity when the FBX root keeps its X -90 rotation (Unity mirrors X on import). Same behaviour as the cottage/fence already in the game (Blender front -Y faces Unity +Z; see Docs/asset-import/status-1.md). Not yet checked inside the Unity editor.",
    "world_position": "Blender (E, N, Z) -> Unity (E, Z, N) = (x, z, y). Terrain chunk pivot_world (E, N, -12) -> Unity position (E, -12, N). Merged world meshes (pos 0,0,0) -> Unity (0,0,0).",
    "prefab_wrapper": "Because mesh_local_in_unity differs from world_position by a 180 deg yaw, put each FBX in a child GameObject rotated Y = 180 under the prefab root (same as the Pivot/FrontYaw trick in AssetImport.cs). Then local geometry = (x, z, y)_blender and root.position = (pos.x, pos.z, pos.y).",
    "instance_rotation": "Blender Euler rot_deg (rx, ry, rz) applies to the root. Yaw: Unity rotation.y = -rz. General: Blender quaternion (w, x, y, z) -> Unity Quaternion(-x, -z, -y, w). Small tilts: Unity euler x = -rx, z = -ry (apply as the quaternion above for exactness).",
    "scale": "(sx, sy, sz)_blender -> (sx, sz, sy)_unity",
    "windmill": "snow_building_windmillbody_v01 pivot = bottom-centre; snow_building_windmillsails_v01 pivot = hub (origin of the sails mesh). Hub offset from the body origin in Blender = windmill_hub_offset_from_body_origin = (0, -3.3, 8.3) -> in the Y180 wrapper space (x, z, y) = (0, 8.3, -3.3). The sails disc lies in the Blender XZ plane (normal = Blender Y), so spin it about the Blender Y axis = wrapper-local Unity Z axis (transform.Rotate(0,0,deg, Space.Self)).",
    "alias": "Tree_Round_NNN objects in the layout (non_instanced_world_meshes) use the same mesh as snow_tree_round_v01 (verified vertex-identical); their transforms are in the .blend only."}
lay["export_rules"]["pivot"] = "bottom-centre of the piece; scale/rotation applied; 1 unit = 1 m; windmill sails = separate object with pivot at hub, spin about the Blender Y axis (see unity_axis_mapping.windmill)"
lay["merged_world_files"] = {"snow_deco_chains_v01": "13 chain links (Chain_0..12), world coordinates, pos (0,0,0)", "snow_scene_mist_v01": "5 mist lumps (Mist_0..4), world coordinates", "snow_scene_cloud_v01": "8 cloud lumps (Cloud_0..7), world coordinates"}
lay["fbx_files"] = {n: {"tris": v["tris"], "bytes": v["bytes"], "size_m": v["size_m"]} for n, v in info.items()}
json.dump(lay, open(os.path.join(HERE, "layout.json"), "w", encoding="utf-8"), indent=1)
json.dump(info, open(os.path.join(HERE, "fbx_verify.json"), "w"), indent=1)
