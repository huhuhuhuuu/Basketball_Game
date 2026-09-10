using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt
{
    /// <summary>
    /// Procedural mesh helpers. All meshes are built in local space with Y up and
    /// come back with normals/bounds computed. Meshes are NOT registered with the
    /// BuildContext here – <see cref="BuildContext.MeshObject"/> does that when you use them.
    /// </summary>
    public static class MeshFactory
    {
        // ── Small builder used by every generator ────────────────────────────
        public sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            public int Add(Vector3 p, Vector3 n, Vector2 uv)
            {
                V.Add(p); N.Add(n); UV.Add(uv);
                return V.Count - 1;
            }

            public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
            public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

            public Mesh ToMesh(string name, bool recalcNormals = false)
            {
                var m = new Mesh();
                m.name = name;
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V);
                m.SetUVs(0, UV);
                if (!recalcNormals && N.Count == V.Count) m.SetNormals(N);
                m.SetTriangles(T, 0);
                if (recalcNormals || N.Count != V.Count) m.RecalculateNormals();
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }

            /// <summary>Appends another builder's geometry with an optional transform.</summary>
            public void Append(Builder other, Matrix4x4 xf)
            {
                int baseIndex = V.Count;
                for (int i = 0; i < other.V.Count; i++)
                {
                    V.Add(xf.MultiplyPoint3x4(other.V[i]));
                    N.Add(xf.MultiplyVector(other.N[i]).normalized);
                    UV.Add(other.UV[i]);
                }
                for (int i = 0; i < other.T.Count; i++) T.Add(other.T[i] + baseIndex);
            }
        }

        // ── Torus (rim) ──────────────────────────────────────────────────────

        /// <summary>Torus lying in the XZ plane, centred at the origin. `majorRadius` is to the tube centre.</summary>
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments = 48, int minorSegments = 12, string name = "Torus")
        {
            var b = new Builder();
            for (int i = 0; i <= majorSegments; i++)
            {
                float a = (float)i / majorSegments * Mathf.PI * 2f;
                Vector3 ring = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= minorSegments; j++)
                {
                    float t = (float)j / minorSegments * Mathf.PI * 2f;
                    Vector3 n = ring * Mathf.Cos(t) + Vector3.up * Mathf.Sin(t);
                    Vector3 p = ring * majorRadius + n * minorRadius;
                    b.Add(p, n, new Vector2((float)i / majorSegments * 8f, (float)j / minorSegments));
                }
            }
            int stride = minorSegments + 1;
            for (int i = 0; i < majorSegments; i++)
            for (int j = 0; j < minorSegments; j++)
            {
                int a = i * stride + j, c = (i + 1) * stride + j;
                b.Quad(a, a + 1, c + 1, c);
            }
            return b.ToMesh(name);
        }

        // ── Flat ribbons (painted lines, tape, paths) ────────────────────────

        /// <summary>Points along a circular arc in the XZ plane (degrees, 0° = +X, counter-clockwise seen from above).</summary>
        public static List<Vector3> ArcPoints(Vector3 center, float radius, float startDeg, float endDeg, int segments, float y)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(startDeg, endDeg, (float)i / segments) * Mathf.Deg2Rad;
                pts.Add(new Vector3(center.x + Mathf.Cos(a) * radius, y, center.z + Mathf.Sin(a) * radius));
            }
            return pts;
        }

        /// <summary>
        /// Appends a flat ribbon (facing +Y) of constant width along a polyline. Joins are mitred, so keep
        /// the polyline reasonably smooth; for sharp corners add separate ribbons with `extendEnds = width/2`.
        /// UV u runs along the length in metres × `uvPerMeter`, v across 0..1.
        /// </summary>
        public static void AppendRibbon(Builder b, IList<Vector3> pts, float width, float uvPerMeter = 2f, float extendEnds = 0f, bool closed = false)
        {
            int n = pts.Count;
            if (n < 2) return;
            var p = new List<Vector3>(pts);
            if (!closed && extendEnds > 0f)
            {
                Vector3 d0 = (p[1] - p[0]).normalized, d1 = (p[n - 1] - p[n - 2]).normalized;
                p[0] -= d0 * extendEnds;
                p[n - 1] += d1 * extendEnds;
            }
            float half = width * 0.5f;
            float dist = 0f;
            int first = b.V.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir;
                if (closed)
                {
                    Vector3 prev = p[(i - 1 + n) % n], next = p[(i + 1) % n];
                    dir = ((p[i] - prev).normalized + (next - p[i]).normalized).normalized;
                }
                else if (i == 0) dir = (p[1] - p[0]).normalized;
                else if (i == n - 1) dir = (p[n - 1] - p[n - 2]).normalized;
                else dir = ((p[i] - p[i - 1]).normalized + (p[i + 1] - p[i]).normalized).normalized;
                if (i > 0) dist += Vector3.Distance(p[i], p[i - 1]);
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * half;
                // widen the mitre so the ribbon keeps a constant visual width around bends/corners
                if (closed || (i > 0 && i < n - 1))
                {
                    Vector3 incoming = (p[i] - p[(i - 1 + n) % n]).normalized;
                    float cosHalf = Vector3.Dot(incoming, dir);
                    if (cosHalf > 0.2f) side /= cosHalf;
                }
                float u = dist * uvPerMeter;
                b.Add(p[i] - side, Vector3.up, new Vector2(u, 0f));
                b.Add(p[i] + side, Vector3.up, new Vector2(u, 1f));
            }
            int count = closed ? n : n - 1;
            for (int i = 0; i < count; i++)
            {
                int a = first + i * 2, c = first + ((i + 1) % n) * 2;
                b.Quad(a, c, c + 1, a + 1);
            }
        }

        /// <summary>Straight flat segment from a to b (y taken from the points).</summary>
        public static void AppendSegment(Builder b, Vector3 a, Vector3 c, float width, float extendEnds = 0f, float uvPerMeter = 2f)
        {
            AppendRibbon(b, new[] { a, c }, width, uvPerMeter, extendEnds);
        }

        /// <summary>Flat rectangle outline (4 mitred corners) lying at `y`.</summary>
        public static void AppendRectOutline(Builder b, float xMin, float zMin, float xMax, float zMax, float width, float y, float uvPerMeter = 2f)
        {
            var pts = new List<Vector3>
            {
                new Vector3(xMin, y, zMin), new Vector3(xMax, y, zMin),
                new Vector3(xMax, y, zMax), new Vector3(xMin, y, zMax),
            };
            AppendRibbon(b, pts, width, uvPerMeter, 0f, closed: true);
        }

        /// <summary>Filled flat disc facing +Y.</summary>
        public static Mesh Disc(float radius, int segments = 32, string name = "Disc")
        {
            var b = new Builder();
            int c = b.Add(Vector3.zero, Vector3.up, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                b.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius), Vector3.up,
                    new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 1; i <= segments; i++) b.Tri(c, c + i + 1, c + i);
            return b.ToMesh(name);
        }

        /// <summary>Flat convex polygon (fan) facing +Y; points are (x,z) ordered by increasing angle from +X towards +Z. UVs are the normalised bounding box.</summary>
        public static Mesh Polygon(IList<Vector2> pts, string name = "Polygon")
        {
            var b = new Builder();
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
            float w = Mathf.Max(maxX - minX, 1e-4f), h = Mathf.Max(maxZ - minZ, 1e-4f);
            foreach (var p in pts)
                b.Add(new Vector3(p.x, 0f, p.y), Vector3.up, new Vector2((p.x - minX) / w, (p.y - minZ) / h));
            for (int i = 1; i < pts.Count - 1; i++) b.Tri(0, i + 1, i);
            return b.ToMesh(name);
        }

        // ── Tubes and lathes (net strands, gooseneck, bottles, cans, cups) ───

        /// <summary>
        /// Tube swept along a polyline (smooth joins, capped ends). Good for bent pipes and net cords.
        /// </summary>
        public static void AppendTube(Builder b, IList<Vector3> path, float radius, int sides = 8, bool caps = true, float uvPerMeter = 1f)
        {
            int n = path.Count;
            if (n < 2) return;
            Vector3 prevRight = Vector3.zero;
            int first = b.V.Count;
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir;
                if (i == 0) dir = (path[1] - path[0]).normalized;
                else if (i == n - 1) dir = (path[n - 1] - path[n - 2]).normalized;
                else dir = ((path[i] - path[i - 1]).normalized + (path[i + 1] - path[i]).normalized).normalized;
                if (i > 0) dist += Vector3.Distance(path[i], path[i - 1]);
                Vector3 right;
                if (i == 0)
                {
                    Vector3 helper = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                    right = Vector3.Cross(dir, helper).normalized;
                }
                else
                {
                    // parallel-transport the frame so the tube does not twist
                    right = (prevRight - dir * Vector3.Dot(prevRight, dir)).normalized;
                    if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(dir, Vector3.up).normalized;
                }
                prevRight = right;
                Vector3 fwd = Vector3.Cross(right, dir).normalized;
                for (int s = 0; s <= sides; s++)
                {
                    float a = (float)s / sides * Mathf.PI * 2f;
                    Vector3 nrm = right * Mathf.Cos(a) + fwd * Mathf.Sin(a);
                    b.Add(path[i] + nrm * radius, nrm, new Vector2((float)s / sides, dist * uvPerMeter));
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = first + i * stride + s, c = first + (i + 1) * stride + s;
                b.Quad(a, c, c + 1, a + 1);
            }
            if (caps)
            {
                AppendCap(b, path[0], -(path[1] - path[0]).normalized, radius, sides);
                AppendCap(b, path[n - 1], (path[n - 1] - path[n - 2]).normalized, radius, sides);
            }
        }

        static void AppendCap(Builder b, Vector3 center, Vector3 normal, float radius, int sides)
        {
            Vector3 helper = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 r = Vector3.Cross(normal, helper).normalized, f = Vector3.Cross(r, normal).normalized;
            int c = b.Add(center, normal, new Vector2(0.5f, 0.5f));
            for (int s = 0; s <= sides; s++)
            {
                float a = (float)s / sides * Mathf.PI * 2f;
                b.Add(center + (r * Mathf.Cos(a) + f * Mathf.Sin(a)) * radius, normal, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int s = 0; s < sides; s++) b.Tri(c, c + s + 2, c + s + 1);
        }

        /// <summary>Stand-alone tube mesh along a path.</summary>
        public static Mesh Tube(IList<Vector3> path, float radius, int sides = 8, bool caps = true, string name = "Tube")
        {
            var b = new Builder();
            AppendTube(b, path, radius, sides, caps);
            return b.ToMesh(name);
        }

        /// <summary>
        /// Surface of revolution around the Y axis. `profile` is a list of (radius, y) from bottom to top.
        /// A radius of 0 at either end closes the shape (e.g. a bottle cap or can bottom).
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments = 24, string name = "Lathe", bool smooth = true)
        {
            var b = new Builder();
            int n = profile.Count;
            float total = 0f;
            for (int i = 1; i < n; i++) total += Vector2.Distance(profile[i], profile[i - 1]);
            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) run += Vector2.Distance(profile[i], profile[i - 1]);
                // outward normal of the profile in (r, y) space
                Vector2 tangent;
                if (i == 0) tangent = profile[1] - profile[0];
                else if (i == n - 1) tangent = profile[n - 1] - profile[n - 2];
                else tangent = (profile[i + 1] - profile[i - 1]);
                tangent.Normalize();
                Vector2 nrm2 = new Vector2(tangent.y, -tangent.x);
                for (int s = 0; s <= segments; s++)
                {
                    float a = (float)s / segments * Mathf.PI * 2f;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    Vector3 p = new Vector3(profile[i].x * cs, profile[i].y, profile[i].x * sn);
                    Vector3 nrm = new Vector3(nrm2.x * cs, nrm2.y, nrm2.x * sn).normalized;
                    b.Add(p, nrm, new Vector2((float)s / segments, total > 0f ? run / total : 0f));
                }
            }
            int stride = segments + 1;
            for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < segments; s++)
            {
                int a = i * stride + s, c = (i + 1) * stride + s;
                b.Quad(a, c, c + 1, a + 1);
            }
            return b.ToMesh(name, !smooth);
        }

        // ── Boxes / panels / deformation ─────────────────────────────────────

        /// <summary>Axis-aligned box mesh with per-face UVs (tiling in metres × `uvPerMeter`).</summary>
        public static Mesh Box(Vector3 size, float uvPerMeter = 1f, string name = "Box")
        {
            var b = new Builder();
            Vector3 h = size * 0.5f;
            AddFace(b, new Vector3(0, 0, -h.z), Vector3.back, Vector3.right, Vector3.up, size.x, size.y, uvPerMeter);
            AddFace(b, new Vector3(0, 0, h.z), Vector3.forward, Vector3.left, Vector3.up, size.x, size.y, uvPerMeter);
            AddFace(b, new Vector3(-h.x, 0, 0), Vector3.left, Vector3.back, Vector3.up, size.z, size.y, uvPerMeter);
            AddFace(b, new Vector3(h.x, 0, 0), Vector3.right, Vector3.forward, Vector3.up, size.z, size.y, uvPerMeter);
            AddFace(b, new Vector3(0, h.y, 0), Vector3.up, Vector3.right, Vector3.forward, size.x, size.z, uvPerMeter);
            AddFace(b, new Vector3(0, -h.y, 0), Vector3.down, Vector3.right, Vector3.back, size.x, size.z, uvPerMeter);
            return b.ToMesh(name);
        }

        static void AddFace(Builder b, Vector3 center, Vector3 normal, Vector3 right, Vector3 up, float w, float hgt, float uvPerMeter)
        {
            Vector3 r = right * (w * 0.5f), u = up * (hgt * 0.5f);
            int a = b.Add(center - r - u, normal, new Vector2(0, 0));
            int c = b.Add(center + r - u, normal, new Vector2(w * uvPerMeter, 0));
            int d = b.Add(center + r + u, normal, new Vector2(w * uvPerMeter, hgt * uvPerMeter));
            int e = b.Add(center - r + u, normal, new Vector2(0, hgt * uvPerMeter));
            b.Quad(a, e, d, c);
        }

        /// <summary>
        /// Vertical rectangular panel in the XY plane (facing +Z on the front, −Z on the back when `twoSided`),
        /// centred at the origin, subdivided so it can be bent/dented with <see cref="Displace"/>.
        /// UVs tile in metres × `uvPerMeter` (so a chain-link texture can be given a real mesh size).
        /// </summary>
        public static Mesh Panel(float width, float height, int subX, int subY, float uvPerMeter, bool twoSided, string name = "Panel")
        {
            var b = new Builder();
            AddGrid(b, width, height, subX, subY, uvPerMeter, Vector3.forward);
            if (twoSided) AddGrid(b, width, height, subX, subY, uvPerMeter, Vector3.back);
            return b.ToMesh(name);
        }

        static void AddGrid(Builder b, float width, float height, int subX, int subY, float uvPerMeter, Vector3 normal)
        {
            int first = b.V.Count;
            for (int j = 0; j <= subY; j++)
            for (int i = 0; i <= subX; i++)
            {
                float x = ((float)i / subX - 0.5f) * width;
                float y = ((float)j / subY - 0.5f) * height;
                b.Add(new Vector3(x, y, 0f), normal, new Vector2((x + width * 0.5f) * uvPerMeter, (y + height * 0.5f) * uvPerMeter));
            }
            int stride = subX + 1;
            for (int j = 0; j < subY; j++)
            for (int i = 0; i < subX; i++)
            {
                int a = first + j * stride + i, c = first + (j + 1) * stride + i;
                if (normal.z > 0f) b.Quad(a, a + 1, c + 1, c);
                else b.Quad(a, c, c + 1, a + 1);
            }
        }

        /// <summary>Moves every vertex by `offset(position)` and recomputes normals – crumple, dent, sag.</summary>
        public static Mesh Displace(Mesh mesh, Func<Vector3, Vector3> offset)
        {
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++) v[i] += offset(v[i]);
            mesh.vertices = v;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>UV sphere (so procedural textures like a basketball map predictably: u = longitude, v = latitude).</summary>
        public static Mesh UvSphere(float radius, int lon = 32, int lat = 20, string name = "UvSphere")
        {
            var b = new Builder();
            for (int j = 0; j <= lat; j++)
            {
                float t = (float)j / lat, phi = (t - 0.5f) * Mathf.PI;
                for (int i = 0; i <= lon; i++)
                {
                    float s = (float)i / lon, theta = s * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    b.Add(n * radius, n, new Vector2(s, t));
                }
            }
            int stride = lon + 1;
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int a = j * stride + i, c = (j + 1) * stride + i;
                b.Quad(a, c, c + 1, a + 1);
            }
            return b.ToMesh(name);
        }

        /// <summary>Combines several meshes (with transforms) into one. Useful before static batching or for nets.</summary>
        public static Mesh Combine(IList<Mesh> meshes, IList<Matrix4x4> transforms, string name = "Combined")
        {
            var ci = new CombineInstance[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
            {
                ci[i].mesh = meshes[i];
                ci[i].transform = transforms != null ? transforms[i] : Matrix4x4.identity;
            }
            var m = new Mesh();
            m.name = name;
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.CombineMeshes(ci, true, true);
            m.RecalculateBounds();
            return m;
        }
    }
}
