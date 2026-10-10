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
        public static float MoveX()
        {
            float x = 0;
            if (Held(KeyCode.A) || Held(KeyCode.LeftArrow)) x -= 1;
            if (Held(KeyCode.D) || Held(KeyCode.RightArrow)) x += 1;
            return x;
        }

#if ENABLE_INPUT_SYSTEM
        static Key Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.A: return Key.A;
                case KeyCode.D: return Key.D;
                case KeyCode.W: return Key.W;
                case KeyCode.H: return Key.H;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.R: return Key.R;
                case KeyCode.T: return Key.T;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.F1: return Key.F1;
                case KeyCode.Space: return Key.Space;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
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
#else
        public static bool Down(KeyCode k) { return Input.GetKeyDown(k); }
        public static bool Held(KeyCode k) { return Input.GetKey(k); }
#endif
    }
}
