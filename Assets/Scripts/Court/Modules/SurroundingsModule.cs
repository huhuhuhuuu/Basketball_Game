using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// SurroundingsModule – everything outside the fence that makes the court read as a real park lot.
    /// <para>
    ///  • A 300 × 300 m grass plane (tileable grass texture + a slab-wide variation map so the tiling
    ///    never shows), a trodden dirt strip hugging the slab edge and a dirt patch outside the gate.
    ///  • A concrete path from the gate westwards with expansion-joint grooves.
    ///  • Trees (tapered bark trunk + mottled canopy spheres), low hedges further out.
    ///  • Two flood-light poles in opposite corners inside the fence: base plate, junction box, cross-arm
    ///    and two lamp heads aimed at the court, each with a (switched-off) spot Light for later.
    ///  • A galvanised bike rack beside the path and a concrete drinking fountain in the SE corner.
    /// </para>
    /// </summary>
    public static class SurroundingsModule
    {
        public static void Build(BuildContext ctx, Transform parent)
        {
            BuildGrass(ctx, ctx.Group("Ground", parent).transform);
            BuildPath(ctx, ctx.Group("Path", parent).transform);
            BuildTrees(ctx, ctx.Group("Trees", parent).transform);
            BuildLightPoles(ctx, ctx.Group("LightPoles", parent).transform);
            BuildBikeRack(ctx, ctx.Group("BikeRack", parent).transform);
            BuildFountain(ctx, ctx.Group("DrinkingFountain", parent).transform);
            BuildHedges(ctx, ctx.Group("Hedges", parent).transform);
        }

        // ── Grass and dirt ───────────────────────────────────────────────────

        static void BuildGrass(BuildContext ctx, Transform g)
        {
            int seed = ctx.RangeInt(1, 100000);
            Texture2D grass = ctx.Tex.Get("grass_albedo", () => ProceduralTextures.Create(512, 512, (u, v) =>
            {
                float blades = ProceduralTextures.Fbm(u, v, 64, 3, seed);
                float clumps = ProceduralTextures.Fbm(u, v, 6, 3, seed + 1);
                float dirt = Mathf.Clamp01((ProceduralTextures.Fbm(u, v, 3, 3, seed + 2) - 0.62f) * 5f);
                Color green = Color.Lerp(new Color(0.22f, 0.38f, 0.12f), new Color(0.42f, 0.52f, 0.2f), blades);
                green *= 0.85f + clumps * 0.3f;
                Color c = Color.Lerp(green, new Color(0.4f, 0.32f, 0.2f) * (0.8f + blades * 0.3f), dirt);
                c.a = 1f;
                return c;
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 8, "grass_albedo"));
            // Big soft blotches over the whole plane so the 4 m tiling never reads as a grid.
            Texture2D variation = ctx.Tex.Get("grass_variation", () => ProceduralTextures.Create(512, 512, (u, v) =>
            {
                float big = ProceduralTextures.Fbm(u, v, 8, 4, seed + 3);
                // Kept subtle: the Standard shader's detail multiply is ×2 in sRGB (≈×4.6 in linear).
                float d = 0.5f + (big - 0.5f) * 0.22f;
                return new Color(d, d, d * 0.96f, 1f);
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "grass_variation"));

            float size = CourtSpec.GroundHalf * 2f;            // 300 m
            Material mat = ctx.Mats.Get("Grass", () => MatKit.Make("Grass", Color.white, 0.1f, 0f)
                .WithAlbedo(grass, size / 4f, size / 4f)
                .WithDetail(variation, 1f, 1f));
            ctx.Primitive(PrimitiveType.Plane, "GrassPlane", g, new Vector3(0f, CourtSpec.GroundY, 0f), Vector3.zero,
                new Vector3(size / 10f, 1f, size / 10f), mat, true, false, true);

            // Trodden dirt strip around the slab (a 0.6 m ribbon just outside the concrete edge).
            Material dirtMat = DirtMaterial(ctx);
            float y = CourtSpec.GroundY + 0.002f;
            var b = new MeshFactory.Builder();
            float ox = CourtSpec.SlabHalfX + 0.3f, oz = CourtSpec.SlabHalfZ + 0.3f;
            MeshFactory.AppendRectOutline(b, -ox, -oz, ox, oz, 0.6f, y, 0.5f);
            ctx.MeshObject("TroddenStrip", g, b.ToMesh("TroddenStrip"), dirtMat, Vector3.zero, Vector3.zero, Vector3.one, false, false, true);

            // Bare patch where everyone steps off the path at the gate.
            ctx.FloorQuad("GateDirt", g, new Vector3(-CourtSpec.SlabHalfX - 1.3f, y, CourtSpec.GateZ + 1.6f), new Vector2(2.4f, 1.6f), 8f, dirtMat);
        }

        static Material DirtMaterial(BuildContext ctx)
        {
            Texture2D tex = ctx.Tex.Get("trodden_dirt", () => ProceduralTextures.Create(256, 256, (u, v) =>
            {
                float n = ProceduralTextures.Fbm(u, v, 8, 4, 121);
                float fine = ProceduralTextures.Fbm(u, v, 48, 2, 122);
                float shade = 0.85f + fine * 0.3f;
                return new Color(shade, shade * 0.95f, shade * 0.85f, Mathf.Clamp01(n * 1.3f - 0.1f));
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "trodden_dirt"));
            return ctx.Mats.Get("TroddenDirt", () => MatKit.Make("TroddenDirt", new Color(0.38f, 0.3f, 0.19f), 0.1f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.45f));
        }

        // ── Concrete path ────────────────────────────────────────────────────

        static void BuildPath(BuildContext ctx, Transform g)
        {
            float xStart = -CourtSpec.SlabHalfX, xEnd = -30f;
            float length = xStart - xEnd;                                   // 19.5
            Vector3 center = new Vector3((xStart + xEnd) * 0.5f, -0.07f, CourtSpec.GateZ);   // top −0.02, bottom −0.12 (under the grass)
            Mesh slab = MeshFactory.Box(new Vector3(length, 0.10f, CourtSpec.PathWidth), 0.5f, "PathSlab");
            GameObject path = ctx.MeshObject("PathSlab", g, slab, ctx.Mats.Concrete, center, Vector3.zero, Vector3.one, false);
            var col = path.AddComponent<BoxCollider>();
            col.size = new Vector3(length, 0.10f, CourtSpec.PathWidth);

            // Expansion joints every 1.5 m: thin dark grooves on the top face.
            Material groove = ctx.Mats.Flat("PathGroove", new Color(0.3f, 0.3f, 0.3f), 0.1f, 0f);
            for (float x = xStart - 1.5f; x > xEnd + 0.5f; x -= 1.5f)
                ctx.Box("Joint", g, new Vector3(x, -0.019f, CourtSpec.GateZ), new Vector3(0.012f, 0.004f, CourtSpec.PathWidth), groove, false);
        }

        // ── Trees and hedges ─────────────────────────────────────────────────

        static void BuildTrees(BuildContext ctx, Transform g)
        {
            Texture2D bark = ctx.Tex.Get("bark_albedo", () => ProceduralTextures.Create(256, 256, (u, v) =>
            {
                float streak = ProceduralTextures.Fbm(u, v * 0.15f, 16, 3, 131);   // stretched along the trunk
                float fine = ProceduralTextures.Fbm(u, v, 32, 2, 132);
                Color c = Color.Lerp(new Color(0.22f, 0.16f, 0.1f), new Color(0.42f, 0.34f, 0.25f), streak) * (0.85f + fine * 0.3f);
                c.a = 1f;
                return c;
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "bark_albedo"));
            Texture2D barkNormal = ctx.Tex.Get("bark_normal", () => ProceduralTextures.NormalFromHeight(256,
                (u, v) => ProceduralTextures.Fbm(u, v * 0.15f, 16, 3, 131), 4f, "bark_normal"));
            Material barkMat = ctx.Mats.Get("Bark", () => MatKit.Make("Bark", Color.white, 0.12f, 0f).WithAlbedo(bark, 2f, 1f).WithNormal(barkNormal, 1f));
            Material canopy = CanopyMaterial(ctx);

            Vector3[] spots =
            {
                new Vector3(-16f, 0f, 8f), new Vector3(-18f, 0f, -14f), new Vector3(17f, 0f, 12f), new Vector3(19f, 0f, -6f),
                new Vector3(-8f, 0f, 24f), new Vector3(9f, 0f, 25f), new Vector3(-22f, 0f, -24f), new Vector3(24f, 0f, 22f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = spots[i] + ctx.JitterXZ(1.5f);
                p.y = CourtSpec.GroundY;
                BuildTree(ctx, g, "Tree_" + i, p, barkMat, canopy);
            }
        }

        static void BuildTree(BuildContext ctx, Transform g, string name, Vector3 at, Material bark, Material canopy)
        {
            Transform t = ctx.Group(name, g, at, new Vector3(ctx.Range(-3f, 3f), ctx.Range(0f, 360f), ctx.Range(-3f, 3f))).transform;
            float h = ctx.Range(2.6f, 3.6f), r0 = ctx.Range(0.16f, 0.22f);
            var profile = new List<Vector2>
            {
                new Vector2(r0 * 1.25f, -0.2f), new Vector2(r0, 0.25f), new Vector2(r0 * 0.85f, h * 0.6f), new Vector2(r0 * 0.65f, h), new Vector2(0f, h + 0.05f),
            };
            ctx.MeshObject("Trunk", t, MeshFactory.Lathe(profile, 14, name + "_Trunk"), bark, Vector3.zero, Vector3.zero, Vector3.one, false);
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.radius = r0;
            col.height = h;

            // A couple of bare branches poking out of the canopy.
            var branch = new List<Vector3> { new Vector3(0f, h * 0.7f, 0f), new Vector3(0.6f, h * 0.9f, 0.3f) + ctx.Jitter(0.2f), new Vector3(1.1f, h * 1.05f, 0.5f) + ctx.Jitter(0.3f) };
            ctx.MeshObject("Branch", t, MeshFactory.Tube(branch, 0.05f, 6, true, name + "_Branch"), bark, Vector3.zero, Vector3.zero, Vector3.one, false);

            int blobs = ctx.RangeInt(3, 6);
            for (int i = 0; i < blobs; i++)
            {
                float d = ctx.Range(2.4f, 3.8f);
                Vector3 off = ctx.InDiscXZ(1.2f) + Vector3.up * (h + 0.8f + ctx.Range(-0.3f, 1.2f));
                ctx.Sphere("Canopy_" + i, t, off, d, CanopyVariant(ctx, canopy, ctx.RangeInt(0, 3)), false);
            }
        }

        /// <summary>Three greens so neighbouring canopy blobs and trees differ.</summary>
        static Material CanopyVariant(BuildContext ctx, Material canopy, int i)
        {
            return ctx.Mats.Get("Canopy_" + i, () =>
            {
                Material m = new Material(canopy);
                m.name = "Canopy_" + i;
                Color tint = i == 0 ? Color.white : i == 1 ? new Color(0.85f, 0.95f, 0.75f) : new Color(0.75f, 0.85f, 0.7f);
                return m.WithColor(tint);
            });
        }

        static Material CanopyMaterial(BuildContext ctx)
        {
            Texture2D tex = ctx.Tex.Get("canopy_albedo", () => ProceduralTextures.Create(256, 256, (u, v) =>
            {
                float leaf = ProceduralTextures.Fbm(u, v, 24, 3, 141);
                float patch = ProceduralTextures.Fbm(u, v, 4, 2, 142);
                Color c = Color.Lerp(new Color(0.14f, 0.32f, 0.1f), new Color(0.36f, 0.52f, 0.18f), leaf) * (0.85f + patch * 0.3f);
                c.a = 1f;
                return c;
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "canopy_albedo"));
            return ctx.Mats.Get("Canopy", () => MatKit.Make("Canopy", Color.white, 0.1f, 0f).WithAlbedo(tex, 3f, 2f));
        }

        static void BuildHedges(BuildContext ctx, Transform g)
        {
            Material canopy = CanopyMaterial(ctx);
            Vector3[] spots = { new Vector3(-26f, 0f, 12f), new Vector3(-27f, 0f, -18f), new Vector3(27f, 0f, 4f), new Vector3(4f, 0f, 30f), new Vector3(-6f, 0f, -30f) };
            float[] yaw = { 90f, 90f, 90f, 0f, 0f };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 size = new Vector3(ctx.Range(4f, 7f), ctx.Range(0.9f, 1.3f), 0.9f);
                Vector3 c = spots[i] + ctx.JitterXZ(1f);
                c.y = CourtSpec.GroundY + size.y * 0.5f - 0.05f;
                ctx.Box("Hedge_" + i, g, c, size, canopy, false, new Vector3(0f, yaw[i] + ctx.Range(-6f, 6f), 0f));
            }
        }

        // ── Flood-light poles ────────────────────────────────────────────────

        static void BuildLightPoles(BuildContext ctx, Transform g)
        {
            Material steel = ctx.Mats.Galvanized;
            Material dark = ctx.Mats.Flat("LampHousing", new Color(0.2f, 0.21f, 0.22f), 0.45f, 0.6f);
            Material lens = ctx.Mats.Flat("LampLens", new Color(0.5f, 0.55f, 0.6f), 0.9f, 0.2f);
            Material bolt = ctx.Mats.Flat("BoltSteel", new Color(0.30f, 0.30f, 0.32f), 0.5f, 0.8f);

            for (int i = 0; i < CourtSpec.LightPolePos.Length; i++)
            {
                Vector3 p = CourtSpec.LightPolePos[i];
                Transform pole = ctx.Group("LightPole_" + i, g, p, Vector3.zero).transform;
                const float height = 8f, radius = 0.08f;

                ctx.Box("BasePlate", pole, new Vector3(0f, 0.015f, 0f), new Vector3(0.4f, 0.03f, 0.4f), steel, false);
                for (int k = 0; k < 4; k++)
                {
                    float sx = (k % 2 == 0) ? -1f : 1f, sz = (k < 2) ? -1f : 1f;
                    ctx.Post("Bolt_" + k, pole, new Vector3(sx * 0.15f, 0.03f, sz * 0.15f), 0.015f, 0.014f, bolt, false);
                }
                ctx.Post("Pole", pole, Vector3.zero, height, radius, steel);
                ctx.Box("JunctionBox", pole, new Vector3(radius + 0.05f, 1.2f, 0f), new Vector3(0.1f, 0.25f, 0.15f), dark, false);

                // Head: cross-arm perpendicular to the court direction, lamps aimed 30° down at the court.
                Vector3 toCourt = new Vector3(-p.x, 0f, -p.z).normalized;
                float yaw = Mathf.Atan2(toCourt.x, toCourt.z) * Mathf.Rad2Deg;   // Euler(0,yaw,0) * forward == toCourt
                Transform head = ctx.Group("Head", pole, new Vector3(0f, height - 0.1f, 0f), new Vector3(0f, yaw, 0f)).transform;
                ctx.Tube("CrossArm", head, new Vector3(-0.65f, 0f, 0f), new Vector3(0.65f, 0f, 0f), 0.04f, steel, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    Transform lamp = ctx.Group(s < 0 ? "Lamp_L" : "Lamp_R", head, new Vector3(s * 0.5f, 0.02f, 0.12f), new Vector3(30f, 0f, 0f)).transform;
                    ctx.Box("Housing", lamp, Vector3.zero, new Vector3(0.5f, 0.25f, 0.3f), dark, false);
                    ctx.Box("Lens", lamp, new Vector3(0f, 0f, 0.152f), new Vector3(0.44f, 0.2f, 0.004f), lens, false);
                    ctx.Tube("Bracket", lamp, new Vector3(0f, 0.10f, -0.13f), new Vector3(0f, -0.02f, -0.13f), 0.02f, steel, false);   // housing → cross-arm
                    // Switched-off spot light the user can enable later for a night scene.
                    var light = lamp.gameObject.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.range = 45f;
                    light.spotAngle = 75f;
                    light.intensity = 3f;
                    light.color = new Color(1f, 0.95f, 0.85f);
                    light.shadows = LightShadows.Soft;
                    light.enabled = false;
                }
            }
        }

        // ── Bike rack ────────────────────────────────────────────────────────

        static void BuildBikeRack(BuildContext ctx, Transform g)
        {
            // Beside the path, just outside the gate, on the north side of the path.
            Vector3 origin = new Vector3(-CourtSpec.SlabHalfX - 1.8f, CourtSpec.GroundY, CourtSpec.GateZ + CourtSpec.PathWidth * 0.5f + 0.9f);
            for (int i = 0; i < 3; i++)
            {
                var path = new List<Vector3>
                {
                    new Vector3(0f, -0.1f, -0.35f), new Vector3(0f, 0.62f, -0.35f), new Vector3(0f, 0.78f, -0.25f),
                    new Vector3(0f, 0.8f, 0f), new Vector3(0f, 0.78f, 0.25f), new Vector3(0f, 0.62f, 0.35f), new Vector3(0f, -0.1f, 0.35f),
                };
                Vector3 p = origin + new Vector3(-i * 0.65f, 0f, 0f);
                ctx.MeshObject("RackLoop_" + i, g, MeshFactory.Tube(path, 0.025f, 10, false, "RackLoop"), ctx.Mats.Galvanized, p, new Vector3(0f, ctx.Range(-2f, 2f), 0f), Vector3.one, true);
            }
        }

        // ── Drinking fountain ────────────────────────────────────────────────

        static void BuildFountain(BuildContext ctx, Transform g)
        {
            Transform f = ctx.Group("Fountain", g, new Vector3(CourtSpec.FenceHalfX - 1.2f, 0f, -CourtSpec.FenceHalfZ + 1.5f), new Vector3(0f, 40f, 0f)).transform;
            var pedestal = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.2f, 0.04f), new Vector2(0.15f, 0.06f), new Vector2(0.15f, 0.8f),
                new Vector2(0.21f, 0.86f), new Vector2(0.21f, 0.9f), new Vector2(0.18f, 0.9f), new Vector2(0.1f, 0.85f), new Vector2(0f, 0.85f),
            };
            ctx.MeshObject("Pedestal", f, MeshFactory.Lathe(pedestal, 24, "FountainPedestal"), ctx.Mats.Concrete, Vector3.zero, Vector3.zero, Vector3.one, false);
            var col = f.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.radius = 0.21f;
            col.height = 0.9f;

            Material steel = ctx.Mats.Galvanized;
            ctx.Tube("Bubbler", f, new Vector3(0f, 0.85f, 0.05f), new Vector3(0f, 0.95f, 0.05f), 0.015f, steel, false);
            ctx.Sphere("BubblerHead", f, new Vector3(0f, 0.96f, 0.05f), 0.04f, steel, false);
            ctx.Tube("PushButton", f, new Vector3(0.14f, 0.7f, 0f), new Vector3(0.19f, 0.7f, 0f), 0.02f, steel, false);
            ctx.MeshObject("Drain", f, MeshFactory.Disc(0.035f, 12, "Drain"), ctx.Mats.Flat("DrainGrate", new Color(0.15f, 0.15f, 0.15f), 0.4f, 0.6f),
                new Vector3(0f, 0.851f, -0.03f), Vector3.zero, Vector3.one, false, false, true);
            // Wet ring on the slab where the overflow drips.
            Texture2D dampTex = ctx.Tex.Get("damp_blob", () => ProceduralTextures.Create(128, 128, (u, v) =>
            {
                float blob = ProceduralTextures.SoftBlob(u, v, 0.5f, 0.5f, 0.45f, 0.8f, 151);
                float speck = ProceduralTextures.Fbm(u, v, 16, 3, 152);
                return new Color(0.9f, 0.92f, 0.92f, Mathf.Clamp01(blob * 1.3f) * Mathf.Clamp01(speck * 1.6f - 0.2f));
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "damp_blob"));
            Material damp = ctx.Mats.Get("FountainDamp", () => MatKit.Make("FountainDamp", new Color(0.10f, 0.11f, 0.11f), 0.6f, 0f).WithAlbedo(dampTex, 1f, 1f).Cutout(0.35f));
            ctx.FloorQuad("DampPatch", f, new Vector3(0.1f, CourtSpec.DecalY, 0.2f), new Vector2(0.7f, 0.5f), 0f, damp);
        }
    }
}
