using UnityEngine;

namespace MedievalKnight
{
    // Smooth motion for something that another machine simulates (a remote knight, a client's copy of an enemy).
    // Samples arrive 15-20 times a second stamped with the sender's clock. The object is drawn a little in the
    // past ("delay"), between the two samples around that moment, so it glides instead of jumping.
    // The sender's clock is mapped to the local one by the freshest sample seen so far (offset), which slowly
    // relaxes so a clock that drifts or a connection that gets slower is followed.
    public class NetInterp
    {
        const int Size = 8;
        const float MaxExtrapolate = 0.06f, TeleportDistance = 6f;

        readonly float[] time = new float[Size];
        readonly Vector3[] pos = new Vector3[Size];
        readonly float[] yaw = new float[Size];
        int count;
        float offset;                   // sender time - local time
        bool hasOffset;

        public float delay = 0.1f;
        public Vector3 Latest { get { return count > 0 ? pos[count - 1] : Vector3.zero; } }
        public bool Empty { get { return count == 0; } }

        // Forget the history and stand at one place (spawn, teleport).
        public void Snap(Vector3 p, float y)
        {
            count = 0;
            hasOffset = false;
            pos[0] = p; yaw[0] = y; time[0] = 0f;
            snapPos = p; snapYaw = y; hasSnap = true;
        }

        Vector3 snapPos; float snapYaw; bool hasSnap;

        public void Push(float senderTime, float localNow, Vector3 p, float y)
        {
            if (count > 0 && senderTime <= time[count - 1]) return;      // the same or an older sample again
            float o = senderTime - localNow;
            if (!hasOffset) { offset = o; hasOffset = true; }
            else offset = Mathf.Max(offset - 0.0004f, o);
            if (count == Size)
            {
                for (int i = 1; i < Size; i++) { time[i - 1] = time[i]; pos[i - 1] = pos[i]; yaw[i - 1] = yaw[i]; }
                count--;
            }
            time[count] = senderTime; pos[count] = p; yaw[count] = y;
            count++;
            hasSnap = false;
        }

        // Where to draw it now. False: nothing known yet.
        public bool Sample(float localNow, out Vector3 p, out float y)
        {
            if (count == 0)
            {
                p = snapPos; y = snapYaw;
                return hasSnap;
            }
            float t = localNow + offset - delay;
            if (t <= time[0]) { p = pos[0]; y = yaw[0]; return true; }
            for (int i = count - 1; i > 0; i--)
            {
                if (t < time[i - 1]) continue;
                if (t >= time[i]) break;
                float span = time[i] - time[i - 1];
                float k = span > 0.0001f ? (t - time[i - 1]) / span : 1f;
                if ((pos[i] - pos[i - 1]).sqrMagnitude > TeleportDistance * TeleportDistance) k = 1f;   // a warp is not walked
                p = Vector3.Lerp(pos[i - 1], pos[i], k);
                y = Mathf.LerpAngle(yaw[i - 1], yaw[i], k);
                return true;
            }
            // newer than the newest sample: carry on for a moment at the last speed, then wait
            int n = count - 1;
            p = pos[n]; y = yaw[n];
            if (n > 0)
            {
                float span = time[n] - time[n - 1];
                Vector3 step = pos[n] - pos[n - 1];
                if (span > 0.0001f && step.sqrMagnitude < TeleportDistance * TeleportDistance)
                    p += step / span * Mathf.Min(t - time[n], MaxExtrapolate);
            }
            return true;
        }
    }
}
