using System;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// FloorModule – the green painted asphalt slab that the whole court stands on.
    ///
    /// What it builds (everything is parented under a "Slab" group inside the parent we are given):
    ///   • CourtSurface – a Unity Plane scaled to 21 × 37 m with its top exactly at y = 0. This is
    ///     the playing surface. The Plane primitive already has a MeshCollider, so balls can bounce on it.
    ///   • SlabBody     – a 21 × 0.12 × 37 m concrete block whose top sits 1 mm under the plane, so
    ///     the slab edge shows ~10 cm above the grass (grass is at y = −0.10) like a real poured slab.
    ///
    /// How the realism is achieved (everything is generated in code – no image files):
    ///   • Asphalt albedo (1024², repeats every 2.5 m): cellular (Worley) noise makes little rounded
    ///     stones (the "aggregate"), Fbm noise adds grit between them. Green paint lies on top; on some
    ///     stone tops the paint has worn through to grey stone, and the valleys between the stones are
    ///     darker where dirt collects. A slow "roller" variation makes the paint thickness uneven.
    ///   • Normal map made from the SAME height field, so the bumps in the lighting line up with
    ///     the bumps in the colour.
    ///   • A NON-tiled "detail" map stretched once over the whole slab carries the big-picture wear:
    ///     sun-fade patches, dark worn zones under both baskets and inside both keys, tyre/shoe
    ///     scuff bands along the sidelines, two dried water-stain blooms with tide marks, hairline
    ///     cracks (the edges of a large Worley pattern) and lighter chalky patches. For a Standard
    ///     shader detail map mid-grey (0.5) is neutral: darker = dirt, lighter = fade.
    ///
    /// The detail map has to know where "under the +Z basket" is in TEXTURE space. Rather than
    /// guessing how Unity's Plane mesh lays out its UVs, we read the UVs at the plane's corners and
    /// build the mapping from that (see <see cref="PlaneUvFrame"/>). That also tells us which UV
    /// axis runs along the long side of the court, so the tiled asphalt texture repeats every 2.5 m
    /// in BOTH directions instead of being stretched.
    /// </summary>
    public static class FloorModule
    {
        // Texture resolution and how often the asphalt texture repeats across the floor (metres).
        const int   TexSize    = 1024;
        const float TileMetres = 2.5f;
        // Stones per texture tile: 2.5 m / 96 ≈ 2.6 cm per stone – typical outdoor asphalt aggregate.
        const int   PebbleCells = 96;
        // Normal-map strength (the spec asks for 4–6 on a 1024² texture).
        const float NormalStrength = 5f;
        // The concrete texture on the slab edge repeats every this many metres.
        const float ConcreteTileMetres = 1.25f;

        public static void Build(BuildContext ctx, Transform parent)
        {
            Transform slab = ctx.Group("Slab", parent).transform;

            // One seed for every floor texture, drawn from the shared RNG so the scene is reproducible.
            int seed = ctx.RangeInt(1, 100000);
            // The paint colour: court green with a small per-build variation ("vary" the base colour).
            Color paint = ctx.Vary(CourtSpec.CourtGreen, 0.08f);

            GameObject surface = BuildSurfacePlane(ctx, slab);
            PlaneUvFrame frame = PlaneUvFrame.FromMesh(GetMesh(surface));

            Material asphalt = BuildAsphaltMaterial(ctx, frame, paint, seed);
            BuildContext.ApplyRenderer(surface, asphalt, true, true);

            BuildSlabBody(ctx, slab);
        }

        // ── Objects ──────────────────────────────────────────────────────────

        /// <summary>The Unity Plane (10 × 10 m by default) scaled to the slab size, top at y = 0.</summary>
        static GameObject BuildSurfacePlane(BuildContext ctx, Transform slab)
        {
            // A Plane is 10 m across, so scale = size / 10 → (2.1, 1, 3.7).
            Vector3 scale = new Vector3(CourtSpec.SlabHalfX * 2f / 10f, 1f, CourtSpec.SlabHalfZ * 2f / 10f);
            // The material is assigned afterwards: we need the plane's mesh first to work out its UV layout.
            // collider stays on (the primitive's MeshCollider) – this is the surface balls bounce on.
            return ctx.Primitive(PrimitiveType.Plane, "CourtSurface", slab,
                Vector3.zero, Vector3.zero, scale, null,
                collider: true, castShadows: true, receiveShadows: true);
        }

        /// <summary>
        /// Concrete block under the surface: top at y = −0.001, bottom at y = −0.121, so it pokes
        /// 10 cm above the grass (GroundY = −0.10) and is buried 2 cm below it – nothing floats.
        /// Built with <see cref="MeshFactory.Box"/> (per-metre UVs) instead of a Cube primitive so the
        /// concrete texture keeps square texels on the long, thin side faces.
        /// </summary>
        static void BuildSlabBody(BuildContext ctx, Transform slab)
        {
            Vector3 size   = new Vector3(CourtSpec.SlabHalfX * 2f, CourtSpec.SlabThick, CourtSpec.SlabHalfZ * 2f);
            Vector3 center = new Vector3(0f, -CourtSpec.SlabThick * 0.5f - 0.001f, 0f);

            Mesh body = MeshFactory.Box(size, 1f / ConcreteTileMetres, "SlabBody");
            GameObject go = ctx.MeshObject("SlabBody", slab, body, SlabConcreteMaterial(ctx),
                center, Vector3.zero, Vector3.one, meshCollider: false, castShadows: true, receiveShadows: true);

            // A BoxCollider is cheaper than a MeshCollider and exactly fits the block.
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
        }

        static Mesh GetMesh(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        // ── Materials ────────────────────────────────────────────────────────

        /// <summary>
        /// Concrete for the thin slab edge. It shares the foundation's concrete texture (same cache key,
        /// so nothing is generated twice) but is tinted a little darker – the edge has been rained on for years.
        /// The box mesh already has UVs in metres, so a tiling of (1,1) repeats every ConcreteTileMetres.
        /// </summary>
        static Material SlabConcreteMaterial(BuildContext ctx)
        {
            return ctx.Mats.Get("SlabConcrete", () =>
            {
                Texture2D albedo = ctx.Tex.Get("concrete_albedo", () => ProceduralTextures.Concrete(512, 41));
                return MatKit.Make("SlabConcrete", new Color(0.80f, 0.80f, 0.78f), 0.18f, 0f)
                    .WithAlbedo(albedo, 1f, 1f);
            });
        }

        /// <summary>Green painted asphalt: tiled albedo + normal map, plus the slab-wide detail map.</summary>
        static Material BuildAsphaltMaterial(BuildContext ctx, PlaneUvFrame frame, Color paint, int seed)
        {
            return ctx.Mats.Get("CourtAsphalt", () =>
            {
                // The height field is computed once and shared by the albedo and the normal map.
                float[] field = ProceduralTextures.HeightField(TexSize, (u, v) => AsphaltHeight(u, v, seed));

                Texture2D albedo = ctx.Tex.Get("court_asphalt_albedo", () => AsphaltAlbedo(field, paint, seed));
                Texture2D normal = ctx.Tex.Get("court_asphalt_normal", () =>
                {
                    Texture2D n = ProceduralTextures.NormalFromField(field, TexSize, NormalStrength, "court_asphalt_normal");
                    n.anisoLevel = 8;   // the floor is mostly seen at a grazing angle
                    return n;
                });
                Texture2D detail = ctx.Tex.Get("court_asphalt_detail", () => DetailMap(frame, seed));

                // Metres covered by the texture's U and V axes, so one repeat is ~2.5 m either way.
                float metresAlongU = frame.UAlongX ? CourtSpec.SlabHalfX * 2f : CourtSpec.SlabHalfZ * 2f;
                float metresAlongV = frame.UAlongX ? CourtSpec.SlabHalfZ * 2f : CourtSpec.SlabHalfX * 2f;

                // The colour lives in the texture, so the material tint stays white. Dry asphalt is matte.
                return MatKit.Make("CourtAsphalt", Color.white, 0.25f, 0f)
                    .WithAlbedo(albedo, metresAlongU / TileMetres, metresAlongV / TileMetres)
                    .WithNormal(normal, 1f)
                    .WithDetail(detail, 1f, 1f);   // Plane UV is 0..1 → (1,1) covers the whole slab once
            });
        }

        // ── Tiled asphalt textures ───────────────────────────────────────────

        /// <summary>Height 0..1: little rounded stone domes (Worley cells) plus fine grit in the binder between them.</summary>
        static float AsphaltHeight(float u, float v, int seed)
        {
            float f2;
            float f1 = ProceduralTextures.Cellular(u, v, PebbleCells, seed, out f2);
            float r = Mathf.Clamp01(f1 * 1.75f);           // 0 at the stone centre → 1 at its edge
            float dome = Mathf.Sqrt(1f - r * r);            // rounded stone top
            float grit = ProceduralTextures.Fbm(u, v, 128, 2, seed + 1);
            return dome * 0.75f + grit * 0.25f;
        }

        /// <summary>Green paint over the stones; worn through to grey on some stone tops, darker in the valleys.</summary>
        static Texture2D AsphaltAlbedo(float[] field, Color paint, int seed)
        {
            Color stone = new Color(0.36f, 0.35f, 0.33f);
            return ProceduralTextures.Create(TexSize, TexSize, (u, v) =>
            {
                // Look up the height of this very texel (Create passes u = (x + 0.5) / width).
                int px = Mathf.Clamp((int)(u * TexSize), 0, TexSize - 1);
                int py = Mathf.Clamp((int)(v * TexSize), 0, TexSize - 1);
                float hgt = field[py * TexSize + px];

                // Where the paint has been scuffed thin (large soft patches) the stone tops show through.
                float wearZone = Smooth01(0.45f, 0.70f, ProceduralTextures.Fbm(u, v, 4, 3, seed + 11));
                float exposed  = Smooth01(0.72f, 0.95f, hgt) * wearZone;

                // Uneven paint thickness from the roller, and fine grit speckle.
                float roller = 1f + (ProceduralTextures.Fbm(u, v, 2, 3, seed + 12) - 0.5f) * 0.12f;
                float grit   = (ProceduralTextures.Fbm(u, v, 128, 2, seed + 13) - 0.5f) * 0.10f;

                Color c = Color.Lerp(paint * roller, stone, exposed * 0.85f);
                float shade = (0.72f + 0.28f * hgt) * (1f + grit);   // valleys between stones are darker
                c *= shade;
                c.a = 1f;
                return c;
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 8, "court_asphalt_albedo");
        }

        // ── Slab-wide detail map (not tiled) ─────────────────────────────────

        /// <summary>
        /// Large-scale variation over the whole 21 × 37 m slab. Every feature is placed in WORLD
        /// metres (x across, z along the court) using the plane's UV frame, so the worn zones really
        /// sit under the baskets no matter how the Plane mesh orients its UVs.
        /// </summary>
        static Texture2D DetailMap(PlaneUvFrame frame, int seed)
        {
            float slabW = CourtSpec.SlabHalfX * 2f, slabL = CourtSpec.SlabHalfZ * 2f;
            return ProceduralTextures.Create(TexSize, TexSize, (u, v) =>
            {
                Vector2 local = frame.UvToLocal01(u, v);          // 0..1 along local X and local Z
                float x = (local.x - 0.5f) * slabW;               // world metres
                float z = (local.y - 0.5f) * slabL;
                float d = DetailValue(x, z, seed);
                // Dirt is a little warm/brown rather than pure grey.
                float dirt = Mathf.Clamp01((0.5f - d) * 3f);
                return new Color(d + dirt * 0.02f, d, d - dirt * 0.025f, 1f);
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 8, "court_asphalt_detail");
        }

        /// <summary>Detail brightness at a world position: 0.5 = neutral, lower = dirt, higher = fade.</summary>
        static float DetailValue(float x, float z, int seed)
        {
            // Square noise coordinates (0..1 over 37 m in both axes) so the noise blobs are round, not stretched.
            float su = (x + CourtSpec.SlabHalfZ) / (CourtSpec.SlabHalfZ * 2f);
            float sv = (z + CourtSpec.SlabHalfZ) / (CourtSpec.SlabHalfZ * 2f);

            float d = 0.5f;
            d += SunFade(su, sv, seed);
            d -= TrafficWear(x, z, su, sv, seed);
            d -= SidelineScuffs(x, su, sv, seed);
            d -= WaterStain(x, z, -6.8f, -11.5f, 1.3f, seed + 21);   // low spot near the SW corner
            d -= WaterStain(x, z,  4.5f,  15.6f, 0.9f, seed + 22);   // near the NE corner
            d -= Cracks(x, z, su, sv, seed);
            d += ChalkyPatches(su, sv, seed);
            d += (ProceduralTextures.Fbm(su, sv, 64, 2, seed + 8) - 0.5f) * 0.04f;   // fine speckle hides banding
            // The Standard shader doubles the detail map (and the texture is sRGB), so keep the range gentle.
            return Mathf.Clamp(d, 0.25f, 0.72f);
        }

        /// <summary>Soft big patches: some areas bleached by the sun, some still deep green.</summary>
        static float SunFade(float su, float sv, int seed)
        {
            float big  = ProceduralTextures.Fbm(su, sv, 3, 4, seed + 1);
            float fade = Smooth01(0.58f, 0.72f, ProceduralTextures.Fbm(su, sv, 2, 3, seed + 2));
            return (big - 0.5f) * 0.10f + fade * 0.05f;
        }

        /// <summary>Darker worn zones under each basket and inside each key (feet scuff the paint most there).</summary>
        static float TrafficWear(float x, float z, float su, float sv, int seed)
        {
            float endInner = CourtSpec.HalfLength - CourtSpec.LineWidth;                         // 13.95
            float ftZ      = endInner - CourtSpec.FreeThrowLineDist + CourtSpec.LineWidth * 0.5f; // 8.175 (FT line centre)
            float halfKey  = CourtSpec.KeyWidth * 0.5f;
            float wearNoise = 0.6f + 0.4f * ProceduralTextures.Fbm(su, sv, 12, 3, seed + 3);

            float wear = 0f;
            for (int end = 0; end < 2; end++)
            {
                float sign = end == 0 ? 1f : -1f;                 // +Z end first, then the mirrored −Z end
                float basketZ = sign * CourtSpec.BasketZ;
                wear += 0.08f * Ellipse(x, z, 0f, basketZ, 2.4f, 1.9f);
                float zMin = Mathf.Min(sign * ftZ, sign * endInner);
                float zMax = Mathf.Max(sign * ftZ, sign * endInner);
                wear += 0.04f * RectMask(x, z, -halfKey, halfKey, zMin, zMax, 0.6f) * wearNoise;
            }
            // A touch of wear at the centre circle where players jostle for tip-offs.
            wear += 0.02f * Ellipse(x, z, 0f, 0f, 2.2f, 2.2f);
            return wear;
        }

        /// <summary>Tyre and shoe scuff darkening in a band just outside both sidelines (streaks run along Z).</summary>
        static float SidelineScuffs(float x, float su, float sv, int seed)
        {
            float ax = Mathf.Abs(x);
            float band = Smooth01(7.3f, 8.0f, ax) * (1f - Smooth01(9.4f, 10.2f, ax));
            if (band <= 0f) return 0f;
            // Noise squashed along Z so it reads as streaks in the direction people walk.
            float streak = ProceduralTextures.Fbm(su * 2f, sv * 0.5f, 16, 3, seed + 4);
            return band * (0.03f + 0.05f * streak);
        }

        /// <summary>A dried puddle: slightly dark inside with a darker "tide mark" ring at its edge.</summary>
        static float WaterStain(float x, float z, float cx, float cz, float radius, int seed)
        {
            // SoftBlob works in any units; we pass metres / 10 so a radius of 1.3 m becomes 0.13.
            float s = ProceduralTextures.SoftBlob(x * 0.1f, z * 0.1f, cx * 0.1f, cz * 0.1f, radius * 0.1f, 0.5f, seed);
            if (s <= 0f) return 0f;
            float inner = 0.05f * Smooth01(0.05f, 0.5f, s);
            float t = (s - 0.12f) / 0.07f;
            float ring = 0.06f * Mathf.Exp(-t * t);
            return inner + ring;
        }

        /// <summary>
        /// Hairline cracks: the edges of big (≈6 m) Worley cells, wobbled with ridged noise, drawn only
        /// in some regions of the slab so it is not one uniform net.
        /// </summary>
        static float Cracks(float x, float z, float su, float sv, int seed)
        {
            float where = Smooth01(0.50f, 0.62f, ProceduralTextures.Fbm(su, sv, 3, 3, seed + 6));
            if (where <= 0f) return 0f;

            // 8 cells over a 48 m period → 6 m cells, and the pattern never repeats inside the slab.
            float wob = (ProceduralTextures.Ridged(su * 3f, sv * 3f, 8, 3, seed + 5) - 0.5f) * 0.006f;
            float cu = x / 48f + 0.5f + wob, cv = z / 48f + 0.5f - wob;
            float f2;
            float f1 = ProceduralTextures.Cellular(cu, cv, 8, seed + 7, out f2);
            float edge = f2 - f1;                                   // 0 exactly on a cell boundary

            float crack = 1f - Smooth01(0.004f, 0.012f, edge);     // ~2–7 cm dark hairline
            float rim   = Smooth01(0.012f, 0.02f, edge) * (1f - Smooth01(0.02f, 0.04f, edge));
            return where * (0.25f * crack - 0.04f * rim);           // dark crack, faint lighter edge
        }

        /// <summary>Lighter chalky patches where the surface has gone dusty.</summary>
        static float ChalkyPatches(float su, float sv, int seed)
        {
            float chalk = Smooth01(0.60f, 0.70f, ProceduralTextures.Fbm(su, sv, 6, 4, seed + 9));
            return chalk * 0.06f;
        }

        // ── Small maths helpers ──────────────────────────────────────────────

        /// <summary>Smooth 0→1 ramp between e0 and e1 (a "smoothstep").</summary>
        static float Smooth01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>1 at the centre of an ellipse (radii rx, rz), fading softly to 0 at its edge.</summary>
        static float Ellipse(float x, float z, float cx, float cz, float rx, float rz)
        {
            float dx = (x - cx) / rx, dz = (z - cz) / rz;
            return 1f - Smooth01(0.35f, 1f, Mathf.Sqrt(dx * dx + dz * dz));
        }

        /// <summary>1 inside a rectangle, softly fading to 0 over `feather` metres around its edges.</summary>
        static float RectMask(float x, float z, float xMin, float xMax, float zMin, float zMax, float feather)
        {
            float ix = Smooth01(xMin - feather, xMin + feather, x) * (1f - Smooth01(xMax - feather, xMax + feather, x));
            float iz = Smooth01(zMin - feather, zMin + feather, z) * (1f - Smooth01(zMax - feather, zMax + feather, z));
            return ix * iz;
        }

        // ── Plane UV frame ───────────────────────────────────────────────────

        /// <summary>
        /// Describes how the Plane mesh maps its UVs onto its local X/Z. We read the UVs at three
        /// corners (min-x/min-z, max-x/min-z, min-x/max-z); every UV is then
        /// uv = origin + a·t + b·s where t = 0..1 along local X and s = 0..1 along local Z.
        /// Inverting that (Cramer's rule) turns a texel back into a position on the slab.
        /// If the mesh cannot be read we fall back to the plain u→x, v→z mapping.
        /// </summary>
        sealed class PlaneUvFrame
        {
            Vector2 _origin, _a, _b;
            float _det;

            /// <summary>True when the texture's U axis runs along the plane's local X.</summary>
            public bool UAlongX { get { return Mathf.Abs(_a.x) >= Mathf.Abs(_b.x); } }

            public static PlaneUvFrame FromMesh(Mesh mesh)
            {
                PlaneUvFrame f = new PlaneUvFrame();
                f.SetDefault();
                if (mesh != null && mesh.uv != null && mesh.uv.Length == mesh.vertexCount && mesh.vertexCount >= 3)
                {
                    Vector3[] verts = mesh.vertices;
                    Vector2[] uvs = mesh.uv;
                    Bounds bb = mesh.bounds;
                    Vector2 uv00 = NearestUv(verts, uvs, new Vector3(bb.min.x, 0f, bb.min.z));
                    Vector2 uv10 = NearestUv(verts, uvs, new Vector3(bb.max.x, 0f, bb.min.z));
                    Vector2 uv01 = NearestUv(verts, uvs, new Vector3(bb.min.x, 0f, bb.max.z));
                    f._origin = uv00;
                    f._a = uv10 - uv00;
                    f._b = uv01 - uv00;
                    f._det = f._a.x * f._b.y - f._a.y * f._b.x;
                    if (Mathf.Abs(f._det) < 1e-6f) f.SetDefault();   // degenerate: fall back to u→x, v→z
                }
                return f;
            }

            void SetDefault()
            {
                _origin = Vector2.zero;
                _a = new Vector2(1f, 0f);
                _b = new Vector2(0f, 1f);
                _det = 1f;
            }

            /// <summary>Texel (u,v) → (t, s): fraction along local X and local Z, each 0..1 on the plane.</summary>
            public Vector2 UvToLocal01(float u, float v)
            {
                Vector2 d = new Vector2(u, v) - _origin;
                float t = (d.x * _b.y - d.y * _b.x) / _det;
                float s = (_a.x * d.y - _a.y * d.x) / _det;
                return new Vector2(t, s);
            }

            static Vector2 NearestUv(Vector3[] verts, Vector2[] uvs, Vector3 target)
            {
                int best = 0;
                float bestDist = float.MaxValue;
                for (int i = 0; i < verts.Length; i++)
                {
                    float dx = verts[i].x - target.x, dz = verts[i].z - target.z;
                    float dist = dx * dx + dz * dz;
                    if (dist < bestDist) { bestDist = dist; best = i; }
                }
                return uvs[best];
            }
        }
    }
}
