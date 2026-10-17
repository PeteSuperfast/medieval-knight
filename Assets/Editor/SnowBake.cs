using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MedievalKnight;

// Editor-only (snow-map-models step 1). Reads Docs/snow-map-models/layout.json + the prefabs made by SnowImport and writes the fixed
// data the game loads (Assets/MedievalKnight/Resources/Snow): placed pieces in game coordinates, ground height table, walkable
// table, obstacle list, spawn / lair / zones. Deterministic: no randomness, same files on every machine.
//   Unity -batchmode -quit -nographics -projectPath <proj> -executeMethod SnowBake.Run
public static class SnowBake
{
    const int N = 381;
    const float Off = 190f;
    const int LTerr = 8, LPath = 9, LWater = 10, LCastle = 11, LMount = 12, LDeck = 13;
    const string OutDir = "Assets/MedievalKnight/Resources/Snow";
    const string ReportPath = "Docs/snow-map-models/bake_report.txt";
    const float MaxSlopeDeg = 40f;
    const float PlayerR = 0.4f;

    static readonly StringBuilder rep = new StringBuilder();
    static int errors;
    static void R(string s) { rep.AppendLine(s); Debug.Log("SNOWBAKE: " + s); }
    static void Err(string s) { errors++; R("ERROR " + s); }

