using UnityEngine;

namespace BasketballCourt
{
    /// <summary>
    /// Single source of truth for every dimension in the scene.
    /// Units are metres. The court is centred on the world origin, Y is up,
    /// the long axis of the court runs along Z. The playing surface (top of the
    /// asphalt) is at y = 0. Outer edges of the boundary lines sit at |x| = 7.5 and |z| = 14.
    /// Everything below the "Placement" header is a layout decision (where props go),
    /// not a rule-book number, so modules never collide with each other.
    /// </summary>
    public static class CourtSpec
    {
        // ── Court (FIBA full court) ───────────────────────────────────────────
        public const float CourtLength = 28f;      // along Z
        public const float CourtWidth  = 15f;      // along X
        public const float HalfLength  = 14f;
        public const float HalfWidth   = 7.5f;
        public const float LineWidth   = 0.05f;

        /// <summary>Painted lines float this far above the floor to avoid z-fighting.</summary>
        public const float LineY  = 0.004f;
        /// <summary>Flat decals (dirt, cracks, puddles) sit between the floor and the lines.</summary>
        public const float DecalY = 0.002f;
        /// <summary>Decals that should sit on top of the lines (leaves, gum, scuffs).</summary>
        public const float OverlayY = 0.006f;

        // FIBA markings (outer-edge measurements unless noted)
        public const float CenterCircleRadius    = 1.8f;    // outer edge
        public const float FreeThrowLineDist     = 5.8f;    // inner edge of endline -> far edge of FT line
        public const float FreeThrowLineLength   = 3.6f;
        public const float KeyWidth              = 4.9f;    // outer edges of the key (restricted area)
        public const float FreeThrowCircleRadius = 1.8f;    // outer edge
        public const float ThreePointRadius      = 6.75f;   // outer edge, measured from the basket centre on the floor
        public const float ThreePointSideInset   = 0.9f;    // straight part of the 3pt line is 0.9 m from the sideline inner edge
        public const float NoChargeRadius        = 1.25f;   // inner edge, from basket centre
        public const float BasketFromEndline     = 1.575f;  // basket centre -> inner edge of endline
        public const float BackboardFromEndline  = 1.2f;    // backboard front face -> inner edge of endline
        public const float ThrowInMarkDist       = 8.325f;  // from inner edge of endline, outside the sideline
        public const float CenterLineOverhang    = 0.15f;   // centre line extends this far beyond each sideline

        // ── Basket / backboard ───────────────────────────────────────────────
        public const float RimHeight       = 3.05f;   // top of the ring
        public const float RimRadius       = 0.225f;  // inner radius of the ring (45 cm diameter)
        public const float RimRodRadius    = 0.009f;
        public const float BackboardWidth  = 1.8f;
        public const float BackboardHeight = 1.05f;
        public const float BackboardThick  = 0.03f;
        public const float BackboardBottom = 2.9f;    // height of the lower edge
        public const float ShooterSquareW  = 0.59f;   // outer width of the black square
        public const float ShooterSquareH  = 0.45f;   // outer height; its bottom edge is level with the rim top
        public const float BackboardBorder = 0.05f;   // black border around the board edge
        public const float PoleSetback     = 0.8f;    // pole centre this far behind the endline (outside the court)
        public const float PoleRadius      = 0.057f;  // 4.5" outdoor pole

        /// <summary>Basket centre Z for the +Z end (12.425). The −Z end is the mirror image.</summary>
        public static float BasketZ    { get { return HalfLength - BasketFromEndline; } }
        /// <summary>Backboard front face Z for the +Z end (12.8).</summary>
        public static float BackboardZ { get { return HalfLength - BackboardFromEndline; } }
        /// <summary>Pole centre Z for the +Z end (14.8).</summary>
        public static float PoleZ      { get { return HalfLength + PoleSetback; } }
        /// <summary>Ring centre (torus centre) height.</summary>
        public static float RimCenterY { get { return RimHeight - RimRodRadius; } }

        // ── Slab, fence, surroundings ────────────────────────────────────────
        public const float SlabHalfX   = 10.5f;   // asphalt slab is 21 m × 37 m
        public const float SlabHalfZ   = 18.5f;
        public const float SlabThick   = 0.12f;
        public const float GroundY     = -0.10f;  // grass level (slab edge shows 10 cm)
        public const float GroundHalf  = 150f;    // grass plane extends this far from the origin
        public const float FenceHalfX  = 10.0f;   // fence line is 0.5 m inside the slab edge
        public const float FenceHalfZ  = 18.0f;
        public const float FenceHeight = 4.0f;
        public const float FencePostSpacing = 3.0f;   // nominal; posts are spread evenly per side
        public const float GateWidth   = 1.2f;
        public const float GateHeight  = 2.1f;

        // ── Placement (layout decisions) ─────────────────────────────────────
        /// <summary>The gate sits in the west (−X) fence, centred on this Z.</summary>
        public const float GateZ = -5f;
        /// <summary>Concrete path leaves the gate towards −X at this Z, this wide.</summary>
        public const float PathWidth = 1.5f;

        /// <summary>Benches stand along the east (+X) sideline, facing the court (−X).</summary>
        public const float BenchEastX = 9.1f;
        public static readonly float[] BenchEastZ = { -5.5f, 0f, 5.5f };
        /// <summary>One more bench on the west side, north of the gate, facing +X.</summary>
        public const float BenchWestX = -9.1f;
        public const float BenchWestZ = 6.5f;
        public const float BenchLength = 1.8f;

        /// <summary>Trash can just inside the gate, against the west fence.</summary>
        public static readonly Vector3 TrashCanPos = new Vector3(-9.35f, 0f, -2.6f);
        /// <summary>Loose basketballs collect in the north-east corner (+X, +Z).</summary>
        public static readonly Vector3 BallCornerPos = new Vector3(9.3f, 0f, 17.2f);
        /// <summary>Flood-light poles in two opposite corners, inside the fence line.</summary>
        public static readonly Vector3[] LightPolePos =
        {
            new Vector3(-9.5f, 0f,  17.5f),
            new Vector3( 9.5f, 0f, -17.5f),
        };

        /// <summary>Sign posts / misc metal use this colour; kept here so modules match.</summary>
        public static readonly Color RimOrange      = new Color(0.95f, 0.36f, 0.05f);
        public static readonly Color CourtGreen     = new Color(0.16f, 0.42f, 0.20f);
        public static readonly Color LinePaintBlack = new Color(0.06f, 0.06f, 0.06f);
    }
}
