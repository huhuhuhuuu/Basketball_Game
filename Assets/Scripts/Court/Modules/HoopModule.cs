using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// HoopModule – builds the two outdoor basketball goals.
    /// <para>
    /// One hoop is designed in a LOCAL frame where +Z points "toward the endline, away from the
    /// court centre". It is built twice: hoop A at the +Z end (frame == world), hoop B at the −Z
    /// end by turning the frame 180° about Y (and leaning it 0.6° about Z so the two are not clones).
    /// All Z positions therefore come straight from CourtSpec (BackboardZ, BasketZ, PoleZ).
    /// </para>
    /// <para>
    /// What each hoop contains, bottom to top:
    ///  • Ground plate with 4 bolt heads (bottom flat on the slab at y 0).
    ///  • Galvanised 4.5" pole, a gooseneck pipe swept along a smooth arc (MeshFactory.AppendTube),
    ///    a diagonal brace pipe and a clamp where the arm meets the board.
    ///  • Backboard 1.8 × 1.05 × 0.03 with a procedural off-white albedo: grey grime streaks running
    ///    down from the top edge, dirt in the lower corners and faint orange ball scuffs near the square.
    ///  • The crisp BLACK shooter's square and a black border, made of thin boxes 2 mm proud of the
    ///    face so they never z-fight with the board.
    ///  • Steel H-frame and two angled brackets on the back of the board.
    ///  • Orange rim (torus + MeshCollider) hanging from a mounting plate with bolts, a bracket and
    ///    two struts; the ring is tilted 1° so it looks a little bent from dunks.
    ///  • A 12-loop nylon net as ONE mesh of thin tubes in the classic diamond lattice, with 2 mm
    ///    per-node jitter. Hoop B's net is greyer and has one torn, dangling strand.
    /// </para>
    /// </summary>
    public static class HoopModule
    {
        // ── Layout numbers that are NOT rule-book values (rule-book values live in CourtSpec) ──
        const float PoleHeight = 3.7f;    // straight pole from y 0 to 3.7
        const float ArcRise    = 0.35f;   // the gooseneck climbs from 3.7 to 4.05 …
        const float ArcEndZ    = 13.15f;  // … while reaching forward to z 13.15
        const float ArmEndY    = 3.95f;   // the straight arm lands at the board's top edge …
        const float ArmEndZ    = 12.85f;  // … just behind the board's back face
        const float BarSize    = 0.04f;   // H-frame bars are 4 cm square
        const float PaintThick = 0.004f;  // black paint strips on the board face
        const float PaintProud = 0.002f;  // gap between the face and those strips

        // Net lattice: how far each row of knots hangs below the ring, and its radius.
        static readonly float[] NetDrop  = { 0f, 0.10f, 0.20f, 0.30f, 0.40f };
        static readonly float[] NetRadii = { 0.228f, 0.20f, 0.17f, 0.145f, 0.13f };   // row 0 starts inside the ring tube
        const int   NetLoops  = 12;
        const float NetCordR  = 0.004f;

        // ── Entry point ──────────────────────────────────────────────────────

        public static void Build(BuildContext ctx, Transform parent)
        {
            // Both rims share one ring mesh (registered once, used twice).
            Mesh ring = MeshFactory.Torus(CourtSpec.RimRadius + CourtSpec.RimRodRadius,
                CourtSpec.RimRodRadius, 48, 12, "RimTorus");
            ctx.Register(ring);

            // Hoop A: the +Z end. Its local frame is exactly the world frame.
            GameObject hoopA = ctx.Group("Hoop_A_PlusZ", parent, Vector3.zero, Vector3.zero);
            BuildHoop(ctx, hoopA.transform, ring, 0);

            // Hoop B: the −Z end. Turn the frame 180° about Y so local +Z points to world −Z,
            // and lean it 0.6° about Z so the two poles are not perfect copies.
            GameObject hoopB = ctx.Group("Hoop_B_MinusZ", parent, Vector3.zero, new Vector3(0f, 180f, 0.6f));
            BuildHoop(ctx, hoopB.transform, ring, 1);
        }

        /// <summary>Builds one complete goal in the given local frame. `index` = 0 (A) or 1 (B).</summary>
        static void BuildHoop(BuildContext ctx, Transform root, Mesh ring, int index)
        {
            Material steel = PoleSteel(ctx, index);
            BuildPole(ctx, ctx.Group("Pole", root).transform, steel);
            BuildBackboard(ctx, ctx.Group("Backboard", root).transform, steel, index);
            BuildRim(ctx, ctx.Group("Rim", root).transform, ring, index);
        }

        // ── Pole, gooseneck, brace ───────────────────────────────────────────

        static void BuildPole(BuildContext ctx, Transform g, Material steel)
        {
            float pz = CourtSpec.PoleZ;           // 14.8
            Material bolt = BoltMaterial(ctx);

            // Ground plate: 35 cm square, 2 cm thick, bottom flat on the slab (y 0 → 0.02).
            ctx.Box("BasePlate", g, new Vector3(0f, 0.01f, pz), new Vector3(0.35f, 0.02f, 0.35f), steel);

            // Four bolt heads standing on the plate (Ø 3 cm, 1.2 cm tall).
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sz = (i < 2) ? -1f : 1f;
                ctx.Post("BaseBolt_" + i, g, new Vector3(sx * 0.13f, 0.02f, pz + sz * 0.13f),
                    0.012f, 0.015f, bolt, false);
            }

            // The straight pole.
            ctx.Post("Pole", g, new Vector3(0f, 0f, pz), PoleHeight, CourtSpec.PoleRadius, steel);

            // The gooseneck: one swept pipe, same radius as the pole, starting inside the pole top.
            var b = new MeshFactory.Builder();
            MeshFactory.AppendTube(b, GooseneckPath(), CourtSpec.PoleRadius, 16, true, 1f);
            ctx.MeshObject("Gooseneck", g, b.ToMesh("Gooseneck"), steel,
                Vector3.zero, Vector3.zero, Vector3.one, true);

            // Diagonal brace from the pole (y 3.0) up to the underside of the arm.
            ctx.Tube("Brace", g, new Vector3(0f, 3.0f, pz), new Vector3(0f, 4.02f, 13.25f), 0.02f, steel);

            // Clamp block that grabs the arm end behind the board (hides the pipe's end cap).
            float clampZ = CourtSpec.BackboardZ + CourtSpec.BackboardThick + 0.04f;   // 12.87
            ctx.Box("ArmClamp", g, new Vector3(0f, 3.93f, clampZ), new Vector3(0.14f, 0.12f, 0.06f), steel, false);
        }

        /// <summary>
        /// Path of the bent pipe: a short straight bit hidden inside the pole, a quarter of an
        /// ellipse (rises 0.35 m while reaching 1.65 m forward) and a straight arm to the board.
        /// </summary>
        static List<Vector3> GooseneckPath()
        {
            var pts = new List<Vector3>();
            float pz = CourtSpec.PoleZ;
            float reach = pz - ArcEndZ;                     // 1.65 m forward (toward −Z)

            pts.Add(new Vector3(0f, PoleHeight - 0.10f, pz)); // buried inside the pole top

            const int arcSteps = 9;                         // 10 points on the bend
            for (int i = 0; i <= arcSteps; i++)
            {
                float t = (float)i / arcSteps * Mathf.PI * 0.5f;   // 0 → 90°
                float y = PoleHeight + Mathf.Sin(t) * ArcRise;      // 3.7 → 4.05
                float z = pz - (1f - Mathf.Cos(t)) * reach;        // 14.8 → 13.15
                pts.Add(new Vector3(0f, y, z));
            }

            pts.Add(new Vector3(0f, ArmEndY, ArmEndZ));       // straight arm to (0, 3.95, 12.85)
            return pts;
        }

        // ── Backboard ────────────────────────────────────────────────────────

        static void BuildBackboard(BuildContext ctx, Transform g, Material steel, int index)
        {
            float centreY = CourtSpec.BackboardBottom + CourtSpec.BackboardHeight * 0.5f;   // 3.425
            float centreZ = CourtSpec.BackboardZ + CourtSpec.BackboardThick * 0.5f;         // 12.815

            ctx.Box("Board", g, new Vector3(0f, centreY, centreZ),
                new Vector3(CourtSpec.BackboardWidth, CourtSpec.BackboardHeight, CourtSpec.BackboardThick),
                BoardMaterial(ctx, index));

            BuildShooterSquare(ctx, g);
            BuildBorder(ctx, g);
            BuildBackFrame(ctx, g, steel);
        }

        /// <summary>One thin black strip lying on the front face (2 mm proud of it, no collider).</summary>
        static void PaintStrip(BuildContext ctx, Transform g, string name, float cx, float cy, float w, float h)
        {
            float z = CourtSpec.BackboardZ - PaintProud - PaintThick * 0.5f;   // 12.796
            ctx.Box(name, g, new Vector3(cx, cy, z), new Vector3(w, h, PaintThick), ctx.Mats.BlackPaint, false);
        }

        /// <summary>The black shooter's square: 0.59 × 0.45 outer, 5 cm stroke, bottom edge at rim height.</summary>
        static void BuildShooterSquare(BuildContext ctx, Transform g)
        {
            float lw = CourtSpec.LineWidth;              // 0.05 stroke
            float w  = CourtSpec.ShooterSquareW;          // 0.59
            float h  = CourtSpec.ShooterSquareH;          // 0.45
            float bottom = CourtSpec.RimHeight;           // 3.05

            PaintStrip(ctx, g, "Square_Bottom", 0f, bottom + lw * 0.5f, w, lw);
            PaintStrip(ctx, g, "Square_Top",    0f, bottom + h - lw * 0.5f, w, lw);

            float sideX = w * 0.5f - lw * 0.5f;           // 0.27
            float sideY = bottom + h * 0.5f;              // 3.275
            float sideH = h - 2f * lw;                    // 0.35 (between the two bars)
            PaintStrip(ctx, g, "Square_Left",  -sideX, sideY, lw, sideH);
            PaintStrip(ctx, g, "Square_Right",  sideX, sideY, lw, sideH);
        }

        /// <summary>Black 5 cm border around the edge of the board face.</summary>
        static void BuildBorder(BuildContext ctx, Transform g)
        {
            float bw = CourtSpec.BackboardBorder;         // 0.05
            float w  = CourtSpec.BackboardWidth;          // 1.8
            float h  = CourtSpec.BackboardHeight;         // 1.05
            float bottom = CourtSpec.BackboardBottom;     // 2.9
            float top = bottom + h;                       // 3.95

            PaintStrip(ctx, g, "Border_Top",    0f, top - bw * 0.5f, w, bw);
            PaintStrip(ctx, g, "Border_Bottom", 0f, bottom + bw * 0.5f, w, bw);

            float sideX = w * 0.5f - bw * 0.5f;           // 0.875
            float sideY = bottom + h * 0.5f;              // 3.425
            float sideH = h - 2f * bw;                    // 0.95
            PaintStrip(ctx, g, "Border_Left",  -sideX, sideY, bw, sideH);
            PaintStrip(ctx, g, "Border_Right",  sideX, sideY, bw, sideH);
        }

        /// <summary>Steel H-frame on the back of the board plus two angled brackets up to the arm.</summary>
        static void BuildBackFrame(BuildContext ctx, Transform g, Material steel)
        {
            float z = CourtSpec.BackboardZ + CourtSpec.BackboardThick + BarSize * 0.5f;   // 12.85
            float lowY  = CourtSpec.BackboardBottom + 0.05f;                              // 2.95
            float highY = CourtSpec.BackboardBottom + CourtSpec.BackboardHeight - 0.05f;  // 3.90
            float vx = 0.55f;

            // Two verticals.
            Vector3 vSize = new Vector3(BarSize, highY - lowY, BarSize);
            float vy = (lowY + highY) * 0.5f;
            ctx.Box("Frame_Left",  g, new Vector3(-vx, vy, z), vSize, steel, false);
            ctx.Box("Frame_Right", g, new Vector3( vx, vy, z), vSize, steel, false);

            // Two horizontals: one near the rim height, one just under the arm end.
            Vector3 hSize = new Vector3(1.2f, BarSize, BarSize);
            ctx.Box("Frame_Lower", g, new Vector3(0f, 3.00f, z), hSize, steel, false);
            ctx.Box("Frame_Upper", g, new Vector3(0f, 3.88f, z), hSize, steel, false);

            // Angled brackets: from the arm pipe down and out to the lower bar.
            Vector3 armPoint = new Vector3(0f, 4.03f, 13.25f);   // inside the arm pipe
            ctx.Tube("ArmBracket_L", g, armPoint, new Vector3(-0.50f, 3.02f, z), 0.015f, steel, false);
            ctx.Tube("ArmBracket_R", g, armPoint, new Vector3( 0.50f, 3.02f, z), 0.015f, steel, false);
        }

        // ── Rim and net ──────────────────────────────────────────────────────

        static void BuildRim(BuildContext ctx, Transform g, Mesh ring, int index)
        {
            float faceZ = CourtSpec.BackboardZ;           // 12.8
            Material orange = ctx.Mats.RimOrange;
            Material bolt = BoltMaterial(ctx);

            // Mounting plate flush on the board face (z 12.78 → 12.80), centred a little above rim height.
            float plateY = CourtSpec.RimHeight + 0.01f;   // 3.06 (spans 3.00 → 3.12)
            ctx.Box("RimPlate", g, new Vector3(0f, plateY, faceZ - 0.01f), new Vector3(0.12f, 0.12f, 0.02f), orange, false);

            // Four bolt heads poking out of the plate front.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                Vector3 a = new Vector3(sx * 0.04f, plateY + sy * 0.04f, faceZ - 0.019f);
                Vector3 b = new Vector3(sx * 0.04f, plateY + sy * 0.04f, faceZ - 0.028f);
                ctx.Tube("RimBolt_" + i, g, a, b, 0.008f, bolt, false);
            }

            // Bracket from the plate out to the back of the ring (z 12.79 → 12.65), just under the tube.
            float bz0 = faceZ - 0.01f;
            float bz1 = CourtSpec.BasketZ + CourtSpec.RimRadius;   // 12.65
            ctx.Box("RimBracket", g, new Vector3(0f, CourtSpec.RimCenterY - 0.016f, (bz0 + bz1) * 0.5f),
                new Vector3(0.10f, 0.03f, bz0 - bz1), orange, false);

            // Two thin struts from the plate bottom to the underside of the ring, 45° either side.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = new Vector3(s * 0.045f, 3.005f, faceZ - 0.015f);
                Vector3 b = new Vector3(s * 0.16f, CourtSpec.RimCenterY - 0.01f, CourtSpec.BasketZ + 0.165f);
                ctx.Tube(s < 0 ? "RimStrut_L" : "RimStrut_R", g, a, b, 0.008f, orange, false);
            }

            // Pivot at the ring centre, tilted about X. A negative angle makes the FRONT of the ring
            // (toward the court, local −Z) dip a little – a rim that has taken a few dunks.
            float tilt = (index == 0) ? -1.2f : -0.8f;
            GameObject pivot = ctx.Group("RimPivot", g,
                new Vector3(0f, CourtSpec.RimCenterY, CourtSpec.BasketZ), new Vector3(tilt, 0f, 0f));

            // The ring itself, with a non-convex MeshCollider so a ball can bounce off it.
            ctx.MeshObject("Ring", pivot.transform, ring, orange, Vector3.zero, Vector3.zero, Vector3.one, true);

            // The net hangs from the same pivot so it follows the ring's tilt. No collider.
            Mesh net = BuildNetMesh(ctx, index);
            ctx.MeshObject("Net", pivot.transform, net, NetMaterial(ctx, index),
                Vector3.zero, Vector3.zero, Vector3.one, false);
        }

        /// <summary>
        /// The whole net as one mesh, built in ring-centre space (ring centre = origin).
        /// Rows of knots hang below the ring; each knot connects down-left and down-right to the
        /// next row so the cords form diamonds; the bottom row closes into a loop.
        /// </summary>
        static Mesh BuildNetMesh(BuildContext ctx, int index)
        {
            int rows = NetDrop.Length;

            // 1) Knot positions, with 2 mm of jitter so the lattice is not machine-perfect.
            //    Odd rows are turned half a step (15°) – that is what makes the diamonds.
            Vector3[,] knots = new Vector3[rows, NetLoops];
            float step = 360f / NetLoops;
            for (int r = 0; r < rows; r++)
            for (int i = 0; i < NetLoops; i++)
            {
                float ang = (i + 0.5f * (r % 2)) * step * Mathf.Deg2Rad;
                // Row 0 sits just inside the ring tube (so the cords look tied on); lower rows hang below it.
                float y = (r == 0 ? -0.005f : -CourtSpec.RimRodRadius) - NetDrop[r];
                Vector3 p = new Vector3(Mathf.Cos(ang) * NetRadii[r], y, Mathf.Sin(ang) * NetRadii[r]);
                knots[r, i] = p + ctx.Jitter(0.002f);
            }

            // 2) Cords between rows. Hoop B is missing one strand (it dangles instead).
            var b = new MeshFactory.Builder();
            int torn = (index == 1) ? 23 : -1;
            int cord = 0;
            for (int r = 0; r < rows - 1; r++)
            for (int i = 0; i < NetLoops; i++)
            {
                int left  = (r % 2 == 0) ? (i + NetLoops - 1) % NetLoops : i;
                int right = (r % 2 == 0) ? i : (i + 1) % NetLoops;
                AddCord(b, knots[r, i], knots[r + 1, left],  cord++ == torn);
                AddCord(b, knots[r, i], knots[r + 1, right], cord++ == torn);
            }

            // 3) Bottom loop: the last row joined into a ring (first knot repeated to close it).
            var loop = new List<Vector3>();
            for (int i = 0; i <= NetLoops; i++) loop.Add(knots[rows - 1, i % NetLoops]);
            MeshFactory.AppendTube(b, loop, NetCordR, 5, false, 1f);

            return b.ToMesh("Net_" + index);
        }

        /// <summary>One net cord. A torn cord only goes half way and droops – it hangs loose.</summary>
        static void AddCord(MeshFactory.Builder b, Vector3 from, Vector3 to, bool torn)
        {
            Vector3 end = to;
            if (torn)
            {
                Vector3 outward = new Vector3(from.x, 0f, from.z).normalized;
                end = Vector3.Lerp(from, to, 0.55f) + Vector3.down * 0.03f + outward * 0.015f;
            }
            var path = new List<Vector3>();
            path.Add(from);
            path.Add(end);
            MeshFactory.AppendTube(b, path, NetCordR, 5, false, 1f);
        }

        // ── Materials & textures ─────────────────────────────────────────────

        /// <summary>Hoop A uses the shared galvanised look; hoop B is a duller, more weathered copy.</summary>
        static Material PoleSteel(BuildContext ctx, int index)
        {
            if (index == 0) return ctx.Mats.Galvanized;
            return ctx.Mats.Get("GalvanizedWorn", () =>
            {
                Material m = new Material(ctx.Mats.Galvanized);
                m.name = "GalvanizedWorn";
                return m.WithColor(new Color(0.68f, 0.68f, 0.67f)).WithSmoothness(0.45f);
            });
        }

        static Material BoltMaterial(BuildContext ctx)
        {
            return ctx.Mats.Flat("BoltSteel", new Color(0.30f, 0.30f, 0.32f), 0.5f, 0.8f);
        }

        /// <summary>Off-white nylon; hoop B's net is greyer from weather and dirt.</summary>
        static Material NetMaterial(BuildContext ctx, int index)
        {
            if (index == 0)
                return ctx.Mats.Get("NetNylon", () => MatKit.Make("NetNylon", new Color(0.90f, 0.90f, 0.85f), 0.2f, 0f));
            return ctx.Mats.Get("NetNylonDirty", () => MatKit.Make("NetNylonDirty", new Color(0.78f, 0.76f, 0.70f), 0.15f, 0f));
        }

        /// <summary>Matte-ish acrylic/aluminium board with its own grime texture per hoop.</summary>
        static Material BoardMaterial(BuildContext ctx, int index)
        {
            string key = "Backboard_" + index;
            return ctx.Mats.Get(key, () =>
            {
                Texture2D albedo = ctx.Tex.Get("backboard_albedo_" + index, () => BackboardTexture(index));
                return MatKit.Make(key, Color.white, 0.35f, 0f).WithAlbedo(albedo, 1f, 1f);
            });
        }

        /// <summary>
        /// 512² backboard albedo. The Unity cube maps the whole texture onto each face with v = 1 at the
        /// top, so grime that is strong at v ≈ 1 and streaky along u reads as rain streaks running down
        /// from the top edge. Lower corners get dirt, and a few soft orange smudges sit near the square
        /// (u 0.34..0.66, v 0.14..0.57) where the ball hits.
        /// </summary>
        static Texture2D BackboardTexture(int index)
        {
            int seed = 700 + index * 37;
            float grimeAmount = (index == 0) ? 0.50f : 0.70f;   // hoop B is dirtier

            Color paint = new Color(0.91f, 0.91f, 0.88f);
            Color grime = new Color(0.60f, 0.60f, 0.58f);
            Color dirt  = new Color(0.70f, 0.68f, 0.63f);
            Color scuff = new Color(0.86f, 0.56f, 0.32f);

            // Scuff centres are hashed from the seed so the two boards differ.
            Vector2[] scuffs = new Vector2[4];
            for (int i = 0; i < scuffs.Length; i++)
            {
                float hu = ProceduralTextures.Hash(i, 1, seed);
                float hv = ProceduralTextures.Hash(i, 2, seed);
                scuffs[i] = new Vector2(0.30f + hu * 0.40f, 0.12f + hv * 0.50f);
            }

            return ProceduralTextures.Create(512, 512, (u, v) =>
            {
                // Base: off-white with soft mottling and a fine speckle.
                float mottle = ProceduralTextures.Fbm(u, v, 4, 3, seed);
                float speck  = ProceduralTextures.Fbm(u, v, 64, 2, seed + 1);
                Color c = paint * (1f + (mottle - 0.5f) * 0.06f + (speck - 0.5f) * 0.04f);

                // Grime streaks: fast noise along u, very slow along v, strongest near the top edge.
                float streak  = ProceduralTextures.Fbm(u, v * 0.15f, 28, 3, seed + 2);
                float topFade = Mathf.Clamp01((v - 0.45f) / 0.55f);
                float g = Mathf.Clamp01((streak - 0.5f) * 3f) * topFade * topFade;
                c = Color.Lerp(c, grime, g * grimeAmount);

                // A dusty line right at the top edge.
                if (v > 0.975f) c = Color.Lerp(c, grime, 0.35f);

                // Dirt gathers in the two lower corners.
                float corner = ProceduralTextures.SoftBlob(u, v, 0f, 0f, 0.42f, 0.7f, seed + 3)
                             + ProceduralTextures.SoftBlob(u, v, 1f, 0f, 0.38f, 0.7f, seed + 4);
                float cornerNoise = ProceduralTextures.Fbm(u, v, 12, 3, seed + 5);
                c = Color.Lerp(c, dirt, Mathf.Clamp01(corner * (0.5f + cornerNoise * 0.5f)) * 0.6f);

                // Faint orange ball scuffs around the shooter's square.
                for (int i = 0; i < scuffs.Length; i++)
                {
                    float s = ProceduralTextures.SoftBlob(u, v, scuffs[i].x, scuffs[i].y,
                        0.05f + 0.02f * i, 0.9f, seed + 10 + i);
                    c = Color.Lerp(c, scuff, s * s * 0.28f);
                }

                c.a = 1f;
                return c;
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "backboard_albedo_" + index);
        }
    }
}