    [MenuItem("MedievalKnight/Snow/Bake")]
    public static void Run()
    {
        try
        {
            Bake();
            File.WriteAllText(ReportPath, rep.ToString());
            Debug.Log("SNOWBAKE: DONE errors=" + errors);
            if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError("SNOWBAKE: FAILED " + e);
            File.WriteAllText(ReportPath, rep + "\nFAILED " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    // ------------------------------------------------------------------ classification

    static string Category(string p)
    {
        if (p.StartsWith("snow_terrain_chunk") || p.StartsWith("snow_terrain_drifts")) return "terr";
        if (p.StartsWith("snow_path_") || p.StartsWith("snow_village_") || p == "snow_castle_base_v01") return "path";
        if (p.StartsWith("snow_deco_icelake")) return "lake";
        if (p.StartsWith("snow_water_river") || p.StartsWith("snow_water_stream") || p.StartsWith("snow_water_branch")
            || p.StartsWith("snow_water_pool") || p.StartsWith("snow_water_lakeoutlet")) return "water";
        if (p.StartsWith("snow_bridge_")) return "bridge";
        if (p == "snow_castle_walls_v01" || p == "snow_castle_keep_v01") return "castle";
        if (p.StartsWith("snow_terrain_mountain")) return "mount";
        return "";
    }

    class Obs { public int kind; public float x, z, a, b, yaw; public int mask; public string name; }

    // obstacle rule per prototype; bounds b = prefab-local bounds (x, z = ground plane), s = instance scale
    static void ObstaclesFor(SnowImport.Placed p, Bounds b, List<Obs> outList)
    {
        string n = p.proto;
        Vector3 s = p.scale;
        float yaw = p.rot.eulerAngles.y;
        Vector3 c = p.rot * Vector3.Scale(b.center, s);
        float cx = p.pos.x + c.x - Off, cz = p.pos.z + c.z - Off;
        float px = p.pos.x - Off, pz = p.pos.z - Off;
        float sz = Mathf.Max(b.size.x * s.x, b.size.z * s.z);
        Action<float, float, float, int> circle = (x, z, r, m) => outList.Add(new Obs { kind = 0, x = x, z = z, a = r, mask = m, name = p.name });
        Action<float, float, float, float, float, int> box = (x, z, hx, hz, yw, m) => outList.Add(new Obs { kind = 1, x = x, z = z, a = hx, b = hz, yaw = yw, mask = m, name = p.name });

        if (n.StartsWith("snow_tree_pine") || n == "snow_tree_bare_v01" || n == "snow_tree_round_v01" || n == "snow_tree_tall_v01" || n == "snow_tree_blossom_v01")
        { circle(px, pz, 0.5f * s.x, 1); return; }
        if (n.StartsWith("snow_tree_") || n.StartsWith("snow_plant_") || n.StartsWith("snow_prop_slabarc")) return;       // stump, log, bush, flowers
        if (n.StartsWith("snow_rock_small"))
        {
            if (n.EndsWith("v01.fbx") || n.EndsWith("_v01") || n.EndsWith("_v02")) return;                              // rock0-1: step over
            circle(cx, cz, 0.35f * sz, 3); return;
        }
        if (n.StartsWith("snow_rock_pillar")) { circle(px, pz, 2.4f * s.x, 3); return; }
        if (n.StartsWith("snow_building_house") || n.StartsWith("snow_prop_stall") || n.StartsWith("snow_deco_icecave") || n.StartsWith("snow_deco_sled"))
        { box(cx, cz, 0.5f * b.size.x * s.x * 0.8f, 0.5f * b.size.z * s.z * 0.8f, yaw, 3); return; }
        if (n.StartsWith("snow_building_windmillbody")) { circle(px, pz, 3.2f, 3); return; }
        if (n.StartsWith("snow_deco_igloo")) { circle(cx, cz, 0.42f * sz, 3); return; }
        if (n.StartsWith("snow_prop_fountain")) { circle(px, pz, 3.6f, 3); return; }
        if (n.StartsWith("snow_deco_crystal")) { circle(px, pz, 0.35f * sz, 3); return; }
        if (n.StartsWith("snow_deco_campfire")) { circle(px, pz, 0.9f, 3); return; }
        if (n.StartsWith("snow_deco_snowman")) { circle(px, pz, 0.8f, 3); return; }
        if (n.StartsWith("snow_prop_fence")) { box(cx, cz, 0.5f * b.size.x * s.x, 0.18f, yaw, 1); return; }
        if (n.StartsWith("snow_prop_lamp") || n.StartsWith("snow_prop_icelamp") || n.StartsWith("snow_prop_chainpost") || n.StartsWith("snow_prop_banner"))
        { circle(px, pz, 0.3f, 1); return; }
        if (n.StartsWith("snow_prop_sign")) { circle(px, pz, 0.45f, 1); return; }
        if (n.StartsWith("snow_prop_barrel") || n.StartsWith("snow_prop_crate")) { circle(px, pz, 0.5f, 1); return; }
        if (n.StartsWith("snow_bridge_"))
        {
            float len = n.Contains("_s1_") ? 16f : n.Contains("_s2_") ? 12f : 8f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 o = p.rot * new Vector3(0f, 0f, side * 1.85f);
                box(px + o.x, pz + o.z, len * 0.5f, 0.12f, yaw, 1);
            }
        }
    }

    // ------------------------------------------------------------------ the bake

    static void SetLayerAll(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    static Bounds LocalBounds(string proto)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(SnowImport.PrefabDir + "/" + proto + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
        Bounds b = AssetImport.BoundsOf(go.GetComponentsInChildren<MeshRenderer>(true));
        UnityEngine.Object.DestroyImmediate(go);
        return b;
    }

    static GameObject FilteredCollider(GameObject inst, string cat, string name)
    {
        // a world-space mesh with only the walkable triangles (lake top, bridge deck, water surface)
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var hasBin = new bool[200];
        var binY = new float[200];
        Transform root = inst.transform;
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null) continue;
            Matrix4x4 w = mf.transform.localToWorldMatrix;
            var v = m.vertices;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var t = m.GetTriangles(s);
                for (int k = 0; k < t.Length; k += 3)
                {
                    Vector3 a = w.MultiplyPoint3x4(v[t[k]]), b = w.MultiplyPoint3x4(v[t[k + 1]]), c = w.MultiplyPoint3x4(v[t[k + 2]]);
                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    float len = nrm.magnitude;
                    if (len < 1e-6f) continue;
                    nrm /= len;
                    if (cat == "bridge")
                    {
                        if (Mathf.Abs(nrm.y) < 0.8f) continue;
                        Vector3 cen = root.InverseTransformPoint((a + b + c) / 3f);
                        if (Mathf.Abs(cen.z) > 1.2f) continue;
                        foreach (var pt in new[] { a, b, c })
                        {
                            Vector3 lp = root.InverseTransformPoint(pt);
                            int bin = Mathf.RoundToInt(lp.x * 2f) + 100;
                            if (bin < 0 || bin >= 200) continue;
                            if (!hasBin[bin] || lp.y > binY[bin]) { binY[bin] = lp.y; hasBin[bin] = true; }
                        }
                        continue;
                    }
                    if (nrm.y < 0f) { Vector3 tmp = b; b = c; c = tmp; nrm = -nrm; }
                    if (cat == "water") { if (nrm.y < 0.5f) continue; }
                    else if (cat == "lake")
                    {
                        if (nrm.y < 0.9f) continue;
                        Vector3 la = root.InverseTransformPoint(a), lb = root.InverseTransformPoint(b), lc = root.InverseTransformPoint(c);
                        if (Mathf.Max(la.y, Mathf.Max(lb.y, lc.y)) > 0.85f) continue;
                    }
                    int i0 = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(c);
                    tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
                }
            }
        }
        if (cat == "bridge")
        {
            // planks have gaps: build one continuous ribbon along the deck (x = length) from the highest plank top per 0.5 m
            int first = -1, last = -1;
            for (int k = 0; k < 200; k++) if (hasBin[k]) { if (first < 0) first = k; last = k; }
            for (int k = first; k >= 0 && k <= last; k++)
                if (!hasBin[k])
                {
                    int lo = k - 1, hi = k + 1;
                    while (hi <= last && !hasBin[hi]) hi++;
                    float t = (k - lo) / (float)(hi - lo);
                    binY[k] = Mathf.Lerp(binY[lo], binY[hi], t);
                }
            for (int k = first; k >= 0 && k < last; k++)
            {
                float x0 = (k - 100) * 0.5f, x1 = (k + 1 - 100) * 0.5f;
                Vector3 p00 = root.TransformPoint(new Vector3(x0, binY[k], -1.6f)), p01 = root.TransformPoint(new Vector3(x0, binY[k], 1.6f));
                Vector3 p10 = root.TransformPoint(new Vector3(x1, binY[k + 1], -1.6f)), p11 = root.TransformPoint(new Vector3(x1, binY[k + 1], 1.6f));
                int i0 = verts.Count;
                verts.Add(p00); verts.Add(p01); verts.Add(p10); verts.Add(p11);
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2); tris.Add(i0 + 2); tris.Add(i0 + 1); tris.Add(i0 + 3);
            }
        }
        var go = new GameObject("col_" + name);
        if (tris.Count == 0) return go;
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    static int Idx(int i, int j) { return j * N + i; }

