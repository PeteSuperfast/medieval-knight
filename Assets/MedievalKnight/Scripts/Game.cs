using UnityEngine;

namespace MedievalKnight
{
    // Entry point. Builds the whole 2.5D test arena from code: perspective camera, parallax
    // background layers placed at real Z depth, a floor, the straw dummy, the knight and the HUD.
    public class Game : MonoBehaviour
    {
        public static Game I;

        public const float CamDist = 14f;      // camera distance to the gameplay plane (z = 0)
        public const float CamY = 4f;
        public const float ViewHeight = 11f;   // world units visible at z = 0
        public const float ArenaHalf = 11f;    // knight can walk -ArenaHalf .. +ArenaHalf

        public Camera cam;
        public Knight knight;
        public Dummy dummy;
        public Hud hud;
        public bool counterMode;               // T: the dummy swings back after being hit

        float shakeAmp, shakeTime, shakeDur;
        float hitStopUntil;
        float camX;

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
            BuildWorld();

            new GameObject("Fx").AddComponent<Fx>().transform.SetParent(transform, false);

            dummy = new GameObject("StrawDummy").AddComponent<Dummy>();
            dummy.transform.SetParent(transform, false);
            dummy.transform.position = new Vector3(4.5f, 0f, 0f);

            knight = new GameObject("Knight").AddComponent<Knight>();
            knight.transform.SetParent(transform, false);
            knight.transform.position = new Vector3(0f, 0f, 0f);

            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);

            SelfTest.StartIfRequested(gameObject);
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = false;
            cam.fieldOfView = 2f * Mathf.Atan(ViewHeight * 0.5f / CamDist) * Mathf.Rad2Deg;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.5f, 0.78f);
            cam.transform.position = new Vector3(0f, CamY, -CamDist);
            cam.transform.rotation = Quaternion.identity;
        }

        // ---------------------------------------------------------------- world

        void BuildWorld()
        {
            var world = new GameObject("World").transform;
            world.SetParent(transform, false);

            // Sky rides with the camera so it never scrolls.
            const float skyDist = 120f;
            var sky = Art.NewSprite("Sky", Art.Get("bg_1_sky"), cam.transform, -100);
            float skyScale = skyDist * ViewHeight / CamDist / (270f / Art.PPU) * 1.06f;
            sky.transform.localPosition = new Vector3(0f, 0f, skyDist);
            sky.transform.localScale = Vector3.one * skyScale;

            // Parallax layers: pushed back in Z and scaled up so they keep their on-screen size
            // but scroll slower than the knight (same idea as DepthLayer.cs in the reference pack).
            Layer(world, "bg_2_mountains_far", 80f, -90, 2);
            Layer(world, "bg_3_mountains_mid", 52f, -80, 2);
            Layer(world, "bg_4_castle", 34f, -70, 0);
            Layer(world, "bg_5_forest", 20f, -60, 3);
            Layer(world, "bg_6_hills_near", 9f, -50, 3);

            // Walkable grass floor (a real horizontal surface) and the earth wall in front of it.
            const float zFront = -1.5f, zBack = 3.2f, width = 200f;
            var floor = Art.NewSprite("Floor", Art.GrassTop(), world, -12);
            floor.drawMode = SpriteDrawMode.Tiled;
            floor.size = new Vector2(width, zBack - zFront);
            floor.transform.position = new Vector3(0f, 0f, (zFront + zBack) * 0.5f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var edge = Art.NewSprite("FloorEdge", Art.Get("ground_grass", 0.5f, 0.5f, true), world, -11);
            edge.drawMode = SpriteDrawMode.Tiled;
            edge.size = new Vector2(width, 1f);
            edge.transform.position = new Vector3(0f, -0.5f, zFront);

            var dirt = Art.NewSprite("Dirt", Art.Get("dirt", 0.5f, 0.5f, true), world, -11);
            dirt.drawMode = SpriteDrawMode.Tiled;
            dirt.size = new Vector2(width, 4f);
            dirt.transform.position = new Vector3(0f, -3f, zFront);

            // Decoration standing on the floor a little behind the fighting line.
            Decor(world, "banner", -8.5f, 2.4f, 1.5f, 1, 1);
            Decor(world, "banner", -2.5f, 2.4f, 1.5f, 1, 1);
            Decor(world, "banner", 8f, 2.4f, 1.5f, 1, 1);
            Decor(world, "banner", 12.5f, 2.4f, 1.5f, 1, 1);
            Decor(world, "stone_brick", -5.5f, 2.7f, 1f, 4, 2);
            Decor(world, "stone_brick", 10.2f, 2.7f, 1f, 3, 1);
            Decor(world, "wood_plank", 1.8f, 2.8f, 1f, 3, 1);
        }

        void Layer(Transform parent, string sprite, float depth, int order, int copiesEachSide)
        {
            const float flatScale = 0.68f;                       // art is 480x270 px, shown 20.4 x 11.5 units
            float flatW = 480f / Art.PPU * flatScale;
            float flatY = -1.75f + 270f / Art.PPU * flatScale * 0.5f;
            float s = (CamDist + depth) / CamDist;
            for (int i = -copiesEachSide; i <= copiesEachSide; i++)
            {
                var sr = Art.NewSprite(sprite, Art.Get(sprite), parent, order);
                sr.transform.position = new Vector3(i * flatW * s, CamY + (flatY - CamY) * s, depth);
                sr.transform.localScale = Vector3.one * (flatScale * s * 1.002f);
            }
        }

        void Decor(Transform parent, string sprite, float x, float z, float scale, int tilesX, int tilesY)
        {
            Sprite sp = Art.Get(sprite, 0.5f, 0f, true);
            var sr = Art.NewSprite(sprite, sp, parent, -8);
            if (tilesX > 1 || tilesY > 1)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(sp.bounds.size.x * tilesX, sp.bounds.size.y * tilesY);
            }
            sr.transform.position = new Vector3(x, 0f, z);
            sr.transform.localScale = Vector3.one * scale;
            sr.color = new Color(0.86f, 0.86f, 0.9f);

            var sh = Art.NewSprite("Shadow", Art.Blob(), parent, -9);
            sh.color = new Color(0f, 0f, 0f, 0.3f);
            sh.transform.position = new Vector3(x, 0.01f, z - 0.05f);
            sh.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = new Vector3(sp.bounds.size.x * tilesX * scale * 1.3f, 0.7f, 1f);
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
            Time.timeScale = Time.unscaledTime < hitStopUntil ? 0.02f : 1f;

            if (InputX.Down(KeyCode.Escape)) Application.Quit();
            if (InputX.Down(KeyCode.T))
            {
                counterMode = !counterMode;
                hud.Banner(counterMode ? "Dummy counter-attack ON" : "Dummy counter-attack OFF", Color.white);
            }
            if (InputX.Down(KeyCode.R))
            {
                knight.ResetAll();
                dummy.ResetAll();
                hud.Banner("Reset", Color.white);
            }
            if (InputX.Down(KeyCode.H)) knight.TakeDamage(20f, knight.transform.position.x + knight.Facing);
        }

        void LateUpdate()
        {
            float target = Mathf.Clamp(knight.transform.position.x * 0.9f, -9f, 9f);
            camX = Mathf.Lerp(camX, target, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));

            Vector3 offset = Vector3.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDur));
                offset = (Vector3)(Random.insideUnitCircle * shakeAmp * k);
                if (shakeTime <= 0f) shakeAmp = 0f;
            }
            cam.transform.position = new Vector3(camX, CamY, -CamDist) + offset;
        }
    }
}
