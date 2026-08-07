using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public static class FormationArea
    {
        public static bool Bounds(IList<Vector2> area, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            if (area == null || area.Count == 0) return false;
            foreach (var p in area)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return true;
        }

        public static Vector2 Centroid(IList<Vector2> area)
        {
            if (area == null || area.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var p in area) sum += p;
            return sum / area.Count;
        }

        public static float Area(IList<Vector2> area)
        {
            if (area == null || area.Count < 3) return 0f;
            float sum = 0f;
            int j = area.Count - 1;
            for (int i = 0; i < area.Count; i++)
            {
                sum += area[j].x * area[i].y - area[i].x * area[j].y;
                j = i;
            }
            return Mathf.Abs(sum) * 0.5f;
        }

        public static float SignedDistance(IList<Vector2> area, Vector2 p)
        {
            bool inside = false;
            float md = float.MaxValue;
            int count = area.Count;
            int j = count - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = area[i];
                Vector2 b = area[j];
                if (((a.y > p.y) != (b.y > p.y)) &&
                    (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
                    inside = !inside;
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(Vector2.Dot(ab, ab), 1e-6f));
                Vector2 d = p - (a + ab * t);
                md = Mathf.Min(md, d.sqrMagnitude);
                j = i;
            }
            float dist = Mathf.Sqrt(md);
            return inside ? dist : -dist;
        }

        public static Vector2 PeakPoint(IList<Vector2> area, out float maxDistance)
        {
            maxDistance = 0f;
            Vector2 best = Centroid(area);
            if (area == null || area.Count < 3) return best;
            if (!Bounds(area, out Vector2 min, out Vector2 max)) return best;

            const int steps = 28;
            Vector2 span = max - min;
            for (int yi = 0; yi <= steps; yi++)
            {
                for (int xi = 0; xi <= steps; xi++)
                {
                    Vector2 p = new Vector2(
                        min.x + span.x * xi / steps,
                        min.y + span.y * yi / steps);
                    float d = SignedDistance(area, p);
                    if (d > maxDistance)
                    {
                        maxDistance = d;
                        best = p;
                    }
                }
            }
            return best;
        }

        public static int ComputeHash(Formation f, Vector3 terrainPos, Vector3 terrainSize, int res)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + terrainPos.GetHashCode();
                h = h * 31 + terrainSize.GetHashCode();
                h = h * 31 + res;
                h = h * 31 + (f.enabled ? 1 : 0);
                h = h * 31 + (int)f.blendMode;
                h = h * 31 + f.edgeFalloff.GetHashCode();
                h = h * 31 + f.height.GetHashCode();
                h = h * 31 + f.baseHeight.GetHashCode();
                h = h * 31 + f.domeReach.GetHashCode();
                h = h * 31 + CurveHash(f.domeProfile);
                h = h * 31 + (int)f.noiseType;
                h = h * 31 + f.noiseHeight.GetHashCode();
                h = h * 31 + f.noiseScale.GetHashCode();
                h = h * 31 + f.octaves;
                h = h * 31 + f.lacunarity.GetHashCode();
                h = h * 31 + f.gain.GetHashCode();
                h = h * 31 + f.warp.GetHashCode();
                h = h * 31 + f.seed;
                h = h * 31 + f.noiseFollowsDome.GetHashCode();
                h = h * 31 + f.smooth.GetHashCode();
                h = h * 31 + f.smoothIterations;
                h = h * 31 + (f.thermalEnabled ? 1 : 0);
                h = h * 31 + f.thermalIterations;
                h = h * 31 + f.thermalRepose.GetHashCode();
                h = h * 31 + f.thermalStrength.GetHashCode();
                h = h * 31 + (f.hydraulicEnabled ? 1 : 0);
                h = h * 31 + f.hydraulicIterations;
                h = h * 31 + f.rain.GetHashCode();
                h = h * 31 + f.dropletInertia.GetHashCode();
                h = h * 31 + f.evaporation.GetHashCode();
                h = h * 31 + f.sedimentCapacity.GetHashCode();
                h = h * 31 + f.erosionRate.GetHashCode();
                h = h * 31 + f.depositionRate.GetHashCode();
                h = h * 31 + f.erosionRadius;
                h = h * 31 + (f.snowEnabled ? 1 : 0);
                h = h * 31 + f.snowAmount.GetHashCode();
                h = h * 31 + f.snowLineLow.GetHashCode();
                h = h * 31 + f.snowLineHigh.GetHashCode();
                h = h * 31 + f.snowSlopeStart.GetHashCode();
                h = h * 31 + f.snowSlopeFull.GetHashCode();
                h = h * 31 + f.snowCrevice.GetHashCode();
                h = h * 31 + f.snowSettleIterations;
                h = h * 31 + f.snowAddsHeight.GetHashCode();
                h = h * 31 + f.rockStrength.GetHashCode();
                if (f.area != null)
                    foreach (var p in f.area)
                        h = h * 31 + p.GetHashCode();
                return h;
            }
        }

        static int CurveHash(AnimationCurve curve)
        {
            if (curve == null) return 0;
            unchecked
            {
                int h = 17;
                foreach (var k in curve.keys)
                {
                    h = h * 31 + k.time.GetHashCode();
                    h = h * 31 + k.value.GetHashCode();
                    h = h * 31 + k.inTangent.GetHashCode();
                    h = h * 31 + k.outTangent.GetHashCode();
                }
                return h;
            }
        }
    }

}