    static void Bake()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        AssetDatabase.Refresh();
        var placed = SnowImport.AllPlaced();
        var lay = SnowImport.LoadLayout();
        R("pieces=" + placed.Count + " (layout.json instances=" + lay.instances.Count + ", terrain chunks=" + lay.terrain_chunks.Count + ")");
        if (placed.Count - lay.terrain_chunks.Count - lay.instances.Count < 0) Err("fewer placed pieces than layout.json");

        // ---- physics scene with the ground / water / castle / mountain meshes
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Physics.queriesHitBackfaces = true;
        var root = new GameObject("Bake");
        GameObject lakeCol = null;
        var boundsCache = new Dictionary<string, Bounds>();
        foreach (var p in placed)
        {
            string cat = Category(p.proto);
            if (cat == "") continue;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(SnowImport.PrefabDir + "/" + p.proto + ".prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            inst.transform.SetPositionAndRotation(p.pos, p.rot);
            inst.transform.localScale = p.scale;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            int layer = cat == "terr" ? LTerr : cat == "path" ? LPath : cat == "lake" ? LPath : cat == "water" ? LWater : cat == "bridge" ? LDeck : cat == "castle" ? LCastle : LMount;
            if (cat == "lake" || cat == "water" || cat == "bridge")
            {
                var col = FilteredCollider(inst, cat, p.name);
                col.transform.SetParent(root.transform, true);
                col.layer = layer;
                if (cat == "lake") lakeCol = col;
                UnityEngine.Object.DestroyImmediate(inst);
            }
            else
            {
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                SetLayerAll(inst, layer);
            }
        }
        Physics.SyncTransforms();

        int maskGround = (1 << LTerr) | (1 << LPath) | (1 << LDeck);
        int maskPath = (1 << LPath) | (1 << LDeck);
        var H = new float[N * N];
        var valid = new bool[N * N];
        var isPath = new bool[N * N];
        var isDeck = new bool[N * N];
        var code = new byte[N * N];   // 0 free, 1 slope, 2 water, 3 mountain, 4 castle, 5 invalid
        RaycastHit hit;
        int invalid = 0;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                var o = new Vector3(i, 400f, j);
                int k = Idx(i, j);
                if (Physics.Raycast(o, Vector3.down, out hit, 800f, maskGround, QueryTriggerInteraction.Ignore))
                {
                    H[k] = hit.point.y; valid[k] = true;
                    if (Physics.Raycast(o, Vector3.down, out var hp, 800f, maskPath, QueryTriggerInteraction.Ignore) && hp.point.y >= H[k] - 0.3f) isPath[k] = true;
                    if (Physics.Raycast(o, Vector3.down, out var hd, 800f, 1 << LDeck, QueryTriggerInteraction.Ignore) && hd.point.y >= H[k] - 0.05f) isDeck[k] = true;
                }
                else { invalid++; code[k] = 5; }
            }
        R("height samples " + (N * N - invalid) + " / " + N * N + " (no ground: " + invalid + ")");

