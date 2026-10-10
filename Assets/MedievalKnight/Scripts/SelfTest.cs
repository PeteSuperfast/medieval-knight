using System.Collections;
using System.IO;
using UnityEngine;

namespace MedievalKnight
{
    // Automated smoke test, only active when the game is started with:
    //     MedievalKnight.exe -selftest <output folder>
    // It plays every attack against the dummy, exercises block / damage / stamina, saves
    // screenshots and logs the bar values, then quits. Normal play never runs this.
    public class SelfTest : MonoBehaviour
    {
        string dir;
        int shot;

        public static void StartIfRequested(GameObject host)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-selftest") continue;
                host.AddComponent<SelfTest>().dir = args[i + 1];
                return;
            }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            Knight k = Game.I.knight;
            Dummy d = Game.I.dummy;
            yield return new WaitForSeconds(1.0f);
            Shot("idle");
            yield return null;

            for (int i = 0; i < Knight.Attacks.Length; i++)
            {
                AttackDef a = Knight.Attacks[i];
                k.transform.position = new Vector3(d.transform.position.x - 1.9f, 0f, 0f);
                k.stamina = Knight.MaxStamina;
                float s0 = k.stamina, hp0 = d.hp, ar0 = d.armor;
                yield return new WaitForSeconds(0.3f);
                k.DebugAttack(i);
                yield return new WaitForSeconds(a.windupTime * 0.9f);
                Shot("attack" + (i + 1) + "_windup");
                yield return new WaitForSeconds(a.windupTime * 0.1f + a.strikeTime * 0.55f);
                Shot("attack" + (i + 1) + "_strike");
                yield return new WaitForSeconds(a.strikeTime * 0.45f + 0.03f);
                Shot("attack" + (i + 1) + "_after");
                yield return new WaitForSeconds(a.recoverTime + 0.3f);
                Debug.Log("SELFTEST attack " + (i + 1) + " " + a.name + ": stamina " + s0 + " -> " + k.stamina.ToString("0.0")
                    + " (cost " + a.stamina + "), dummy armor " + ar0.ToString("0.0") + " -> " + d.armor.ToString("0.0")
                    + ", dummy hp " + hp0.ToString("0.0") + " -> " + d.hp.ToString("0.0") + ", hits " + d.hits);
            }

#if ENABLE_INPUT_SYSTEM
            // --- real keyboard path: synthetic key events through the Input System ---
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) Debug.Log("SELFTEST input: no keyboard device, key tests skipped");
            else
            {
                k.ResetAll(); d.ResetAll();
                yield return new WaitForSeconds(0.2f);

                float x0 = k.transform.position.x;
                Keys(kb, UnityEngine.InputSystem.Key.D);
                yield return new WaitForSeconds(0.4f);
                Keys(kb);
                Debug.Log("SELFTEST input D held 0.4s: x " + x0.ToString("0.00") + " -> " + k.transform.position.x.ToString("0.00"));

                k.transform.position = new Vector3(d.transform.position.x - 1.9f, 0f, 0f);
                int h0 = d.hits;
                Keys(kb, UnityEngine.InputSystem.Key.Digit3);
                yield return null; yield return null; yield return null;
                Debug.Log("SELFTEST input key 3: attacking=" + k.IsAttacking);
                Keys(kb);
                yield return new WaitForSeconds(0.7f);
                Keys(kb, UnityEngine.InputSystem.Key.J);
                yield return null; yield return null; yield return null;
                Debug.Log("SELFTEST input key J: attacking=" + k.IsAttacking + ", dummy hits " + h0 + " -> " + d.hits);
                Keys(kb);
                yield return new WaitForSeconds(0.7f);
                Debug.Log("SELFTEST input after 3 and J: dummy hits " + h0 + " -> " + d.hits);

                // shield block
                k.stamina = Knight.MaxStamina;
                Keys(kb, UnityEngine.InputSystem.Key.LeftShift);
                yield return new WaitForSeconds(0.3f);
                float ba = k.armor, bh = k.hp, bs = k.stamina;
                k.TakeDamage(20f, k.transform.position.x + k.Facing);
                Debug.Log("SELFTEST block: blocking=" + k.IsBlocking + " armor " + ba.ToString("0.0") + " -> " + k.armor.ToString("0.0")
                    + ", hp " + bh.ToString("0.0") + " -> " + k.hp.ToString("0.0") + ", stamina " + bs.ToString("0.0") + " -> " + k.stamina.ToString("0.0"));
                yield return new WaitForSeconds(0.1f);
                Shot("block");
                yield return new WaitForSeconds(0.2f);
                Keys(kb);
                yield return new WaitForSeconds(0.2f);

                // dummy counter-attack toggle
                Keys(kb, UnityEngine.InputSystem.Key.T);
                yield return null; yield return null; yield return null;
                Keys(kb);
                k.transform.position = new Vector3(d.transform.position.x - 1.9f, 0f, 0f);
                k.stamina = Knight.MaxStamina;
                float ca = k.armor, ch = k.hp;
                k.DebugAttack(0);
                yield return new WaitForSeconds(0.85f);
                Shot("counter_telegraph");
                yield return new WaitForSeconds(0.7f);
                Debug.Log("SELFTEST counter: mode=" + Game.I.counterMode + " knight armor " + ca.ToString("0.0") + " -> " + k.armor.ToString("0.0")
                    + ", hp " + ch.ToString("0.0") + " -> " + k.hp.ToString("0.0"));
                Keys(kb, UnityEngine.InputSystem.Key.T);
                yield return null; yield return null; yield return null;
                Keys(kb);
                Debug.Log("SELFTEST counter mode after second T: " + Game.I.counterMode);
                k.ResetAll(); d.ResetAll();
                yield return new WaitForSeconds(0.2f);
            }
