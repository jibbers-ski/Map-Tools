using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class LiftPath
    {
        public List<Vector3> points = new();
        public List<float> cumLen = new();
        internal readonly List<Vector3> chunkMin = new();
        internal readonly List<Vector3> chunkMax = new();
        internal int chunkedCount = -1;
        public float TotalLength => cumLen.Count > 0 ? cumLen[^1] : 0f;
        public float LoopLength => points.Count > 1 ? TotalLength + Vector3.Distance(points[^1], points[0]) : 0f;
    }

    public static class LiftGeometry
    {

        struct CableAnchor
        {
            public Vector3 origin;
            public Vector3 axis;
            public float min;
            public float max;
            public float offset;
            public Vector3 Position => origin + axis * offset;
            public static CableAnchor Fixed(Vector3 p) => new() { origin = p, axis = Vector3.up };
        }

        public const float DefaultSlack = 0.5f;
        const int StrandSolveIterations = 256;
        const float MinPointSpacing = 0.01f;
        const float SpanSampleStep = 2f;
        internal const float ArcSampleDegrees = 12f;
        const int ChunkPoints = 16;

        public static float GetFloat(List<CustomParameter> parameters, string name, float fallback)
        {
            var p = parameters?.Find(x => x.name == name);
            return p != null && p.type == CustomParameterType.Float ? p.floatValue : fallback;
        }

        public static bool GetBool(List<CustomParameter> parameters, string name, bool fallback)
        {
            var p = parameters?.Find(x => x.name == name);
            return p != null && p.type == CustomParameterType.Bool ? p.boolValue : fallback;
        }

        public static Vector3 SamplePoint(LiftPath path, float distance)
        {
            var pts = path.points;
            int n = pts.Count;
            if(n == 0)
                return Vector3.zero;
            float loop = path.LoopLength;
            if(loop <= 1e-6f)
                return pts[0];

            distance = Mathf.Repeat(distance, loop);
            float total = path.TotalLength;
            if(distance >= total)
            {
                float closing = loop - total;
                return closing > 1e-6f ? Vector3.Lerp(pts[n - 1], pts[0], (distance - total) / closing) : pts[0];
            }

            int lo = 0, hi = n - 1;
            while(lo < hi)
            {
                int mid = (lo + hi) / 2;
                if(path.cumLen[mid] < distance) lo = mid + 1;
                else hi = mid;
            }
            if(lo == 0)
                return pts[0];
            float span = path.cumLen[lo] - path.cumLen[lo - 1];
            float u = span > 1e-6f ? (distance - path.cumLen[lo - 1]) / span : 0f;
            return Vector3.Lerp(pts[lo - 1], pts[lo], u);
        }

        public static Vector3 SampleDirection(LiftPath path, float distance)
        {
            const float h = 0.25f;
            var d = SamplePoint(path, distance + h) - SamplePoint(path, distance - h);
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }

        public static float NearestDistance(LiftPath path, Vector3 position, out float distanceToPath)
        {
            var pts = path.points;
            int n = pts.Count;
            float bestSqr = float.MaxValue;
            float bestAlong = 0f;

            void Scan(int chunk)
            {
                int end = Mathf.Min((chunk + 1) * ChunkPoints, n);
                for(int i = chunk * ChunkPoints; i < end; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % n];
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                    float sqr = (position - (a + ab * t)).sqrMagnitude;
                    if(sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        bestAlong = (i < n - 1 ? path.cumLen[i] : path.TotalLength) + ab.magnitude * t;
                    }
                }
            }

            EnsureChunks(path);
            int chunks = path.chunkMin.Count;
            int nearest = 0;
            float nearestSqr = float.MaxValue;
            for(int c = 0; c < chunks; c++)
            {
                float sqr = BoxSqrDistance(path.chunkMin[c], path.chunkMax[c], position);
                if(sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = c;
                }
            }
            if(chunks > 0)
                Scan(nearest);
            for(int c = 0; c < chunks; c++)
                if(c != nearest && BoxSqrDistance(path.chunkMin[c], path.chunkMax[c], position) < bestSqr)
                    Scan(c);

            distanceToPath = Mathf.Sqrt(bestSqr);
            return bestAlong;
        }

        static void EnsureChunks(LiftPath path)
        {
            int n = path.points.Count;
            if(path.chunkedCount == n)
                return;
            path.chunkedCount = n;
            path.chunkMin.Clear();
            path.chunkMax.Clear();
            for(int start = 0; start < n; start += ChunkPoints)
            {
                var min = path.points[start];
                var max = min;
                int end = Mathf.Min(start + ChunkPoints, n);
                for(int i = start + 1; i <= end; i++)
                {
                    var p = path.points[i % n];
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                path.chunkMin.Add(min);
                path.chunkMax.Add(max);
            }
        }

        static float BoxSqrDistance(Vector3 min, Vector3 max, Vector3 p)
            => Vector3.Max(Vector3.Max(min - p, p - max), Vector3.zero).sqrMagnitude;

        public static List<(Vector3 a, Vector3 b)> BuildChain(LiftPath path, float tolerance)
        {
            var result = new List<(Vector3, Vector3)>();
            var pts = path.points;
            int n = pts.Count;
            if(n < 2)
                return result;

            Vector3 P(int i) => pts[i % n];

            int start = 0;
            while(start < n)
            {
                int end = start + 1;
                while(end < n && ChordFits(pts, start, end + 1, tolerance))
                    end++;
                result.Add((pts[start], P(end)));
                start = end;
            }
            return result;
        }

        static bool ChordFits(List<Vector3> pts, int start, int end, float tolerance)
        {
            var a = pts[start];
            var b = pts[end % pts.Count];
            var ab = b - a;
            float abSqr = Mathf.Max(ab.sqrMagnitude, 1e-8f);
            for(int i = start + 1; i < end; i++)
            {
                var p = pts[i];
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abSqr);
                if((p - (a + ab * t)).sqrMagnitude > tolerance * tolerance)
                    return false;
            }
            return true;
        }

        public static void OrientPieces(Transform basePiece, Transform topPiece, IReadOnlyList<Transform> masts)
        {
            if(!basePiece || !topPiece)
                return;

            var baseTarget = topPiece.position;
            var topTarget = basePiece.position;
            if(masts != null)
            {
                for(int i = 0; i < masts.Count; i++)
                    if(masts[i]) { baseTarget = masts[i].position; break; }
                for(int i = masts.Count - 1; i >= 0; i--)
                    if(masts[i]) { topTarget = masts[i].position; break; }
            }

            FaceAlong(basePiece, baseTarget - basePiece.position);
            FaceAlong(topPiece, topTarget - topPiece.position);

            if(masts == null)
                return;
            for(int i = 0; i < masts.Count; i++)
            {
                if(!masts[i])
                    continue;
                var prev = i > 0 && masts[i - 1] ? masts[i - 1].position : basePiece.position;
                var next = i < masts.Count - 1 && masts[i + 1] ? masts[i + 1].position : topPiece.position;
                FaceAlong(masts[i], next - prev);
            }
        }

        static void FaceAlong(Transform t, Vector3 direction)
        {
            direction.y = 0;
            if(direction.sqrMagnitude < 1e-6f)
                return;
            t.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public static bool TryGetRoute(Transform root, List<Vector3> route)
        {
            route.Clear();
            var anchors = new List<CableAnchor>();
            AppendChain(root, anchors);
            foreach(var anchor in anchors)
                route.Add(anchor.Position);
            return route.Count >= 2;
        }

        static void AppendChain(Transform root, List<CableAnchor> anchors)
        {
            if(!root)
                return;
            var arc = new List<Vector3>();
            for(int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if(!child.gameObject.activeSelf)
                    continue;
                if(child.TryGetComponent(out LiftCableWheel wheel))
                {
                    arc.Clear();
                    wheel.AppendArc(arc);
                    foreach(var p in arc)
                        anchors.Add(CableAnchor.Fixed(p));
                }
                else if(child.TryGetComponent(out LiftCableSlot slot))
                {
                    float min = Mathf.Min(slot.min, slot.max);
                    float max = Mathf.Max(slot.min, slot.max);
                    anchors.Add(new CableAnchor { origin = child.position, axis = child.up, min = min, max = max, offset = Mathf.Clamp(0f, min, max) });
                }
                else
                    anchors.Add(CableAnchor.Fixed(child.position));
            }
        }

        public static LiftPath BuildLoop(LiftStation baseStation, LiftStation topStation, IReadOnlyList<LiftMast> masts, float slack)
        {
            if(!baseStation || !topStation)
                return null;

            var baseRoute = new List<Vector3>();
            var topRoute = new List<Vector3>();
            bool baseChain = TryGetRoute(baseStation.cablePath, baseRoute);
            bool topChain = TryGetRoute(topStation.cablePath, topRoute);
            if(!baseChain && (!baseStation.inPoint || !baseStation.outPoint))
                return null;
            if(!topChain && (!topStation.inPoint || !topStation.outPoint))
                return null;

            var upAnchors = new List<CableAnchor> { CableAnchor.Fixed(baseChain ? baseRoute[^1] : baseStation.outPoint.position) };
            if(masts != null)
                foreach(var mast in masts)
                    if(mast)
                        AppendChain(mast.upPath, upAnchors);
            upAnchors.Add(CableAnchor.Fixed(topChain ? topRoute[0] : topStation.inPoint.position));

            var downAnchors = new List<CableAnchor> { CableAnchor.Fixed(topChain ? topRoute[^1] : topStation.outPoint.position) };
            if(masts != null)
                for(int i = masts.Count - 1; i >= 0; i--)
                    if(masts[i])
                        AppendChain(masts[i].downPath, downAnchors);
            downAnchors.Add(CableAnchor.Fixed(baseChain ? baseRoute[0] : baseStation.inPoint.position));

            var up = SolveStrand(upAnchors, slack);
            var down = SolveStrand(downAnchors, slack);

            var path = new LiftPath();
            AddPoint(path, up[0]);
            AddSpans(path, up, slack);
            if(topChain)
                AddRoute(path, topRoute);
            else
                AddArc(path, topStation, baseStation.transform.position);
            AddSpans(path, down, slack);
            if(baseChain)
                AddRoute(path, baseRoute);
            else
                AddArc(path, baseStation, topStation.transform.position);

            return path.points.Count >= 3 ? path : null;
        }

        static List<Vector3> SolveStrand(List<CableAnchor> anchors, float slack)
        {
            for(int iteration = 0; iteration < StrandSolveIterations; iteration++)
            {
                float moved = 0f;
                for(int i = 1; i < anchors.Count - 1; i++)
                {
                    var anchor = anchors[i];
                    if(anchor.max - anchor.min < 1e-4f)
                        continue;
                    var prev = anchors[i - 1].Position;
                    var next = anchors[i + 1].Position;
                    var chord = next - prev;
                    var flatChord = Vector3.ProjectOnPlane(chord, anchor.axis);
                    if(flatChord.sqrMagnitude < 1e-6f)
                        continue;
                    float t = Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(anchor.origin - prev, anchor.axis), flatChord) / flatChord.sqrMagnitude);
                    float length = chord.magnitude;
                    float dip = slack * (length / 50f) * (length / 50f);
                    var target = prev + chord * t + Vector3.down * (4f * dip * t * (1f - t));
                    float offset = Mathf.Clamp(Vector3.Dot(target - anchor.origin, anchor.axis), anchor.min, anchor.max);
                    moved = Mathf.Max(moved, Mathf.Abs(offset - anchor.offset));
                    anchor.offset = offset;
                    anchors[i] = anchor;
                }
                if(moved < 1e-4f)
                    break;
            }
            return anchors.ConvertAll(a => a.Position);
        }

        static void AddRoute(LiftPath path, List<Vector3> route)
        {
            for(int i = 1; i < route.Count; i++)
                AddPoint(path, route[i]);
        }

        static void AddSpans(LiftPath path, List<Vector3> anchors, float slack)
        {
            for(int i = 0; i < anchors.Count - 1; i++)
            {
                var a = anchors[i];
                var b = anchors[i + 1];
                float length = Vector3.Distance(a, b);
                if(length < MinPointSpacing)
                    continue;
                float dip = slack * (length / 50f) * (length / 50f);
                int steps = Mathf.Clamp(Mathf.CeilToInt(length / SpanSampleStep), 1, 64);
                for(int s = 1; s <= steps; s++)
                {
                    float t = (float) s / steps;
                    var p = Vector3.Lerp(a, b, t);
                    p.y -= 4f * dip * t * (1f - t);
                    AddPoint(path, p);
                }
            }
        }

        static void AddArc(LiftPath path, LiftStation station, Vector3 awayFrom)
        {
            var from = path.points[^1];
            var to = station.outPoint.position;
            if(!station.wheelCenter)
            {
                AddPoint(path, to);
                return;
            }

            var center = station.wheelCenter.position;
            var axis = station.wheelCenter.up;
            var pf = Vector3.ProjectOnPlane(from - center, axis);
            var pt = Vector3.ProjectOnPlane(to - center, axis);
            float rf = pf.magnitude;
            float rt = pt.magnitude;
            if(rf < 0.01f || rt < 0.01f)
            {
                AddPoint(path, to);
                return;
            }

            float hf = Vector3.Dot(from - center, axis);
            float ht = Vector3.Dot(to - center, axis);
            float angle = Vector3.SignedAngle(pf, pt, axis);
            if(Mathf.Approximately(angle, 0f))
                angle = 360f;
            float alt = angle > 0 ? angle - 360f : angle + 360f;

            Vector3 Mid(float sweepAngle) =>
                center + Quaternion.AngleAxis(sweepAngle * 0.5f, axis) * (pf.normalized * ((rf + rt) * 0.5f)) + axis * ((hf + ht) * 0.5f);
            float sweep = (Mid(angle) - awayFrom).sqrMagnitude >= (Mid(alt) - awayFrom).sqrMagnitude ? angle : alt;

            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) / ArcSampleDegrees), 2, 64);
            for(int s = 1; s <= steps; s++)
            {
                float t = (float) s / steps;
                var radial = Quaternion.AngleAxis(sweep * t, axis) * pf.normalized;
                AddPoint(path, center + radial * Mathf.Lerp(rf, rt, t) + axis * Mathf.Lerp(hf, ht, t));
            }
            AddPoint(path, to);
        }

        static void AddPoint(LiftPath path, Vector3 p)
        {
            int n = path.points.Count;
            if(n > 0 && (path.points[n - 1] - p).sqrMagnitude < MinPointSpacing * MinPointSpacing)
                return;
            if(n > 0 && (path.points[0] - p).sqrMagnitude < MinPointSpacing * MinPointSpacing)
                return;
            path.points.Add(p);
            path.cumLen.Add(n == 0 ? 0f : path.cumLen[n - 1] + Vector3.Distance(path.points[n - 1], p));
        }

        public static Mesh BuildTube(LiftPath path, float radius, int sides, Transform space)
        {
            int n = path?.points.Count ?? 0;
            if(n < 3)
                return null;
            sides = Mathf.Clamp(sides, 3, 24);

            var pts = path.points;
            float total = path.cumLen[n - 1] + Vector3.Distance(pts[n - 1], pts[0]);

            var tangents = new Vector3[n];
            for(int i = 0; i < n; i++)
            {
                var t = pts[(i + 1) % n] - pts[(i - 1 + n) % n];
                tangents[i] = t.sqrMagnitude > 1e-10f ? t.normalized : Vector3.forward;
            }

            var normals = new Vector3[n];
            normals[0] = Vector3.ProjectOnPlane(Vector3.up, tangents[0]);
            if(normals[0].sqrMagnitude < 1e-6f)
                normals[0] = Vector3.ProjectOnPlane(Vector3.right, tangents[0]);
            normals[0].Normalize();
            for(int i = 1; i < n; i++)
                normals[i] = (Quaternion.FromToRotation(tangents[i - 1], tangents[i]) * normals[i - 1]).normalized;

            var closed = Quaternion.FromToRotation(tangents[n - 1], tangents[0]) * normals[n - 1];
            float twist = Vector3.SignedAngle(closed, normals[0], tangents[0]);
            for(int i = 1; i < n; i++)
                normals[i] = Quaternion.AngleAxis(twist * (path.cumLen[i] / total), tangents[i]) * normals[i];

            int rings = n + 1;
            int cols = sides + 1;
            var verts = new Vector3[rings * cols];
            var norms = new Vector3[rings * cols];
            var tans = new Vector4[rings * cols];
            var uvs = new Vector2[rings * cols];

            for(int i = 0; i < rings; i++)
            {
                int pi = i % n;
                var center = pts[pi];
                var tangent = tangents[pi];
                var normal = normals[pi];
                var binormal = Vector3.Cross(tangent, normal);
                float u = i < n ? path.cumLen[i] : total;
                var tanLocal = space ? space.InverseTransformDirection(tangent) : tangent;

                for(int s = 0; s < cols; s++)
                {
                    float ang = (float) s / sides * Mathf.PI * 2f;
                    var radial = normal * Mathf.Cos(ang) + binormal * Mathf.Sin(ang);
                    int vi = i * cols + s;
                    var world = center + radial * radius;
                    verts[vi] = space ? space.InverseTransformPoint(world) : world;
                    norms[vi] = space ? space.InverseTransformDirection(radial) : radial;
                    tans[vi] = new Vector4(tanLocal.x, tanLocal.y, tanLocal.z, -1f);
                    uvs[vi] = new Vector2(u, (float) s / sides);
                }
            }

            var tris = new int[n * sides * 6];
            int ti = 0;
            for(int i = 0; i < n; i++)
                for(int s = 0; s < sides; s++)
                {
                    int a = i * cols + s;
                    int b = a + cols;
                    tris[ti++] = a;
                    tris[ti++] = a + 1;
                    tris[ti++] = b;
                    tris[ti++] = a + 1;
                    tris[ti++] = b + 1;
                    tris[ti++] = b;
                }

            var mesh = new Mesh { name = "LiftCable" };
            if(verts.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.tangents = tans;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

    }

}
