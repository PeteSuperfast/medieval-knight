using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Shared by everything the knight can hit: health + armor (armor absorbs first), hit flash,
    // knockback, stun, a billboard health bar and the damage read-out.
    public abstract class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public string label = "Enemy";
        public float hp, maxHp, armor, maxArmor;
        public float radius = 0.6f, height = 1.2f, expReward, mass = 1f;
        public bool solid;                       // the knight cannot walk through it
        public bool Dead { get; protected set; }

        // read-out for the HUD / self-test
        public int hits;
        public float totalDamage, lastDamage;
        public bool lastCrit;
        public string lastHit = "-";

        protected Transform rig;
        protected Tint tint;
        protected float flash, stunUntil;
        protected Vector3 knock;
        protected float knockResist = 1f, stunResist = 1f;

        Transform bar, barFill, barArmor;
        float barWidth;

        public bool Stunned { get { return Time.time < stunUntil; } }
        public Vector3 Center { get { return transform.position + Vector3.up * height * 0.55f; } }

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        protected void InitStats(float health, float armorPoints)
        {
            hp = maxHp = health;
            armor = maxArmor = armorPoints;
        }

        protected void BuildBar(float width)
        {
            barWidth = width;
            bar = Prims.Node(transform, "Bar", new Vector3(0f, height + 0.55f, 0f));
            Prims.Box(bar, "Bg", Vector3.zero, new Vector3(width + 0.1f, 0.24f, 0.02f), Prims.Glow(new Color(0.05f, 0.05f, 0.07f)), false);
            barFill = Prims.Node(bar, "Fill", new Vector3(-width * 0.5f, -0.03f, -0.03f));
            Prims.Box(barFill, "Q", new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 0.02f), Prims.Glow(new Color(0.9f, 0.2f, 0.18f)), false);
            barArmor = Prims.Node(bar, "Armor", new Vector3(-width * 0.5f, 0.07f, -0.03f));
            Prims.Box(barArmor, "Q", new Vector3(0.5f, 0f, 0f), new Vector3(1f, 1f, 0.02f), Prims.Glow(new Color(0.55f, 0.75f, 1f)), false);
        }

        // damage is the final number (already multiplied / crit). pierce = share that ignores armor.
        public virtual void TakeHit(float damage, float pierce, Vector3 from, bool crit, float knockback, float stun, string source)
        {
            if (Dead) return;
            float direct = damage * Mathf.Clamp01(pierce);
            float rest = damage - direct;
            float toArmor = Mathf.Min(armor, rest);
            armor -= toArmor;
            float toHp = direct + rest - toArmor;
            hp = Mathf.Max(0f, hp - toHp);

            hits++;
            totalDamage += damage;
            lastDamage = damage;
            lastCrit = crit;
            lastHit = source + " " + Mathf.RoundToInt(damage) + (crit ? " CRIT" : "");
            flash = 1f;

            Vector3 away = transform.position - from; away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            knock += away * knockback * knockResist;
            if (stun > 0f) stunUntil = Mathf.Max(stunUntil, Time.time + stun * stunResist);

            Vector3 at = Center + new Vector3(Random.Range(-0.3f, 0.3f), height * 0.5f, 0f);
            if (crit) Game.I.hud.Popup(at, Mathf.RoundToInt(damage) + "!", new Color(1f, 0.62f, 0.1f), 34);
            else if (toHp < 0.5f) Game.I.hud.Popup(at, Mathf.RoundToInt(damage).ToString(), new Color(0.65f, 0.82f, 1f), 22);
            else Game.I.hud.Popup(at, Mathf.RoundToInt(damage).ToString(), Color.white, 24);
            if (stun > 0.3f) Game.I.hud.Popup(at + Vector3.up * 0.6f, "STUN", new Color(1f, 0.9f, 0.4f), 20);

            Fx.I.Burst(Center - away * radius * 0.6f, crit ? 14 : 8, SparkColor(toHp > 0.5f), 3f, 8f, away + Vector3.up * 0.5f, 0.6f, 0.3f, 12f, 0.12f);
            if (crit) Fx.I.Ring(new Vector3(transform.position.x, 0.06f, transform.position.z), radius, radius + 1.4f, new Color(1f, 0.7f, 0.2f, 0.8f), 0.25f);

            OnHit(from, damage);
            if (hp <= 0f)
            {
                Dead = true;
                if (bar != null) bar.gameObject.SetActive(false);
                Game.I.OnEnemyKilled(this);
                OnDeath(away);
            }
        }

        protected virtual Color SparkColor(bool flesh) { return flesh ? new Color(1f, 0.85f, 0.4f) : new Color(0.7f, 0.85f, 1f); }
        protected virtual void OnHit(Vector3 from, float damage) { }
        protected abstract void OnDeath(Vector3 away);

        // Call once per frame from the subclass.
        protected void BaseTick(float dt)
        {
            flash = Mathf.Max(0f, flash - dt / 0.18f);
            if (tint != null) tint.Set(Color.white, flash * 0.85f);

            if (knock.sqrMagnitude > 0.0001f)
            {
                transform.position += knock * dt;
                knock = Vector3.MoveTowards(knock, Vector3.zero, 26f * dt);
            }
            ClampToArena();
        }

        protected void ClampToArena()
        {
            Vector3 p = transform.position;
            float lim = Game.ArenaHalf - radius * 0.5f;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim, lim);
            transform.position = p;
        }

        protected void ShowBar(bool on) { if (bar != null && bar.gameObject.activeSelf != on) bar.gameObject.SetActive(on); }

        protected virtual void LateUpdate()
        {
            if (bar == null || !bar.gameObject.activeSelf || Game.I == null) return;
            bar.rotation = Game.I.cam.transform.rotation;
            float k = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            barFill.localScale = new Vector3(Mathf.Max(0.001f, barWidth * k), 0.13f, 1f);
            bool hasArmor = maxArmor > 0f && armor > 0.5f;
            barArmor.gameObject.SetActive(hasArmor);
            if (hasArmor) barArmor.localScale = new Vector3(Mathf.Max(0.001f, barWidth * Mathf.Clamp01(armor / maxArmor)), 0.07f, 1f);
        }

        protected static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        protected void TurnTowards(Vector3 dir, float degPerSec, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * dt);
        }
    }
}