#endif
            // stamina refusal
            k.stamina = 5f;
            k.DebugAttack(3);
            yield return new WaitForSeconds(0.1f);
            Shot("no_stamina");
            Debug.Log("SELFTEST low stamina: attacking=" + k.IsAttacking + " stamina=" + k.stamina.ToString("0.0"));
            yield return new WaitForSeconds(2.0f);
            Debug.Log("SELFTEST stamina regen after 2.1s: " + k.stamina.ToString("0.0"));

            // unblocked hits: armor first, then health
            for (int i = 0; i < 4; i++)
            {
                float a0 = k.armor, h0 = k.hp;
                k.TakeDamage(20f, k.transform.position.x + k.Facing);
                Debug.Log("SELFTEST knight hit 20: armor " + a0.ToString("0.0") + " -> " + k.armor.ToString("0.0")
                    + ", hp " + h0.ToString("0.0") + " -> " + k.hp.ToString("0.0"));
                yield return new WaitForSeconds(0.12f);
                if (i == 3) Shot("knight_hurt");
                yield return new WaitForSeconds(0.25f);
            }

            // break the dummy
            int guard = 0;
            while (!d.Broken && guard++ < 20)
            {
                k.transform.position = new Vector3(d.transform.position.x - 1.9f, 0f, 0f);
                k.stamina = Knight.MaxStamina;
                k.DebugAttack(1);
                yield return new WaitForSeconds(0.75f);
            }
            Debug.Log("SELFTEST dummy broken=" + d.Broken + " after " + d.hits + " hits, total damage " + d.totalDamage);
            Shot("dummy_broken");
            yield return new WaitForSeconds(2.4f);
            Debug.Log("SELFTEST dummy respawned: broken=" + d.Broken + " hp=" + d.hp + " armor=" + d.armor);
            Shot("end");
            yield return new WaitForSeconds(0.3f);
            Debug.Log("SELFTEST done, screenshots=" + shot);
            Application.Quit();
        }

#if ENABLE_INPUT_SYSTEM
        // Replaces the whole keyboard state: the listed keys are down, everything else is up.
        static void Keys(UnityEngine.InputSystem.Keyboard kb, params UnityEngine.InputSystem.Key[] keys)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
        }
#endif

        void Shot(string label)
        {
            shot++;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, shot.ToString("00") + "_" + label + ".png"));
        }
    }
}
