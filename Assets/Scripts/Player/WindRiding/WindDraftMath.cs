using UnityEngine;

namespace Category5.Player.WindRiding
{
    // pure math helpers for wind drafts, kept separate so they can be unit tested in edit mode
    public static class WindDraftMath
    {
        // smoothstep falloff at both ends of the cylinder
        // returns 0 at t=0 and t=1, 1 in the middle (outside the falloff bands)
        // band is the fraction of length (each end) over which strength eases 0 -> 1 -> 0
        // if invert is true, full strength at t=0, tapering to 0 at t=1 (useful for vertical updrafts)
        public static float FalloffStrength(float t, float band, bool invert = false)
        {
            if (band <= 0.001f) return 1f;

            float b = Mathf.Clamp(band, 0.001f, 0.5f);

            if (invert)
            {
                // full at entry (t=0), taper to 0 at exit (t=1)
                float exit = Mathf.Clamp01((1f - t) / b);
                return exit * exit * (3f - 2f * exit);
            }

            float entry = Mathf.Clamp01(t / b);
            float exitBand = Mathf.Clamp01((1f - t) / b);

            float entrySmooth = entry * entry * (3f - 2f * entry);
            float exitSmooth = exitBand * exitBand * (3f - 2f * exitBand);

            return Mathf.Min(entrySmooth, exitSmooth);
        }

        // strength-weighted normalized blend of a set of directions
        // returns Vector3.zero if total weight is negligible
        public static Vector3 BlendDirections(Vector3[] directions, float[] weights)
        {
            if (directions == null || weights == null || directions.Length == 0) return Vector3.zero;
            int n = Mathf.Min(directions.Length, weights.Length);
            Vector3 sum = Vector3.zero;
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                sum += directions[i] * weights[i];
                total += weights[i];
            }
            if (total <= 0.0001f) return Vector3.zero;
            return (sum / total).normalized;
        }

        // the tightest (minimum) speed cap among contributions; returns float.MaxValue if empty
        public static float TightestCap(float[] caps)
        {
            if (caps == null || caps.Length == 0) return float.MaxValue;
            float min = float.MaxValue;
            for (int i = 0; i < caps.Length; i++)
            {
                if (caps[i] < min) min = caps[i];
            }
            return min;
        }
    }
}
