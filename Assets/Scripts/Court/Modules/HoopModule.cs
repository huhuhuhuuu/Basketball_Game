using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// HoopModule – builds the two outdoor basketball goals (pole, gooseneck arm, backboard, rim, net).
    /// <para>
    /// HOW THE TWO HOOPS ARE MADE FROM ONE DESIGN
    /// One hoop is designed in a LOCAL frame where +Z points "toward the endline, away from the court
    /// centre". Hoop A sits at the +Z end, so its local frame equals the world frame. Hoop B is the same
    /// design inside a group turned 180° about Y (local +Z now points to world −Z) and leaned 0.6° about Z
    /// so the two poles are not perfect clones. Every Z number therefore comes straight from CourtSpec:
    /// BackboardZ (12.8), BasketZ (12.425) and PoleZ (14.8).
    /// </para>
    /// <para>
    /// WHAT ONE HOOP CONTAINS, BOTTOM TO TOP
    ///  • A rusty steel base plate with four bolt heads, flat on the slab (bottom exactly at y 0).
    ///  • A galvanised 4.5" pole, a gooseneck pipe swept along a smooth arc with MeshFactory.AppendTube,
    ///    a weld collar at the joint, a diagonal brace pipe and a clamp block where the arm meets the board.
    ///  • A 1.8 × 1.05 × 0.03 backboard with its own procedural off-white albedo: grey grime streaks running
    ///    DOWN from the top edge, dirt in the lower corners and faint orange ball scuffs around the square.
    ///  • The crisp BLACK shooter's square (0.59 × 0.45, bottom edge level with the rim top) and a black
    ///    border, both made of thin boxes 2 mm proud of the face so they can never z-fight with the board.
    ///  • A steel H-frame on the back of the board plus two angled brackets up to the arm.
    ///  • The orange rim: a torus with a non-convex MeshCollider, hanging off a bolted mounting plate, a
    ///    neck bar and two gusset struts. Each ring is tilted a degree or so about X – bent by dunks.
    ///  • A 12-loop nylon net as ONE mesh of thin tubes: cord loops wrapped around the ring, the classic
    ///    diamond lattice below them and a closed loop at the bottom, with 2 mm knot jitter.
    /// </para>
    /// <para>
    /// REALISM / VARIATION BETWEEN THE TWO HOOPS
    ///  Hoop B uses a duller, more weathered steel, a dirtier grey net with one torn dangling strand,
    ///  a grimier backboard and a slightly different rim tilt and pole lean.
    /// </para>
    /// </summary>
    public static class HoopModule
    {
        // ── Layout numbers that are NOT rule-book values (rule-book values live in CourtSpec) ──
        const float PoleHeight  = 3.7f;    // straight pole from y 0 to 3.7
        const float ArcRise     = 0.35f;   // the gooseneck climbs from 3.7 to 4.05 …
        const float ArcEndZ     = 13.15f;  // … while reaching forward to z 13.15
        const int   ArcSteps    = 9;       // 10 points on the bend (spec: 8–10)
        const float ArmEndY     = 3.95f;   // the straight arm lands level with the board's top edge …
        const float ArmEndZ     = 12.85f;  // … just behind the board's back face
        const float BarSize     = 0.04f;   // H-frame bars are 4 cm square
        const float PaintThick  = 0.004f;  // black paint strips on the board face
        const float PaintProud  = 0.002f;  // gap between the face and those strips
        const float Sink        = 0.001f;  // things bolted to a face are sunk 1 mm into it (no coplanar faces)

        // Net lattice: how far each row of knots hangs below the ring centre, and the row radius.
        // Row 0 is the knot where each cord loop leaves the ring tube (≈ 0.225, the ring's inner radius).
        static readonly float[] NetDrop  = { 0f, 0.10f, 0.20f, 0.30f, 0.40f };
        static readonly float[] NetRadii = { CourtSpec.RimRadius, 0.20f, 0.17f, 0.145f, 0.13f };
        const int   NetLoops   = 12;
        const float NetCordR   = 0.004f;
        const int   NetSides   = 5;
        const int   LoopSteps  = 12;       // points around each cord loop that wraps the ring tube

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

            // Hoop B: the −Z end. Turn the frame 180° about Y so local +Z points to world −Z, and lean it
            // 0.6° about Z. The Z axis passes through the pole foot (x = 0, y = 0), so the foot stays put
            // and only the top of the pole moves sideways by ~4 cm.
            GameObject hoopB = ctx.Group("Hoop_B_MinusZ", parent, Vector3.zero, new Vector3(0f, 180f, 0.6f));
            BuildHoop(ctx, hoopB.transform, ring, 1);
        }

        /// <summary>Builds one complete goal in the given local frame. `index` = 0 (hoop A) or 1 (hoop B).</summary>
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
            float pz = CourtSpec.PoleZ;               // 14.8
            Material bolt = BoltMaterial(ctx);

            // Ground plate: 35 cm square, 2 cm thick, bottom flat on the slab (y 0 → 0.02). Old base
            // plates are the first thing to rust, so it gets the rusty-iron look.
            ctx.Box("BasePlate", g, new Vector3(0f, 0.01f, pz), new Vector3(0.35f, 0.02f, 0.35f), ctx.Mats.RustyIron);

            // Four bolt heads standing on the plate (Ø 3 cm, 1.2 cm tall).
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sz = (i < 2) ? -1f : 1f;
                ctx.Post("BaseBolt_" + i, g, new Vector3(sx * 0.13f, 0.02f, pz + sz * 0.13f),
                    0.012f, 0.015f, bolt, false);
            }

            // The straight pole, standing on the plate.
            ctx.Post("Pole", g, new Vector3(0f, 0f, pz), PoleHeight, CourtSpec.PoleRadius, steel);

            // The gooseneck: one swept pipe that starts inside the pole top and bends over to the board.
            // It is a hair thinner than the pole so the two walls never overlap exactly (no z-fighting).
            var b = new MeshFactory.Builder();
            MeshFactory.AppendTube(b, GooseneckPath(), CourtSpec.PoleRadius - 0.0015f, 16, true, 1f);
            ctx.MeshObject("Gooseneck", g, b.ToMesh("Gooseneck"), steel,
                Vector3.zero, Vector3.zero, Vector3.one, true);

            // A short weld collar hides the seam where the bent pipe leaves the pole.
            ctx.Tube("WeldCollar", g, new Vector3(0f, PoleHeight - 0.05f, pz), new Vector3(0f, PoleHeight + 0.05f, pz),
                CourtSpec.PoleRadius + 0.006f, steel, false);

            // Diagonal brace from the pole (y 3.0) up into the arm just behind the board.
            // The arm's centre line at z 13.0 is at y 4.0, so ending at 3.98 keeps the brace inside the pipe.
            ctx.Tube("Brace", g, new Vector3(0f, 3.0f, pz), new Vector3(0f, 3.98f, 13.0f), 0.02f, steel);

            // Clamp block that grabs the arm end behind the board (also hides the pipe's end cap).
            float clampZ = CourtSpec.BackboardZ + CourtSpec.BackboardThick + 0.04f + Sink;   // 12.871
            ctx.Box("ArmClamp", g, new Vector3(0f, 3.95f, clampZ), new Vector3(0.16f, 0.14f, 0.08f), steel, false);   // bottom 3.88, inside the upper bar
        }

        /// <summary>
        /// Path of the bent pipe: a short straight bit hidden inside the pole, a quarter of an ellipse
        /// (rises 0.35 m while reaching 1.65 m forward) and a straight arm down to the board.
        /// </summary>
        static List<Vector3> GooseneckPath()
        {
            var pts = new List<Vector3>();
            float pz = CourtSpec.PoleZ;
            float reach = pz - ArcEndZ;                       // 1.65 m forward (toward −Z)

            pts.Add(new Vector3(0f, PoleHeight - 0.08f, pz));   // buried inside the pole top

            for (int i = 0; i <= ArcSteps; i++)
            {
                float t = (float)i / ArcSteps * Mathf.PI * 0.5f;    // 0 → 90°
                float y = PoleHeight + Mathf.Sin(t) * ArcRise;       // 3.7 → 4.05
                float z = pz - (1f - Mathf.Cos(t)) * reach;         // 14.8 → 13.15
                pts.Add(new Vector3(0f, y, z));
            }

            pts.Add(new Vector3(0f, ArmEndY, ArmEndZ));        // straight arm to (0, 3.95, 12.85)
            return pts;
        }

        // ── Backboard ────────────────────────────────────────────────────────

        static void BuildBackboard(BuildContext ctx, Transform g, Material steel, int index)
        {
            float centreY = CourtSpec.BackboardBottom + CourtSpec.BackboardHeight * 0.5f;   // 3.425
            float centreZ = CourtSpec.BackboardZ + CourtSpec.BackboardThick * 0.5f;         // 12.815
            Vector3 size = new Vector3(CourtSpec.BackboardWidth, CourtSpec.BackboardHeight, CourtSpec.BackboardThick);

            // The board is a hand-built box so we KNOW the grime texture's v = 1 is at the top edge.
            GameObject board = ctx.MeshObject("Board", g, BoardMesh(size), BoardMaterial(ctx, index),
                new Vector3(0f, centreY, centreZ), Vector3.zero, Vector3.one, false);
            BoxCollider bc = board.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = size;

            BuildShooterSquare(ctx, g);
            BuildBorder(ctx, g);
            BuildBackFrame(ctx, g, steel);
        }

        /// <summary>
        /// Box mesh centred at the origin. The front (−Z) and back (+Z) faces get the whole texture with
        /// v running upward; the four thin edge faces sample a narrow clean band; the top edge samples the
        /// dusty band at the very top of the texture.
        /// </summary>
        static Mesh BoardMesh(Vector3 size)
        {
            var b = new MeshFactory.Builder();
            Vector3 h = size * 0.5f;
            Vector2 full0 = new Vector2(0f, 0f), full1 = new Vector2(1f, 1f);
            Vector2 band0 = new Vector2(0f, 0.30f), band1 = new Vector2(1f, 0.33f);
            Vector2 top0  = new Vector2(0f, 0.97f), top1  = new Vector2(1f, 1.00f);

            AddBoardFace(b, new Vector3(0f, 0f, -h.z), Vector3.back,    Vector3.right,   Vector3.up,      size.x, size.y, full0, full1);
            AddBoardFace(b, new Vector3(0f, 0f,  h.z), Vector3.forward, Vector3.left,    Vector3.up,      size.x, size.y, full0, full1);
            AddBoardFace(b, new Vector3(-h.x, 0f, 0f), Vector3.left,    Vector3.back,    Vector3.up,      size.z, size.y, band0, band1);
            AddBoardFace(b, new Vector3( h.x, 0f, 0f), Vector3.right,   Vector3.forward, Vector3.up,      size.z, size.y, band0, band1);
            AddBoardFace(b, new Vector3(0f,  h.y, 0f), Vector3.up,      Vector3.right,   Vector3.forward, size.x, size.z, top0,  top1);
            AddBoardFace(b, new Vector3(0f, -h.y, 0f), Vector3.down,    Vector3.right,   Vector3.back,    size.x, size.z, band0, band1);
            return b.ToMesh("Backboard");
        }

        /// <summary>One rectangular face (two triangles, clockwise = front) mapped to a UV rectangle.</summary>
        static void AddBoardFace(MeshFactory.Builder b, Vector3 center, Vector3 normal, Vector3 right, Vector3 up,
            float w, float hgt, Vector2 uv0, Vector2 uv1)
        {
            Vector3 r = right * (w * 0.5f), u = up * (hgt * 0.5f);
            int a = b.Add(center - r - u, normal, new Vector2(uv0.x, uv0.y));
            int c = b.Add(center + r - u, normal, new Vector2(uv1.x, uv0.y));
            int d = b.Add(center + r + u, normal, new Vector2(uv1.x, uv1.y));
            int e = b.Add(center - r + u, normal, new Vector2(uv0.x, uv1.y));
            b.Quad(a, e, d, c);
        }

        /// <summary>One thin black strip lying on the front face (2 mm proud of it, no collider).</summary>
        static void PaintStrip(BuildContext ctx, Transform g, string name, float cx, float cy, float w, float h)
        {
            float z = CourtSpec.BackboardZ - PaintProud - PaintThick * 0.5f;   // 12.796 (strip spans 12.794 → 12.798)
            ctx.Box(name, g, new Vector3(cx, cy, z), new Vector3(w, h, PaintThick), ctx.Mats.BlackPaint, false);
        }

        /// <summary>The black shooter's square: 0.59 × 0.45 outer, 5 cm stroke, bottom edge at rim height (3.05).</summary>
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
            // Bars sit against the back face (12.83), sunk 1 mm into it so no faces are coplanar.
            float z = CourtSpec.BackboardZ + CourtSpec.BackboardThick + BarSize * 0.5f - Sink;   // 12.849
            float lowY  = CourtSpec.BackboardBottom + 0.05f;                              // 2.95
            float highY = CourtSpec.BackboardBottom + CourtSpec.BackboardHeight - 0.05f;  // 3.90
            float vx = 0.55f;

            // Two verticals.
            Vector3 vSize = new Vector3(BarSize, highY - lowY, BarSize);
            float vy = (lowY + highY) * 0.5f;
            ctx.Box("Frame_Left",  g, new Vector3(-vx, vy, z), vSize, steel, false);
            ctx.Box("Frame_Right", g, new Vector3( vx, vy, z), vSize, steel, false);

            // Two horizontals: one at rim height (the rim bolts go through to it), one under the arm end.
            Vector3 hSize = new Vector3(2f * vx + BarSize, BarSize, BarSize);
            float lowBarY = 3.00f, highBarY = 3.88f;
            ctx.Box("Frame_Lower", g, new Vector3(0f, lowBarY,  z), hSize, steel, false);
            ctx.Box("Frame_Upper", g, new Vector3(0f, highBarY, z), hSize, steel, false);

            // Angled brackets: from inside the arm pipe down and out to the lower bar's ends.
            Vector3 armPoint = new Vector3(0f, 4.03f, 13.25f);   // arm centre line here is y ≈ 4.05
            ctx.Tube("ArmBracket_L", g, armPoint, new Vector3(-0.50f, lowBarY, z), 0.015f, steel, false);
            ctx.Tube("ArmBracket_R", g, armPoint, new Vector3( 0.50f, lowBarY, z), 0.015f, steel, false);
        }

        // ── Rim and net ──────────────────────────────────────────────────────

        static void BuildRim(BuildContext ctx, Transform g, Mesh ring, int index)
        {
            BuildRimMount(ctx, g);

            // Pivot at the ring centre, tilted about X. A negative angle makes the FRONT of the ring
            // (toward the court, local −Z) dip a little – a rim that has taken a few dunks.
            float tilt = (index == 0) ? -1.5f : -1.0f;
            GameObject pivot = ctx.Group("RimPivot", g,
                new Vector3(0f, CourtSpec.RimCenterY, CourtSpec.BasketZ), new Vector3(tilt, 0f, 0f));

            // The ring itself, with a non-convex MeshCollider so a ball can bounce off it.
            ctx.MeshObject("Ring", pivot.transform, ring, ctx.Mats.RimOrange, Vector3.zero, Vector3.zero, Vector3.one, true);

            // The net hangs from the same pivot so it follows the ring's tilt. No collider.
            Mesh net = BuildNetMesh(ctx, index);
            ctx.MeshObject("Net", pivot.transform, net, NetMaterial(ctx, index),
                Vector3.zero, Vector3.zero, Vector3.one, false);
        }

        /// <summary>Mounting plate with bolts, the neck bar out to the ring, and two gusset struts underneath.</summary>
        static void BuildRimMount(BuildContext ctx, Transform g)
        {
            float faceZ = CourtSpec.BackboardZ;            // 12.8
            Material orange = ctx.Mats.RimOrange;
            Material bolt = BoltMaterial(ctx);

            // Mounting plate 0.12 × 0.12 × 0.02 on the board face, its bottom edge just below the rim top
            // (y 3.04 → 3.16). Sunk 1 mm into the face: z 12.781 → 12.801.
            float plateY = CourtSpec.RimHeight + 0.05f;    // 3.10
            float plateZ = faceZ - 0.01f + Sink;           // 12.791
            ctx.Box("RimPlate", g, new Vector3(0f, plateY, plateZ), new Vector3(0.12f, 0.12f, 0.02f), orange, false);

            // Four bolt heads poking 9 mm out of the plate front.
            float plateFront = plateZ - 0.01f;             // 12.781
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sy = (i < 2) ? -1f : 1f;
                Vector3 a = new Vector3(sx * 0.04f, plateY + sy * 0.04f, plateFront + 0.002f);
                Vector3 c = new Vector3(sx * 0.04f, plateY + sy * 0.04f, plateFront - 0.009f);
                ctx.Tube("RimBolt_" + i, g, a, c, 0.008f, bolt, false);
            }

            // Neck bar from the plate out to the back of the ring (z 12.79 → 12.65) at ring level, so it
            // merges into the tube; the ring looks welded on top of it.
            float neckZ0 = plateZ;                                   // 12.791
            float neckZ1 = CourtSpec.BasketZ + CourtSpec.RimRadius;  // 12.65 (inside the ring tube)
            ctx.Box("RimNeck", g, new Vector3(0f, CourtSpec.RimCenterY - 0.005f, (neckZ0 + neckZ1) * 0.5f),
                new Vector3(0.10f, 0.03f, neckZ0 - neckZ1), orange, false);

            // A small foot plate lower on the board and two gusset struts from it up to the ring's
            // underside, 40° either side of the neck – the triangle that keeps a heavy-duty rim rigid.
            float footY = 2.965f;
            ctx.Box("RimFoot", g, new Vector3(0f, footY, plateZ), new Vector3(0.14f, 0.03f, 0.02f), orange, false);
            float R = CourtSpec.RimRadius + CourtSpec.RimRodRadius;  // 0.234 (to the tube centre)
            float ang = 40f * Mathf.Deg2Rad;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = new Vector3(s * 0.05f, footY, plateZ - 0.006f);
                Vector3 c = new Vector3(s * Mathf.Sin(ang) * R, CourtSpec.RimCenterY - 0.006f,
                    CourtSpec.BasketZ + Mathf.Cos(ang) * R);
                ctx.Tube(s < 0 ? "RimStrut_L" : "RimStrut_R", g, a, c, 0.008f, orange, false);
            }
        }

        /// <summary>
        /// The whole net as ONE mesh, built in ring-centre space (ring centre = origin, ring in the XZ plane).
        ///  1. Twelve cord loops, each wrapped around the ring tube at angle i·30°. The point where a loop
        ///     leaves the tube (inner side, just below centre) is that loop's row-0 knot.
        ///  2. Rows of knots hang below the ring; odd rows are turned half a step (15°). Every knot connects
        ///     DOWN-LEFT and DOWN-RIGHT to the next row, which is what makes the diamonds.
        ///  3. The bottom row is closed into a loop.
        /// Knots below the ring get 2 mm of jitter so the lattice is not machine-perfect.
        /// Hoop B (index 1) is missing one strand: it hangs loose instead of reaching the next knot.
        /// </summary>
        static Mesh BuildNetMesh(BuildContext ctx, int index)
        {
            var b = new MeshFactory.Builder();
            int rows = NetDrop.Length;
            float step = 360f / NetLoops;
            Vector3[,] knots = new Vector3[rows, NetLoops];

            // 1) Loops around the ring; each returns its row-0 knot.
            for (int i = 0; i < NetLoops; i++)
                knots[0, i] = AddRimLoop(b, i * step + ctx.Range(-1f, 1f));

            // 2) Lower rows of knots.
            for (int r = 1; r < rows; r++)
            for (int i = 0; i < NetLoops; i++)
            {
                float a = (i + 0.5f * (r % 2)) * step * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * NetRadii[r], -NetDrop[r], Mathf.Sin(a) * NetRadii[r]);
                knots[r, i] = p + ctx.Jitter(0.002f);
            }

            // Cords between rows. On even rows "left" is knot i−1 of the next row and "right" is knot i;
            // on odd rows (already turned 15°) it is knots i and i+1.
            int torn = (index == 1) ? 39 : -1;   // cord number 39 = row 1, knot 7, down-right
            int cord = 0;
            for (int r = 0; r < rows - 1; r++)
            for (int i = 0; i < NetLoops; i++)
            {
                int left  = (r % 2 == 0) ? (i + NetLoops - 1) % NetLoops : i;
                int right = (r % 2 == 0) ? i : (i + 1) % NetLoops;
                AddCord(b, knots[r, i], knots[r + 1, left],  cord == torn); cord++;
                AddCord(b, knots[r, i], knots[r + 1, right], cord == torn); cord++;
            }

            // 3) Bottom loop: the last row joined into a ring (first knot repeated to close it).
            var loop = new List<Vector3>();
            for (int i = 0; i <= NetLoops; i++) loop.Add(knots[rows - 1, i % NetLoops]);
            MeshFactory.AppendTube(b, loop, NetCordR, NetSides, false, 1f);

            return b.ToMesh("Net_" + index);
        }

        /// <summary>
        /// One cord loop wrapped around the ring tube at `angleDeg` (0° = +X, 90° = +Z, seen from above).
        /// The loop is a circle around the tube's cross-section, slightly embedded in it so it looks tight.
        /// Returns the knot where the lattice starts: the inner side of the tube, just below its centre.
        /// </summary>
        static Vector3 AddRimLoop(MeshFactory.Builder b, float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));               // points away from the ring centre
            Vector3 tubeCentre = radial * (CourtSpec.RimRadius + CourtSpec.RimRodRadius);
            float loopR = CourtSpec.RimRodRadius + NetCordR * 0.6f;                      // cord half sunk into the tube

            // φ = 0 is the outer side of the tube, 90° the top, 180° the inner side, 270° the bottom.
            const float startDeg = 205f;                                                  // inner side, a little below centre
            var path = new List<Vector3>(LoopSteps + 1);
            for (int k = 0; k <= LoopSteps; k++)
            {
                float phi = (startDeg + 360f * k / LoopSteps) * Mathf.Deg2Rad;
                path.Add(tubeCentre + (radial * Mathf.Cos(phi) + Vector3.up * Mathf.Sin(phi)) * loopR);
            }
            MeshFactory.AppendTube(b, path, NetCordR, NetSides, false, 1f);
            return path[0];
        }

        /// <summary>One net cord. A torn cord only goes half way and droops – it hangs loose.</summary>
        static void AddCord(MeshFactory.Builder b, Vector3 from, Vector3 to, bool torn)
        {
            Vector3 end = to;
            if (torn)
            {
                Vector3 outward = new Vector3(from.x, 0f, from.z).normalized;
                end = Vector3.Lerp(from, to, 0.5f) + Vector3.down * 0.04f + outward * 0.02f;
            }
            var path = new List<Vector3>(2);
            path.Add(from);
            path.Add(end);
            MeshFactory.AppendTube(b, path, NetCordR, NetSides, false, 1f);
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

        /// <summary>Dark zinc-plated bolt heads.</summary>
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
        /// 512² backboard albedo (v = 1 is the top edge of the board, see BoardMesh):
        ///  • off-white base with soft mottling and a fine speckle;
        ///  • grey grime streaks – fast noise along u, very slow along v – strongest near the top edge so
        ///    they read as rain streaks running DOWN from the top;
        ///  • a dusty line right at the top edge (also what the top face samples);
        ///  • dirt gathering in the two lower corners;
        ///  • a faint orange haze plus a few soft orange ball scuffs around the shooter's square
        ///    (the square occupies u 0.34..0.66, v 0.14..0.57).
        /// Hoop B (index 1) is grimier.
        /// </summary>
        static Texture2D BackboardTexture(int index)
        {
            int seed = 700 + index * 37;
            float grimeAmount = (index == 0) ? 0.50f : 0.70f;

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

                // Grime streaks running down from the top edge.
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

                // Faint orange haze where the ball keeps hitting, then a few sharper scuff marks.
                float haze = ProceduralTextures.SoftBlob(u, v, 0.5f, 0.36f, 0.30f, 0.5f, seed + 6);
                c = Color.Lerp(c, scuff, haze * haze * 0.10f);
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
