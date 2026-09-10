using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// FenceModule – the 4 m chain-link ("iron") fence that encloses the court.
    /// <para>
    /// WHAT IT BUILDS (fence line |x| = 10, |z| = 18, from CourtSpec):
    ///  • Galvanised steel posts (corner posts thicker) every ~3 m, sunk 5 cm into the slab, with dome caps.
    ///  • A top rail along every side, a thin tension wire near the ground.
    ///  • One two-sided mesh panel between each pair of posts, textured with a tileable chain-link
    ///    pattern whose diamonds are real-world size (7 cm pitch). Panels get a BoxCollider so a ball
    ///    bounces off the fence later.
    ///  • A gate in the west fence (z = −5, 1.2 m wide) hinged on its north post and left 25° open,
    ///    with a tube frame, diagonal brace, hinges, latch and its own mesh; fence mesh continues above it.
    ///  • A small white sign zip-tied to the fence beside the gate.
    /// HOW IT LOOKS REAL
    ///  • Panels are tinted individually (some browner/darker from rust and dirt); the two posts flanking
    ///    the gate use the rusty-iron look.
    ///  • One east panel bulges outward 25 cm (someone ran into it), another sags at the top.
    ///  • Post spacing is computed per side so the corners always land on posts.
    /// </para>
    /// </summary>
    public static class FenceModule
    {
        // Layout numbers that are not rule-book values.
        const float PostSink       = 0.05f;   // posts go this far into the slab
        const float PostTop        = 4.05f;   // top of the posts (fence height 4.0 + a little)
        const float CornerPostR    = 0.045f;
        const float LinePostR      = 0.030f;
        const float GatePostR      = 0.040f;
        const float TopRailR       = 0.021f;
        const float TopRailY       = 3.98f;
        const float WireR          = 0.004f;
        const float WireY          = 0.08f;
        const float MeshBottom     = 0.10f;
        const float MeshHeight     = 3.85f;   // mesh spans y 0.10 → 3.95, just under the top rail
        const float MeshThickness  = 0.02f;   // box collider thickness
        const float DoorHeight     = 2.1f;    // gate door: y 0.10 → 2.20
        const float DoorOpenDeg    = 25f;     // how far the gate is swung open (toward −X, outside)
        const float DiamondPitch   = 0.07f;   // chain-link diamond width (5 cm mesh measured between wires)
        const int   TileDiamonds   = 2;       // diamonds across one texture repeat
        const int   TintVariants   = 4;       // how many differently weathered panel materials

        // One repeat of the chain-link texture covers TileDiamonds diamonds.
        static float MeshUvPerMeter { get { return 1f / (DiamondPitch * TileDiamonds); } }

        // ── Entry point ──────────────────────────────────────────────────────

        public static void Build(BuildContext ctx, Transform parent)
        {
            float hx = CourtSpec.FenceHalfX, hz = CourtSpec.FenceHalfZ;
            float gateS = CourtSpec.GateZ - CourtSpec.GateWidth * 0.5f;   // −5.6 (south gate post)
            float gateN = CourtSpec.GateZ + CourtSpec.GateWidth * 0.5f;   // −4.4 (north gate post)

            Material[] meshMats = MeshMaterials(ctx);

            // East side (x = +10), running south → north. Panel index 1 gets the dent, 6 the sag.
            var east = new RunOptions { dentPanel = 1, sagPanel = 6, outward = Vector3.right };
            BuildRun(ctx, ctx.Group("East", parent).transform, new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), true, true, meshMats, east);

            // North side (z = +18), west → east, and south side (z = −18), east → west (corners already have posts).
            BuildRun(ctx, ctx.Group("North", parent).transform, new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), true, false,
                meshMats, new RunOptions { outward = Vector3.forward });
            BuildRun(ctx, ctx.Group("South", parent).transform, new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), false, true,
                meshMats, new RunOptions { outward = Vector3.back });

            // West side (x = −10) is two runs with the gate opening between them.
            Transform west = ctx.Group("West", parent).transform;
            BuildRun(ctx, west, new Vector3(-hx, 0, hz), new Vector3(-hx, 0, gateN), false, true,
                meshMats, new RunOptions { outward = Vector3.left, endPostRadius = GatePostR, endPostRusty = true });
            BuildRun(ctx, west, new Vector3(-hx, 0, gateS), new Vector3(-hx, 0, -hz), true, false,
                meshMats, new RunOptions { outward = Vector3.left, startPostRadius = GatePostR, startPostRusty = true });

            BuildGate(ctx, ctx.Group("Gate", parent).transform, meshMats[0]);
            BuildSign(ctx, ctx.Group("Sign", parent).transform);
        }

        // ── One straight run of fence ─────────────────────────────────────────

        sealed class RunOptions
        {
            public Vector3 outward = Vector3.right;   // which way is "outside the court" for this run
            public int dentPanel = -1;                // index of the panel that bulges outward
            public int sagPanel = -1;                 // index of the panel whose top sags
            public float startPostRadius = 0f;        // 0 = default (corner or line post)
            public float endPostRadius = 0f;
            public bool startPostRusty, endPostRusty;
        }

        /// <summary>
        /// Posts, rail, wire and mesh panels from `a` to `b`. `postAtStart/End` decide whether this run
        /// owns the post at each end (shared corner posts are built once).
        /// </summary>
        static void BuildRun(BuildContext ctx, Transform g, Vector3 a, Vector3 b, bool postAtStart, bool postAtEnd,
            Material[] meshMats, RunOptions opt)
        {
            Vector3 dir = (b - a).normalized;
            float length = Vector3.Distance(a, b);
            int spans = Mathf.Max(1, Mathf.CeilToInt(length / CourtSpec.FencePostSpacing));
            float span = length / spans;
            float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;   // Euler(0,yaw,0) * right == dir

            // Posts (with caps).
            for (int i = 0; i <= spans; i++)
            {
                if (i == 0 && !postAtStart) continue;
                if (i == spans && !postAtEnd) continue;
                Vector3 p = a + dir * (span * i);
                bool corner = (i == 0 || i == spans);
                float r = corner ? CornerPostR : LinePostR;
                if (i == 0 && opt.startPostRadius > 0f) r = opt.startPostRadius;
                if (i == spans && opt.endPostRadius > 0f) r = opt.endPostRadius;
                bool rusty = (i == 0 && opt.startPostRusty) || (i == spans && opt.endPostRusty);
                BuildPost(ctx, g, "Post_" + i, p, r, rusty ? ctx.Mats.RustyIron : ctx.Mats.Galvanized);
            }

            // Top rail and tension wire for the whole run.
            ctx.Tube("TopRail", g, a + Vector3.up * TopRailY, b + Vector3.up * TopRailY, TopRailR, ctx.Mats.Galvanized, false);
            ctx.Tube("TensionWire", g, a + Vector3.up * WireY, b + Vector3.up * WireY, WireR, ctx.Mats.Galvanized, false, false);

            // One mesh panel per span.
            for (int i = 0; i < spans; i++)
            {
                Vector3 center = a + dir * (span * (i + 0.5f)) + Vector3.up * (MeshBottom + MeshHeight * 0.5f);
                float w = span - LinePostR * 2f;
                Mesh mesh = MeshFactory.Panel(w, MeshHeight, 6, 6, MeshUvPerMeter, true, "FencePanel");

                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                // Which local Z direction points outside the court (needed for the dent).
                float outSign = Mathf.Sign(Vector3.Dot(rot * Vector3.forward, opt.outward));
                if (i == opt.dentPanel) Dent(mesh, w, outSign);
                if (i == opt.sagPanel) Sag(mesh, w);

                Material mat = meshMats[(i + g.GetSiblingIndex() * 3) % meshMats.Length];
                GameObject panel = ctx.MeshObject("MeshPanel_" + i, g, mesh, mat, center, new Vector3(0f, yaw, 0f), Vector3.one, false);
                var col = panel.AddComponent<BoxCollider>();
                col.size = new Vector3(w, MeshHeight, MeshThickness);
            }
        }

        static void BuildPost(BuildContext ctx, Transform g, string name, Vector3 footPos, float radius, Material mat)
        {
            Vector3 foot = footPos + Vector3.down * PostSink;
            ctx.Post(name, g, foot, PostTop + PostSink, radius, mat);
            // Dome cap: a sphere a touch wider than the post, half sunk into its top.
            ctx.Sphere(name + "_Cap", g, footPos + Vector3.up * (PostTop - 0.005f), radius * 2f + 0.02f, mat, false);
        }

        /// <summary>Gaussian bulge in the middle-low part of the panel, pushed toward the outside.</summary>
        static void Dent(Mesh mesh, float width, float outSign)
        {
            MeshFactory.Displace(mesh, v =>
            {
                float nx = v.x / (width * 0.35f);
                float ny = (v.y + 0.6f) / 1.0f;
                float bulge = 0.25f * Mathf.Exp(-(nx * nx + ny * ny));
                return new Vector3(0f, 0f, bulge * outSign);
            });
        }

        /// <summary>The top edge of the mesh droops a few centimetres in the middle of the span.</summary>
        static void Sag(Mesh mesh, float width)
        {
            float halfH = MeshHeight * 0.5f;
            MeshFactory.Displace(mesh, v =>
            {
                float t = Mathf.Clamp01((v.y + halfH) / MeshHeight);          // 0 bottom → 1 top
                float across = 1f - Mathf.Clamp01(Mathf.Abs(v.x) / (width * 0.5f));
                return new Vector3(0f, -0.04f * t * t * across, 0.03f * t * across);
            });
        }

        // ── Gate ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Door hinged on the north gate post, swung open toward −X. Built in a group whose origin is the
        /// hinge; the door runs along local −Z, so a positive yaw swings its free end outward.
        /// </summary>
        static void BuildGate(BuildContext ctx, Transform g, Material meshMat)
        {
            float x = -CourtSpec.FenceHalfX;
            float gateN = CourtSpec.GateZ + CourtSpec.GateWidth * 0.5f;
            float doorW = CourtSpec.GateWidth - GatePostR * 2f - 0.10f;   // 1.02: fits between the posts with play
            float y0 = MeshBottom, y1 = MeshBottom + DoorHeight;    // 0.10 → 2.20

            // Hinge axis 6 cm south of the north gate post's centre (2 cm outside its surface).
            GameObject door = ctx.Group("Door", g, new Vector3(x, 0f, gateN - GatePostR - 0.02f), new Vector3(0f, DoorOpenDeg, 0f));
            Transform d = door.transform;
            Material steel = ctx.Mats.Galvanized;
            float zA = -0.02f, zB = zA - doorW;              // hinge side → latch side (local −Z)
            float fr = 0.02f;

            // Tube frame + diagonal brace.
            ctx.Tube("Frame_Hinge", d, new Vector3(0, y0, zA), new Vector3(0, y1, zA), fr, steel, false);
            ctx.Tube("Frame_Latch", d, new Vector3(0, y0, zB), new Vector3(0, y1, zB), fr, steel, false);
            ctx.Tube("Frame_Top", d, new Vector3(0, y1, zA), new Vector3(0, y1, zB), fr, steel, false);
            ctx.Tube("Frame_Bottom", d, new Vector3(0, y0, zA), new Vector3(0, y0, zB), fr, steel, false);
            ctx.Tube("Frame_Brace", d, new Vector3(0, y0 + 0.02f, zA), new Vector3(0, y1 - 0.02f, zB), fr * 0.8f, steel, false);

            // Door mesh (runs along local Z, so yaw −90 puts the panel's width along Z).
            Mesh mesh = MeshFactory.Panel(doorW - fr * 2f, DoorHeight - fr * 2f, 3, 4, MeshUvPerMeter, true, "GatePanel");
            GameObject panel = ctx.MeshObject("DoorMesh", d, mesh, meshMat, new Vector3(0f, (y0 + y1) * 0.5f, (zA + zB) * 0.5f),
                new Vector3(0f, -90f, 0f), Vector3.one, false);
            var col = panel.AddComponent<BoxCollider>();
            col.size = new Vector3(doorW, DoorHeight, MeshThickness);

            // Hinges (dark steel barrels on the post side) and a latch box on the free end.
            Material dark = ctx.Mats.Flat("DarkSteel", new Color(0.25f, 0.25f, 0.26f), 0.45f, 0.8f);
            ctx.Tube("Hinge_Low", d, new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.57f, 0f), 0.028f, dark, false);
            ctx.Tube("Hinge_High", d, new Vector3(0f, 1.73f, 0f), new Vector3(0f, 1.85f, 0f), 0.028f, dark, false);
            ctx.Box("Latch", d, new Vector3(0f, 1.05f, zB - 0.01f), new Vector3(0.05f, 0.14f, 0.06f), dark, false);

            // Fence mesh above the door so the fence line stays continuous up to the rail.
            float aboveH = MeshBottom + MeshHeight - y1;   // 3.95 − 2.20 = 1.75
            Mesh above = MeshFactory.Panel(CourtSpec.GateWidth, aboveH, 2, 3, MeshUvPerMeter, true, "GateTopPanel");
            GameObject top = ctx.MeshObject("MeshAboveGate", g, above, meshMat,
                new Vector3(x, y1 + aboveH * 0.5f, CourtSpec.GateZ), new Vector3(0f, -90f, 0f), Vector3.one, false);
            var topCol = top.AddComponent<BoxCollider>();
            topCol.size = new Vector3(CourtSpec.GateWidth, aboveH, MeshThickness);
            // The top rail continues over the opening, and a horizontal bar under the mesh spans the two gate posts.
            ctx.Tube("TopRailOverGate", g, new Vector3(x, TopRailY, CourtSpec.GateZ - CourtSpec.GateWidth * 0.5f),
                new Vector3(x, TopRailY, CourtSpec.GateZ + CourtSpec.GateWidth * 0.5f), TopRailR, steel, false);
            ctx.Tube("GateHeadRail", g, new Vector3(x, y1 + 0.01f, CourtSpec.GateZ - CourtSpec.GateWidth * 0.5f),
                new Vector3(x, y1 + 0.01f, CourtSpec.GateZ + CourtSpec.GateWidth * 0.5f), 0.018f, steel, false);
        }

        // ── Sign ─────────────────────────────────────────────────────────────

        /// <summary>White sign with a black border on the outside of the west fence, north of the gate.</summary>
        static void BuildSign(BuildContext ctx, Transform g)
        {
            float x = -CourtSpec.FenceHalfX - 0.02f;          // just outside the mesh plane
            float z = CourtSpec.GateZ + CourtSpec.GateWidth * 0.5f + 0.55f;   // −3.85
            float y = 1.8f;
            // Everything hangs in one group tilted 1.5° so the board and its border stay aligned.
            Transform s = ctx.Group("SignBoard", g, new Vector3(x, y, z), new Vector3(0f, 0f, 1.5f)).transform;
            Material white = ctx.Mats.Get("SignWhite", () => MatKit.Make("SignWhite", new Color(0.9f, 0.9f, 0.86f), 0.4f, 0f));
            ctx.Box("Board", s, Vector3.zero, new Vector3(0.01f, 0.35f, 0.5f), white, false);

            // Black border: thin strips 1 mm proud of the outer (−X) face.
            float bx = -0.006f;
            Material black = ctx.Mats.BlackPaint;
            ctx.Box("Border_Top", s, new Vector3(bx, 0.16f, 0f), new Vector3(0.002f, 0.03f, 0.5f), black, false);
            ctx.Box("Border_Bottom", s, new Vector3(bx, -0.16f, 0f), new Vector3(0.002f, 0.03f, 0.5f), black, false);
            ctx.Box("Border_N", s, new Vector3(bx, 0f, 0.235f), new Vector3(0.002f, 0.35f, 0.03f), black, false);
            ctx.Box("Border_S", s, new Vector3(bx, 0f, -0.235f), new Vector3(0.002f, 0.35f, 0.03f), black, false);

            // Two cable ties hold it to the mesh (they wrap over the top edge toward the fence).
            Material tie = ctx.Mats.Flat("CableTie", new Color(0.1f, 0.1f, 0.1f), 0.3f, 0f);
            ctx.Box("Tie_A", s, new Vector3(0.008f, 0.16f, -0.2f), new Vector3(0.03f, 0.006f, 0.02f), tie, false);
            ctx.Box("Tie_B", s, new Vector3(0.008f, 0.16f, 0.2f), new Vector3(0.03f, 0.006f, 0.02f), tie, false);
        }

        // ── Materials ────────────────────────────────────────────────────────

        /// <summary>A few weathered variants of the chain-link material (same texture, different tints).</summary>
        static Material[] MeshMaterials(BuildContext ctx)
        {
            Texture2D tex = ctx.Tex.Get("chainlink_albedo", () => ChainLinkTexture(ctx.RangeInt(1, 100000)));
            var mats = new Material[TintVariants];
            for (int i = 0; i < TintVariants; i++)
            {
                int idx = i;
                mats[i] = ctx.Mats.Get("ChainLink_" + idx, () =>
                {
                    // 0: clean galvanised, 1: slightly dull, 2: dusty brown, 3: rust-tinted.
                    Color tint = idx == 0 ? Color.white
                               : idx == 1 ? new Color(0.9f, 0.9f, 0.9f)
                               : idx == 2 ? new Color(0.85f, 0.8f, 0.72f)
                               : new Color(0.8f, 0.68f, 0.55f);
                    return MatKit.Make("ChainLink_" + idx, tint, 0.5f, 0.6f)
                        .WithAlbedo(tex, 1f, 1f)
                        .Cutout(0.4f);
                });
            }
            return mats;
        }

        /// <summary>
        /// 256² tileable chain-link texture: two families of diagonal wires forming diamonds
        /// (TileDiamonds across the tile). Alpha = wire mask with a 1 px soft edge; RGB = light grey
        /// galvanised with speckle, a little darker where wires cross.
        /// </summary>
        static Texture2D ChainLinkTexture(int seed)
        {
            const int size = 256;
            const float wireHalf = 0.012f;   // in tile units (~3 mm wire on a 14 cm tile)
            const float edge = 1.2f / size;
            return ProceduralTextures.Create(size, size, (u, v) =>
            {
                float n = TileDiamonds;
                float sa = Mathf.Repeat((u + v) * n, 1f); float da = Mathf.Min(sa, 1f - sa) / n * 0.7071f;
                float sb = Mathf.Repeat((u - v) * n, 1f); float db = Mathf.Min(sb, 1f - sb) / n * 0.7071f;
                float dist = Mathf.Min(da, db);
                float alpha = 1f - Mathf.Clamp01((dist - wireHalf) / edge);
                float cross = Mathf.Clamp01(1f - Mathf.Max(da, db) / (wireHalf * 2.5f));   // both wires close → a knot

                float speck = ProceduralTextures.Fbm(u, v, 32, 2, seed);
                float g = 0.74f + (speck - 0.5f) * 0.16f - cross * 0.12f;
                // Simple round-wire shading: brighter along the wire centre.
                g += (1f - Mathf.Clamp01(dist / wireHalf)) * 0.08f;
                return new Color(g, g, g * 1.02f, alpha);
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 8, "chainlink_albedo");
        }
    }
}
