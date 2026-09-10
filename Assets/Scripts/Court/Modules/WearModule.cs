using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// WearModule – the scratches, dirt and small debris that make the slab look used.
    /// <para>
    /// Everything flat is a cutout decal quad (no shadows) on one of three height layers from CourtSpec:
    /// DecalY under the painted lines, OverlayY on top of them.
    ///  • Dirt/grime patches in the corners, along the fence base and under the benches.
    ///  • Long thin scratches and scuff streaks around both baskets and along the bench sideline.
    ///  • Cracks drawn as random-walk branches into a texture; one has a tuft of weeds growing out.
    ///  • Gum spots, a shallow puddle (the only alpha-blended decal) with a damp ring around it.
    ///  • ~50 leaves as ONE mesh, each picking one of four leaf drawings from a 2×2 atlas.
    ///  • Rust streaks under the hoop poles and the gate posts, a faint ghost of an old key line,
    ///    bird droppings on the benches and on top of a backboard.
    /// </para>
    /// </summary>
    public static class WearModule
    {
        public static void Build(BuildContext ctx, Transform parent)
        {
            BuildDirt(ctx, ctx.Group("Dirt", parent).transform);
            BuildScratches(ctx, ctx.Group("Scratches", parent).transform);
            BuildCracks(ctx, ctx.Group("Cracks", parent).transform);
            BuildGum(ctx, ctx.Group("Gum", parent).transform);
            BuildPuddle(ctx, ctx.Group("Puddle", parent).transform);
            BuildLeaves(ctx, ctx.Group("Leaves", parent).transform);
            BuildRustStreaks(ctx, ctx.Group("RustStreaks", parent).transform);
            BuildGhostLine(ctx, ctx.Group("GhostLine", parent).transform);
            BuildDroppings(ctx, ctx.Group("BirdDroppings", parent).transform);
        }

        // ── Dirt patches ─────────────────────────────────────────────────────

        static void BuildDirt(BuildContext ctx, Transform g)
        {
            Material[] mats =
            {
                DirtMaterial(ctx, "Dirt_Dark", new Color(0.12f, 0.10f, 0.08f)),
                DirtMaterial(ctx, "Dirt_Grey", new Color(0.16f, 0.15f, 0.14f)),
                DirtMaterial(ctx, "Dirt_Brown", new Color(0.18f, 0.13f, 0.08f)),
            };
            float fx = CourtSpec.FenceHalfX, fz = CourtSpec.FenceHalfZ;
            // Corners, fence base and bench feet – never in the middle of the keys.
            Vector3[] spots =
            {
                new Vector3(-fx + 0.9f, 0f, -fz + 1.0f), new Vector3(fx - 1.1f, 0f, fz - 1.4f),
                new Vector3(-fx + 1.3f, 0f, fz - 2.2f), new Vector3(fx - 1.4f, 0f, -fz + 1.8f),
                new Vector3(-fx + 0.7f, 0f, 4.0f), new Vector3(fx - 0.8f, 0f, -11.0f),
                new Vector3(3.5f, 0f, fz - 0.8f), new Vector3(-4.2f, 0f, -fz + 0.7f),
                new Vector3(CourtSpec.BenchEastX, 0f, CourtSpec.BenchEastZ[0]),
                new Vector3(CourtSpec.BenchEastX, 0f, CourtSpec.BenchEastZ[2]),
                new Vector3(CourtSpec.TrashCanPos.x + 0.5f, 0f, CourtSpec.TrashCanPos.z + 0.3f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                float w = ctx.Range(0.8f, 2.5f), h = w * ctx.Range(0.6f, 1.1f);
                Vector3 p = spots[i] + ctx.JitterXZ(0.3f);
                p.y = CourtSpec.DecalY;
                ctx.FloorQuad("Dirt_" + i, g, p, new Vector2(w, h), ctx.Range(0f, 360f), mats[i % mats.Length]);
            }
        }

        static Material DirtMaterial(BuildContext ctx, string key, Color tint)
        {
            Texture2D tex = ctx.Tex.Get("dirt_blob", () => ProceduralTextures.Create(256, 256, (u, v) =>
            {
                float blob = ProceduralTextures.SoftBlob(u, v, 0.5f, 0.5f, 0.46f, 0.9f, 101);
                float speck = ProceduralTextures.Fbm(u, v, 12, 4, 102);
                float alpha = Mathf.Clamp01(blob * 1.3f) * (0.35f + speck * 0.75f);
                float shade = 0.85f + speck * 0.3f;
                return new Color(shade, shade, shade, alpha);
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "dirt_blob"));
            return ctx.Mats.Get(key, () => MatKit.Make(key, tint, 0.12f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.3f));
        }

        // ── Scratches and scuffs ─────────────────────────────────────────────

        static void BuildScratches(BuildContext ctx, Transform g)
        {
            Texture2D tex = ctx.Tex.Get("scratch_streak", () => ProceduralTextures.Create(256, 32, (u, v) =>
            {
                float across = 1f - Mathf.Abs(v - 0.5f) * 2f;
                float along = Mathf.Sin(u * Mathf.PI);
                float rough = ProceduralTextures.Fbm(u, v, 24, 3, 111);
                float alpha = Mathf.Clamp01(across * across * along * (0.5f + rough * 0.9f));
                float shade = 0.7f + rough * 0.4f;
                return new Color(shade, shade, shade, alpha);
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "scratch_streak"));
            Material dark = ctx.Mats.Get("Scratch_Dark", () => MatKit.Make("Scratch_Dark", new Color(0.06f, 0.06f, 0.06f), 0.15f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.35f));
            Material pale = ctx.Mats.Get("Scratch_Pale", () => MatKit.Make("Scratch_Pale", new Color(0.55f, 0.55f, 0.5f), 0.1f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.4f));

            int n = 0;
            // Around both baskets (where the ball and feet scuff most).
            for (int end = 0; end < 2; end++)
            {
                float zc = (end == 0 ? 1f : -1f) * (CourtSpec.BasketZ - 1.2f);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = new Vector3(ctx.Range(-2.6f, 2.6f), CourtSpec.OverlayY, zc + ctx.Range(-2.0f, 2.0f));
                    Scratch(ctx, g, n++, p, i % 3 == 0 ? pale : dark);
                }
            }
            // Along the bench sideline and near the gate.
            for (int i = 0; i < 5; i++)
                Scratch(ctx, g, n++, new Vector3(ctx.Range(7.6f, 8.9f), CourtSpec.OverlayY, ctx.Range(-7f, 7f)), dark);
            for (int i = 0; i < 3; i++)
                Scratch(ctx, g, n++, new Vector3(ctx.Range(-9.4f, -8.2f), CourtSpec.OverlayY, ctx.Range(-6f, -2f)), i == 0 ? pale : dark);
        }

        static void Scratch(BuildContext ctx, Transform g, int i, Vector3 p, Material mat)
        {
            float len = ctx.Range(0.4f, 1.6f), wid = ctx.Range(0.02f, 0.05f);
            ctx.FloorQuad("Scratch_" + i, g, p, new Vector2(len, wid), ctx.Range(0f, 360f), mat);
        }

        // ── Cracks + weed ────────────────────────────────────────────────────

        static void BuildCracks(BuildContext ctx, Transform g)
        {
            Material[] mats =
            {
                CrackMaterial(ctx, 0, ctx.RangeInt(1, 100000)),
                CrackMaterial(ctx, 1, ctx.RangeInt(1, 100000)),
            };
            Vector3[] spots = { new Vector3(-6.5f, 0f, -12.5f), new Vector3(5.2f, 0f, 3.4f), new Vector3(-3.8f, 0f, 15.6f), new Vector3(8.3f, 0f, -9.2f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = spots[i]; p.y = CourtSpec.DecalY;
                float s = ctx.Range(2.0f, 3.0f);
                ctx.FloorQuad("Crack_" + i, g, p, new Vector2(s, s), ctx.Range(0f, 360f), mats[i % 2]);
            }
            BuildWeed(ctx, g, spots[0] + new Vector3(0.15f, 0f, -0.1f));
        }

        /// <summary>Crack texture: 2–3 random-walk branches (with side branches) drawn into a buffer.</summary>
        static Material CrackMaterial(BuildContext ctx, int variant, int seed)
        {
            const int size = 512;
            Texture2D tex = ctx.Tex.Get("crack_" + variant, () =>
            {
                float[] ink = new float[size * size];
                var rng = new System.Random(seed);
                int branches = 2 + rng.Next(2);
                for (int b = 0; b < branches; b++)
                {
                    double heading = rng.NextDouble() * Math.PI * 2.0;
                    WalkBranch(ink, size, rng, size * 0.5, size * 0.5, heading, 3.4f, 150 + rng.Next(90), 2);
                }
                return ProceduralTextures.Create(size, size, (u, v) =>
                {
                    int x = Mathf.Min((int)(u * size), size - 1), y = Mathf.Min((int)(v * size), size - 1);
                    float k = ink[y * size + x];
                    // Dark core, a faint lighter halo where the edge has chipped.
                    float alpha = Mathf.Clamp01(k * 1.4f);
                    float shade = Mathf.Lerp(0.55f, 0.06f, Mathf.Clamp01(k));
                    return new Color(shade, shade, shade * 0.95f, alpha);
                }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "crack_" + variant);
            });
            return ctx.Mats.Get("Crack_" + variant, () => MatKit.Make("Crack_" + variant, Color.white, 0.1f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.45f));
        }

        static void WalkBranch(float[] ink, int size, System.Random rng, double x, double y, double heading, float thickness, int steps, int depth)
        {
            for (int i = 0; i < steps; i++)
            {
                heading += (rng.NextDouble() - 0.5) * 0.7;
                x += Math.Cos(heading) * 2.0;
                y += Math.Sin(heading) * 2.0;
                if (x < 8 || y < 8 || x >= size - 8 || y >= size - 8) return;
                float t = Mathf.Lerp(thickness, 1.2f, (float)i / steps);
                Stamp(ink, size, (int)x, (int)y, t);
                if (depth > 0 && rng.NextDouble() < 0.02)
                    WalkBranch(ink, size, rng, x, y, heading + (rng.NextDouble() < 0.5 ? 1.0 : -1.0) * (0.6 + rng.NextDouble() * 0.6), t * 0.7f, steps / 2, depth - 1);
            }
        }

        static void Stamp(float[] ink, int size, int cx, int cy, float radius)
        {
            int r = Mathf.CeilToInt(radius) + 1;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float v = Mathf.Clamp01(1f - (d - radius + 1f));   // 1 inside, soft 1 px edge, halo beyond
                float halo = Mathf.Clamp01(1f - (d - radius) / 3f) * 0.35f;
                int idx = y * size + x;
                ink[idx] = Mathf.Max(ink[idx], Mathf.Max(v, halo));
            }
        }

        /// <summary>A few green blades pushing up through a crack.</summary>
        static void BuildWeed(BuildContext ctx, Transform g, Vector3 at)
        {
            Transform weed = ctx.Group("Weed", g, at, Vector3.zero).transform;
            Material green = ctx.Mats.Flat("WeedGreen", new Color(0.28f, 0.48f, 0.16f), 0.2f, 0f);
            Material dry = ctx.Mats.Flat("WeedDry", new Color(0.55f, 0.5f, 0.25f), 0.2f, 0f);
            int blades = ctx.RangeInt(5, 8);
            for (int i = 0; i < blades; i++)
            {
                float h = ctx.Range(0.08f, 0.2f);
                Vector3 tilt = new Vector3(ctx.Range(-30f, 30f), ctx.Range(0f, 360f), ctx.Range(-30f, 30f));
                Vector3 basePos = ctx.JitterXZ(0.03f);
                // Blades are thin boxes standing on the floor; the centre is half the height up along the tilt.
                Vector3 up = Quaternion.Euler(tilt) * Vector3.up;
                ctx.Box("Blade_" + i, weed, basePos + up * (h * 0.5f), new Vector3(0.012f, h, 0.002f), i == 0 ? dry : green, false, tilt);
            }
        }

        // ── Gum spots ────────────────────────────────────────────────────────

        static void BuildGum(BuildContext ctx, Transform g)
        {
            Material gum = ctx.Mats.Flat("Gum", new Color(0.2f, 0.19f, 0.18f), 0.35f, 0f);
            Material gumPale = ctx.Mats.Flat("GumPale", new Color(0.36f, 0.34f, 0.32f), 0.3f, 0f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = i < 6
                    ? new Vector3(ctx.Range(8.2f, 9.0f), CourtSpec.OverlayY, ctx.Range(-6.5f, 6.5f))          // by the benches
                    : new Vector3(ctx.Range(-9.4f, -8.4f), CourtSpec.OverlayY, ctx.Range(-6f, -2.5f));       // by the gate
                Mesh disc = MeshFactory.Disc(ctx.Range(0.015f, 0.03f), 14, "Gum");
                ctx.MeshObject("Gum_" + i, g, disc, ctx.Chance(0.3f) ? gumPale : gum, p, Vector3.zero, Vector3.one, false, false, true);
            }
        }

        // ── Puddle ───────────────────────────────────────────────────────────

        static void BuildPuddle(BuildContext ctx, Transform g)
        {
            // Same low spot the floor's detail map darkens (south-west), so the two agree.
            Vector3 p = new Vector3(-6.8f, CourtSpec.DecalY, -11.5f);
            Material damp = DirtMaterial(ctx, "DampRing", new Color(0.09f, 0.1f, 0.09f));
            ctx.FloorQuad("DampRing", g, p, new Vector2(1.9f, 1.4f), 25f, damp);
            Material water = ctx.Mats.Get("PuddleWater", () =>
                MatKit.Make("PuddleWater", new Color(0.1f, 0.12f, 0.12f, 0.62f), 0.96f, 0f).Fade());
            Vector3 q = p; q.y = CourtSpec.DecalY + 0.0008f;
            ctx.FloorQuad("Puddle", g, q, new Vector2(1.25f, 0.85f), 25f, water);
        }

        // ── Leaves ───────────────────────────────────────────────────────────

        static void BuildLeaves(BuildContext ctx, Transform g)
        {
            Texture2D atlas = ctx.Tex.Get("leaf_atlas", () => LeafAtlas(ctx.RangeInt(1, 100000)));
            Material mat = ctx.Mats.Get("Leaves", () => MatKit.Make("Leaves", Color.white, 0.2f, 0f).WithAlbedo(atlas, 1f, 1f).Cutout(0.5f));

            var b = new MeshFactory.Builder();
            int count = ctx.RangeInt(45, 60);
            float fx = CourtSpec.FenceHalfX, fz = CourtSpec.FenceHalfZ;
            for (int i = 0; i < count; i++)
            {
                Vector3 p;
                if (ctx.Chance(0.4f))
                    p = new Vector3(ctx.Range(6.5f, fx - 0.2f), 0f, ctx.Range(13.5f, fz - 0.2f));     // blown into the NE corner
                else if (ctx.Chance(0.5f))
                    p = new Vector3((ctx.Chance(0.5f) ? 1f : -1f) * ctx.Range(fx - 1.0f, fx - 0.15f), 0f, ctx.Range(-fz + 0.3f, fz - 0.3f));
                else
                    p = new Vector3(ctx.Range(-fx + 0.3f, fx - 0.3f), 0f, (ctx.Chance(0.5f) ? 1f : -1f) * ctx.Range(fz - 1.0f, fz - 0.15f));
                p.y = CourtSpec.OverlayY;
                AddLeaf(b, p, ctx.Range(0.06f, 0.10f), ctx.Range(0f, 360f), ctx.RangeInt(0, 4));
            }
            ctx.MeshObject("LeafPile", g, b.ToMesh("Leaves"), mat, Vector3.zero, Vector3.zero, Vector3.one, false, false, true);
        }

        /// <summary>One flat leaf quad facing +Y with the UVs of atlas quadrant `variant` (0..3).</summary>
        static void AddLeaf(MeshFactory.Builder b, Vector3 c, float size, float yawDeg, int variant)
        {
            float w = size * 0.55f, h = size * 0.5f;
            Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);
            float u0 = (variant % 2) * 0.5f, v0 = (variant / 2) * 0.5f;
            int a = b.Add(c + rot * new Vector3(-w, 0f, -h), Vector3.up, new Vector2(u0, v0));
            int bb = b.Add(c + rot * new Vector3(w, 0f, -h), Vector3.up, new Vector2(u0 + 0.5f, v0));
            int cc = b.Add(c + rot * new Vector3(w, 0f, h), Vector3.up, new Vector2(u0 + 0.5f, v0 + 0.5f));
            int d = b.Add(c + rot * new Vector3(-w, 0f, h), Vector3.up, new Vector2(u0, v0 + 0.5f));
            b.Quad(a, d, cc, bb);
        }

        /// <summary>2×2 atlas of leaf silhouettes (ellipse with a pointed tip, midrib, veins) in autumn colours.</summary>
        static Texture2D LeafAtlas(int seed)
        {
            Color[] colours =
            {
                new Color(0.55f, 0.32f, 0.12f), new Color(0.72f, 0.5f, 0.15f),
                new Color(0.6f, 0.2f, 0.1f), new Color(0.45f, 0.42f, 0.18f),
            };
            return ProceduralTextures.Create(256, 256, (u, v) =>
            {
                int q = (u < 0.5f ? 0 : 1) + (v < 0.5f ? 0 : 2);
                float lu = Mathf.Repeat(u * 2f, 1f), lv = Mathf.Repeat(v * 2f, 1f);
                // Leaf outline: ellipse that narrows toward the tip (lu → 1).
                float t = Mathf.Clamp01((lu - 0.1f) / 0.85f);
                float halfWidth = 0.34f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * (1f - t * 0.25f) + 0.01f;
                float edgeNoise = (ProceduralTextures.Fbm(lu, lv, 10, 2, seed + q) - 0.5f) * 0.06f;
                float inside = Mathf.Clamp01((halfWidth + edgeNoise - Mathf.Abs(lv - 0.5f)) / 0.012f);
                if (lu < 0.1f || lu > 0.96f) inside = 0f;
                // Midrib and veins.
                float rib = 1f - Mathf.Clamp01((Mathf.Abs(lv - 0.5f) - 0.008f) / 0.006f);
                float veins = 0f;
                for (int k = 1; k <= 4; k++)
                {
                    float vx = 0.2f + k * 0.16f;
                    float d = Mathf.Abs((lv - 0.5f) - (lu - vx) * 0.9f * Mathf.Sign(lv - 0.5f + 0.0001f));
                    if (lu > vx && lu < vx + 0.22f) veins = Mathf.Max(veins, 1f - Mathf.Clamp01((d - 0.004f) / 0.006f));
                }
                float mottle = ProceduralTextures.Fbm(lu, lv, 6, 3, seed + 10 + q);
                Color c = colours[q] * (0.8f + mottle * 0.4f);
                c = Color.Lerp(c, c * 0.55f, Mathf.Max(rib, veins * 0.6f));
                c.a = inside;
                return c;
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "leaf_atlas");
        }

        // ── Rust streaks, ghost line, droppings ──────────────────────────────

        static void BuildRustStreaks(BuildContext ctx, Transform g)
        {
            Material rust = DirtMaterial(ctx, "RustStain", new Color(0.55f, 0.25f, 0.08f));
            Vector3[] at =
            {
                new Vector3(0f, 0f, CourtSpec.PoleZ), new Vector3(0f, 0f, -CourtSpec.PoleZ),
                new Vector3(-CourtSpec.FenceHalfX + 0.25f, 0f, CourtSpec.GateZ - CourtSpec.GateWidth * 0.5f),
                new Vector3(-CourtSpec.FenceHalfX + 0.25f, 0f, CourtSpec.GateZ + CourtSpec.GateWidth * 0.5f),
                new Vector3(CourtSpec.FenceHalfX - 0.2f, 0f, -CourtSpec.FenceHalfZ + 0.2f),
                new Vector3(-CourtSpec.FenceHalfX + 0.2f, 0f, CourtSpec.FenceHalfZ - 0.2f),
            };
            for (int i = 0; i < at.Length; i++)
            {
                Vector3 p = at[i] + ctx.JitterXZ(0.05f); p.y = CourtSpec.DecalY;
                float s = ctx.Range(0.3f, 0.55f);
                ctx.FloorQuad("Rust_" + i, g, p, new Vector2(s, s * ctx.Range(0.8f, 1.3f)), ctx.Range(0f, 360f), rust);
            }
        }

        /// <summary>Faint grey outline 0.3 m outside the +Z key: the court was re-lined once.</summary>
        static void BuildGhostLine(BuildContext ctx, Transform g)
        {
            Texture2D tex = ctx.Tex.Get("ghost_paint", () => ProceduralTextures.Create(256, 32, (u, v) =>
            {
                float n = ProceduralTextures.Fbm(u, v * 0.125f, 32, 3, 55);
                return new Color(0.6f, 0.6f, 0.58f, n);
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "ghost_paint"));
            Material mat = ctx.Mats.Get("GhostPaint", () => MatKit.Make("GhostPaint", Color.white, 0.2f, 0f).WithAlbedo(tex, 0.5f, 1f).Cutout(0.58f));

            float y = CourtSpec.DecalY;
            float x = CourtSpec.KeyWidth * 0.5f + 0.3f;                                   // 2.75
            float zEnd = CourtSpec.HalfLength - CourtSpec.LineWidth;                       // 13.95
            float zTop = zEnd - CourtSpec.FreeThrowLineDist - 0.3f;                        // 7.85
            var b = new MeshFactory.Builder();
            MeshFactory.AppendSegment(b, new Vector3(-x, y, zEnd), new Vector3(-x, y, zTop), CourtSpec.LineWidth, 0.025f);
            MeshFactory.AppendSegment(b, new Vector3(x, y, zEnd), new Vector3(x, y, zTop), CourtSpec.LineWidth, 0.025f);
            MeshFactory.AppendSegment(b, new Vector3(-x, y, zTop), new Vector3(x, y, zTop), CourtSpec.LineWidth, 0f);
            ctx.MeshObject("GhostKey", g, b.ToMesh("GhostKey"), mat, Vector3.zero, Vector3.zero, Vector3.one, false, false, true);
        }

        static void BuildDroppings(BuildContext ctx, Transform g)
        {
            Texture2D tex = ctx.Tex.Get("dropping", () => ProceduralTextures.Create(64, 64, (u, v) =>
            {
                float blob = ProceduralTextures.SoftBlob(u, v, 0.5f, 0.5f, 0.42f, 1.0f, 66);
                float core = ProceduralTextures.SoftBlob(u, v, 0.52f, 0.48f, 0.15f, 0.6f, 67);
                float shade = Mathf.Lerp(0.92f, 0.45f, core);
                return new Color(shade, shade, shade * 0.95f, Mathf.Clamp01(blob * 1.5f));
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 2, "dropping"));
            Material mat = ctx.Mats.Get("Dropping", () => MatKit.Make("Dropping", Color.white, 0.3f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.4f));

            // On the bench seats (seat top 0.45; seats run ±0.9 along Z at x ≈ 8.9..9.3).
            for (int i = 0; i < 4; i++)
            {
                int bench = i % CourtSpec.BenchEastZ.Length;
                Vector3 p = new Vector3(CourtSpec.BenchEastX + ctx.Range(-0.18f, 0.18f), 0.451f, CourtSpec.BenchEastZ[bench] + ctx.Range(-0.8f, 0.8f));
                float s = ctx.Range(0.04f, 0.08f);
                ctx.FloorQuad("Dropping_" + i, g, p, new Vector2(s, s * 0.8f), ctx.Range(0f, 360f), mat);
            }
            // One on the top edge of the +Z backboard (top face at y 3.95, z 12.80..12.83).
            float topY = CourtSpec.BackboardBottom + CourtSpec.BackboardHeight;
            ctx.Box("Dropping_Board", g, new Vector3(0.3f, topY + 0.003f, CourtSpec.BackboardZ + CourtSpec.BackboardThick * 0.5f),
                new Vector3(0.035f, 0.006f, 0.028f), ctx.Mats.Flat("DroppingWhite", new Color(0.9f, 0.9f, 0.86f), 0.3f, 0f), false);
        }
    }
}
