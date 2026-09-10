// LinesModule.cs — the painted FIBA court markings (black on the green asphalt).
//
// WHAT IT BUILDS
//   Every painted line of a 28 × 15 m FIBA court:
//     • boundary rectangle, centre line (with its 15 cm overhang past each sideline), centre circle
//     • for BOTH ends: free-throw line, key (restricted area), free-throw semicircle,
//       three-point line (arc + two straight legs), no-charge semicircle, lane (rebound) marks
//       and the throw-in mark outside each sideline.
//   All of it is flat geometry: thin "ribbons" of triangles hovering 4 mm above the floor
//   (CourtSpec.LineY) so they can never z-fight with the asphalt. Three combined meshes are made
//   (middle, north end, south end) → only three GameObjects in total, no colliders, no shadows.
//
// HOW IT LOOKS REALISTIC
//   • One "worn paint" cutout material: near-black albedo with a faint grey speckle, and an alpha
//     channel made from tileable noise. Alpha-testing removes ~10–15 % of the paint as flaked
//     chips, and the outer millimetres of every line are extra ragged (as real road paint wears).
//   • The texture is tiled ALONG each line (ribbon u = metres × 2), so the wear pattern follows
//     every straight line and every arc instead of being a stretched square.
//   • The geometry itself is exact and never randomised (a real court is measured with a tape);
//     only the paint alpha gets a little seeded randomness (noise seed + how much has flaked).
//   • Lines are split where they would cross (centre line vs circle / sidelines, arcs vs the lines
//     they touch) so no two quads overlap in the same plane and nothing flickers.
//
// GEOMETRY RULE OF THUMB
//   FIBA measures the OUTER edge of a line. Our ribbons are built from their CENTRE-line, so every
//   radius / offset below is "the rule-book number minus half a line width" (or plus, for the
//   no-charge semicircle whose rule-book number is the INNER edge).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    public static class LinesModule
    {
        // ── Shorthands (all derived from CourtSpec) ───────────────────────────
        const float LineW = CourtSpec.LineWidth;           // 0.05 m
        const float HalfW = CourtSpec.LineWidth * 0.5f;    // 0.025 m
        const float Y     = CourtSpec.LineY;               // 0.004 m above the floor

        // Ribbon CENTRE-line coordinates.
        const float SideX      = CourtSpec.HalfWidth  - HalfW;                       // 7.475
        const float EndZ       = CourtSpec.HalfLength - HalfW;                       // 13.975
        const float EndInnerZ  = CourtSpec.HalfLength - LineW;                       // 13.95 (inner edge of the endline)
        const float SideInnerX = CourtSpec.HalfWidth  - LineW;                       // 7.45  (inner edge of the sideline)
        const float CentreOverhangX = CourtSpec.HalfWidth + CourtSpec.CenterLineOverhang; // 7.65
        const float CentreCircleR   = CourtSpec.CenterCircleRadius - HalfW;          // 1.775
        const float FtZ        = EndInnerZ - CourtSpec.FreeThrowLineDist + HalfW;    // 8.175
        const float KeyX       = CourtSpec.KeyWidth * 0.5f - HalfW;                  // 2.425
        const float KeyOuterX  = CourtSpec.KeyWidth * 0.5f;                          // 2.45
        const float FtCircleR  = CourtSpec.FreeThrowCircleRadius - HalfW;            // 1.775
        const float ThreeR     = CourtSpec.ThreePointRadius - HalfW;                 // 6.725
        const float ThreeLegX  = CourtSpec.HalfWidth - CourtSpec.ThreePointSideInset - HalfW; // 6.575
        const float NoChargeR  = CourtSpec.NoChargeRadius + HalfW;                   // 1.275 (rule-book number is the inner edge)
        const float ThrowInZ   = EndInnerZ - CourtSpec.ThrowInMarkDist;              // 5.625
        const float ThrowInLength = 0.15f;

        // Lane (rebound) marks beside the key — numbers not in CourtSpec, so named here.
        const float LaneBlockZ0    = 12.2f;   // neutral-zone block runs z 12.2 → 12.6
        const float LaneBlockZ1    = 12.6f;
        const float LaneBlockWidth = 0.10f;
        const float LaneMarkLength = 0.10f;   // small marks stick 10 cm out from the key edge
        static readonly float[] LaneMarkZ = { 11.35f, 10.5f, 9.65f };

        // Paint texture: it repeats every TileMetres along a line and spans the line width across.
        const float UvPerMeter = 2f;          // AppendRibbon: u = metres × 2
        const float TileMetres = 2f;          // one texture repeat = 2 m of line
        const int   TexW = 2048, TexH = 64;   // 2 m × 0.05 m → ≈ 1 mm texels both ways
        const float Cutoff = 0.35f;           // alpha below this is a flaked chip

        // ── Entry point ──────────────────────────────────────────────────────
        public static void Build(BuildContext ctx, Transform parent)
        {
            Material paint = WornPaint(ctx);

            // Middle of the court: boundary, centre line, centre circle.
            var middle = new MeshFactory.Builder();
            AddBoundary(middle);
            AddCentreLine(middle);
            AddCentreCircle(middle);
            AddLineObject(ctx, parent, middle, "CourtLines_Middle", paint);

            // The two ends are mirror images: build with z-sign +1 and −1.
            var north = new MeshFactory.Builder();
            AddHalfCourt(north, +1f);
            AddLineObject(ctx, parent, north, "CourtLines_NorthEnd", paint);

            var south = new MeshFactory.Builder();
            AddHalfCourt(south, -1f);
            AddLineObject(ctx, parent, south, "CourtLines_SouthEnd", paint);
        }

        /// <summary>Turns a builder into a flat, shadow-less mesh object under `parent`.</summary>
        static void AddLineObject(BuildContext ctx, Transform parent, MeshFactory.Builder b, string name, Material mat)
        {
            Mesh mesh = b.ToMesh(name);
            // The long overload lets us switch shadow casting OFF (flat paint casts no shadow).
            ctx.MeshObject(name, parent, mesh, mat, Vector3.zero, Vector3.zero, Vector3.one,
                false, false, true);
        }

        // ── Small geometry helpers ───────────────────────────────────────────

        /// <summary>A point on the line layer.</summary>
        static Vector3 P(float x, float z) { return new Vector3(x, Y, z); }

        /// <summary>Straight painted segment between two floor points.</summary>
        static void Seg(MeshFactory.Builder b, float x0, float z0, float x1, float z1, float width, float extendEnds)
        {
            MeshFactory.AppendSegment(b, P(x0, z0), P(x1, z1), width, extendEnds, UvPerMeter);
        }

        /// <summary>Painted ribbon of normal line width along a polyline.</summary>
        static void Ribbon(MeshFactory.Builder b, List<Vector3> pts, bool closed)
        {
            MeshFactory.AppendRibbon(b, pts, LineW, UvPerMeter, 0f, closed);
        }

        /// <summary>Points on an arc on the line layer (degrees: 0° = +X, 90° = +Z).</summary>
        static List<Vector3> Arc(float cx, float cz, float radius, float startDeg, float endDeg, int segments)
        {
            return MeshFactory.ArcPoints(new Vector3(cx, Y, cz), radius, startDeg, endDeg, segments, Y);
        }

        /// <summary>Copies a point list with z multiplied by `s` (+1 keeps it, −1 mirrors to the other end).</summary>
        static List<Vector3> MirrorZ(List<Vector3> pts, float s)
        {
            var result = new List<Vector3>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                result.Add(new Vector3(pts[i].x, pts[i].y, pts[i].z * s));
            return result;
        }

        // ── Middle of the court ──────────────────────────────────────────────

        /// <summary>Boundary rectangle: outer edges at |x| = 7.5, |z| = 14 (mitred corners).</summary>
        static void AddBoundary(MeshFactory.Builder b)
        {
            MeshFactory.AppendRectOutline(b, -SideX, -EndZ, SideX, EndZ, LineW, Y, UvPerMeter);
        }

        /// <summary>
        /// Centre line at z = 0, from −7.65 to 7.65. It is drawn in five pieces that stop at the
        /// centre circle and at the sidelines, so nothing is painted twice in the same spot.
        /// </summary>
        static void AddCentreLine(MeshFactory.Builder b)
        {
            float circleIn  = CentreCircleR - HalfW;   // 1.75  inner edge of the circle
            float circleOut = CentreCircleR + HalfW;   // 1.80  outer edge of the circle
            Seg(b, -circleIn, 0f, circleIn, 0f, LineW, 0f);                          // inside the circle
            Seg(b, circleOut, 0f, SideInnerX, 0f, LineW, 0f);                        // circle → east sideline
            Seg(b, -SideInnerX, 0f, -circleOut, 0f, LineW, 0f);                      // west sideline → circle
            Seg(b, CourtSpec.HalfWidth, 0f, CentreOverhangX, 0f, LineW, 0f);         // east overhang
            Seg(b, -CentreOverhangX, 0f, -CourtSpec.HalfWidth, 0f, LineW, 0f);       // west overhang
        }

        /// <summary>Centre circle, outer radius 1.8, as one closed ribbon of 64 segments.</summary>
        static void AddCentreCircle(MeshFactory.Builder b)
        {
            var pts = Arc(0f, 0f, CentreCircleR, 0f, 360f, 64);
            pts.RemoveAt(pts.Count - 1);   // ArcPoints repeats the first point at 360°; a closed ribbon must not
            Ribbon(b, pts, true);
        }

        // ── One end of the court (s = +1 north / −1 south) ───────────────────

        static void AddHalfCourt(MeshFactory.Builder b, float s)
        {
            AddFreeThrowLineAndKey(b, s);
            AddFreeThrowSemicircle(b, s);
            AddThreePointLine(b, s);
            AddNoChargeSemicircle(b, s);
            AddLaneMarks(b, s);
            AddThrowInMarks(b, s);
        }

        /// <summary>
        /// Free-throw line (its far edge 5.8 m from the endline's inner edge) and the two long sides of
        /// the key. FIBA closes the key with the "extended free-throw line", so the FT line is drawn
        /// across the whole 4.9 m key width; the sides run from the endline to the FT line's near edge.
        /// </summary>
        static void AddFreeThrowLineAndKey(MeshFactory.Builder b, float s)
        {
            float ftZ = s * FtZ;
            // Free-throw line: extend both ends by half a width so the key corners are solid squares.
            Seg(b, -KeyX, ftZ, KeyX, ftZ, LineW, HalfW);
            // Key sides: from the endline's inner edge to the FT line's edge that faces the endline.
            float keyTopZ = s * (FtZ + HalfW);
            Seg(b,  KeyX, s * EndInnerZ,  KeyX, keyTopZ, LineW, 0f);
            Seg(b, -KeyX, s * EndInnerZ, -KeyX, keyTopZ, LineW, 0f);
        }

        /// <summary>
        /// Free-throw semicircle (outer radius 1.8) centred on the FT line midpoint; only the half that
        /// faces the centre of the court is painted (FIBA no longer draws the half inside the key).
        /// </summary>
        static void AddFreeThrowSemicircle(MeshFactory.Builder b, float s)
        {
            // Start a hair (≈0.8°) past 180° so the arc begins at the FT line's edge instead of its middle.
            float tuck = Mathf.Asin(HalfW / FtCircleR) * Mathf.Rad2Deg;
            var arc = Arc(0f, FtZ, FtCircleR, 180f + tuck, 360f - tuck, 32);   // through 270° = toward −Z (court centre)
            Ribbon(b, MirrorZ(arc, s), false);
        }

        /// <summary>
        /// Three-point line: an arc of outer radius 6.75 around the basket's floor point, joined to two
        /// straight legs 0.9 m in from the sidelines. Leg and arc are ONE polyline, so they meet exactly.
        /// </summary>
        static void AddThreePointLine(MeshFactory.Builder b, float s)
        {
            float basketZ = CourtSpec.BasketZ;                                   // 12.425
            // Where the leg (x = 6.575) meets the arc (r = 6.725): dz = sqrt(r² − x²) ≈ 1.4125.
            float dz = Mathf.Sqrt(ThreeR * ThreeR - ThreeLegX * ThreeLegX);
            float startDeg = Mathf.Atan2(-dz, ThreeLegX) * Mathf.Rad2Deg;       // ≈ −12.1° (east leg end)
            float endDeg   = -180f - startDeg;                                   // ≈ −167.9° (west leg end)

            var pts = new List<Vector3>();
            pts.Add(P(ThreeLegX, EndInnerZ));                                    // east leg starts at the endline
            pts.AddRange(Arc(0f, basketZ, ThreeR, startDeg, endDeg, 64));       // first arc point == east leg end
            pts.Add(P(-ThreeLegX, EndInnerZ));                                   // west leg back to the endline
            Ribbon(b, MirrorZ(pts, s), false);
        }

        /// <summary>
        /// No-charge semicircle (inner radius 1.25) around the basket point, open toward the basket,
        /// with each end continued straight to the backboard plane (z = 12.8).
        /// </summary>
        static void AddNoChargeSemicircle(MeshFactory.Builder b, float s)
        {
            float basketZ = CourtSpec.BasketZ;        // 12.425
            float boardZ  = CourtSpec.BackboardZ;     // 12.8
            var pts = new List<Vector3>();
            pts.Add(P(NoChargeR, boardZ));                                       // east leg at the backboard
            pts.AddRange(Arc(0f, basketZ, NoChargeR, 0f, -180f, 24));           // 0° → −90° (toward court) → −180°
            pts.Add(P(-NoChargeR, boardZ));                                      // west leg at the backboard
            Ribbon(b, MirrorZ(pts, s), false);
        }

        /// <summary>
        /// Lane (rebound) marks hugging the outside of each key side: one 0.10 × 0.40 neutral-zone
        /// block and three 0.05 × 0.10 marks.
        /// </summary>
        static void AddLaneMarks(MeshFactory.Builder b, float s)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float xIn  = side * KeyOuterX;                       // 2.45: key outer edge
                float xOut = side * (KeyOuterX + LaneMarkLength);    // 2.55
                float xMid = (xIn + xOut) * 0.5f;
                // Block: a 0.10-wide ribbon running along z.
                Seg(b, xMid, s * LaneBlockZ0, xMid, s * LaneBlockZ1, LaneBlockWidth, 0f);
                // Three thin marks sticking out sideways from the key.
                for (int i = 0; i < LaneMarkZ.Length; i++)
                    Seg(b, xIn, s * LaneMarkZ[i], xOut, s * LaneMarkZ[i], LineW, 0f);
            }
        }

        /// <summary>Throw-in mark: 0.15 m line outside each sideline, 8.325 m from the endline's inner edge.</summary>
        static void AddThrowInMarks(MeshFactory.Builder b, float s)
        {
            float z = s * ThrowInZ;
            Seg(b,  CourtSpec.HalfWidth, z,  CourtSpec.HalfWidth + ThrowInLength, z, LineW, 0f);
            Seg(b, -CourtSpec.HalfWidth - ThrowInLength, z, -CourtSpec.HalfWidth, z, LineW, 0f);
        }

        // ── Worn paint material ──────────────────────────────────────────────

        /// <summary>Near-black, slightly flaked paint. Cached so all three line meshes share it.</summary>
        static Material WornPaint(BuildContext ctx)
        {
            return ctx.Mats.Get("LinePaint_Worn", () =>
            {
                Texture2D tex = ctx.Tex.Get("line_paint_albedo", () => PaintTexture(ctx));
                // Ribbon u = metres × UvPerMeter; we want one repeat per TileMetres → scale u down.
                float tileX = 1f / (UvPerMeter * TileMetres);
                return MatKit.Make("LinePaint_Worn", Color.white, 0.3f, 0f)
                    .WithAlbedo(tex, tileX, 1f)
                    .Cutout(Cutoff);
            });
        }

        /// <summary>
        /// 2048 × 64 RGBA texture: RGB = black paint with grey speckle, A = "is paint still here".
        /// The noise is first evaluated for every texel, then sorted, so that EXACTLY the wanted
        /// fraction (10–15 %) of texels ends up below the cutoff — no guessing at noise ranges.
        /// </summary>
        static Texture2D PaintTexture(BuildContext ctx)
        {
            // The only randomness in this module: which noise seed, and how much paint has flaked.
            int seed = ctx.RangeInt(1, 100000);
            float flakedFraction = ctx.Range(0.10f, 0.15f);

            // 1) Wear noise per texel: fine chips (~8 mm) blended with larger worn patches (~8 cm).
            float[] wear = new float[TexW * TexH];
            for (int y = 0; y < TexH; y++)
            {
                float v = (y + 0.5f) / TexH;
                for (int x = 0; x < TexW; x++)
                {
                    float u = (x + 0.5f) / TexW;
                    wear[y * TexW + x] = LineNoise(u, v, 256, 4, seed) * 0.55f
                                       + LineNoise(u, v, 24, 3, seed + 77) * 0.45f;
                }
            }

            // 2) The noise value below which `flakedFraction` of the texels lie.
            float[] sorted = (float[])wear.Clone();
            Array.Sort(sorted);
            int cut = Mathf.Clamp((int)(sorted.Length * flakedFraction), 0, sorted.Length - 1);
            float threshold = sorted[cut];

            // 3) Colour + alpha. Alpha equals Cutoff exactly at the threshold.
            Texture2D tex = ProceduralTextures.Create(TexW, TexH, (u, v) =>
            {
                int x = Mathf.Min((int)(u * TexW), TexW - 1);
                int y = Mathf.Min((int)(v * TexH), TexH - 1);
                float n = wear[y * TexW + x];
                float alpha = Cutoff + (n - threshold) * 2.5f;

                // The outer 15 % of the width (≈ 7 mm) wears more, giving a ragged paint edge.
                float edge = Mathf.Min(v, 1f - v);                     // 0 at the edge, 0.5 mid-line
                float edgeWear = Mathf.Clamp01(1f - edge / 0.15f);
                alpha -= edgeWear * edgeWear * 0.18f;

                // Near-black with grey speckle; thin paint (alpha just above the cutoff) shows
                // a bit more grey asphalt through it. Never pure black.
                float speck = LineNoise(u, v, 512, 2, seed + 5);
                float g = 0.055f + speck * 0.06f + (n - 0.5f) * 0.05f;
                float thin = Mathf.Clamp01(1f - (alpha - Cutoff) / 0.3f);
                g += thin * 0.05f;
                g = Mathf.Clamp(g, 0.04f, 0.2f);
                return new Color(g, g, g + 0.006f, Mathf.Clamp01(alpha));
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 8, "line_paint_albedo");

            // Repeat along the line (u) but clamp across it (v): the ribbon's v runs 0..1 exactly once.
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Clamp;
            return tex;
        }

        /// <summary>
        /// Multi-octave value noise for a tile that is 40× longer (u) than wide (v). v is sampled at
        /// 1/40 of the u frequency so the noise cells are square in metres, and the u axis wraps
        /// (period = cells) so the texture tiles along the line.
        /// </summary>
        static float LineNoise(float u, float v, int cellsAlong, int octaves, int seed)
        {
            const float aspect = LineW / TileMetres;   // 0.025: the tile is 2 m long and 5 cm wide
            float sum = 0f, amp = 1f, norm = 0f;
            int freq = cellsAlong;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ProceduralTextures.ValueNoise(u * freq, v * freq * aspect, freq, seed + o * 101);
                norm += amp;
                amp *= 0.5f;
                freq *= 2;
            }
            return sum / norm;
        }
    }
}
