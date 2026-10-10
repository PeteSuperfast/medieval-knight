using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MedievalKnight
{
    // Thin wrapper so the game runs with either the new Input System or the legacy Input Manager.
    // Any key the game reads must be listed in Map(), otherwise it always reads as "not pressed".
    public static class InputX
    {
        // WASD / arrows as a direction on the ground plane (x = right, y = up the screen = world +Z).
        public static Vector2 Move()
        {
            float x = 0, y = 0;
            if (Held(KeyCode.A) || Held(KeyCode.LeftArrow)) x -= 1;
            if (Held(KeyCode.D) || Held(KeyCode.RightArrow)) x += 1;
            if (Held(KeyCode.S) || Held(KeyCode.DownArrow)) y -= 1;
            if (Held(KeyCode.W) || Held(KeyCode.UpArrow)) y += 1;
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

#if ENABLE_INPUT_SYSTEM
        static Key Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.A: return Key.A;
                case KeyCode.D: return Key.D;
                case KeyCode.W: return Key.W;
                case KeyCode.S: return Key.S;
                case KeyCode.Q: return Key.Q;
                case KeyCode.E: return Key.E;
                case KeyCode.R: return Key.R;
                case KeyCode.F: return Key.F;
                case KeyCode.H: return Key.H;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.I: return Key.I;
                case KeyCode.M: return Key.M;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.F1: return Key.F1;
                case KeyCode.F5: return Key.F5;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
            }
            return Key.None;
        }

        public static bool Down(KeyCode k)
        {
            var kb = Keyboard.current;
            var key = Map(k);
            return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
        }

        public static bool Held(KeyCode k)
        {
            var kb = Keyboard.current;
            var key = Map(k);
            return kb != null && key != Key.None && kb[key].isPressed;
        }

        // button: 0 = left, 1 = right
        public static bool MouseDown(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
        }

        public static bool MouseHeld(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.isPressed : m.rightButton.isPressed;
        }

        public static bool HasMouse { get { return Mouse.current != null; } }

        public static Vector2 MousePos
        {
            get
            {
                var m = Mouse.current;
                return m != null ? m.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }
#else
        public static bool Down(KeyCode k) { return Input.GetKeyDown(k); }
        public static bool Held(KeyCode k) { return Input.GetKey(k); }
        public static bool MouseDown(int button) { return Input.GetMouseButtonDown(button); }
        public static bool MouseHeld(int button) { return Input.GetMouseButton(button); }
        public static bool HasMouse { get { return true; } }
        public static Vector2 MousePos { get { return Input.mousePosition; } }
#endif
    }
}