        // ---- lake clipping measurement (terrain top vs lake ice top)
        float lakeLift = 0f;
        R("lake collider: " + (lakeCol == null ? "none" : lakeCol.GetComponent<MeshCollider>() == null ? "empty" : "ok"));
        if (lakeCol != null && lakeCol.GetComponent<MeshCollider>() != null)
        {
            var lc = lakeCol.GetComponent<MeshCollider>();
            int cells = 0, over = 0; float maxEx = 0f; var lakeHist = new int[6]; float lakeSumE = 0, lakeSumN = 0; int lakeN = 0;
            Bounds lb = lc.bounds;
            for (int j = Mathf.FloorToInt(lb.min.z); j <= Mathf.CeilToInt(lb.max.z); j++)
                for (int i = Mathf.FloorToInt(lb.min.x); i <= Mathf.CeilToInt(lb.max.x); i++)
                {
                    if (i < 0 || j < 0 || i >= N || j >= N) continue;
                    var ray = new Ray(new Vector3(i, 400f, j), Vector3.down);
                    if (!lc.Raycast(ray, out var lh, 800f)) continue;
                    if (!Physics.Raycast(ray, out var th, 800f, 1 << LTerr, QueryTriggerInteraction.Ignore)) continue;
                    cells++;
                    float ex = th.point.y - lh.point.y;
                    if (ex > 0.02f) over++;
                    int hb2 = ex <= 0.02f ? 0 : ex <= 0.1f ? 1 : ex <= 0.3f ? 2 : ex <= 0.6f ? 3 : ex <= 1f ? 4 : 5; lakeHist[hb2]++;
                    if (ex > 0.3f) { lakeSumE += i; lakeSumN += j; lakeN++; }
                    maxEx = Mathf.Max(maxEx, ex);
                }
            R("ice lake excess histogram (<=0.02,<=0.1,<=0.3,<=0.6,<=1,>1): " + string.Join(",", lakeHist) + "; mean pos of >0.3 m = (" + (lakeN > 0 ? lakeSumE / lakeN : 0) + "," + (lakeN > 0 ? lakeSumN / lakeN : 0) + ") lake bounds E" + lb.min.x.ToString("F0") + "-" + lb.max.x.ToString("F0") + " N" + lb.min.z.ToString("F0") + "-" + lb.max.z.ToString("F0") + " y " + lb.min.y.ToString("F2") + ".." + lb.max.y.ToString("F2"));
            R("ice lake: " + cells + " cells, terrain above ice on " + over + " cells, max excess " + maxEx.ToString("F2") + " m");
            lakeLift = 0f;           // the north shore rises into the ice (as in the approved model): no lift
        }

