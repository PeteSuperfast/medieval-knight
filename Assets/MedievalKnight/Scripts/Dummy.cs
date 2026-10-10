using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Straw training dummy. Has its own armor + health: armor soaks damage first, regenerates after
    // a pause; at 0 health the dummy falls apart and a fresh one pops up.
    public class Dummy : MonoBehaviour
    {
        public const float MaxHp = 100f, MaxArmor = 50f;
        public const float HalfWidth = 0.55f;

        public float hp = MaxHp, armor = MaxArmor;
        public bool Broken { get; private set; }

        // stats shown by the HUD
        public int hits;
        public float totalDamage;
        public string lastHit = "-";
        readonly List<Vector2> recent = new List<Vector2>();   // (time, damage)

        Transform pivot;                 // rotates around the feet for the wobble
        SpriteRenderer body, flash, shadow;
        Transform barRoot, hpFill, armorFill;
        Coroutine wobble, counter;
        float lastHitTime = -10f, lastCounterTime = -10f;

        static readonly Color Straw = new Color(0.93f, 0.8f, 0.42f);
        static readonly Color Spark = new Color(1f, 0.97f, 0.8f);

        void Awake()
        {
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);

            body = Art.NewSprite("Body", Art.Get("training_dummy", 0.5f, 0f), pivot, 0);
            flash = Art.NewSprite("Flash", Art.Silhouette("training_dummy", 0.5f, 0f), pivot, 1);
            flash.color = new Color(1, 1, 1, 0);

            shadow = Art.NewSprite("Shadow", Art.Blob(), transform, -9);
            shadow.color = new Color(0, 0, 0, 0.38f);
            shadow.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(1.9f, 0.9f, 1f);

            // small world-space bars above the head
            barRoot = new GameObject("Bars").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, 2.45f, 0f);
            armorFill = Bar(0.14f, new Color(0.55f, 0.72f, 0.95f));
            hpFill = Bar(0f, new Color(0.9f, 0.22f, 0.2f));
        }

        Transform Bar(float y, Color c)
        {
            const float w = 1.6f, h = 0.1f;
            var bg = Art.NewSprite("BarBg", Art.Pixel(0f), barRoot, 2);
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            bg.transform.localPosition = new Vector3(-w * 0.5f - 0.03f, y, 0f);
            bg.transform.localScale = new Vector3(w + 0.06f, h + 0.06f, 1f);
            var fill = Art.NewSprite("BarFill", Art.Pixel(0f), barRoot, 3);
            fill.color = c;
            fill.transform.localPosition = new Vector3(-w * 0.5f, y, 0f);
            fill.transform.localScale = new Vector3(w, h, 1f);
            return fill.transform;
        }

        public Vector3 Center { get { return transform.position + new Vector3(0f, 1.05f, 0f); } }

        public float Dps
        {
            get
            {
                float sum = 0f;
                for (int i = recent.Count - 1; i >= 0; i--)
                {
                    if (Time.time - recent[i].x > 5f) recent.RemoveAt(i);
                    else sum += recent[i].y;
                }
                return sum / 5f;
            }
        }

        void Update()
        {
            if (!Broken && armor < MaxArmor && Time.time - lastHitTime > 3f)
                armor = Mathf.Min(MaxArmor, armor + 20f * Time.deltaTime);

            SetBar(hpFill, hp / MaxHp);
            SetBar(armorFill, armor / MaxArmor);

            Color f = flash.color;
            if (f.a > 0f) { f.a = Mathf.Max(0f, f.a - Time.deltaTime / 0.14f); flash.color = f; }
        }

        static void SetBar(Transform t, float k)
        {
            Vector3 s = t.localScale;
            s.x = 1.6f * Mathf.Clamp01(k);
            t.localScale = s;
        }

        // pierce: 0..1 share of the damage that ignores armor (the thrust uses this).
        // hitDirDeg: direction the sparks fly (0 = right, 90 = up).
        public void TakeHit(float damage, float pierce, float attackerX, string attackName, float hitDirDeg, bool isThrust)
        {
            if (Broken) return;

            float direct = damage * Mathf.Clamp01(pierce);
            float rest = damage - direct;
            float toArmor = Mathf.Min(armor, rest);
            armor -= toArmor;
            float toHp = direct + (rest - toArmor);
            hp = Mathf.Max(0f, hp - toHp);
            lastHitTime = Time.time;

            hits++;
            totalDamage += damage;
            recent.Add(new Vector2(Time.time, damage));
            lastHit = attackName + "  " + Mathf.RoundToInt(damage) + "  (armor -" + Mathf.RoundToInt(toArmor)
                + ", health -" + Mathf.RoundToInt(toHp) + ")";

            float dir = Mathf.Sign(transform.position.x - attackerX);
            if (dir == 0f) dir = 1f;
            Vector3 c = Center;
            Vector3 front = c + new Vector3(-dir * 0.35f, 0f, 0f);

            // numbers
            if (toArmor >= 0.5f) Game.I.hud.Popup(c + new Vector3(-0.35f, 1.0f, 0f), "-" + Mathf.RoundToInt(toArmor), new Color(0.65f, 0.82f, 1f), 26);
            if (toHp >= 0.5f) Game.I.hud.Popup(c + new Vector3(0.35f, 1.25f, 0f), "-" + Mathf.RoundToInt(toHp), new Color(1f, 0.35f, 0.25f), 32);

            // flash + sparks + straw
            flash.color = new Color(1, 1, 1, 0.95f);
            Fx.I.Burst(front, isThrust ? 8 : 12, Spark, 3f, 9f, hitDirDeg, isThrust ? 50f : 110f, 0.28f, 6f, 0.11f);
            if (toArmor > 0.5f) Fx.I.Burst(front, 6, new Color(0.7f, 0.85f, 1f), 2f, 6f, hitDirDeg, 160f, 0.3f, 8f, 0.09f);
            if (isThrust)   // straw blown straight out of the back
                Fx.I.Burst(c + new Vector3(dir * 0.4f, 0f, 0f), 14, Straw, 4f, 11f, dir > 0 ? 0f : 180f, 30f, 0.55f, 9f, 0.1f);
            else
                Fx.I.Burst(c, 9, Straw, 1.5f, 6f, hitDirDeg, 150f, 0.6f, 14f, 0.1f);

            if (hp <= 0f)
            {
                StartCoroutine(BreakRoutine(dir));
                return;
            }

            if (wobble != null) StopCoroutine(wobble);
            wobble = StartCoroutine(Wobble(dir, Mathf.Lerp(10f, 24f, damage / 30f)));

            if (Game.I.counterMode && counter == null && Time.time - lastCounterTime > 1.1f)
                counter = StartCoroutine(CounterRoutine());
        }

        IEnumerator Wobble(float dir, float amount)
        {
            body.color = new Color(1f, 0.55f, 0.5f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
            {
                float w = Mathf.Sin(t * 26f) * (1f - t) * amount;
                pivot.localRotation = Quaternion.Euler(0, 0, -dir * w);
                body.color = Color.Lerp(new Color(1f, 0.55f, 0.5f), Color.white, t);
                yield return null;
            }
            pivot.localRotation = Quaternion.identity;
            body.color = Color.white;
            wobble = null;
        }

        // Optional (T): after a short telegraph the dummy whips toward the knight and hurts it,
        // so the knight's own health / armor / block can be tested.
        IEnumerator CounterRoutine()
        {
            yield return new WaitForSeconds(0.25f);
            if (Broken) { counter = null; yield break; }
            if (wobble != null) { StopCoroutine(wobble); wobble = null; }

            Knight k = Game.I.knight;
            float side = Mathf.Sign(k.transform.position.x - transform.position.x);
            if (side == 0f) side = -1f;

            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)      // telegraph: lean away, glow yellow
            {
                if (Broken) { counter = null; yield break; }
                pivot.localRotation = Quaternion.Euler(0, 0, side * 22f * t);
                body.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.3f), Mathf.PingPong(t * 4f, 1f));
                yield return null;
            }
            bool struck = false;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.1f)      // whip forward
            {
                if (Broken) { counter = null; yield break; }
                pivot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(side * 22f, -side * 40f, t * t));
                if (!struck && t > 0.5f)
                {
                    struck = true;
                    float dx = Mathf.Abs(k.transform.position.x - transform.position.x);
                    if (dx < 2.1f && k.transform.position.y < 1.4f) k.TakeDamage(15f, transform.position.x);
                    Fx.I.Burst(Center + new Vector3(side * 0.8f, 0.3f, 0f), 6, Straw, 2f, 6f, side > 0 ? 0f : 180f, 70f, 0.4f, 10f, 0.09f);
                }
                yield return null;
            }
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.3f)
            {
                if (Broken) { counter = null; yield break; }
                pivot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-side * 40f, 0f, t * t * (3f - 2f * t)));
                body.color = Color.white;
                yield return null;
            }
            pivot.localRotation = Quaternion.identity;
            lastCounterTime = Time.time;
            counter = null;
        }

        IEnumerator BreakRoutine(float dir)
        {
            Broken = true;
            if (wobble != null) { StopCoroutine(wobble); wobble = null; }
            Game.I.hud.Banner("Dummy destroyed!", new Color(1f, 0.85f, 0.4f));
            Fx.I.Burst(Center, 40, Straw, 2f, 10f, 90f, 360f, 0.9f, 14f, 0.13f);
            Game.I.Shake(0.28f, 0.3f);

            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)     // topple over
            {
                pivot.localRotation = Quaternion.Euler(0, 0, -dir * 85f * t * t);
                body.color = new Color(1, 1, 1, 1f - t * 0.7f);
                yield return null;
            }
            body.color = new Color(1, 1, 1, 0f);
            shadow.enabled = false;
            barRoot.gameObject.SetActive(false);
            yield return new WaitForSeconds(1.4f);
            Restore();
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.25f)     // pop back up
            {
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                pivot.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, t) * s, Mathf.Lerp(0.3f, 1f, t) * (2f - s), 1f);
                yield return null;
            }
            pivot.localScale = Vector3.one;
        }

        void Restore()
        {
            hp = MaxHp;
            armor = MaxArmor;
            Broken = false;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            body.color = Color.white;
            flash.color = new Color(1, 1, 1, 0);
            shadow.enabled = true;
            barRoot.gameObject.SetActive(true);
        }

        public void ResetAll()
        {
            StopAllCoroutines();
            wobble = null;
            counter = null;
            Restore();
            hits = 0;
            totalDamage = 0f;
            lastHit = "-";
            recent.Clear();
        }
    }
}
