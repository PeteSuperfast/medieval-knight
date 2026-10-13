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
        public int soulReward;                   // Soul points for every living knight when it dies
        public bool solid;                       // the knight cannot walk through it
        public bool Dead { get; protected set; }
        public int netId;                        // the same number on every machine in an online game
        public bool puppet;                      // on a client: no AI, it only shows what the host reports
        public readonly NetInterp interp = new NetInterp();
        protected float puppetSpeed;             // how fast the puppet is being moved (for walk animations)

        // read-out for the HUD / self-test
        public int hits;
        public float totalDamage, lastDamage, lastToArmor, lastToHp;
        public bool lastCrit;
        public int lastNumberKind;               // 0 plain, 1 armor soaked it all, 2 heavy, 3 crit, 4 crit + heavy (read-out colour)
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

        protected virtual void OnEnable()
        {
            All.Add(this);
            if (netId == 0 && Net.Authority && Game.I != null) netId = Game.I.NewNetId();
        }

        protected virtual void OnDisable() { All.Remove(this); }

        public static Enemy Find(int netId)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].netId == netId) return All[i];
            return null;
        }

        // ---------------------------------------------------------------- online: host -> client state

        // 0 / 1 / 2 = green / blue / red slime, 3 = Minotaur, 4 = straw dummy (tells a client which puppet to build)
        public abstract int NetType { get; }

        // Host: this enemy's part of a snapshot.
        public void WriteNet(NetW w)
        {
            Vector3 p = transform.position;
            w.Pos(p.x).Pos(p.z).Yaw(transform.eulerAngles.y).F32(hp).F32(maxHp).F32(armor).F32(maxArmor).U8((Dead ? 1 : 0) | (Stunned ? 2 : 0));
            WriteExtra(w);
        }

        // Client: the same fields, applied to the puppet. first = it was created for this snapshot.
        public void ReadNet(NetR r, float hostTime, bool first)
        {
            float x = r.Pos(), z = r.Pos(), yaw = r.Yaw();
            hp = r.F32(); maxHp = r.F32(); armor = r.F32(); maxArmor = r.F32();
            int flags = r.U8();
            var p = new Vector3(x, 0f, z);
            if (first)
            {
                interp.delay = Net.EnemyDelay;
                interp.Snap(p, yaw);
                transform.position = p;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            interp.Push(hostTime, Time.unscaledTime, p, yaw);
            stunUntil = (flags & 2) != 0 ? Time.time + 0.25f : 0f;
            OnNetDead((flags & 1) != 0);
            ReadExtra(r);
        }

        protected virtual void WriteExtra(NetW w) { }
        protected virtual void ReadExtra(NetR r) { }
        protected virtual void OnNetDead(bool dead) { }

        // A client's puppet: glide to where the host says it is.
        protected void PuppetMove()
        {
            Vector3 p; float y;
            if (!interp.Sample(Time.unscaledTime, out p, out y)) return;
            Vector3 d = p - transform.position; d.y = 0f;
            float dt = Time.deltaTime;
            if (dt > 0f) puppetSpeed = Mathf.Lerp(puppetSpeed, d.magnitude / dt, 0.3f);
            transform.position = new Vector3(p.x, 0f, p.z);
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        // The knight this enemy goes for: the nearest living one.
        protected Knight Target() { return Game.I.NearestKnight(transform.position); }

        // How a blow would be shared between armor and health (nothing is changed).
        public void Split(float damage, float pierce, out float toArmor, out float toHp)
        {
            float direct = damage * Mathf.Clamp01(pierce);
            float rest = damage - direct;
            toArmor = Mathf.Min(armor, rest);
            toHp = direct + rest - toArmor;
        }

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
        // Only where the world is simulated (single player / host). "by" = the knight who struck (null: nobody, e.g. a test).
        public virtual void TakeHit(float damage, float pierce, Vector3 from, bool crit, float knockback, float stun, string source, Knight by = null, bool heavy = false)
        {
            if (Dead || puppet) return;
            float toArmor, toHp;
            Split(damage, pierce, out toArmor, out toHp);
            armor -= toArmor;
            hp = Mathf.Max(0f, hp - toHp);

            Vector3 away = transform.position - from; away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
            knock += away * knockback * knockResist;
            if (stun > 0f) stunUntil = Mathf.Max(stunUntil, Time.time + stun * stunResist);

            bool killed = hp <= 0f;
            if (Net.IsHost) Net.BroadcastEnemyHit(this, damage, toArmor, toHp, crit, stun > 0.3f, killed, away, by != null ? by.pid : 255, heavy);
            ShowHit(damage, toArmor, toHp, crit, heavy, stun > 0.3f, away, source, by != null && by.remote);

            OnHit(from, damage);
            if (killed)
            {
                Dead = true;
                if (bar != null) bar.gameObject.SetActive(false);
                Game.I.OnEnemyKilled(this, by);
                OnDeath(away);
            }
        }

        // A client shows a hit that the host applied (numbers, sparks, wobble, and death when it was the last one).
        public void NetHit(float damage, float toArmor, float toHp, bool crit, bool stunned, bool killed, Vector3 away, bool byOther, float newHp, float newArmor, bool heavy = false)
        {
            if (Dead) return;
            hp = newHp; armor = newArmor;
            if (away.sqrMagnitude < 0.0001f) away = transform.forward;
            ShowHit(damage, toArmor, toHp, crit, heavy, stunned, away, "Hit", byOther);
            OnHit(transform.position - away, damage);
            if (killed)
            {
                Dead = true;
                if (bar != null) bar.gameObject.SetActive(false);
                Game.I.OnPuppetKilled(this);
                OnDeath(away);
            }
        }

        // The read-out and everything visible / audible about one hit. otherPlayer: it was not the local knight's
        // blow, so its sound is played here (quieter); the local knight's own hit sound comes from Knight.HitArea.
        void ShowHit(float damage, float toArmor, float toHp, bool crit, bool heavy, bool stunned, Vector3 away, string source, bool otherPlayer)
        {
            hits++;
            totalDamage += damage;
            lastDamage = damage;
            lastToArmor = toArmor;
            lastToHp = toHp;
            lastCrit = crit;
            lastHit = source + " " + Mathf.RoundToInt(damage) + (crit ? " CRIT" : "");
            flash = 1f;

            Vector3 at = Center + new Vector3(Random.Range(-0.3f, 0.3f), height * 0.5f, 0f);
            Color nc; int ns;
            lastNumberKind = NumberStyle(crit, heavy, toHp, out nc, out ns);
            Game.I.hud.Popup(at, Mathf.RoundToInt(damage) + (crit ? "!" : ""), nc, ns);
            if (stunned) Game.I.hud.Popup(at + Vector3.up * 0.6f, "STUN", new Color(1f, 0.9f, 0.4f), 20);

            Fx.I.Burst(Center - away * radius * 0.6f, crit ? 14 : 8, SparkColor(toHp > 0.5f), 3f, 8f, away + Vector3.up * 0.5f, 0.6f, 0.3f, 12f, 0.12f);
            if (crit) Fx.I.Ring(new Vector3(transform.position.x, 0.06f, transform.position.z), radius, radius + 1.4f, new Color(1f, 0.7f, 0.2f, 0.8f), 0.25f);

            if (otherPlayer) Sfx.I.Play(toArmor > toHp ? ArmorSound : Snd.Slice, Game.I.RemoteVolume(transform.position));
        }

        // Colour + size of a damage number. Order: crit > heavy > armor soaked it all > plain. Returns the kind (lastNumberKind).
        public static int NumberStyle(bool crit, bool heavy, float toHp, out Color color, out int size)
        {
            if (crit) { color = new Color(1f, 0.62f, 0.1f); size = heavy ? 38 : 34; return heavy ? 4 : 3; }
            if (heavy) { color = new Color(1f, 0.31f, 0.25f); size = 36; return 2; }
            if (toHp < 0.5f) { color = new Color(0.65f, 0.82f, 1f); size = 22; return 1; }
            color = Color.white; size = 24; return 0;
        }

        protected virtual Color SparkColor(bool flesh) { return flesh ? new Color(1f, 0.85f, 0.4f) : new Color(0.7f, 0.85f, 1f); }
        // The sound of a sword blow that this enemy's armor soaked up (the Minotaur's is metal on metal).
        public virtual Snd ArmorSound { get { return Snd.ArmorImpact; } }
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
            if (!puppet) p = Obstacles.Push(p, Mathf.Max(0.35f, radius * 0.6f), Obstacles.Enemies);
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
