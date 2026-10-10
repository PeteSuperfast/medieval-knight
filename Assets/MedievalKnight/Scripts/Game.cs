using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Entry point. Builds the whole 3D top-down arena from code (camera, sun, ground, fence, trees),
    // spawns the knight / dummy / slime waves / Minotaur, and runs levelling, pausing, shake and hit-stop.
    public class Game : MonoBehaviour
    {
        public static Game I;

        public const float ArenaHalf = 190f;       // the fenced map is 380 x 380 m (see World.cs: MapLayout)
        public const float CamPitch = 56f, CamDist = 16f, CamFov = 40f;
        public const float MinotaurRespawn = 20f;
        // co-op rules: enemy health per extra player, seconds a downed knight waits, protection while picking a level-up card
        public const float CoopHpPerPlayer = 0.6f, ReviveDelaySingle = 3f, ReviveDelayCoop = 5f, LevelUpProtect = 6f;
        public const float MaxHitDamage = 20000f;  // host-side sanity limit for one reported hit
        static readonly Vector3 MinotaurHome = MapLayout.Lair;
        static readonly Vector3 DummyPos = MapLayout.DummyPos;
        static readonly Vector3 KnightSpawn = MapLayout.TownSpawn;
        static readonly Vector3 MenuFocus = MapLayout.MenuFocus;

        public Camera cam;
        public Menu menu;
        public Knight knight;                      // the knight this machine controls (HUD, camera, input)
        public readonly List<Knight> players = new List<Knight>();   // everyone in the arena, the local knight included
        public Dummy dummy;
        public Minotaur minotaur;
        public Hud hud;
        public Progress progress { get { return knight.progress; } }   // level / upgrades / stat points belong to the knight
        public UpgradeOffer[] offers;              // the cards currently on screen
        public int wave, slimesAlive;
        public float inputBlockUntil;              // unscaled time; combat input is ignored until then
        public int dropOverride = -1;              // self-test hook: -1 = roll normally, 0 = never drop, 1 = always drop
        public int foodOverride = -1;              // self-test hook for food: -1 = roll normally, 0 = never, 1 = a slime always drops an Apple
        public int lastFoodHeal, fullHints;        // test read-outs: the last health a food gave, "HP full" hints shown
        public float camZoom = 1f;                 // self-test hook: < 1 moves the camera closer for screenshots
        public Pickup lastPickup;                  // the most recent sword drop
        public string lastNotice = "";             // the most recent "picked up ..." line

        // The level-up panel has two steps: 1 = pick an upgrade card, 2 = spend the stat point. 0 = closed.
        public int LevelStep { get; private set; }
        public bool LevelUpOpen { get { return LevelStep != 0; } }
        public bool InventoryOpen { get; private set; }
        public bool InSession { get; private set; }                    // false while the start menu is up
        public bool PanelOpen { get { return LevelStep != 0 || InventoryOpen; } }
        // The panels freeze the game in single player only; online the world keeps running for everybody.
        public bool Paused { get { return PanelOpen && !Net.Multiplayer; } }
        public float MinotaurRespawnIn { get { return minoRespawnAt - Time.time; } }

        // Average level of everyone in the arena (single player: the knight's level).
        public float PartyLevel
        {
            get
            {
                if (players.Count == 0) return 1f;
                float sum = 0f;
                for (int i = 0; i < players.Count; i++) sum += players[i].progress.level;
                return sum / players.Count;
            }
        }

        // Monsters get tougher as the party levels, so the upgrades keep mattering; each extra player adds health.
        public float EnemyHpScale { get { return (1f + 0.45f * (PartyLevel - 1f)) * (1f + CoopHpPerPlayer * Mathf.Max(0, players.Count - 1)); } }
        public float EnemyDamageScale { get { return 1f + 0.08f * (PartyLevel - 1f); } }
        // EXP per monster grows with the party level too (read when the monster appears), so level 25 is reachable
        public float EnemyExpScale { get { return 1f + 0.15f * (PartyLevel - 1f); } }

        Transform actors;
        bool started, readyPending, wiped;
        int nextNetId;
        float leaveAskedAt = -10f;
        float levelUpOpenAt, minoRespawnAt;
        float shakeAmp, shakeTime, shakeDur;
        float hitStopUntil;
        Vector3 camFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            new GameObject("MedievalKnight").AddComponent<Game>();
        }

        void Awake()
        {
            I = this;
            Args.Parse();
            QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;        // the menu must keep running (a join in progress) without focus

            SetupCamera();
            SetupLight();
            SwordIcons.Build(cam);                     // the Tab window pictures: needs the light, hides its stage layer from the camera
            BuildWorld();

            new GameObject("Fx").AddComponent<Fx>().transform.SetParent(transform, false);
            new GameObject("Sfx").AddComponent<Sfx>().transform.SetParent(transform, false);
            actors = Prims.Node(transform, "Actors", Vector3.zero);

            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.SetVisible(false);
            menu = new GameObject("Menu").AddComponent<Menu>();
            menu.transform.SetParent(transform, false);
            camFocus = MenuFocus;
            PlaceCamera(Vector3.zero);

            // Normal play starts at the menu. The command line can go straight into a mode (see Args.cs).
            string name = Args.name ?? "Knight";
            if (Args.host)
            {
                string error = Net.StartHost(Args.port, name);
                if (error == null) StartSession();
                else menu.Show(error);
            }
            else if (Args.joinHost != null)
            {
                menu.ShowConnecting(Args.joinHost, Args.port);
                StartCoroutine(JoinFromCommandLine(name));
            }
            else if (Args.selfTestDir != null || Args.single)
            {
                Net.StartSingle(name);
                StartSession();
            }
            else menu.Show("");

            SelfTest.StartIfRequested(gameObject);
            MpTest.StartIfRequested(gameObject);
        }

        System.Collections.IEnumerator JoinFromCommandLine(string name)
        {
            if (Args.joinDelay > 0f) yield return new WaitForSecondsRealtime(Args.joinDelay);
            menu.BeginJoin(Args.joinHost, Args.port, name);
        }

        void OnApplicationQuit()
        {
            Net.Shutdown(Net.IsHost ? "The host closed the game." : "left the game");
        }

        void Start()
        {
            started = true;
            if (readyPending) Sfx.I.Play(Snd.Ready);   // "stand ready" as the game begins
            readyPending = false;
        }

        // ---------------------------------------------------------------- session (one run in the arena)

        // Net.Mode / Net.LocalPid / Net.LocalName are already set: single player, host or a client that was let in.
        public void StartSession()
        {
            if (InSession) return;
            InSession = true;
            Application.runInBackground = Net.Multiplayer || Args.Scripted;   // an online game must not stop when the window loses focus
            Time.timeScale = 1f;
            wave = 0; slimesAlive = 0; wiped = false;
            minoRespawnAt = 0f; hitStopUntil = 0f; inputBlockUntil = 0f; levelUpOpenAt = 0f;
            knight = SpawnKnight(Net.LocalPid, Net.LocalName, false);
            menu.Hide();
            hud.SetVisible(true);
            if (Net.Authority) SpawnActors();
            camFocus = knight.transform.position;
            PlaceCamera(Vector3.zero);
            if (started) Sfx.I.Play(Snd.Ready); else readyPending = true;
            Debug.Log("SESSION start: " + Net.Mode + ", player " + Net.LocalPid + " '" + Net.LocalName + "'");
        }

        // Back to the start menu (Esc twice, the host closed the game, the connection dropped). The sockets are
        // closed by the caller (Net.Shutdown) - this only takes the arena apart.
        public void EndSession(string message)
        {
            if (!InSession) { menu.Show(message); return; }
            InSession = false;
            foreach (Knight k in players) { k.gameObject.SetActive(false); Destroy(k.gameObject); }
            players.Clear();
            knight = null;
            ClearWorld();
            offers = null;
            LevelStep = 0;
            InventoryOpen = false;
            hud.HideLevelUp();
            hud.HideDeath();
            hud.HideInventory();
            hud.SetVisible(false);
            Time.timeScale = 1f;
            Application.runInBackground = true;
            camFocus = MenuFocus;
            PlaceCamera(Vector3.zero);
            Debug.Log("SESSION end: " + message);
            menu.Show(message);
        }

        // Esc in the arena: asks once, the second press within 3 seconds leaves.
        void LeaveKey()
        {
            if (Time.unscaledTime > leaveAskedAt + 3f)
            {
                leaveAskedAt = Time.unscaledTime;
                hud.Notice(Net.IsHost ? "Press Esc again to close the game for everyone and go to the menu"
                    : "Press Esc again to leave to the menu", new Color(1f, 0.85f, 0.5f));
                return;
            }
            LeaveToMenu();
        }

        public void LeaveToMenu()
        {
            Net.Shutdown(Net.IsHost ? "The host closed the game." : "left the game");
            EndSession("");
        }

        // ---------------------------------------------------------------- online: other players

        // quiet: somebody who was already in the game when this machine joined.
        public void OnPlayerJoined(int pid, string name, bool quiet)
        {
            if (!InSession || pid == Net.LocalPid || PlayerByPid(pid) != null) return;
            SpawnKnight(pid, name, true);
            if (!quiet)
            {
                hud.Notice(name + " joined the game   (" + players.Count + " / " + Net.MaxPlayers + " players)", Knight.UiColor(pid));
                Sfx.I.Play(Snd.Ready, 0.5f);
            }
            Debug.Log("PLAYER joined: " + pid + " '" + name + "', players " + players.Count);
        }

        public void OnPlayerLeft(int pid, string why)
        {
            Knight k = PlayerByPid(pid);
            if (!InSession || k == null || !k.remote) return;
            players.Remove(k);
            Fx.I.Dust(k.transform.position, 14, 1.4f);
            hud.Notice(k.playerName + " left the game   (" + players.Count + " / " + Net.MaxPlayers + " players)", new Color(1f, 0.7f, 0.5f));
            Debug.Log("PLAYER left: " + pid + " '" + k.playerName + "' (" + why + "), players " + players.Count);
            k.gameObject.SetActive(false);
            Destroy(k.gameObject);
        }

        // ---------------------------------------------------------------- online: the world snapshot

        readonly HashSet<int> seenIds = new HashSet<int>();

        // Host, 15 times a second: counters, every knight, every enemy, every pickup.
        public NetW WriteSnapshot()
        {
            var w = new NetW(Msg.Snapshot);
            w.F32(Time.unscaledTime).U16(wave).U8(Mathf.Min(slimesAlive, 255));
            w.F32(minotaur != null && !minotaur.Dead ? -1f : Mathf.Max(0f, MinotaurRespawnIn));

            w.U8(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                w.U8(players[i].pid);
                Net.WriteKnight(w, players[i]);
            }

            int n = 0;
            for (int i = 0; i < Enemy.All.Count; i++) if (InSnapshot(Enemy.All[i])) n++;
            w.U8(Mathf.Min(n, 255));
            int written = 0;
            for (int i = 0; i < Enemy.All.Count && written < 255; i++)
            {
                Enemy e = Enemy.All[i];
                if (!InSnapshot(e)) continue;
                w.U16(e.netId).U8(e.NetType);
                e.WriteNet(w);
                written++;
            }

            w.U8(Mathf.Min(Pickup.All.Count, 255));
            for (int i = 0; i < Pickup.All.Count && i < 255; i++)
            {
                Pickup p = Pickup.All[i];
                w.U16(p.netId).U8((int)p.kind).U8(p.weaponId).U8(p.owner < 0 ? 255 : p.owner).Pos(p.transform.position.x).Pos(p.transform.position.z);
            }
            return w;
        }

        // A dying slime / Minotaur is not sent any more (its death went out as an event); the dummy always is.
        static bool InSnapshot(Enemy e) { return !e.Dead || e is Dummy; }

        // Client: make the local copy of the world match.
        public void ReadSnapshot(NetR r)
        {
            float hostTime = r.F32();
            wave = r.U16();
            slimesAlive = r.U8();
            float respawnIn = r.F32();
            if (respawnIn >= 0f) minoRespawnAt = Time.time + respawnIn;

            int np = r.U8();
            for (int i = 0; i < np; i++)
            {
                Knight k = PlayerByPid(r.U8());
                Net.ReadKnight(r, k);
            }

            seenIds.Clear();
            int ne = r.U8();
            for (int i = 0; i < ne; i++)
            {
                int id = r.U16(), type = r.U8();
                seenIds.Add(id);
                Enemy e = Enemy.Find(id);
                if (e != null && e.NetType != type)                       // the id was reused for something else
                {
                    e.gameObject.SetActive(false);
                    Destroy(e.gameObject);
                    e = null;
                }
                bool first = e == null;
                if (first) e = SpawnPuppet(type, id);
                e.ReadNet(r, hostTime, first);
            }
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                Enemy e = Enemy.All[i];
                if (e.Dead || seenIds.Contains(e.netId)) continue;        // dead ones finish their death animation
                if (e == minotaur) minotaur = null;
                e.gameObject.SetActive(false);                            // gone without a kill (restart, wave reset)
                Destroy(e.gameObject);
            }

            seenIds.Clear();
            int npk = r.U8();
            for (int i = 0; i < npk; i++)
            {
                int id = r.U16(), pkind = r.U8(), weapon = r.U8(), owner = r.U8();
                float x = r.Pos(), z = r.Pos();
                seenIds.Add(id);
                if (Pickup.Find(id) == null)
                {
                    if (pkind == (int)PickupKind.Food) { if (weapon < Foods.All.Length) SpawnFood(weapon, new Vector3(x, 0f, z), id); }
                    else if (weapon < Weapons.All.Length) SpawnPickup(weapon, new Vector3(x, 0f, z), owner == 255 ? -1 : owner, id);
                }
            }
            for (int i = Pickup.All.Count - 1; i >= 0; i--)
            {
                Pickup p = Pickup.All[i];
                if (seenIds.Contains(p.netId)) continue;
                p.gameObject.SetActive(false);                            // expired on the host
                Destroy(p.gameObject);
            }
        }

        Enemy SpawnPuppet(int type, int id)
        {
            Enemy e;
            if (type <= 2) e = Slime.Spawn(type, Vector3.zero, actors);
            else if (type == 3) e = minotaur = Minotaur.Spawn(MinotaurHome, actors);
            else
            {
                dummy = new GameObject("StrawDummy").AddComponent<Dummy>();
                dummy.transform.SetParent(actors, false);
                e = dummy;
            }
            e.puppet = true;
            e.netId = id;
            return e;
        }

        // Client: the host says a sword (kind 0) or a food (kind 1) was collected.
        public void OnPickupTaken(int id, int pid, int kind, int itemId, Vector3 at)
        {
            Pickup p = Pickup.Find(id);
            bool seen = p != null && !p.hidden;
            if (p != null)
            {
                p.gameObject.SetActive(false);
                Destroy(p.gameObject);
            }
            if (kind == (int)PickupKind.Food)
            {
                if (itemId >= Foods.All.Length) return;
                if (pid == Net.LocalPid) CollectFood(itemId, at);
                else OtherAteFood(PlayerByPid(pid), itemId, at);
                return;
            }
            int weaponId = itemId;
            if (weaponId >= Weapons.All.Length) return;
            if (pid == Net.LocalPid) CollectLocal(weaponId, at);
            else OtherCollected(PlayerByPid(pid), weaponId, at, seen);
        }

        public static Vector3 SpawnPointFor(int pid)
        {
            float x = pid == 0 ? 0f : (pid % 2 == 1 ? -1.8f : 1.8f) * ((pid + 1) / 2);
            return KnightSpawn + new Vector3(x, 0f, 0f);
        }

        public Knight SpawnKnight(int pid, string name, bool remote)
        {
            Knight k = new GameObject(remote ? "Knight " + name : "Knight").AddComponent<Knight>();
            k.transform.SetParent(transform, true);
            k.Init(pid, name, remote);
            players.Add(k);
            return k;
        }

        public Knight PlayerByPid(int pid)
        {
            for (int i = 0; i < players.Count; i++) if (players[i].pid == pid) return players[i];
            return null;
        }

        // The living knight closest to a point (what the monsters go for). Nobody alive: the closest one anyway.
        public Knight NearestKnight(Vector3 pos)
        {
            Knight best = null, bestAny = null;
            float bd = float.MaxValue, bdAny = float.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                Knight k = players[i];
                Vector3 d = k.Pos - pos; d.y = 0f;
                float m = d.sqrMagnitude;
                if (m < bdAny) { bdAny = m; bestAny = k; }
                if (!k.Dead && m < bd) { bd = m; best = k; }
            }
            return best != null ? best : bestAny;
        }

        public int NewNetId()
        {
            nextNetId++;
            if (nextNetId > 60000) nextNetId = 1;
            return nextNetId;
        }

        // ---------------------------------------------------------------- camera / light

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            // sound needs exactly one listener; the scene's camera normally has it already
            if (FindObjectsByType<AudioListener>().Length == 0) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = false;
            cam.fieldOfView = CamFov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.27f, 0.2f);
            cam.allowMSAA = true;
        }

        void SetupLight()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(50f, -34f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.96f, 0.86f);
            l.intensity = 1.35f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.7f;

            Color amb = new Color(0.52f, 0.58f, 0.68f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = amb;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
            sh.AddAmbientLight(amb);
            RenderSettings.ambientProbe = sh;
            RenderSettings.fog = false;

            // a plain sky-coloured cubemap so shiny armor and blades have something to reflect
            var cube = new Cubemap(8, TextureFormat.RGBA32, false);
            Color sky = new Color(0.6f, 0.74f, 0.95f), side = new Color(0.72f, 0.8f, 0.86f), ground = new Color(0.3f, 0.38f, 0.28f);
            for (int face = 0; face < 6; face++)
            {
                Color c = face == (int)CubemapFace.PositiveY ? sky : face == (int)CubemapFace.NegativeY ? ground : side;
                var px = new Color[64];
                for (int i = 0; i < px.Length; i++) px[i] = c;
                cube.SetPixels(px, (CubemapFace)face);
            }
            cube.Apply();
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
        }

        void PlaceCamera(Vector3 shake)
        {
            Quaternion rot = Quaternion.Euler(CamPitch, 0f, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = camFocus + Vector3.up * 1f + Vector3.forward * 0.6f - rot * Vector3.forward * CamDist * camZoom
                + rot * new Vector3(shake.x, shake.y, 0f);
        }

        // World point under the mouse cursor on the ground plane: where the knight looks and leaps.
        public Vector3 AimPoint()
        {
            if (knight == null) return Vector3.zero;
            Ray ray = cam.ScreenPointToRay(InputX.MousePos);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return knight.transform.position + knight.transform.forward;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return knight.transform.position + knight.transform.forward;
            return ray.origin + ray.direction * t;
        }

        // ---------------------------------------------------------------- world

        void BuildWorld()
        {
            Transform world = Prims.Node(transform, "World", Vector3.zero);
            WorldBuilder.Build(world);
        }

        // ---------------------------------------------------------------- actors

        void SpawnActors()
        {
            dummy = new GameObject("StrawDummy").AddComponent<Dummy>();
            dummy.transform.SetParent(actors, false);
            dummy.transform.position = DummyPos;
            dummy.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

            minotaur = Minotaur.Spawn(MinotaurHome, actors);
            wave = 0;                                              // = packs spawned so far (there are no waves any more)
            packDue.Clear();
            packCount.Clear();
        }

        // ---- slime packs: every living knight outside the town gets a pack of slimes round him when few are near ----

        public const int MaxSlimes = 48;
        public bool packsOff;                                      // self-test hook: no new packs while a test needs a fixed set of enemies
        readonly Dictionary<int, float> packDue = new Dictionary<int, float>();
        readonly Dictionary<int, int> packCount = new Dictionary<int, int>();
        float nextDirector;

        int PacksOf(int pid) { int n; return packCount.TryGetValue(pid, out n) ? n : 0; }

        void UpdatePacks()
        {
            if (Time.time < nextDirector) return;
            nextDirector = Time.time + 0.25f;
            if (packsOff) return;                                  // test hook: the whole director sleeps

            // slimes that are far from everybody go away quietly (no EXP, drop or Soul)
            if (Mathf.FloorToInt(Time.time) != Mathf.FloorToInt(Time.time - 0.25f))
            {
                foreach (Enemy e in new List<Enemy>(Enemy.All))
                {
                    if (!(e is Slime) || e.Dead || e.Stunned) continue;
                    bool far = true;
                    for (int i = 0; i < players.Count && far; i++)
                        if (FlatDist(e.transform.position, players[i].Pos) <= 70f) far = false;
                    if (!far) continue;
                    e.gameObject.SetActive(false);
                    Destroy(e.gameObject);
                }
            }

            int alive = 0;
            for (int i = 0; i < Enemy.All.Count; i++)
                if (Enemy.All[i] is Slime && !Enemy.All[i].Dead) alive++;
            slimesAlive = alive;
            if (packsOff) return;

            for (int n = 0; n < players.Count; n++)
            {
                Knight k = players[n];
                int pid = k.pid;
                if (k.Dead) { packCount[pid] = 0; packDue[pid] = -1f; continue; }
                Vector3 p = k.Pos;
                bool safe = MapLayout.InTown(p.x, p.z) || MapLayout.SpawnTier(MapLayout.ZoneAt(p.x, p.z)) <= 0f;
                int near = 0;
                for (int i = 0; i < Enemy.All.Count; i++)
                {
                    Enemy e = Enemy.All[i];
                    if (e is Slime && !e.Dead && FlatDist(e.transform.position, p) <= 45f) near++;
                }
                if (safe || near > 1 || alive >= MaxSlimes) { packDue[pid] = -1f; continue; }
                float due;
                if (!packDue.TryGetValue(pid, out due) || due < 0f) { packDue[pid] = Time.time + 2.5f; continue; }
                if (Time.time < due) continue;
                packDue[pid] = -1f;
                alive += SpawnPack(k);
            }
        }

        static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Returns how many slimes were made. 15-26 m from the knight (the first pack of a life: 10-16 m), never in town,
        // lake or mountains, never inside something solid.
        int SpawnPack(Knight anchor)
        {
            int pid = anchor.pid;
            int packs = PacksOf(pid);
            Vector3 kp = anchor.Pos; kp.y = 0f;
            int helpers = 0;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != anchor && !players[i].Dead && FlatDist(players[i].Pos, kp) <= 45f) helpers++;
            int alive = 0;
            for (int i = 0; i < Enemy.All.Count; i++)
                if (Enemy.All[i] is Slime && !Enemy.All[i].Dead) alive++;
            int count = Mathf.Min(Mathf.Min(4 + packs, 10) + 2 * helpers, 16);
            count = Mathf.Min(count, MaxSlimes - alive);
            float tier = MapLayout.SpawnTier(MapLayout.ZoneAt(kp.x, kp.z));
            float blue = Mathf.Clamp((packs - 2) * 0.15f * tier, 0f, 0.7f), red = Mathf.Clamp((packs - 5) * 0.12f * tier, 0f, 0.5f);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = kp;
                bool placed = false;
                for (int tries = 0; tries < 20 && !placed; tries++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float dist = packs == 0 ? Random.Range(10f, 16f) : Random.Range(15f, 26f);
                    p = kp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                    p.x = Mathf.Clamp(p.x, -ArenaHalf + 1.5f, ArenaHalf - 1.5f);
                    p.z = Mathf.Clamp(p.z, -ArenaHalf + 1.5f, ArenaHalf - 1.5f);
                    if (MapLayout.SpawnTier(MapLayout.ZoneAt(p.x, p.z)) <= 0f || !MapLayout.Walkable(p.x, p.z)) continue;
                    if (FlatDist(p, DummyPos) < 3f) continue;
                    bool bad = false;
                    for (int n = 0; n < players.Count; n++)
                        if (FlatDist(p, players[n].Pos) < 4.5f) bad = true;          // never on top of a player
                    if (bad) continue;
                    if (minotaur != null && FlatDist(p, minotaur.transform.position) < 6f) continue;
                    if ((Obstacles.Push(p, 0.7f, Obstacles.Enemies) - p).sqrMagnitude > 0.0001f) continue;
                    placed = true;
                }
                if (!placed) continue;
                float roll = Random.value;
                int kind = roll < red ? 2 : roll < red + blue ? 1 : 0;
                Slime.Spawn(kind, p, actors);
                Fx.I.Dust(p, 6);
                made++;
            }
            if (made > 0)
            {
                packCount[pid] = packs + 1;
                wave++;
            }
            return made;
        }

        // A banner about the shared world: shown here and, when hosting, on every client too.
        public void WorldBanner(string text, Color color)
        {
            hud.Banner(text, color);
            if (Net.IsHost) Net.BroadcastBanner(text, color);
        }

        // Runs where the world is simulated (single player / host). "by" = who landed the last hit (null: nobody in particular).
        // EXP rule: every living player gets the full EXP of each kill; the kill itself is counted for "by".
        public void OnEnemyKilled(Enemy e, Knight by)
        {
            if (e is Minotaur)
            {
                minotaur = null;
                minoRespawnAt = Time.time + MinotaurRespawn;
            }
            if (e.expReward > 0f)
            {
                if (by == null) by = knight;
                Vector3 at = e.Center + Vector3.up * 0.8f;
                for (int i = 0; i < players.Count; i++)
                {
                    Knight p = players[i];
                    bool credit = p == by;
                    if (Net.Multiplayer && p.Dead && !credit) continue;   // a downed knight gets nothing from kills he did not make
                    bool exp = !Net.Multiplayer || !p.Dead;
                    if (!p.remote)
                    {
                        if (credit) progress.kills++;
                        if (exp) GrantExp(e.expReward, at);
                        if (exp) GrantSouls(e.soulReward, at);
                    }
                    else Net.SendExp(p.pid, exp ? e.expReward : 0f, at, credit, exp ? e.soulReward : 0);
                }
            }
            RollDrop(e);
        }

        // A puppet enemy on a client was reported dead by the host.
        public void OnPuppetKilled(Enemy e)
        {
            if (e is Minotaur) minotaur = null;
        }

        // Every sword / skill hit of a knight goes through here. Where the world is simulated the enemy takes it
        // right away; a client only reports it to the host (and predicts armor-or-flesh for the hit sound).
        public void HitEnemy(Knight by, Enemy e, float damage, float pierce, Vector3 origin, bool crit, float knock, float stun, string source, int src)
        {
            if (Net.Authority) { e.TakeHit(damage, pierce, origin, crit, knock, stun, source, by); return; }
            float toArmor, toHp;
            e.Split(damage, pierce, out toArmor, out toHp);
            e.lastToArmor = toArmor; e.lastToHp = toHp;
            Net.SendHit(e, damage, pierce, crit, knock, stun, origin, src);
        }

        // Host: a client reported a hit. Loose validation (friends-only co-op): the enemy must be alive, the numbers sane,
        // and the swing must have started near where the host last saw that knight and near the enemy.
        public bool ClientHit(Knight by, int enemyId, float damage, float pierce, bool crit, float knock, float stun, Vector3 origin, int src)
        {
            Enemy e = Enemy.Find(enemyId);
            if (by == null || e == null || e.Dead) return false;
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f || damage > MaxHitDamage) return false;
            Vector3 a = origin - by.Pos; a.y = 0f;
            Vector3 b = e.transform.position - origin; b.y = 0f;
            if (a.magnitude > 14f || b.magnitude - e.radius > 9f) return false;
            string source = src >= 10 && src < 10 + Knight.Skills.Length ? Knight.Skills[src - 10].name
                : src >= 0 && src < Knight.Attacks.Length ? Knight.Attacks[src].name : "Sword";
            e.TakeHit(damage, Mathf.Clamp01(pierce), origin, crit, Mathf.Clamp(knock, 0f, 20f), Mathf.Clamp(stun, 0f, 3f), source, by);
            return true;
        }

        // A monster hurts a knight. The local knight takes it here; a remote one is told by the host and applies it
        // on his own machine (his block and dodge are judged where he plays).
        public void DamageKnight(Knight k, float damage, Vector3 from, string source, float knockback = 6f)
        {
            if (k == null) return;
            if (!k.remote) k.TakeDamage(damage, from, source, knockback);
            else if (Net.IsHost) Net.SendKnightHit(k.pid, damage, from, source, knockback);
        }

        // Other players' sounds are quieter and fade with distance from the local knight.
        public float RemoteVolume(Vector3 at)
        {
            if (knight == null) return 0.4f;
            Vector3 d = at - knight.transform.position; d.y = 0f;
            return Mathf.Lerp(0.5f, 0.12f, Mathf.InverseLerp(5f, 24f, d.magnitude));
        }

        // ---------------------------------------------------------------- sword drops

        // All drop randomness is here. Slimes: 4% / 10% / 20% (green / blue / red) for one of the three slime swords;
        // the Minotaur always drops the Horned King's Blade. The dummy drops nothing.
        void RollDrop(Enemy e)
        {
            int id = -1;
            Slime sl = e as Slime;
            if (sl != null)
            {
                float chance = dropOverride >= 0 ? dropOverride : Weapons.SlimeDropChance[sl.kind];
                if (Random.value < chance) id = Weapons.SlimeDrop(Random.value);
            }
            else if (e is Minotaur)
            {
                float chance = dropOverride >= 0 ? dropOverride : Weapons.MinotaurDropChance;
                if (Random.value < chance)
                {
                    if (players.Count <= 1) id = Weapons.Boss;
                    else
                    {
                        // co-op: one boss sword per player, laid out in a ring; each one can only be taken (and seen) by its owner
                        for (int i = 0; i < players.Count; i++)
                        {
                            float ang = i * Mathf.PI * 2f / players.Count;
                            SpawnPickup(Weapons.Boss, e.transform.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 1.8f, players[i].pid);
                        }
                    }
                }
            }
            if (id >= 0) SpawnPickup(id, e.transform.position);

            // food: its own rolls, apart from the sword. Apple 14 / 16 / 18%, Roast Meat 0 / 3 / 8% (green / blue / red);
            // the Minotaur always leaves two Roast Meats. The dummy drops nothing.
            if (foodOverride == 0) return;
            Vector3 at = e.transform.position;
            if (sl != null)
            {
                bool force = foodOverride == 1;
                if (force || Random.value < Foods.AppleChance[sl.kind]) SpawnFood(Foods.Apple, at + FoodOffset());
                if (!force && Random.value < Foods.MeatChance[sl.kind]) SpawnFood(Foods.Meat, at + FoodOffset());
            }
            else if (e is Minotaur)
            {
                for (int i = 0; i < Foods.MinotaurMeat; i++)
                    SpawnFood(Foods.Meat, at + new Vector3(Mathf.Sin(i * 3.14159f + 0.8f), 0f, Mathf.Cos(i * 3.14159f + 0.8f)) * 2.6f);
            }
        }

        static Vector3 FoodOffset() { return new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f)); }

        // Food lying on the ground: whoever with less than full health walks over it first eats it.
        public Pickup SpawnFood(int foodId, Vector3 pos, int netId = 0)
        {
            int count = 0;
            Pickup oldest = null;
            for (int i = 0; i < Pickup.All.Count; i++)
            {
                Pickup q = Pickup.All[i];
                if (q.kind != PickupKind.Food) continue;
                count++;
                if (oldest == null || q.age > oldest.age) oldest = q;
            }
            if (count >= Foods.MaxOnGround && oldest != null && Net.Authority)
            {
                oldest.gameObject.SetActive(false);
                Destroy(oldest.gameObject);
            }
            Pickup p = Pickup.Spawn(foodId, pos, actors, -1, netId != 0 ? netId : NewNetId(), PickupKind.Food);
            Debug.Log("DROP " + Foods.All[foodId].name + " at (" + pos.x.ToString("0.0") + ", " + pos.z.ToString("0.0") + ")");
            return p;
        }

        // owner: -1 = whoever walks over it first, otherwise the pid of the only player who can take it.
        public Pickup SpawnPickup(int weaponId, Vector3 pos, int owner = -1, int netId = 0)
        {
            Pickup p = Pickup.Spawn(weaponId, pos, actors, owner, netId != 0 ? netId : NewNetId());
            WeaponDef w = Weapons.All[weaponId];
            if (!p.hidden)
            {
                lastPickup = p;
                hud.Popup(p.transform.position + Vector3.up * 2.4f, w.name + "!", w.uiColor, 24);
            }
            Debug.Log("DROP " + w.name + " at (" + pos.x.ToString("0.0") + ", " + pos.z.ToString("0.0") + ")" + (owner >= 0 ? " for player " + owner : ""));
            return p;
        }

        // A knight who walks over a sword takes it (decided where the world is simulated).
        void UpdatePickups()
        {
            for (int i = Pickup.All.Count - 1; i >= 0; i--)
            {
                Pickup p = Pickup.All[i];
                if (p.age < 0.35f) continue;
                for (int n = 0; n < players.Count; n++)
                {
                    Knight k = players[n];
                    if (k.Dead || k.Airborne || (p.owner >= 0 && p.owner != k.pid)) continue;
                    if (p.kind == PickupKind.Food && k.hp >= k.MaxHp - 0.5f) continue;     // food is for those who need it
                    Vector3 d = p.transform.position - k.Pos; d.y = 0f;
                    if (d.sqrMagnitude > Pickup.CollectRadius * Pickup.CollectRadius) continue;
                    Collect(k, p);
                    break;
                }
            }
        }

        void Collect(Knight k, Pickup p)
        {
            Vector3 at = p.transform.position;
            int weaponId = p.weaponId, id = p.netId;
            PickupKind kind = p.kind;
            bool seen = !p.hidden;
            p.gameObject.SetActive(false);
            Destroy(p.gameObject);
            if (kind == PickupKind.Food)
            {
                if (!k.remote) CollectFood(weaponId, at);
                else OtherAteFood(k, weaponId, at);
            }
            else if (!k.remote) CollectLocal(weaponId, at);
            else OtherCollected(k, weaponId, at, seen);
            if (Net.IsHost) Net.BroadcastPickupTaken(id, k.pid, (int)kind, weaponId, at);
        }

        // The local knight ate something: health back (a share of his maximum), a green "+N", sparkle.
        public void CollectFood(int foodId, Vector3 at)
        {
            Knight k = knight;
            FoodDef f = Foods.All[foodId];
            int got = Mathf.RoundToInt(k.Heal(f.healFrac * k.MaxHp));
            lastNotice = f.name + "   +" + got + " HP";
            lastFoodHeal = got;
            hud.Popup(k.transform.position + new Vector3(0f, 2.7f, 0f), "+" + got, new Color(0.37f, 0.88f, 0.54f), 22);
            Fx.I.Ring(new Vector3(k.transform.position.x, 0.06f, k.transform.position.z), 0.4f, 2.2f, new Color(f.color.r, f.color.g, f.color.b, 0.9f), 0.45f);
            Fx.I.Burst(k.transform.position + Vector3.up, 12, f.color, 1.5f, 4f, Vector3.up, 0.9f, 0.6f, -1f, 0.1f);
            Sfx.I.Play(Snd.Ready, 0.3f);
            Debug.Log("EAT " + f.name + " +" + got + " HP");
        }

        // Another player ate something: a small flash and his "+N" above him.
        public void OtherAteFood(Knight k, int foodId, Vector3 at)
        {
            FoodDef f = Foods.All[foodId];
            Fx.I.Ring(new Vector3(at.x, 0.06f, at.z), 0.3f, 1.6f, new Color(f.color.r, f.color.g, f.color.b, 0.7f), 0.4f);
            if (k != null) hud.Popup(k.transform.position + new Vector3(0f, 2.7f, 0f), "+" + Mathf.RoundToInt(f.healFrac * k.MaxHp), new Color(0.37f, 0.88f, 0.54f), 18);
            Debug.Log("EAT by " + (k != null ? k.playerName : "?") + ": " + f.name);
        }

        // A hint once per food: the local knight stands next to it but his health is full (the food stays).
        void FoodFullHints()
        {
            if (knight == null || knight.Dead || knight.hp < knight.MaxHp - 0.5f) return;
            for (int i = 0; i < Pickup.All.Count; i++)
            {
                Pickup p = Pickup.All[i];
                if (p.kind != PickupKind.Food || p.fullShown) continue;
                Vector3 d = p.transform.position - knight.transform.position; d.y = 0f;
                if (d.sqrMagnitude > Pickup.CollectRadius * Pickup.CollectRadius) continue;
                p.fullShown = true;
                hud.Popup(knight.transform.position + new Vector3(0f, 2.7f, 0f), "HP full", new Color(0.75f, 0.75f, 0.78f), 16);
                fullHints++;
            }
        }

        // The local knight got a sword. New sword: goes to the inventory. Duplicate: refines the owned one
        // (+1, up to +5), and past that it is worth a little EXP.
        public void CollectLocal(int weaponId, Vector3 at)
        {
            Knight k = knight;
            WeaponDef w = Weapons.All[weaponId];
            int result = k.inventory.Add(w.id);
            if (result == 0) lastNotice = "Picked up " + w.name + "   -   press Tab to equip";
            else if (result == 1) lastNotice = w.name + " refined to +" + k.inventory.plus[w.id] + "   (duplicate: +5% damage)";
            else
            {
                lastNotice = w.name + " is already +" + Weapons.MaxPlus + "   -   duplicate turned into EXP";
                GrantExp(Weapons.DuplicateExp, at + Vector3.up * 2f);
            }
            hud.Notice(lastNotice, w.uiColor);
            Fx.I.Ring(new Vector3(at.x, 0.06f, at.z), 0.4f, 2.6f, new Color(w.uiColor.r, w.uiColor.g, w.uiColor.b, 0.9f), 0.45f);
            Fx.I.Burst(at + Vector3.up, 16, w.uiColor, 2f, 7f, Vector3.up, 0.9f, 0.6f, 6f, 0.12f);
            Debug.Log("PICKUP " + lastNotice);
        }

        // Another player took a sword that was visible here: a small flash, no inventory change.
        public void OtherCollected(Knight k, int weaponId, Vector3 at, bool seen)
        {
            WeaponDef w = Weapons.All[weaponId];
            if (seen) Fx.I.Ring(new Vector3(at.x, 0.06f, at.z), 0.4f, 2.0f, new Color(w.uiColor.r, w.uiColor.g, w.uiColor.b, 0.7f), 0.4f);
            Debug.Log("PICKUP by " + (k != null ? k.playerName : "?") + ": " + w.name);
        }

        // ---------------------------------------------------------------- inventory window (Tab)

        public void SetInventory(bool open)
        {
            if (open == InventoryOpen || (open && (LevelStep != 0 || knight.Dead))) return;
            InventoryOpen = open;
            if (open) hud.ShowInventory();
            else
            {
                hud.HideInventory();
                inputBlockUntil = Time.unscaledTime + 0.2f;
            }
        }

        public void EquipWeapon(int id)
        {
            if (!knight.Equip(id)) return;
            WeaponDef w = knight.Weapon;
            hud.Notice("Equipped " + knight.inventory.NameOf(id), w.uiColor);
            Debug.Log("EQUIP " + knight.inventory.NameOf(id) + ": " + w.StatLine());
        }

        // All EXP goes through here. Returns the number of levels gained.
        public int GrantExp(float amount, Vector3 popupAt)
        {
            if (progress.IsMax) return 0;
            int gained = progress.AddExp(amount);
            hud.Popup(popupAt, "+" + Mathf.RoundToInt(amount) + " EXP", new Color(0.8f, 0.6f, 1f), 20);
            if (gained > 0)
            {
                knight.OnLevelUp();
                hud.Banner("LEVEL UP!   Lv " + progress.level + (progress.IsMax ? "  (MAX)" : ""), new Color(1f, 0.88f, 0.35f));
                levelUpOpenAt = Time.unscaledTime + 0.55f;
                Debug.Log("LEVELUP level=" + progress.level + " totalExp=" + progress.totalExp);
            }
            return gained;
        }

        // Soul points: a plain score for now (nothing to spend them on). They belong to the player and survive death.
        public void GrantSouls(int amount, Vector3 popupAt)
        {
            if (amount <= 0 || knight == null) return;
            knight.souls += amount;
            hud.Popup(popupAt + new Vector3(0.9f, -0.3f, 0f), "+" + amount + " Soul", Hud.SoulColor, 18);
            Fx.I.Burst(popupAt, 5, Hud.SoulColor, 1.5f, 3f, Vector3.up, 0.8f, 0.7f, -2f, 0.08f);
        }

        // Step 1: the three upgrade cards. Step 2 (after a card is picked): where the stat point goes.
        void OpenLevelUp()
        {
            SetInventory(false);
            if (Net.Multiplayer) knight.Protect(LevelUpProtect);     // online nothing pauses: a short shield while choosing instead
            offers = progress.MakeOffers();
            if (offers.Length > 0)
            {
                LevelStep = 1;
                hud.ShowLevelUp(offers, progress.level - progress.pendingChoices + 1);
                return;
            }
            progress.pendingChoices = Mathf.Max(0, progress.pendingChoices - 1);   // every upgrade is capped
            OpenStatStep();
        }

        void OpenStatStep()
        {
            if (!progress.CanSpendAny) { CloseLevelUp(); return; }
            LevelStep = 2;
            hud.ShowStatChoice(knight);
        }

        void CloseLevelUp()
        {
            LevelStep = 0;
            hud.HideLevelUp();
            if (Net.Multiplayer) knight.Protect(0.6f);
            inputBlockUntil = Time.unscaledTime + 0.2f;
            levelUpOpenAt = Time.unscaledTime + 0.3f;
        }

        public void ChooseUpgrade(int index)
        {
            if (LevelStep != 1 || offers == null || index < 0 || index >= offers.Length) return;
            UpgradeOffer o = offers[index];
            progress.Apply(o);
            progress.pendingChoices = Mathf.Max(0, progress.pendingChoices - 1);
            Debug.Log("UPGRADE picked " + (index + 1) + ": " + o.title + " " + o.line);
            hud.Banner(o.title + ":  " + o.line, Hud.StatColor(o.stat));
            OpenStatStep();
        }

        // index 0 / 1 / 2 = HP / BLOCK / STAMINA
        public void ChooseStat(int index)
        {
            if (LevelStep != 2 || index < 0 || index > 2) return;
            Attr a = (Attr)index;
            if (!knight.SpendPoint(a)) return;                    // that stat is maxed: the panel stays open
            Debug.Log("STATPOINT " + Progress.AttrName(a) + " -> " + progress.Points(a) + " (unspent " + progress.statPoints + ")");
            hud.Banner(Progress.AttrName(a) + " +1", Hud.AttrColor(a));
            CloseLevelUp();
        }

        // The local knight got back up.
        // The local knight just went down: close any window and show the YOU DIED screen with the numbers from before
        // the reset (the reset itself happens when he stands up again, in OnKnightRevive).
        public void OnKnightDown(Knight k)
        {
            if (k != knight) return;
            LevelStep = 0;
            offers = null;
            hud.HideLevelUp();
            SetInventory(false);
            progress.pendingChoices = 0;
            hud.ShowDeath(progress.level, k.inventory.OwnedCount, Weapons.All.Length, k.souls, k);
        }

        // The local knight got back up: dying costs the level, EXP, cards and stat points (swords and Soul stay).
        public void OnKnightRevive(Knight k)
        {
            hud.HideDeath();
            k.ResetProgress();
            if (Net.Authority) PushEnemiesFrom(k.spawnPoint);
            hud.Banner("Revived - back to Level 1", new Color(0.7f, 0.9f, 1f));
        }

        // The camera jumps (does not glide) to the local knight.
        public void SnapCamera()
        {
            if (knight != null) camFocus = knight.transform.position;
        }

        // Clears the area around a knight who is about to stand up again in town (single player / host): the slimes
        // within 60 m are removed quietly, the Minotaur (if he somehow is near) goes home.
        public void PushEnemiesFrom(Vector3 home)
        {
            foreach (Enemy e in new List<Enemy>(Enemy.All))
            {
                if (e.Dead || e is Dummy) continue;
                if (FlatDist(e.transform.position, home) > (e is Minotaur ? 8f : 60f)) continue;
                if (e is Minotaur) { e.transform.position = MinotaurHome; continue; }
                e.gameObject.SetActive(false);
                Destroy(e.gameObject);
            }
        }

        // Co-op: every knight is down at the same moment. All slimes are removed and the packs start from zero
        // when they stand up in town; the Minotaur goes back to his lair with full health.
        void OnPartyWipe()
        {
            foreach (Enemy e in new List<Enemy>(Enemy.All))
            {
                if (!(e is Slime)) continue;
                e.gameObject.SetActive(false);
                Destroy(e.gameObject);
            }
            packCount.Clear();
            packDue.Clear();
            if (minotaur != null && !minotaur.Dead) minotaur.ResetToLair(MinotaurHome);
            WorldBanner("Everyone is down - back to town", new Color(1f, 0.5f, 0.4f));
            Debug.Log("WIPE all " + players.Count + " knights down, " + wave + " packs so far");
        }

        // Removes every enemy, pickup, effect and floating number.
        void ClearWorld()
        {
            foreach (Enemy e in new List<Enemy>(Enemy.All))
            {
                e.gameObject.SetActive(false);
                Destroy(e.gameObject);
            }
            foreach (Pickup p in new List<Pickup>(Pickup.All))
            {
                p.gameObject.SetActive(false);
                Destroy(p.gameObject);
            }
            dummy = null;
            minotaur = null;
            lastPickup = null;
            hud.ClearPopups();
            Fx.I.Clear();
        }

        // F5: back to level 1 with a fresh arena. Online only the host can do it, and it restarts everybody.
        public bool RequestReset()
        {
            if (Net.IsClient)
            {
                hud.Notice("Only the host can restart (F5)", new Color(1f, 0.6f, 0.5f));
                return false;
            }
            ResetAll();
            if (Net.IsHost) Net.BroadcastReset();
            return true;
        }

        // The local part of a restart also runs on a client when the host restarts.
        public void ResetAll()
        {
            ClearWorld();
            offers = null;
            LevelStep = 0;
            hud.HideLevelUp();
            hud.HideDeath();
            SetInventory(false);
            hitStopUntil = 0f;
            minoRespawnAt = 0f;
            wiped = false;
            knight.ResetAll();                                    // level, upgrades, stat points and swords
            if (Net.Authority) SpawnActors();
            else { wave = 0; slimesAlive = 0; }
            camFocus = knight.transform.position;
            hud.Banner("Restart - level 1", Color.white);
            Sfx.I.Play(Snd.Ready);
        }

        // ---------------------------------------------------------------- juice

        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmp * (shakeTime / Mathf.Max(0.01f, shakeDur))) return;
            shakeAmp = amplitude;
            shakeDur = shakeTime = duration;
        }

        // Freezes the action for a few hundredths of a second so hits feel heavy.
        public void HitStop(float seconds)
        {
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        }

        // Road signs are arrow boards without text: the words appear on the HUD when a knight walks up to one.
        float signCheckAt;
        int signShown = -1;

        void UpdateSigns()
        {
            if (Time.unscaledTime < signCheckAt || knight == null || knight.Dead) return;
            signCheckAt = Time.unscaledTime + 0.3f;
            Vector3 p = knight.Pos;
            var list = MapLayout.Signs;
            int near = -1;
            for (int i = 0; i < list.Count; i++)
            {
                float dx = p.x - list[i].pos.x, dz = p.z - list[i].pos.z;
                if (dx * dx + dz * dz < 64f) { near = i; break; }
            }
            if (near == signShown) return;
            signShown = near;
            if (near >= 0) hud.Notice(list[near].text, new Color(1f, 0.92f, 0.65f));
        }

        void Update()
        {
            Net.Poll();                                           // network events first: they may start or end the session
            if (!InSession) return;

            if (InputX.Down(KeyCode.Escape))
            {
                // Esc closes the Tab window first; it does not count as the "Esc again" that leaves to the menu
                if (InventoryOpen) SetInventory(false);
                else
                {
                    LeaveKey();
                    if (!InSession) return;
                }
            }
            if (InputX.Down(KeyCode.F1)) hud.ToggleControls();
            UpdateSigns();
            if (InputX.Down(KeyCode.M))
            {
                Sfx.I.CycleVolume();
                hud.Notice("Sound: " + Sfx.I.VolumeLabel, Color.white);
            }

            // The two panels pause the game in single player. Online they are just overlays: the world, the other
            // players and this knight's own movement keep running underneath.
            bool online = Net.Multiplayer;
            if (LevelStep != 0)
            {
                if (!online) Time.timeScale = 0f;
                int pick = hud.LevelUpInput(LevelStep == 1 ? offers.Length : 3);
                if (pick >= 0)
                {
                    if (LevelStep == 1) ChooseUpgrade(pick); else ChooseStat(pick);
                }
                if (!online) return;
            }
            else if (progress.pendingChoices > 0 && Time.unscaledTime >= levelUpOpenAt && !knight.Dead && !knight.Airborne)
            {
                OpenLevelUp();
                if (LevelStep != 0 && !online) { Time.timeScale = 0f; return; }
            }

            // Tab: inventory + stats window
            if (LevelStep == 0)
            {
                if (InputX.Down(KeyCode.Tab)) SetInventory(!InventoryOpen);
                if (InventoryOpen)
                {
                    if (!online) Time.timeScale = 0f;
                    int pick = hud.InventoryInput();
                    if (pick >= 0) EquipWeapon(pick);
                    if (!online) return;
                }
            }

            // hit-stop is a single-player effect: online it would slow the whole world down for everyone
            Time.timeScale = !online && Time.unscaledTime < hitStopUntil ? 0.03f : 1f;

            if (InputX.Down(KeyCode.F5) && RequestReset()) return;
            if (InputX.Down(KeyCode.H) && !PanelOpen) knight.TakeDamage(20f, knight.transform.position + knight.transform.forward, "Test hit");

            FoodFullHints();
            if (Net.Authority) SimulateWorld();
        }

        // Pickups, wave / boss spawning and enemy separation: single player and host only.
        void SimulateWorld()
        {
            UpdatePickups();

            // --- co-op: everybody down at once ---
            if (Net.Multiplayer)
            {
                bool allDown = players.Count > 0;
                for (int i = 0; i < players.Count; i++) if (!players[i].Dead) allDown = false;
                if (allDown && !wiped) OnPartyWipe();
                wiped = allDown;
            }

            // --- spawning ---
            UpdatePacks();
            if (minotaur == null && Time.time >= minoRespawnAt)
            {
                minotaur = Minotaur.Spawn(MinotaurHome, actors);
                WorldBanner("The Minotaur returns!", new Color(1f, 0.5f, 0.35f));
            }

            // --- enemies do not stack on each other ---
            List<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy a = all[i];
                if (a.Dead) continue;
                for (int j = i + 1; j < all.Count; j++)
                {
                    Enemy b = all[j];
                    if (b.Dead) continue;
                    Vector3 d = b.transform.position - a.transform.position; d.y = 0f;
                    float min = a.radius + b.radius, dist = d.magnitude;
                    if (dist >= min || dist < 0.001f) continue;
                    Vector3 n = d / dist;
                    float wa = b.mass / (a.mass + b.mass);
                    a.transform.position -= n * (min - dist) * wa;
                    b.transform.position += n * (min - dist) * (1f - wa);
                }
            }
        }

        void LateUpdate()
        {
            if (!InSession || knight == null) return;
            Net.LateTick();                                       // positions are final for this frame: send them
            Vector3 target = knight.transform.position; target.y = 0f;
            camFocus = Vector3.Lerp(camFocus, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));

            Vector3 offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDur));
                offset = (Vector3)(Random.insideUnitCircle * shakeAmp * k);
                if (shakeTime <= 0f) shakeAmp = 0f;
            }
            PlaceCamera(offset);
        }
    }
}
