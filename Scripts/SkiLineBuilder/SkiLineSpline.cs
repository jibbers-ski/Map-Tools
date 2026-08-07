using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class SkiLineBake
    {
        public Vector3[] pos;
        public float[] cumDist;
        public float[] halfWidth;
        public float[] roll;
        public float[] nodeParam;
        public float totalLength;
        public int Count => pos.Length;
    }

    public static class SkiLineSpline
    {
        const int DensePerSegment = 48;

        public static Vector3 GetPoint(IList<SkiLineNode> nodes, int seg, float u)
        {
            Vector3 p1 = nodes[seg].position;
            Vector3 p2 = nodes[seg + 1].position;
            Vector3 p0 = seg > 0 ? nodes[seg - 1].position : p1 * 2f - p2;
            Vector3 p3 = seg + 2 < nodes.Count ? nodes[seg + 2].position : p2 * 2f - p1;
            return CatmullRom(p0, p1, p2, p3, u);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float t0 = 0f;
            float t1 = t0 + Knot(p0, p1);
            float t2 = t1 + Knot(p1, p2);
            float t3 = t2 + Knot(p2, p3);
            float t = Mathf.Lerp(t1, t2, u);
            Vector3 a1 = Remap(p0, p1, t0, t1, t);
            Vector3 a2 = Remap(p1, p2, t1, t2, t);
            Vector3 a3 = Remap(p2, p3, t2, t3, t);
            Vector3 b1 = Remap(a1, a2, t0, t2, t);
            Vector3 b2 = Remap(a2, a3, t1, t3, t);
            return Remap(b1, b2, t1, t2, t);
        }

        static float Knot(Vector3 a, Vector3 b)
            => Mathf.Max(Mathf.Sqrt(Vector3.Distance(a, b)), 1e-4f);

        static Vector3 Remap(Vector3 a, Vector3 b, float ta, float tb, float t)
            => tb - ta > 1e-6f ? Vector3.LerpUnclamped(a, b, (t - ta) / (tb - ta)) : a;

        public static SkiLineBake Bake(SkiLine line, int samples)
        {
            var nodes = line.nodes;
            if (nodes == null || nodes.Count < 2) return null;

            int segs = nodes.Count - 1;
            int denseCount = segs * DensePerSegment + 1;
            var densePos = new Vector3[denseCount];
            var denseParam = new float[denseCount];
            for (int s = 0; s < segs; s++)
            {
                for (int i = 0; i < DensePerSegment; i++)
                {
                    int idx = s * DensePerSegment + i;
                    float u = (float)i / DensePerSegment;
                    densePos[idx] = GetPoint(nodes, s, u);
                    denseParam[idx] = s + u;
                }
            }
            densePos[denseCount - 1] = nodes[segs].position;
            denseParam[denseCount - 1] = segs;

            var denseCum = new float[denseCount];
            for (int i = 1; i < denseCount; i++)
                denseCum[i] = denseCum[i - 1] + Vector3.Distance(densePos[i - 1], densePos[i]);
            float total = denseCum[denseCount - 1];
            if (total < 1f) return null;

            samples = Mathf.Clamp(samples, 16, 4096);
            var bake = new SkiLineBake
            {
                pos = new Vector3[samples],
                cumDist = new float[samples],
                halfWidth = new float[samples],
                roll = new float[samples],
                nodeParam = new float[samples],
                totalLength = total,
            };

            int cursor = 0;
            for (int i = 0; i < samples; i++)
            {
                float d = total * i / (samples - 1);
                while (cursor < denseCount - 2 && denseCum[cursor + 1] < d) cursor++;
                float span = denseCum[cursor + 1] - denseCum[cursor];
                float u = span > 1e-6f ? (d - denseCum[cursor]) / span : 0f;

                float np = Mathf.Lerp(denseParam[cursor], denseParam[cursor + 1], u);
                int seg = Mathf.Clamp((int)np, 0, segs - 1);
                float su = Mathf.Clamp01(np - seg);

                Vector3 p = GetPoint(nodes, seg, su);

                var n0 = nodes[seg];
                var n1 = nodes[seg + 1];
                float w0 = n0.widthOverride > 0 ? n0.widthOverride : line.width;
                float w1 = n1.widthOverride > 0 ? n1.widthOverride : line.width;
                float sm = su * su * (3f - 2f * su);

                float baseHalfW = Mathf.Max(Mathf.Lerp(w0, w1, sm), 0.1f) * 0.5f;

                bake.pos[i] = p;
                bake.cumDist[i] = d;
                bake.halfWidth[i] = FeatureHalfWidth(line, d, baseHalfW);
                bake.roll[i] = Mathf.Lerp(n0.roll, n1.roll, sm);
                bake.nodeParam[i] = np;
            }

            if (Mathf.Abs(line.autoBank) > 0.01f && samples >= 3)
            {
                var bank = new float[samples];
                float ds = Mathf.Max(total / (samples - 1), 1e-4f);
                for (int i = 1; i < samples - 1; i++)
                {
                    Vector3 t0 = bake.pos[i] - bake.pos[i - 1];
                    Vector3 t1 = bake.pos[i + 1] - bake.pos[i];
                    var a = new Vector2(t0.x, t0.z);
                    var b = new Vector2(t1.x, t1.z);
                    if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) continue;
                    a.Normalize();
                    b.Normalize();
                    float turn = Mathf.Atan2(a.x * b.y - a.y * b.x, Mathf.Clamp(Vector2.Dot(a, b), -1f, 1f));
                    bank[i] = Mathf.Clamp(-line.autoBank * 20f * turn / ds, -60f, 60f);
                }

                int w = Mathf.Clamp(Mathf.RoundToInt(3f / ds), 1, 64);
                var sm2 = new float[samples];
                for (int p2 = 0; p2 < 2; p2++)
                {
                    for (int i = 0; i < samples; i++)
                    {
                        float sum = 0f;
                        int n0i = Mathf.Max(0, i - w), n1i = Mathf.Min(samples - 1, i + w);
                        for (int j = n0i; j <= n1i; j++) sum += bank[j];
                        sm2[i] = sum / (n1i - n0i + 1);
                    }
                    var tmp = bank; bank = sm2; sm2 = tmp;
                }

                for (int i = 0; i < samples; i++)
                    bake.roll[i] = Mathf.Clamp(bake.roll[i] + bank[i], -75f, 75f);
            }
            return bake;
        }

        public static float FeatureOffsetAt(SkiLine line, float d, float lateralMeters)
        {
            var features = line.features;
            if (features == null) return 0f;
            float h = 0f;
            for (int i = 0; i < features.Count; i++)
            {
                var f = features[i];
                if (f == null || !f.enabled || f.length <= 0.01f) continue;
                if (f.profile == null || f.profile.length == 0) continue;
                float t = (d - f.start) / f.length;
                if (t < 0f || t > 1f) continue;
                float v = f.profile.Evaluate(t) * f.height;
                if (f.width > 0.01f)
                {
                    float halfW = f.width * 0.5f;
                    float rel = Mathf.Abs(lateralMeters - f.lateralOffset);
                    if (rel >= halfW) continue;
                    float blend = Mathf.Max(f.sideBlend, 0.01f) * halfW;
                    float e = Mathf.Clamp01((halfW - rel) / blend);
                    e = e * e * (3f - 2f * e);
                    v *= e;
                }
                h += v;
            }
            return h;
        }

        public static float FeatureHalfWidth(SkiLine line, float d, float baseHalfW)
        {
            var features = line.features;
            if (features == null) return baseHalfW;
            float halfW = baseHalfW;
            for (int i = 0; i < features.Count; i++)
            {
                var f = features[i];
                if (f == null || !f.enabled || f.length <= 0.01f || f.width <= 0.01f) continue;
                float t = (d - f.start) / f.length;
                if (t < 0f || t > 1f) continue;
                float candidate = Mathf.Abs(f.lateralOffset) + f.width * 0.5f;
                if (candidate <= baseHalfW) continue;
                float e = Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.25f);
                e = e * e * (3f - 2f * e);
                halfW = Mathf.Max(halfW, Mathf.Lerp(baseHalfW, candidate, e));
            }
            return halfW;
        }

        public static int IndexAtDistance(SkiLineBake bake, float d)
        {
            int lo = 0, hi = bake.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (bake.cumDist[mid] < d) lo = mid + 1;
                else hi = mid;
            }
            return Mathf.Clamp(lo, 0, bake.Count - 1);
        }

        public static Vector3 PointAtDistance(SkiLineBake bake, float d)
        {
            d = Mathf.Clamp(d, 0f, bake.totalLength);
            int i = Mathf.Clamp(IndexAtDistance(bake, d), 1, bake.Count - 1);
            float span = bake.cumDist[i] - bake.cumDist[i - 1];
            float u = span > 1e-6f ? (d - bake.cumDist[i - 1]) / span : 0f;
            return Vector3.Lerp(bake.pos[i - 1], bake.pos[i], u);
        }

        public static Vector3 TangentAt(SkiLineBake bake, int i)
        {
            Vector3 t = i < bake.Count - 1
                ? bake.pos[i + 1] - bake.pos[i]
                : bake.pos[i] - bake.pos[i - 1];
            return t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.forward;
        }

        public static float NearestDistance(SkiLineBake bake, Vector3 local, out float planarDist)
        {
            float bestD2 = float.MaxValue;
            int bestI = 0;
            float bestT = 0f;
            var p = new Vector2(local.x, local.z);
            for (int i = 0; i < bake.Count - 1; i++)
            {
                var a = new Vector2(bake.pos[i].x, bake.pos[i].z);
                var b = new Vector2(bake.pos[i + 1].x, bake.pos[i + 1].z);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                float d2 = (p - (a + ab * t)).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; bestI = i; bestT = t; }
            }
            planarDist = Mathf.Sqrt(bestD2);
            return Mathf.Lerp(bake.cumDist[bestI], bake.cumDist[bestI + 1], bestT);
        }

        public static Vector3 RightAt(SkiLineBake bake, int i)
        {
            Vector3 right = Vector3.Cross(TangentAt(bake, i), Vector3.up);
            return right.sqrMagnitude > 1e-8f ? right.normalized : Vector3.right;
        }

        public static Vector3 SurfacePoint(SkiLine line, SkiLineBake bake, int i, float crossT)
        {
            Vector3 p = bake.pos[i];
            Vector3 right = RightAt(bake, i);

            float lateral = (crossT - 0.5f) * 2f * bake.halfWidth[i];
            float crossVal = line.crossSection != null && line.crossSection.length > 0
                ? line.crossSection.Evaluate(crossT) * line.crossSectionDepth
                : 0f;
            float rollVal = Mathf.Tan(bake.roll[i] * Mathf.Deg2Rad) * lateral;

            float edgeDist = Mathf.Min(crossT, 1f - crossT);
            float fade = line.sideFlatten > 0 ? Mathf.Clamp01(edgeDist / line.sideFlatten) : 1f;
            fade = fade * fade * (3f - 2f * fade);

            float featureOff = FeatureOffsetAt(line, bake.cumDist[i], lateral);

            return new Vector3(
                p.x + right.x * lateral,
                p.y + (crossVal + rollVal) * fade + featureOff,
                p.z + right.z * lateral);
        }

        public static int ComputeHash(SkiLine line, Vector3 terrainPos, Vector3 terrainSize, int res)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + terrainPos.GetHashCode();
                h = h * 31 + terrainSize.GetHashCode();
                h = h * 31 + res;
                h = h * 31 + line.width.GetHashCode();
                h = h * 31 + line.crossSectionDepth.GetHashCode();
                h = h * 31 + line.sideFlatten.GetHashCode();
                h = h * 31 + line.autoBank.GetHashCode();
                h = h * 31 + line.edgeBlend.GetHashCode();
                h = h * 31 + line.edgeFalloff.GetHashCode();
                h = h * 31 + line.endBlend.GetHashCode();
                h = h * 31 + line.bakeResolution;
                h = h * 31 + CurveHash(line.crossSection);
                if (line.nodes != null)
                {
                    foreach (var n in line.nodes)
                    {
                        if (n == null) continue;
                        h = h * 31 + n.position.GetHashCode();
                        h = h * 31 + n.widthOverride.GetHashCode();
                        h = h * 31 + n.roll.GetHashCode();
                    }
                }
                if (line.features != null)
                {
                    foreach (var f in line.features)
                    {
                        if (f == null) continue;
                        h = h * 31 + (f.enabled ? 1 : 0);
                        h = h * 31 + f.start.GetHashCode();
                        h = h * 31 + f.length.GetHashCode();
                        h = h * 31 + f.height.GetHashCode();
                        h = h * 31 + f.lateralOffset.GetHashCode();
                        h = h * 31 + f.width.GetHashCode();
                        h = h * 31 + f.sideBlend.GetHashCode();
                        h = h * 31 + CurveHash(f.profile);
                        if (f.paintStripes != null)
                        {
                            foreach (var s in f.paintStripes)
                            {
                                if (s == null) continue;
                                h = h * 31 + (s.acrossLine ? 7 : 3);
                                h = h * 31 + s.position.GetHashCode();
                                h = h * 31 + s.stripeWidth.GetHashCode();
                                h = h * 31 + s.softness.GetHashCode();
                                h = h * 31 + s.opacity.GetHashCode();
                                h = h * 31 + s.inset.GetHashCode();
                                h = h * 31 + s.colorIdx;
                            }
                        }
                    }
                }
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
