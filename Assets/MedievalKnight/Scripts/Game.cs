using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Entry point. Builds the whole 3D top-down arena from code (camera, sun, ground, fence, trees),
    // spawns the knight / dummy / slime waves / Minotaur, and runs levelling, pausing, shake and hit-stop.
    public class Game : MonoBehaviour
    {
        public static Game I;

        public const float ArenaHalf = 19f;        // the fenced field is 38 x 38 m
        public const float CamPitch = 56f, CamDist = 16f, CamFov = 40f;
        public const float MinotaurRespawn = 20f;
        static readonly Vector3 MinotaurHome = new Vector3(0f, 0f, 12.5f);
        static readonly Vector3 DummyPos = new Vector3(5f, 0f, -3.5f);

        public Camera cam;
        public Knight knight;
        public Dummy dummy;
        public Minotaur minotaur;
        public Hud hud;
        public Progress progress { get { return knight.progress; } }   // level / upgrades / stat points belong to the knight
        public UpgradeOffer[] offers;              // the cards currently on screen
        public int wave, slimesAlive;
        public float inputBlockUntil;              // unscaled time; combat input is ignored until then
        public int dropOverride = -1;              // self-test hook: -1 = roll normally, 0 = never drop, 1 = always drop
        public float camZoom = 1f;                 // self-test hook: < 1 moves the camera closer for screenshots
        public Pickup lastPickup;                  // the most recent sword drop
        public string lastNotice = "";             // the most recent "picked up ..." line

        // The level-up panel has two steps: 1 = pick an upgrade card, 2 = spend the stat point. 0 = closed.
        public int LevelStep { get; private set; }
        public bool LevelUpOpen { get { return LevelStep != 0; } }
        public bool InventoryOpen { get; private set; }
        public bool Paused { get { return LevelStep != 0 || InventoryOpen; } }
        public float MinotaurRespawnIn { get { return minoRespawnAt - Time.time; } }

        // Monsters get tougher as the knight levels, so the upgrades keep mattering.
        public float EnemyHpScale { get { return 1f + 0.45f * (progress.level - 1); } }
        public float EnemyDamageScale { get { return 1f + 0.08f * (progress.level - 1); } }

        Transform actors;
        bool waveQueued;
        float levelUpOpenAt, waveAt, minoRespawnAt;
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
            QualitySettings.vSyncCount = 1;

            SetupCamera();
            SetupLight();
            BuildWorld();

            new GameObject("Fx").AddComponent<Fx>().transform.SetParent(transform, false);
            new GameObject("Sfx").AddComponent<Sfx>().transform.SetParent(transform, false);
            actors = Prims.Node(transform, "Actors", Vector3.zero);

            knight = new GameObject("Knight").AddComponent<Knight>();
            knight.transform.SetParent(transform, true);

            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);

            SpawnActors();
            camFocus = knight.transform.position;
            PlaceCamera(Vector3.zero);

            SelfTest.StartIfRequested(gameObject);
        }

        void Start()
        {
            Sfx.I.Play(Snd.Ready);                 // "stand ready" as the game begins
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
            cam.farClipPlane = 200f;
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
            var rng = new System.Random(11);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);

            Material grassOut = Prims.Lit(new Color(0.2f, 0.4f, 0.2f));
            Material grass = Prims.Lit(new Color(0.37f, 0.62f, 0.28f));
            Material grassA = Prims.Lit(new Color(0.42f, 0.67f, 0.3f));
            Material grassB = Prims.Lit(new Color(0.32f, 0.56f, 0.26f));
            Material dirt = Prims.Lit(new Color(0.6f, 0.47f, 0.3f));
            Material stone = Prims.Lit(new Color(0.62f, 0.62f, 0.64f));
            Material stoneDark = Prims.Lit(new Color(0.45f, 0.45f, 0.48f));
            Material wood = Prims.Lit(new Color(0.45f, 0.3f, 0.16f));
            Material woodDark = Prims.Lit(new Color(0.32f, 0.2f, 0.11f));
            Material trunk = Prims.Lit(new Color(0.36f, 0.24f, 0.13f));
            Material[] leaves =
            {
                Prims.Lit(new Color(0.16f, 0.45f, 0.2f)), Prims.Lit(new Color(0.2f, 0.52f, 0.22f)), Prims.Lit(new Color(0.13f, 0.38f, 0.2f))
            };
            Material[] flowers =
            {
                Prims.Lit(new Color(0.95f, 0.9f, 0.35f)), Prims.Lit(new Color(0.95f, 0.5f, 0.6f)), Prims.Lit(new Color(0.9f, 0.92f, 0.98f))
            };

            float A = ArenaHalf;
            Prims.Box(world, "Outside", new Vector3(0f, -0.5f, 0f), new Vector3(160f, 1f, 160f), grassOut, false).GetComponent<MeshRenderer>().receiveShadows = true;
            Prims.Box(world, "Field", new Vector3(0f, -0.49f, 0f), new Vector3(A * 2f + 2.4f, 1f, A * 2f + 2.4f), grass, false).GetComponent<MeshRenderer>().receiveShadows = true;

            // mottled grass
            for (int i = 0; i < 46; i++)
            {
                float w = R(2f, 6f), d = R(2f, 6f);
                Transform t = Prims.Box(world, "Patch", new Vector3(R(-A + 3f, A - 3f), 0.012f + i * 0.0002f, R(-A + 3f, A - 3f)), new Vector3(w, 0.02f, d), i % 2 == 0 ? grassA : grassB, false);
                t.GetComponent<MeshRenderer>().receiveShadows = true;
                t.Rot(0f, R(0f, 90f), 0f);
            }
            // dirt cross-roads and a round stone plaza in the middle
            GroundBox(world, "RoadNS", new Vector3(0f, 0.03f, 0f), new Vector3(3.2f, 0.02f, A * 2f), dirt);
            GroundBox(world, "RoadEW", new Vector3(0f, 0.032f, 0f), new Vector3(A * 2f, 0.02f, 3.2f), dirt);
            Ground(world, "PlazaRim", Prims.Cyl(14), new Vector3(0f, 0f, 0f), new Vector3(13f, 0.05f, 13f), stoneDark);
            Ground(world, "Plaza", Prims.Cyl(14), new Vector3(0f, 0f, 0f), new Vector3(11.6f, 0.06f, 11.6f), stone);
            Ground(world, "PlazaCore", Prims.Cyl(8), new Vector3(0f, 0f, 0f), new Vector3(3.4f, 0.07f, 3.4f), stoneDark);
            // the Minotaur's lair: a dark slab between broken columns at the north end
            Ground(world, "Lair", Prims.Cyl(10), new Vector3(MinotaurHome.x, 0f, MinotaurHome.z), new Vector3(9f, 0.06f, 9f), stoneDark);
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-8.75f + i * 3.5f, 0f, A + 0.75f);   // a ruined colonnade along the north fence
                float h = R(1.6f, 4.2f);
                Prims.Part(world, "Column", Prims.Cyl(8), p, new Vector3(1.0f, h, 1.0f), stone);
                Prims.Box(world, "ColumnBase", p + Vector3.up * 0.15f, new Vector3(1.4f, 0.3f, 1.4f), stoneDark);
                if (h > 3.4f) Prims.Box(world, "ColumnTop", p + Vector3.up * (h + 0.12f), new Vector3(1.4f, 0.24f, 1.4f), stoneDark);
            }

            // flowers and pebbles (decoration only, nothing blocks movement inside the fence)
            for (int i = 0; i < 90; i++)
            {
                Vector3 p = new Vector3(R(-A + 1f, A - 1f), 0.08f, R(-A + 1f, A - 1f));
                if (Mathf.Abs(p.x) < 2.2f || Mathf.Abs(p.z) < 2.2f || p.magnitude < 7.5f) continue;
                if (i % 4 == 0) Prims.Part(world, "Pebble", Prims.Ball(5), p, new Vector3(R(0.25f, 0.55f), R(0.15f, 0.3f), R(0.25f, 0.5f)), stoneDark);
                else Prims.Box(world, "Flower", p, new Vector3(0.16f, 0.16f, 0.16f), flowers[i % 3], false).Rot(0f, R(0f, 90f), 0f);
            }

            // fence
            float f = A + 0.7f;
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, side * 90f, 0f);
                for (float x = -f; x <= f + 0.01f; x += 2.53f)
                    Prims.Box(world, "Post", q * new Vector3(x, 0.65f, f), new Vector3(0.26f, 1.3f, 0.26f), woodDark).localRotation = q;
                Prims.Box(world, "RailTop", q * new Vector3(0f, 1.0f, f), new Vector3(f * 2f, 0.14f, 0.12f), wood).localRotation = q;
                Prims.Box(world, "RailLow", q * new Vector3(0f, 0.5f, f), new Vector3(f * 2f, 0.14f, 0.12f), wood).localRotation = q;
            }

            // banners at the corners
            Material[] cloth = { Prims.Lit(new Color(0.8f, 0.15f, 0.13f)), Prims.Lit(new Color(0.16f, 0.32f, 0.8f)) };
            int bi = 0;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 p = new Vector3(sx * (A - 0.6f), 0f, sz * (A - 0.6f));
                    Prims.Part(world, "Pole", Prims.Cyl(6), p, new Vector3(0.16f, 4.2f, 0.16f), woodDark);
                    Prims.Box(world, "Flag", p + new Vector3(-sx * 0.62f, 3.3f, 0f), new Vector3(1.2f, 1.5f, 0.06f), cloth[bi++ % 2]);
                    Prims.Part(world, "Finial", Prims.Ball(6), p + Vector3.up * 4.3f, new Vector3(0.3f, 0.3f, 0.3f), Prims.Lit(new Color(0.96f, 0.78f, 0.22f), 0.7f, 0.8f));
                }
            }

            // forest and rocks outside the fence
            for (int i = 0; i < 120; i++)
            {
                float ang = R(0f, Mathf.PI * 2f);
                float dist = R(A + 3f, A + 16f);
                Vector3 p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist * 1.25f;
                p.x = Mathf.Clamp(p.x, -A - 20f, A + 20f); p.z = Mathf.Clamp(p.z, -A - 14f, A + 18f);
                if (Mathf.Abs(p.x) < A + 2.4f && Mathf.Abs(p.z) < A + 2.4f) continue;
                if (i % 6 == 0)
                {
                    float s = R(0.8f, 2.2f);
                    Prims.Part(world, "Rock", Prims.Ball(6), p + Vector3.up * 0.3f * s, new Vector3(1.5f, 1f, 1.2f) * s, i % 12 == 0 ? stone : stoneDark).Rot(0f, R(0f, 360f), 0f);
                    continue;
                }
                float sc = R(0.85f, 1.5f);
                Material leaf = leaves[i % 3];
                Transform tree = Prims.Node(world, "Tree", p);
                tree.localScale = Vector3.one * sc;
                tree.Rot(0f, R(0f, 360f), 0f);
                Prims.Part(tree, "Trunk", Prims.Cyl(6), Vector3.zero, new Vector3(0.55f, 1.6f, 0.55f), trunk);
                Prims.Part(tree, "L1", Prims.Cone(7), new Vector3(0f, 1.1f, 0f), new Vector3(3.0f, 2.3f, 3.0f), leaf);
                Prims.Part(tree, "L2", Prims.Cone(7), new Vector3(0f, 2.3f, 0f), new Vector3(2.3f, 2.0f, 2.3f), leaf);
                Prims.Part(tree, "L3", Prims.Cone(7), new Vector3(0f, 3.4f, 0f), new Vector3(1.5f, 1.7f, 1.5f), leaf);
            }
        }

        static void GroundBox(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            Prims.Box(parent, name, pos, size, m, false).GetComponent<MeshRenderer>().receiveShadows = true;
        }

        static void Ground(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material m)
        {
            Prims.Part(parent, name, mesh, pos, scale, m, false).GetComponent<MeshRenderer>().receiveShadows = true;
        }

        // ---------------------------------------------------------------- actors

        void SpawnActors()
        {
            dummy = new GameObject("StrawDummy").AddComponent<Dummy>();
            dummy.transform.SetParent(actors, false);
            dummy.transform.position = DummyPos;
            dummy.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

            minotaur = Minotaur.Spawn(MinotaurHome, actors);
            wave = 0;
            waveQueued = true;
            waveAt = Time.time + 0.2f;
        }

        void SpawnWave()
        {
            waveQueued = false;
            wave++;
            int count = Mathf.Min(4 + wave, 10);
            float blue = Mathf.Clamp((wave - 2) * 0.15f, 0f, 0.5f), red = Mathf.Clamp((wave - 5) * 0.12f, 0f, 0.35f);
            Vector3 kp = knight.transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = kp;
                for (int tries = 0; tries < 20; tries++)
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float dist = wave == 1 ? Random.Range(5.5f, 9f) : Random.Range(8f, 13f);
                    p = kp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                    p.x = Mathf.Clamp(p.x, -ArenaHalf + 1.5f, ArenaHalf - 1.5f);
                    p.z = Mathf.Clamp(p.z, -ArenaHalf + 1.5f, ArenaHalf - 1.5f);
                    bool bad = (p - kp).magnitude < 4.5f || (p - DummyPos).magnitude < 2f;
                    if (minotaur != null && (p - minotaur.transform.position).magnitude < 6f) bad = true;
                    if (wave == 1 && p.z > 5f) bad = true;                 // first wave stays away from the lair
                    if (!bad) break;
                }
                float roll = Random.value;
                int kind = roll < red ? 2 : roll < red + blue ? 1 : 0;
                Slime.Spawn(kind, p, actors);
                Fx.I.Dust(p, 6);
            }
            if (wave > 1) hud.Banner("Wave " + wave + " - " + count + " slimes", new Color(0.6f, 1f, 0.6f));
        }

        public void OnEnemyKilled(Enemy e)
        {
            if (e is Minotaur)
            {
                minotaur = null;
                minoRespawnAt = Time.time + MinotaurRespawn;
            }
            if (e.expReward > 0f)
            {
                progress.kills++;
                GrantExp(e.expReward, e.Center + Vector3.up * 0.8f);
            }
            RollDrop(e);
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
                if (Random.value < chance) id = Weapons.Boss;
            }
            if (id >= 0) SpawnPickup(id, e.transform.position);
        }

        public Pickup SpawnPickup(int weaponId, Vector3 pos)
        {
            lastPickup = Pickup.Spawn(weaponId, pos, actors);
            WeaponDef w = Weapons.All[weaponId];
            hud.Popup(lastPickup.transform.position + Vector3.up * 2.4f, w.name + "!", w.uiColor, 24);
            Debug.Log("DROP " + w.name + " at (" + pos.x.ToString("0.0") + ", " + pos.z.ToString("0.0") + ")");
            return lastPickup;
        }

        // A knight who walks over a sword takes it. New sword: goes to the inventory. Duplicate: refines the
        // owned one (+1, up to +5), and past that it is worth a little EXP.
        void UpdatePickups()
        {
            if (knight.Dead || knight.Airborne) return;
            for (int i = Pickup.All.Count - 1; i >= 0; i--)
            {
                Pickup p = Pickup.All[i];
                Vector3 d = p.transform.position - knight.transform.position; d.y = 0f;
                if (p.age < 0.35f || d.sqrMagnitude > Pickup.CollectRadius * Pickup.CollectRadius) continue;
                Collect(knight, p);
            }
        }

        void Collect(Knight k, Pickup p)
        {
            WeaponDef w = Weapons.All[p.weaponId];
            int result = k.inventory.Add(w.id);
            Vector3 at = p.transform.position;
            if (result == 0) lastNotice = "Picked up " + w.name + "   -   press I to equip";
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
            p.gameObject.SetActive(false);
            Destroy(p.gameObject);
        }

        // ---------------------------------------------------------------- inventory panel (I)

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

        // Step 1: the three upgrade cards. Step 2 (after a card is picked): where the stat point goes.
        void OpenLevelUp()
        {
            SetInventory(false);
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

        public void OnKnightRevive()
        {
            progress.exp = 0f;                                    // dying costs the progress inside the current level
            Vector3 home = new Vector3(0f, 0f, -5f);
            for (int i = 0; i < Enemy.All.Count; i++)
            {
                Enemy e = Enemy.All[i];
                if (e.Dead || e is Dummy) continue;
                Vector3 d = e.transform.position - home; d.y = 0f;
                if (d.magnitude > 8f) continue;
                if (e is Minotaur) e.transform.position = MinotaurHome;
                else
                {
                    Vector3 to = home + (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.right) * 9f;
                    Slime sl = e as Slime;
                    if (sl != null) sl.Warp(to); else e.transform.position = to;
                }
            }
            hud.Banner("Revived - EXP of this level lost", new Color(0.7f, 0.9f, 1f));
        }

        // F5: back to level 1 with a fresh arena.
        public void ResetAll()
        {
            var old = new List<Enemy>(Enemy.All);
            foreach (Enemy e in old)
            {
                e.gameObject.SetActive(false);
                Destroy(e.gameObject);
            }
            foreach (Pickup p in new List<Pickup>(Pickup.All))
            {
                p.gameObject.SetActive(false);
                Destroy(p.gameObject);
            }
            lastPickup = null;
            offers = null;
            LevelStep = 0;
            hud.HideLevelUp();
            SetInventory(false);
            hud.ClearPopups();
            Fx.I.Clear();
            hitStopUntil = 0f;
            minoRespawnAt = 0f;
            knight.ResetAll();                                    // level, upgrades, stat points and swords
            SpawnActors();
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

        void Update()
        {
            if (InputX.Down(KeyCode.Escape)) Application.Quit();
            if (InputX.Down(KeyCode.F1)) hud.ToggleControls();
            if (InputX.Down(KeyCode.M))
            {
                Sfx.I.CycleVolume();
                hud.Notice("Sound: " + Sfx.I.VolumeLabel, Color.white);
            }

            if (LevelStep != 0)
            {
                Time.timeScale = 0f;
                int pick = hud.LevelUpInput(LevelStep == 1 ? offers.Length : 3);
                if (pick >= 0)
                {
                    if (LevelStep == 1) ChooseUpgrade(pick); else ChooseStat(pick);
                }
                return;
            }
            if (progress.pendingChoices > 0 && Time.unscaledTime >= levelUpOpenAt && !knight.Dead && !knight.Airborne)
            {
                OpenLevelUp();
                if (LevelStep != 0) { Time.timeScale = 0f; return; }
            }

            // I: weapons + stats panel (pauses the game like the level-up panel)
            if (InputX.Down(KeyCode.I)) SetInventory(!InventoryOpen);
            if (InventoryOpen)
            {
                Time.timeScale = 0f;
                int pick = hud.InventoryInput();
                if (pick >= 0) EquipWeapon(pick);
                return;
            }

            Time.timeScale = Time.unscaledTime < hitStopUntil ? 0.03f : 1f;

            if (InputX.Down(KeyCode.F5)) { ResetAll(); return; }
            if (InputX.Down(KeyCode.H)) knight.TakeDamage(20f, knight.transform.position + knight.transform.forward, "Test hit");
            UpdatePickups();

            // --- spawning ---
            slimesAlive = 0;
            for (int i = 0; i < Enemy.All.Count; i++)
                if (Enemy.All[i] is Slime && !Enemy.All[i].Dead) slimesAlive++;
            if (!waveQueued && slimesAlive <= 1)
            {
                waveQueued = true;
                waveAt = Time.time + 2.5f;
            }
            if (waveQueued && Time.time >= waveAt) SpawnWave();
            if (minotaur == null && Time.time >= minoRespawnAt)
            {
                minotaur = Minotaur.Spawn(MinotaurHome, actors);
                hud.Banner("The Minotaur returns!", new Color(1f, 0.5f, 0.35f));
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