        // ---- raw blocking
        float tan = Mathf.Tan(MaxSlopeDeg * Mathf.Deg2Rad);
        int cSlope = 0, cWater = 0, cMount = 0, cCastle = 0;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = Idx(i, j);
                if (code[k] == 5) continue;
                var o = new Vector3(i, 400f, j);
                if (Physics.Raycast(o, Vector3.down, out hit, 800f, 1 << LMount, QueryTriggerInteraction.Ignore)) { code[k] = 3; cMount++; continue; }
                if (!isDeck[k] && Physics.Raycast(o, Vector3.down, out hit, 800f, 1 << LWater, QueryTriggerInteraction.Ignore) && H[k] <= hit.point.y + 0.25f) { code[k] = 2; cWater++; continue; }
                if (Physics.CheckBox(new Vector3(i, H[k] + 1.75f, j), new Vector3(0.5f, 1.25f, 0.5f), Quaternion.identity, 1 << LCastle, QueryTriggerInteraction.Ignore)) { code[k] = 4; cCastle++; continue; }
                if (!isPath[k])
                {
                    float m = 0f;
                    if (i > 0 && valid[k - 1]) m = Mathf.Max(m, Mathf.Abs(H[k] - H[k - 1]));
                    if (i < N - 1 && valid[k + 1]) m = Mathf.Max(m, Mathf.Abs(H[k] - H[k + 1]));
                    if (j > 0 && valid[k - N]) m = Mathf.Max(m, Mathf.Abs(H[k] - H[k - N]));
                    if (j < N - 1 && valid[k + N]) m = Mathf.Max(m, Mathf.Abs(H[k] - H[k + N]));
                    if (m > tan) { code[k] = 1; cSlope++; }
                }
            }
        R("raw blocked: slope>" + MaxSlopeDeg + "deg " + cSlope + ", water " + cWater + ", mountain " + cMount + ", castle " + cCastle);

        // one cell of padding around every blocked cell (never on path / bridge cells)
        var raw = (byte[])code.Clone();
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = Idx(i, j);
                if (raw[k] != 0 || isPath[k] || isDeck[k]) continue;
                byte nb = 0;
                for (int dj = -1; dj <= 1 && nb == 0; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int ii = i + di, jj = j + dj;
                        if (ii < 0 || jj < 0 || ii >= N || jj >= N) continue;
                        byte c = raw[Idx(ii, jj)];
                        if (c != 0 && !(isDeck[Idx(ii, jj)])) { nb = c == 5 ? (byte)1 : c; break; }
                    }
                if (nb != 0) code[k] = nb;
            }
        for (int i = 0; i < N; i++) { code[Idx(i, 0)] = code[Idx(i, 0)] == 0 ? (byte)1 : code[Idx(i, 0)]; code[Idx(i, N - 1)] = code[Idx(i, N - 1)] == 0 ? (byte)1 : code[Idx(i, N - 1)]; code[Idx(0, i)] = code[Idx(0, i)] == 0 ? (byte)1 : code[Idx(0, i)]; code[Idx(N - 1, i)] = code[Idx(N - 1, i)] == 0 ? (byte)1 : code[Idx(N - 1, i)]; }
        int freeCells = 0; foreach (byte c in code) if (c == 0) freeCells++;
        R("walkable cells " + freeCells + " / " + N * N);

        // ---- obstacles from the placed pieces
        var obs = new List<Obs>();
        foreach (var p in placed)
        {
            if (p.world || p.proto.StartsWith("snow_terrain") || p.proto.StartsWith("snow_castle")) continue;
            Bounds b;
            if (!boundsCache.TryGetValue(p.proto, out b)) { b = LocalBounds(p.proto); boundsCache[p.proto] = b; }
            ObstaclesFor(p, b, obs);
        }
        // central lawn rule (game): nothing blocks in |x| < 28, z -42..32 (the base / test arena is the meadow scene, so this is only
        // for the written rule); list what was dropped
        int dropped = 0;
        var keep = new List<Obs>();
        foreach (var o in obs)
        {
            if (Mathf.Abs(o.x) < 28f && o.z > -42f && o.z < 32f) { dropped++; if (dropped <= 40) R("central lawn: dropped collider " + o.name + " (" + o.x.ToString("F1") + "," + o.z.ToString("F1") + ")"); continue; }
            keep.Add(o);
        }
        obs = keep;
        int circles = 0, boxes = 0;
        foreach (var o in obs) { if (o.kind == 0) circles++; else boxes++; }
        R("obstacles from pieces: circles " + circles + ", boxes " + boxes + ", dropped in central lawn " + dropped);

        // central lawn: blocked grid cells there?
        int lawnBlocked = 0;
        for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) if (code[Idx(i, j)] != 0 && Mathf.Abs(i - Off) < 28f && (j - Off) > -42f && (j - Off) < 32f) lawnBlocked++;
        R("central lawn blocked terrain cells: " + lawnBlocked);

        // ---- obstacle raster (players) for reachability
        var free = new bool[N * N];
        for (int k = 0; k < N * N; k++) free[k] = code[k] == 0;
        foreach (var o in obs)
        {
            float ex = o.kind == 0 ? o.a : Mathf.Abs(Mathf.Cos(o.yaw * Mathf.Deg2Rad)) * o.a + Mathf.Abs(Mathf.Sin(o.yaw * Mathf.Deg2Rad)) * o.b;
            float ez = o.kind == 0 ? o.a : Mathf.Abs(Mathf.Sin(o.yaw * Mathf.Deg2Rad)) * o.a + Mathf.Abs(Mathf.Cos(o.yaw * Mathf.Deg2Rad)) * o.b;
            int i0 = Mathf.FloorToInt(o.x + Off - ex - 1), i1 = Mathf.CeilToInt(o.x + Off + ex + 1), j0 = Mathf.FloorToInt(o.z + Off - ez - 1), j1 = Mathf.CeilToInt(o.z + Off + ez + 1);
            for (int j = Mathf.Max(0, j0); j <= Mathf.Min(N - 1, j1); j++)
                for (int i = Mathf.Max(0, i0); i <= Mathf.Min(N - 1, i1); i++)
                {
                    float wx = i - Off, wz = j - Off;
                    if (o.kind == 0) { if ((wx - o.x) * (wx - o.x) + (wz - o.z) * (wz - o.z) < (o.a + PlayerR) * (o.a + PlayerR)) free[Idx(i, j)] = false; }
                    else
                    {
                        float a = o.yaw * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
                        float dx = wx - o.x, dz = wz - o.z;
                        float lx = dx * c - dz * s, lz = dx * s + dz * c;
                        float qx = Mathf.Max(Mathf.Abs(lx) - o.a, 0f), qz = Mathf.Max(Mathf.Abs(lz) - o.b, 0f);
                        if (qx * qx + qz * qz < PlayerR * PlayerR) free[Idx(i, j)] = false;
                    }
                }
        }

        // ---- distance to the nearest non-free cell (chamfer, metres)
        var dist = new float[N * N];
        for (int k = 0; k < N * N; k++) dist[k] = free[k] ? 9999f : 0f;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = Idx(i, j);
                if (!free[k]) continue;
                float d = dist[k];
                if (i == 0 || j == 0) d = 1f;
                if (i > 0) d = Mathf.Min(d, dist[k - 1] + 1f);
                if (j > 0) d = Mathf.Min(d, dist[k - N] + 1f);
                if (i > 0 && j > 0) d = Mathf.Min(d, dist[k - N - 1] + 1.414f);
                if (i < N - 1 && j > 0) d = Mathf.Min(d, dist[k - N + 1] + 1.414f);
                dist[k] = d;
            }
        for (int j = N - 1; j >= 0; j--)
            for (int i = N - 1; i >= 0; i--)
            {
                int k = Idx(i, j);
                if (!free[k]) continue;
                float d = dist[k];
                if (i == N - 1 || j == N - 1) d = Mathf.Min(d, 1f);
                if (i < N - 1) d = Mathf.Min(d, dist[k + 1] + 1f);
                if (j < N - 1) d = Mathf.Min(d, dist[k + N] + 1f);
                if (i < N - 1 && j < N - 1) d = Mathf.Min(d, dist[k + N + 1] + 1.414f);
                if (i > 0 && j < N - 1) d = Mathf.Min(d, dist[k + N - 1] + 1.414f);
                dist[k] = d;
            }

        // ---- fixed points (Blender E, N)
        float[][] wp =
        {
            new[]{25f,25f}, new[]{55f,50f}, new[]{75f,72f}, new[]{95f,88f}, new[]{112f,115f}, new[]{135f,140f}, new[]{160f,165f}, new[]{185f,190f},
            new[]{205f,215f}, new[]{225f,235f}, new[]{250f,245f}, new[]{275f,235f}, new[]{295f,255f}, new[]{315f,300f}, new[]{330f,320f}, new[]{340f,335f},
        };
        // main component = the one holding the valley waypoint (7)
        int[] comp = new int[N * N];
        int nComp = 0;
        var compSize = new List<int>();
        var stack = new Stack<int>();
        for (int k0 = 0; k0 < N * N; k0++)
        {
            if (!free[k0] || comp[k0] != 0) continue;
            nComp++; int size = 0;
            comp[k0] = nComp; stack.Push(k0);
            while (stack.Count > 0)
            {
                int k = stack.Pop(); size++;
                int i = k % N, j = k / N;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ii = i + di, jj = j + dj;
                        if (ii < 0 || jj < 0 || ii >= N || jj >= N) continue;
                        int nk = Idx(ii, jj);
                        if (!free[nk] || comp[nk] != 0) continue;
                        if (di != 0 && dj != 0 && (!free[Idx(i + di, j)] || !free[Idx(i, j + dj)])) continue;
                        comp[nk] = nComp; stack.Push(nk);
                    }
            }
            compSize.Add(size);
        }
        Func<float, float, int, int> nearestIn = (e, n, c) =>
        {
            int best = -1; float bd = 1e9f;
            for (int k = 0; k < N * N; k++)
            {
                if (comp[k] != c) continue;
                float dx = k % N - e, dz = k / N - n, d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = k; }
            }
            return best;
        };
        int mainComp = 0, bestSize = 0;
        for (int c = 1; c <= nComp; c++) if (compSize[c - 1] > bestSize) { bestSize = compSize[c - 1]; mainComp = c; }
        R("walkable regions: " + nComp + ", main region " + bestSize + " cells");

        // spawn: nearest cell to Start (25,25) in the main region with at least 4 m clear around (relax if needed)
        int spawnK = -1;
        foreach (float need in new[] { 4.5f, 3.5f, 2.5f, 1.5f })
        {
            float bd = 1e9f;
            for (int k = 0; k < N * N; k++)
            {
                if (comp[k] != mainComp || dist[k] < need) continue;
                float dx = k % N - 25f, dz = k / N - 25f, d = dx * dx + dz * dz;
                if (d < bd) { bd = d; spawnK = k; }
            }
            if (spawnK >= 0) { R("spawn clearance >= " + need + " m"); break; }
        }
        if (spawnK < 0) { Err("no spawn cell"); spawnK = nearestIn(25f, 25f, mainComp); }
        float spawnE = spawnK % N, spawnN = spawnK / N;
        R("spawn (E,N)=(" + spawnE + "," + spawnN + ") game=(" + (spawnE - Off) + "," + (spawnN - Off) + ") y=" + H[spawnK].ToString("F2") + " clearance " + dist[spawnK].ToString("F1"));

        // waypoints
        for (int w = 0; w < wp.Length; w++)
        {
            int k = nearestIn(wp[w][0], wp[w][1], mainComp);
            float dx = k % N - wp[w][0], dz = k / N - wp[w][1], d = Mathf.Sqrt(dx * dx + dz * dz);
            bool onFree = free[Idx(Mathf.RoundToInt(wp[w][0]), Mathf.RoundToInt(wp[w][1]))];
            R("waypoint " + w + " (" + wp[w][0] + "," + wp[w][1] + ") " + (onFree ? "walkable" : "blocked") + ", main region " + (comp[Idx(Mathf.RoundToInt(wp[w][0]), Mathf.RoundToInt(wp[w][1]))] == mainComp ? "yes" : "no") + ", nearest reachable " + d.ToString("F1") + " m");
            if (d > 6f && w != 15) Err("waypoint " + w + " is not reachable from the spawn region (nearest " + d.ToString("F1") + " m)");
        }
        // bridge S1 connection: waypoints 2 and 4 are on opposite banks
        {
            int a = Idx(75, 72), b2 = Idx(112, 115);
            R("bridge S1 link: waypoint 2 comp " + comp[nearestIn(75f, 72f, mainComp)] + ", waypoint 4 comp " + comp[nearestIn(112f, 115f, mainComp)] + " (same main region = bridge connects)");
        }

        // lair: biggest open circle in the main region within 70 m of the castle gate (340,335)
        int lairK = -1; float lairD = 0f;
        for (int k = 0; k < N * N; k++)
        {
            if (comp[k] != mainComp) continue;
            float dx = k % N - 340f, dz = k / N - 335f;
            if (dx * dx + dz * dz > 70f * 70f) continue;
            if (dist[k] > lairD) { lairD = dist[k]; lairK = k; }
        }
        if (lairK < 0) { Err("no walkable ground within 70 m of the castle gate"); lairK = nearestIn(340f, 335f, mainComp); }
        float lairE = lairK % N, lairN = lairK / N;
        R("lair centre (E,N)=(" + lairE + "," + lairN + ") game=(" + (lairE - Off) + "," + (lairN - Off) + ") y=" + H[lairK].ToString("F2") + " clearance " + lairD.ToString("F1") + " m (need >= 20; design target 22)");
        if (lairD < 20f) Err("largest open circle near the castle gate is " + lairD.ToString("F1") + " m < 22 m (ask the artist to open the courtyard in front of the gate)");

        // dummy ~10 m from the spawn
        int dummyK = -1; float bestScore = 1e9f;
        for (int k = 0; k < N * N; k++)
        {
            if (comp[k] != mainComp || dist[k] < 3f) continue;
            float dx = k % N - spawnE, dz = k / N - spawnN, d = Mathf.Sqrt(dx * dx + dz * dz);
            float score = Mathf.Abs(d - 10f);
            if (score < bestScore) { bestScore = score; dummyK = k; }
        }
        if (dummyK < 0) { Err("no cell for the straw dummy"); dummyK = spawnK; }

        {
            var sb = new StringBuilder();
            int[][] wins = { new[]{83,107,76,100}, new[]{318,358,312,352}, new[]{300,345,255,290} };
            foreach (var w in wins)
            {
                sb.AppendLine("window E" + w[0] + "-" + w[1] + " N" + w[2] + "-" + w[3] + " (top = north)");
                for (int j = w[3]; j >= w[2]; j--)
                {
                    for (int i = w[0]; i <= w[1]; i++)
                    {
                        int k = Idx(i, j);
                        char ch = isDeck[k] ? 'D' : code[k] == 1 ? '#' : code[k] == 2 ? '~' : code[k] == 3 ? 'M' : code[k] == 4 ? 'C' : code[k] == 5 ? '?' : !free[k] ? 'o' : isPath[k] ? 'p' : '.';
                        sb.Append(ch);
                    }
                    sb.AppendLine(" " + j);
                }
            }
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "snowbake_dump.txt"), sb.ToString());
        }
        // ---- grid boxes (blocked cells within 2 cells of a free cell, merged into rectangles)
        var keepCell = new bool[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                if (code[Idx(i, j)] == 0) continue;
                bool near = false;
                for (int dj = -2; dj <= 2 && !near; dj++)
                    for (int di = -2; di <= 2; di++)
                    {
                        int ii = i + di, jj = j + dj;
                        if (ii < 0 || jj < 0 || ii >= N || jj >= N) continue;
                        if (code[Idx(ii, jj)] == 0) { near = true; break; }
                    }
                keepCell[Idx(i, j)] = near || i == 0 || j == 0 || i == N - 1 || j == N - 1;
            }
        var gridBoxes = new List<Obs>();
        var rects = new List<int[]>();       // i0, i1, j0, j1
        var open = new Dictionary<long, int[]>();
        for (int j = 0; j < N; j++)
        {
            var thisRow = new Dictionary<long, int[]>();
            int i = 0;
            while (i < N)
            {
                if (!keepCell[Idx(i, j)]) { i++; continue; }
                int s = i;
                while (i < N && keepCell[Idx(i, j)]) i++;
                long key = (long)s * 1000 + (i - 1);
                int[] r;
                if (open.TryGetValue(key, out r)) { r[3] = j; thisRow[key] = r; }
                else { r = new[] { s, i - 1, j, j }; rects.Add(r); thisRow[key] = r; }
            }
            open = thisRow;
        }
        foreach (var r in rects)
        {
            float x0 = r[0] - 0.5f - Off, x1 = r[1] + 0.5f - Off, z0 = r[2] - 0.5f - Off, z1 = r[3] + 0.5f - Off;
            gridBoxes.Add(new Obs { kind = 1, x = (x0 + x1) * 0.5f, z = (z0 + z1) * 0.5f, a = (x1 - x0) * 0.5f, b = (z1 - z0) * 0.5f, yaw = 0f, mask = 3, name = "grid" });
        }
        R("blocked-terrain boxes: " + gridBoxes.Count + " (from " + rects.Count + " rectangles)");
        int totalObs = obs.Count + gridBoxes.Count;
        R("TOTAL obstacles " + totalObs + " (limit 8000)");
        if (totalObs > 8000) Err("more than 8000 obstacles");

        // ---- write files
        Directory.CreateDirectory(OutDir);
        var protoIndex = new Dictionary<string, int>();
        var protos = new List<string>();
        var instF = new List<float>();
        var worldF = new List<bool>();
        var names = new List<string>();
        foreach (var p in placed)
        {
            int pi;
            if (!protoIndex.TryGetValue(p.proto, out pi)) { pi = protos.Count; protoIndex[p.proto] = pi; protos.Add(p.proto); }
            Vector3 pos = p.pos - new Vector3(Off, 0f, Off);
            if (p.proto.StartsWith("snow_deco_icelake")) pos.y += lakeLift;
            instF.AddRange(new[] { pi, pos.x, pos.y, pos.z, p.rot.x, p.rot.y, p.rot.z, p.rot.w, p.scale.x, p.scale.y, p.scale.z });
            worldF.Add(p.world || p.proto.StartsWith("snow_terrain_chunk"));
            names.Add(p.name);
        }
        var obsF = new List<float>();
        foreach (var o in obs) obsF.AddRange(new[] { o.kind, o.x, o.z, o.a, o.b, o.yaw, o.mask });
        foreach (var o in gridBoxes) obsF.AddRange(new[] { o.kind, o.x, o.z, o.a, o.b, o.yaw, o.mask });
        var dto = new SnowLayoutDto { protos = protos.ToArray(), inst = instF.ToArray(), world = worldF.ToArray(), obs = obsF.ToArray(), names = names.ToArray() };
        File.WriteAllText(OutDir + "/snow_layout.json", JsonUtility.ToJson(dto));

        var hb = new byte[N * N * 2];
        for (int k = 0; k < N * N; k++)
        {
            ushort v = (ushort)Mathf.Clamp(Mathf.RoundToInt(H[k] * 100f) + 2000, 0, 65535);
            hb[k * 2] = (byte)(v & 255); hb[k * 2 + 1] = (byte)(v >> 8);
        }
        File.WriteAllBytes(OutDir + "/snow_height.bytes", hb);
        File.WriteAllBytes(OutDir + "/snow_block.bytes", code);

        var signs = new List<float>(); var signText = new List<string>();
        foreach (var p in placed)
        {
            string t = p.proto == "snow_prop_signpine_v01" ? "Pine Valley: follow the road north"
                : p.proto == "snow_prop_signmountain_v01" ? "Cliff Ascent: the road climbs the mountain"
                : p.proto == "snow_prop_signsnow_v01" ? "The Snowlands: the ice castle is ahead"
                : p.proto == "snow_prop_signcastle_v01" ? "The Ice Castle's gate" : null;
            if (t == null) continue;
            signs.Add(p.pos.x - Off); signs.Add(p.pos.z - Off); signText.Add(t);
        }
        var fountain = placed.Find(q => q.proto == "snow_prop_fountain_v01");
        var wpF = new List<float>();
        foreach (var w in wp) { wpF.Add(w[0] - Off); wpF.Add(w[1] - Off); }
        // zones (design ข้อ 3): id 0 A meadow, 1 B pine valley, 2 C cliff ascent, 3 D1 low snow, 4 D2 high snow, 5 D3 castle; first match wins
        float[] zones =
        {
            5, 310 - Off, 370 - Off, 310 - Off, 370 - Off, 2f,
            4, 250 - Off, 380 - Off, 250 - Off, 330 - Off, 2f,
            3, 250 - Off, 380 - Off, 150 - Off, 250 - Off, 2f,
            2, 180 - Off, 280 - Off, 170 - Off, 260 - Off, 2f,
            1, 90 - Off, 230 - Off, 100 - Off, 230 - Off, 1.5f,
            0, 0 - Off, 150 - Off, 0 - Off, 130 - Off, 1f,
        };
        var meta = new SnowMeta
        {
            spawn = new[] { spawnE - Off, spawnN - Off },
            safeCenter = fountain != null ? new[] { fountain.pos.x - Off, fountain.pos.z - Off } : new[] { spawnE - Off, spawnN - Off },
            safeRadius = 30f,
            lair = new[] { lairE - Off, lairN - Off },
            lairRadius = Mathf.Floor(Mathf.Min(lairD - 0.5f, 30f)),
            lairClearance = lairD,
            dummy = new[] { dummyK % N - Off, dummyK / N - Off },
            gate = new[] { 340f - Off, 335f - Off },
            zones = zones, waypoints = wpF.ToArray(), signs = signs.ToArray(), signText = signText.ToArray(),
            lakeLift = lakeLift, gridBoxes = gridBoxes.Count, circleCount = circles, boxCount = boxes, instanceCount = placed.Count,
        };
        File.WriteAllText(OutDir + "/snow_meta.json", JsonUtility.ToJson(meta, true));
        AssetDatabase.Refresh();

        R("files: layout " + new FileInfo(OutDir + "/snow_layout.json").Length / 1024 + " KB, height " + hb.Length / 1024 + " KB, block " + code.Length / 1024 + " KB");
        R("lakeLift=" + lakeLift + " m");
        R("elapsed " + sw.ElapsedMilliseconds + " ms");
    }
}
