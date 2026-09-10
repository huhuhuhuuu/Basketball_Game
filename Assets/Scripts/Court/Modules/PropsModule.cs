using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// PropsModule – the things people leave behind on a public court.
    /// <para>
    ///  • Three basketballs in the north-east corner (one wedged in the corner, two touching, one of them
    ///    half deflated) with a procedural pebbled orange texture and black seams; each ball a different age.
    ///  • A dented, chipped-paint steel trash can by the gate, lid askew, overflowing: crumpled paper, a
    ///    pizza-box corner and a bottle stick out of it.
    ///  • Leftover food: an open pizza box on the floor by the benches with one slice left (crust, cheese,
    ///    pepperoni, grease spots), a crushed soda can, a tipped paper cup with a straw, a crumpled chip
    ///    bag, a foil-wrapped sandwich half on a bench seat, napkins, a bottle rolled under a bench.
    ///  • A slumped sports bag leaning on the fence next to the benches.
    /// Everything is generated (lathes, displaced primitives, procedural textures); nothing is imported.
    /// </para>
    /// </summary>
    public static class PropsModule
    {
        const float BallRadius = 0.121f;

        public static void Build(BuildContext ctx, Transform parent)
        {
            BuildBasketballs(ctx, ctx.Group("Basketballs", parent).transform);
            BuildTrashCan(ctx, ctx.Group("TrashCan", parent).transform);
            BuildLitter(ctx, ctx.Group("Litter", parent).transform);
            BuildSportsBag(ctx, ctx.Group("SportsBag", parent).transform);
        }

        // ── Basketballs ──────────────────────────────────────────────────────

        static void BuildBasketballs(BuildContext ctx, Transform g)
        {
            Mesh ball = MeshFactory.UvSphere(BallRadius, 40, 24, "Basketball");
            ctx.Register(ball);
            Texture2D albedo = ctx.Tex.Get("basketball_albedo", () => BasketballAlbedo(ctx.RangeInt(1, 100000)));
            Texture2D normal = ctx.Tex.Get("basketball_normal", () => BasketballNormal(ctx.RangeInt(1, 100000)));
            Texture2D dirt = ctx.Tex.Get("basketball_dirt", () => DirtDetail(ctx.RangeInt(1, 100000)));

            Material fresh = ctx.Mats.Get("Basketball_Fresh", () =>
                MatKit.Make("Basketball_Fresh", Color.white, 0.38f, 0f).WithAlbedo(albedo, 1f, 1f).WithNormal(normal, 0.9f));
            Material faded = ctx.Mats.Get("Basketball_Faded", () =>
                MatKit.Make("Basketball_Faded", new Color(0.95f, 0.85f, 0.75f), 0.22f, 0f).WithAlbedo(albedo, 1f, 1f).WithNormal(normal, 0.6f));
            Material dirty = ctx.Mats.Get("Basketball_Dirty", () =>
                MatKit.Make("Basketball_Dirty", new Color(0.8f, 0.72f, 0.62f), 0.2f, 0f).WithAlbedo(albedo, 1f, 1f).WithNormal(normal, 0.7f)
                    .WithDetail(dirt, 1f, 1f));

            Vector3 corner = CourtSpec.BallCornerPos;
            float wall = CourtSpec.FenceHalfX - 0.03f;   // inner face of the fence mesh, roughly
            // Ball A: wedged right into the corner, touching both fences.
            Vector3 pa = new Vector3(wall - BallRadius, BallRadius, CourtSpec.FenceHalfZ - 0.03f - BallRadius);
            PlaceBall(ctx, g, "Basketball_A", ball, fresh, pa, Vector3.one, ctx.Range(0f, 360f));
            // Ball B: a faded old one near the corner, ball C (dirty, half flat) touching it.
            Vector3 pb = new Vector3(corner.x, BallRadius, corner.z);
            PlaceBall(ctx, g, "Basketball_B", ball, faded, pb, Vector3.one, ctx.Range(0f, 360f));
            float flat = 0.88f;
            Vector3 pc = pb + new Vector3(-0.17f, 0f, 0.17f);
            pc.y = BallRadius * flat;
            PlaceBall(ctx, g, "Basketball_C", ball, dirty, pc, new Vector3(1f, flat, 1f), ctx.Range(0f, 360f));
        }

        static void PlaceBall(BuildContext ctx, Transform g, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, float yaw)
        {
            Vector3 euler = new Vector3(ctx.Range(-40f, 40f), yaw, ctx.Range(-40f, 40f));
            GameObject go = ctx.MeshObject(name, g, mesh, mat, pos, euler, scale, false);
            var col = go.AddComponent<SphereCollider>();
            col.radius = BallRadius;
        }

        /// <summary>1 = on a seam. Seams: equator, two meridians and the two classic curved channels.</summary>
        static float SeamMask(float u, float v)
        {
            float lat = (v - 0.5f) * Mathf.PI;
            float cosLat = Mathf.Max(Mathf.Cos(lat), 0.15f);
            float d = Mathf.Abs(v - 0.5f);                                            // equator
            float um = Mathf.Repeat(u, 0.5f);
            d = Mathf.Min(d, Mathf.Min(um, 0.5f - um) * 2f * cosLat);                  // meridians at u = 0 and 0.5
            float wave = 0.27f * Mathf.Cos(u * Mathf.PI * 2f);
            d = Mathf.Min(d, Mathf.Min(Mathf.Abs(v - (0.5f + wave)), Mathf.Abs(v - (0.5f - wave))));
            const float halfWidth = 0.0075f;   // in v units: ≈ 2.9 mm on a 12 cm-radius ball
            return 1f - Mathf.Clamp01((d - halfWidth) / 0.0035f);
        }

        static Texture2D BasketballAlbedo(int seed)
        {
            Color orange = new Color(0.86f, 0.42f, 0.13f);
            Color seam = new Color(0.07f, 0.055f, 0.05f);
            return ProceduralTextures.Create(1024, 512, (u, v) =>
            {
                float pebble = ProceduralTextures.Fbm(u * 2f, v, 96, 3, seed);
                float blotch = ProceduralTextures.Fbm(u, v, 4, 2, seed + 3);
                Color c = orange * (0.8f + pebble * 0.36f) * (0.94f + blotch * 0.12f);
                c = Color.Lerp(c, seam, SeamMask(u, v));
                c.a = 1f;
                return c;
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 8, "basketball_albedo");
        }

        static Texture2D BasketballNormal(int seed)
        {
            return ProceduralTextures.NormalFromHeight(512, (u, v) =>
            {
                float pebble = ProceduralTextures.Fbm(u * 2f, v, 96, 2, seed);
                return pebble * 0.6f - SeamMask(u, v) * 0.5f;   // pebbles up, seams grooved in
            }, 3f, "basketball_normal");
        }

        /// <summary>Mid-grey detail map with a couple of dark dirt smudges (used on the dirty ball).</summary>
        static Texture2D DirtDetail(int seed)
        {
            return ProceduralTextures.Create(256, 256, (u, v) =>
            {
                float smudge = ProceduralTextures.SoftBlob(u, v, 0.3f, 0.55f, 0.28f, 0.8f, seed)
                             + ProceduralTextures.SoftBlob(u, v, 0.75f, 0.3f, 0.2f, 0.9f, seed + 1) * 0.7f;
                float grain = ProceduralTextures.Fbm(u, v, 24, 3, seed + 2);
                float d = 0.5f - Mathf.Clamp01(smudge) * (0.12f + grain * 0.1f);
                return new Color(d, d, d * 0.97f, 1f);
            }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "basketball_dirt");
        }

        // ── Trash can ────────────────────────────────────────────────────────

        static void BuildTrashCan(BuildContext ctx, Transform g)
        {
            Vector3 pos = CourtSpec.TrashCanPos;
            Transform bin = ctx.Group("Bin", g, pos, new Vector3(0f, ctx.Range(0f, 360f), 0f)).transform;

            // Tapered steel body with a rolled rim and an inner wall down to the liner level.
            var body = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.275f, 0.02f), new Vector2(0.31f, 0.83f),
                new Vector2(0.325f, 0.85f), new Vector2(0.32f, 0.87f), new Vector2(0.30f, 0.87f), new Vector2(0.295f, 0.85f),
                new Vector2(0.285f, 0.55f), new Vector2(0f, 0.55f),
            };
            Mesh bodyMesh = MeshFactory.Lathe(body, 32, "TrashCanBody");
            // A dent low on one side (someone kicked it).
            float dentAngle = ctx.Range(0f, Mathf.PI * 2f);
            Vector3 dentDir = new Vector3(Mathf.Cos(dentAngle), 0f, Mathf.Sin(dentAngle));
            MeshFactory.Displace(bodyMesh, v =>
            {
                Vector3 radial = new Vector3(v.x, 0f, v.z);
                float r = radial.magnitude;
                if (r < 0.2f) return Vector3.zero;
                float facing = Vector3.Dot(radial / r, dentDir);
                float dy = (v.y - 0.35f) / 0.18f;
                float k = Mathf.Clamp01((facing - 0.75f) / 0.25f) * Mathf.Exp(-dy * dy);
                return -radial / r * (0.045f * k);
            });
            ctx.MeshObject("Body", bin, bodyMesh, ctx.Mats.PaintedGreenSteel, Vector3.zero, Vector3.zero, Vector3.one, false);
            var col = bin.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.44f, 0f);
            col.radius = 0.31f;
            col.height = 0.9f;

            // Black bin liner: a rumpled disc at the liner level, folded over the rim on one side.
            Mesh liner = MeshFactory.Disc(0.29f, 32, "BinLiner");
            MeshFactory.Displace(liner, v => new Vector3(0f, (ProceduralTextures.Fbm(v.x * 2f + 0.5f, v.z * 2f + 0.5f, 6, 3, 77) - 0.5f) * 0.08f, 0f));
            Material plastic = ctx.Mats.Flat("BinLinerPlastic", new Color(0.05f, 0.05f, 0.06f), 0.55f, 0f);
            ctx.MeshObject("Liner", bin, liner, plastic, new Vector3(0f, 0.6f, 0f), Vector3.zero, Vector3.one, false);
            ctx.Box("LinerFlap", bin, new Vector3(0.30f, 0.80f, 0.05f), new Vector3(0.06f, 0.14f, 0.16f), plastic, false, new Vector3(6f, 15f, 12f));

            // Overflow: crumpled paper, a pizza box corner and a bottle sticking out.
            Material paper = ctx.Mats.Flat("Paper", new Color(0.9f, 0.9f, 0.87f), 0.15f, 0f);
            CrumpledPaper(ctx, bin, "PaperBall_A", new Vector3(0.08f, 0.66f, -0.1f), 0.06f, paper, 11);
            CrumpledPaper(ctx, bin, "PaperBall_B", new Vector3(-0.12f, 0.64f, 0.08f), 0.05f, paper, 12);
            CrumpledPaper(ctx, bin, "PaperBall_C", new Vector3(0.02f, 0.7f, 0.14f), 0.045f, paper, 13);
            ctx.Box("PizzaBoxCorner", bin, new Vector3(-0.05f, 0.82f, -0.02f), new Vector3(0.33f, 0.03f, 0.33f), Cardboard(ctx), false, new Vector3(35f, 25f, 8f));
            Bottle(ctx, bin, "BinBottle", new Vector3(0.14f, 0.78f, 0.1f), new Vector3(-55f, 20f, 30f), 0.9f);

            // Lid resting askew on the rim / the rubbish.
            var lid = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.30f, 0f), new Vector2(0.335f, 0f), new Vector2(0.335f, 0.03f),
                new Vector2(0.30f, 0.045f), new Vector2(0.16f, 0.07f), new Vector2(0f, 0.08f),
            };
            Mesh lidMesh = MeshFactory.Lathe(lid, 32, "TrashCanLid");
            ctx.MeshObject("Lid", bin, lidMesh, ctx.Mats.PaintedGreenSteel, new Vector3(0.09f, 0.9f, -0.06f), new Vector3(18f, 40f, -4f), Vector3.one, false);
            ctx.Sphere("LidHandle", bin, new Vector3(0.09f, 0.9f, -0.06f) + Quaternion.Euler(18f, 40f, -4f) * new Vector3(0f, 0.095f, 0f), 0.05f, ctx.Mats.PaintedGreenSteel, false);
        }

        static void CrumpledPaper(BuildContext ctx, Transform parent, string name, Vector3 pos, float radius, Material mat, int seed)
        {
            Mesh m = MeshFactory.UvSphere(radius, 18, 12, name);
            MeshFactory.Displace(m, v =>
            {
                Vector3 n = v.normalized;
                float k = ProceduralTextures.Ridged(n.x * 1.5f + 0.5f, n.z * 1.5f + n.y * 0.7f + 0.5f, 6, 3, seed);
                return n * ((k - 0.45f) * radius * 0.5f);
            });
            ctx.MeshObject(name, parent, m, mat, pos, new Vector3(ctx.Range(0f, 360f), ctx.Range(0f, 360f), 0f), Vector3.one, false);
        }

        /// <summary>A clear plastic bottle (0.5 L) without water, any pose. `scale` shrinks the whole bottle.</summary>
        static void Bottle(BuildContext ctx, Transform parent, string name, Vector3 pos, Vector3 euler, float scale)
        {
            Transform b = ctx.Group(name, parent, pos, euler).transform;
            b.localScale = Vector3.one * scale;
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.028f, 0f), new Vector2(0.033f, 0.015f), new Vector2(0.033f, 0.15f),
                new Vector2(0.022f, 0.185f), new Vector2(0.014f, 0.2f), new Vector2(0.014f, 0.225f), new Vector2(0f, 0.225f),
            };
            Material plastic = ctx.Mats.Get("ClearPlastic", () =>
                MatKit.Make("ClearPlastic", new Color(0.85f, 0.9f, 0.95f, 0.35f), 0.92f, 0f).Transparent());
            ctx.MeshObject("Bottle", b, MeshFactory.Lathe(profile, 20, name + "Mesh"), plastic, Vector3.zero, Vector3.zero, Vector3.one, false);
            Material cap = ctx.Mats.Flat("BottleCap", new Color(0.15f, 0.4f, 0.85f), 0.5f, 0f);
            ctx.Tube("Cap", b, new Vector3(0f, 0.222f, 0f), new Vector3(0f, 0.245f, 0f), 0.016f, cap, false);
            Material label = ctx.Mats.Flat("BottleLabel", new Color(0.3f, 0.55f, 0.9f), 0.3f, 0f);
            ctx.Tube("Label", b, new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0.12f, 0f), 0.0335f, label, false);
        }

        // ── Litter and leftover food ─────────────────────────────────────────

        static void BuildLitter(BuildContext ctx, Transform g)
        {
            Vector3 bench0 = new Vector3(CourtSpec.BenchEastX, 0f, CourtSpec.BenchEastZ[0]);
            Vector3 bench1 = new Vector3(CourtSpec.BenchEastX, 0f, CourtSpec.BenchEastZ[1]);
            Vector3 bin = CourtSpec.TrashCanPos;

            // In front of bench 1 (the court is toward −X from the benches).
            PizzaBox(ctx, g, bench1 + new Vector3(-0.85f, 0f, -0.55f), 20f);
            CrushedCan(ctx, g, "SodaCan_A", bench1 + new Vector3(-0.55f, 0f, 0.45f), new Color(0.75f, 0.08f, 0.08f));
            Napkin(ctx, g, "Napkin_A", bench1 + new Vector3(-1.1f, CourtSpec.OverlayY, 0.05f), 0.16f, 35f);

            // Around bench 0: a tipped cup, a bottle rolled under the seat, a napkin under it, a foil sandwich on it.
            PaperCup(ctx, g, bench0 + new Vector3(-0.6f, 0f, 0.8f));
            Bottle(ctx, g, "BenchBottle", bench0 + new Vector3(0.05f, 0.033f, 0.25f), new Vector3(90f, 0f, 0f), 1f);
            ctx.Tube("LooseCap", g, bench0 + new Vector3(-0.15f, 0.016f, 0.5f), bench0 + new Vector3(-0.13f, 0.016f, 0.5f), 0.016f,
                ctx.Mats.Flat("BottleCap", new Color(0.15f, 0.4f, 0.85f), 0.5f, 0f), false);
            Napkin(ctx, g, "Napkin_B", bench0 + new Vector3(0.0f, CourtSpec.DecalY, -0.4f), 0.14f, 110f);
            FoilSandwich(ctx, g, bench0 + new Vector3(-0.05f, 0.45f, 0.55f));

            // Around the trash can: chip bag, another can, a cup that missed the bin.
            ChipBag(ctx, g, bin + new Vector3(0.65f, 0f, 0.5f));
            CrushedCan(ctx, g, "SodaCan_B", bin + new Vector3(0.35f, 0f, 0.75f), new Color(0.1f, 0.35f, 0.75f));
            PaperCup(ctx, g, bin + new Vector3(0.45f, 0f, -0.7f));
            Napkin(ctx, g, "Napkin_C", bin + new Vector3(0.9f, CourtSpec.OverlayY, -0.2f), 0.15f, 70f);
        }

        /// <summary>Open pizza box: base, lid hinged open past vertical, one slice left inside, grease spots.</summary>
        static void PizzaBox(BuildContext ctx, Transform g, Vector3 pos, float yaw)
        {
            Transform box = ctx.Group("PizzaBox", g, pos, new Vector3(0f, yaw, 0f)).transform;
            Material card = Cardboard(ctx);
            const float size = 0.33f, wall = 0.03f;

            ctx.Box("Base", box, new Vector3(0f, 0.0075f, 0f), new Vector3(size, 0.015f, size), card, true);
            // Low side walls of the base (three sides; the hinge side is the back, +Z).
            ctx.Box("Wall_Front", box, new Vector3(0f, wall * 0.5f, -size * 0.5f + 0.005f), new Vector3(size, wall, 0.01f), card, false);
            ctx.Box("Wall_L", box, new Vector3(-size * 0.5f + 0.005f, wall * 0.5f, 0f), new Vector3(0.01f, wall, size), card, false);
            ctx.Box("Wall_R", box, new Vector3(size * 0.5f - 0.005f, wall * 0.5f, 0f), new Vector3(0.01f, wall, size), card, false);

            // Lid: hinged at the back edge, rotated 110° so it lies open past vertical.
            Transform lid = ctx.Group("Lid", box, new Vector3(0f, wall, size * 0.5f), new Vector3(110f, 0f, 0f)).transform;
            ctx.Box("LidPanel", lid, new Vector3(0f, 0.006f, -size * 0.5f), new Vector3(size, 0.012f, size), card, false);
            ctx.Box("LidLip", lid, new Vector3(0f, -0.012f, -size + 0.005f), new Vector3(size, 0.036f, 0.01f), card, false);
            // Printed red circle on the lid top (a flat disc a hair above the panel's outer face).
            Material print = ctx.Mats.Flat("PizzaPrint", new Color(0.7f, 0.12f, 0.1f), 0.3f, 0f);
            ctx.MeshObject("LidPrint", lid, MeshFactory.Disc(0.09f, 24, "LidPrint"), print, new Vector3(0f, 0.0125f, -size * 0.5f), Vector3.zero, Vector3.one, false, false, true);

            // The leftover slice: a 60° wedge of cheese, crust tube along the arc, three pepperoni.
            Transform slice = ctx.Group("Slice", box, new Vector3(-0.02f, 0.015f, -0.02f), new Vector3(0f, 15f, 0f)).transform;
            var wedge = new List<Vector2> { Vector2.zero };
            var crustPath = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(-30f, 30f, i / 8f) * Mathf.Deg2Rad;
                wedge.Add(new Vector2(Mathf.Cos(a) * 0.15f, Mathf.Sin(a) * 0.15f));
                crustPath.Add(new Vector3(Mathf.Cos(a) * 0.145f, 0.008f, Mathf.Sin(a) * 0.145f));
            }
            Material cheese = ctx.Mats.Flat("Cheese", new Color(0.93f, 0.78f, 0.36f), 0.45f, 0f);
            Material crust = ctx.Mats.Flat("Crust", new Color(0.72f, 0.48f, 0.24f), 0.25f, 0f);
            Material pepperoni = ctx.Mats.Flat("Pepperoni", new Color(0.62f, 0.15f, 0.1f), 0.35f, 0f);
            ctx.MeshObject("Cheese", slice, MeshFactory.Polygon(wedge, "PizzaWedge"), cheese, new Vector3(0f, 0.006f, 0f), Vector3.zero, Vector3.one, false, false, true);
            ctx.MeshObject("SliceBase", slice, MeshFactory.Polygon(wedge, "PizzaWedgeBase"), crust, new Vector3(0f, 0.003f, 0f), Vector3.zero, Vector3.one, false, false, true);
            ctx.MeshObject("Crust", slice, MeshFactory.Tube(crustPath, 0.012f, 8, true, "Crust"), crust, Vector3.zero, Vector3.zero, Vector3.one, false);
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.Lerp(-18f, 18f, i / 2f) * Mathf.Deg2Rad, r = 0.06f + i * 0.03f;
                ctx.MeshObject("Pepperoni_" + i, slice, MeshFactory.Disc(0.017f, 16, "Pepperoni"), pepperoni,
                    new Vector3(Mathf.Cos(a) * r, 0.0085f, Mathf.Sin(a) * r), Vector3.zero, Vector3.one, false, false, true);
            }
            // A gnawed crust end left on the other side of the box.
            var end = new List<Vector3> { new Vector3(0.09f, 0.008f, 0.1f), new Vector3(0.12f, 0.008f, 0.08f), new Vector3(0.14f, 0.009f, 0.05f) };
            ctx.MeshObject("CrustEnd", box, MeshFactory.Tube(end, 0.012f, 8, true, "CrustEnd"), crust, new Vector3(0f, 0.015f, 0f), Vector3.zero, Vector3.one, false);

            // Grease spots on the box bottom.
            Material grease = ctx.Mats.Get("Grease", () => MatKit.Make("Grease", new Color(0.45f, 0.32f, 0.12f, 0.45f), 0.6f, 0f).Fade());
            ctx.FloorQuad("Grease_A", box, new Vector3(0.07f, 0.0155f, 0.06f), new Vector2(0.09f, 0.07f), 20f, grease);
            ctx.FloorQuad("Grease_B", box, new Vector3(-0.08f, 0.0155f, 0.1f), new Vector2(0.06f, 0.05f), 70f, grease);
        }

        /// <summary>A 330 ml can, crushed to 60 % height with ridged creases, lying on its side.</summary>
        static void CrushedCan(BuildContext ctx, Transform g, string name, Vector3 pos, Color colour)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.033f, 0.006f), new Vector2(0.033f, 0.10f),
                new Vector2(0.028f, 0.11f), new Vector2(0.026f, 0.115f), new Vector2(0f, 0.115f),
            };
            Mesh m = MeshFactory.Lathe(profile, 24, name);
            int seed = ctx.RangeInt(1, 100000);
            MeshFactory.Displace(m, v =>
            {
                Vector3 radial = new Vector3(v.x, 0f, v.z);
                float r = radial.magnitude;
                Vector3 crush = new Vector3(0f, -v.y * 0.4f, 0f);                       // squash to 60 %
                if (r < 0.01f) return crush;
                float ang = Mathf.Atan2(v.z, v.x) / (Mathf.PI * 2f) + 0.5f;
                float ridge = ProceduralTextures.Ridged(ang, v.y * 4f, 12, 2, seed) - 0.5f;
                float mid = Mathf.Sin(Mathf.Clamp01(v.y / 0.115f) * Mathf.PI);            // creases strongest mid-height
                return crush + radial / r * (ridge * 0.012f * mid);
            });
            Material alu = ctx.Mats.Get(name + "_Alu", () => MatKit.Make(name + "_Alu", colour, 0.62f, 0.8f));
            Material silver = ctx.Mats.Flat("CanSilver", new Color(0.8f, 0.8f, 0.82f), 0.7f, 0.9f);
            // Lying on its side: the can's axis along local X after a 90° roll about Z; centre one radius up.
            Transform can = ctx.Group(name, g, pos + Vector3.up * 0.032f, new Vector3(0f, ctx.Range(0f, 360f), 90f)).transform;
            ctx.MeshObject("Can", can, m, alu, new Vector3(0f, -0.035f, 0f), Vector3.zero, Vector3.one, false);
            ctx.MeshObject("Top", can, MeshFactory.Disc(0.027f, 20, "CanTop"), silver, new Vector3(0f, 0.115f * 0.6f - 0.035f + 0.0005f, 0f), Vector3.zero, Vector3.one, false, false, true);
        }

        /// <summary>Paper cup (tapered lathe) with a red band and a straw, tipped over on the ground.</summary>
        static void PaperCup(BuildContext ctx, Transform g, Vector3 pos)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.033f, 0f), new Vector2(0.035f, 0.004f), new Vector2(0.045f, 0.11f),
                new Vector2(0.047f, 0.115f), new Vector2(0.043f, 0.115f), new Vector2(0.032f, 0.006f), new Vector2(0f, 0.006f),
            };
            Material cupMat = ctx.Mats.Flat("PaperCup", new Color(0.92f, 0.91f, 0.88f), 0.25f, 0f);
            Material band = ctx.Mats.Flat("CupBand", new Color(0.75f, 0.1f, 0.1f), 0.25f, 0f);
            Material straw = ctx.Mats.Flat("Straw", new Color(0.9f, 0.2f, 0.2f), 0.4f, 0f);
            // Tipped over: axis nearly horizontal, resting on its side (centre ≈ one radius above the floor).
            Transform cup = ctx.Group("PaperCup", g, pos + Vector3.up * 0.042f, new Vector3(0f, ctx.Range(0f, 360f), 82f)).transform;
            ctx.MeshObject("Cup", cup, MeshFactory.Lathe(profile, 24, "PaperCup"), cupMat, new Vector3(0f, -0.055f, 0f), Vector3.zero, Vector3.one, false);
            ctx.Tube("Band", cup, new Vector3(0f, -0.055f + 0.05f, 0f), new Vector3(0f, -0.055f + 0.075f, 0f), 0.0425f, band, false, false);
            ctx.Tube("Straw", cup, new Vector3(0.012f, -0.055f + 0.03f, 0f), new Vector3(0.03f, -0.055f + 0.2f, 0.01f), 0.003f, straw, false, false);
        }

        /// <summary>Crumpled foil chip bag: a thin box displaced with noise, shiny yellow.</summary>
        static void ChipBag(BuildContext ctx, Transform g, Vector3 pos)
        {
            Mesh m = MeshFactory.Box(new Vector3(0.18f, 0.025f, 0.25f), 4f, "ChipBag");
            int seed = ctx.RangeInt(1, 100000);
            MeshFactory.Displace(m, v =>
            {
                float n1 = ProceduralTextures.Fbm(v.x * 3f + 0.5f, v.z * 3f + 0.5f, 8, 3, seed) - 0.5f;
                float n2 = ProceduralTextures.Fbm(v.z * 3f + 0.5f, v.x * 3f + 0.5f, 8, 3, seed + 1) - 0.5f;
                float edgeLift = Mathf.Clamp01(Mathf.Abs(v.z) / 0.125f - 0.7f) * 0.02f;   // ends curl up a bit
                return new Vector3(n2 * 0.01f, Mathf.Abs(n1) * 0.02f + edgeLift, n1 * 0.01f);
            });
            Material foil = ctx.Mats.Get("ChipBagFoil", () => MatKit.Make("ChipBagFoil", new Color(0.92f, 0.72f, 0.1f), 0.72f, 0.55f));
            ctx.MeshObject("ChipBag", g, m, foil, pos + Vector3.up * 0.014f, new Vector3(0f, ctx.Range(0f, 360f), 0f), Vector3.one, false);
        }

        /// <summary>Half a sandwich wrapped in crinkled foil, on a bench seat.</summary>
        static void FoilSandwich(BuildContext ctx, Transform g, Vector3 seatPoint)
        {
            Mesh m = MeshFactory.Box(new Vector3(0.12f, 0.05f, 0.1f), 6f, "FoilSandwich");
            int seed = ctx.RangeInt(1, 100000);
            MeshFactory.Displace(m, v =>
            {
                float n = ProceduralTextures.Fbm(v.x * 4f + 0.5f, v.z * 4f + v.y * 3f + 0.5f, 6, 2, seed) - 0.5f;
                return new Vector3(0f, v.y > 0f ? n * 0.012f : 0f, 0f);
            });
            Texture2D crinkle = ctx.Tex.Get("foil_normal", () => ProceduralTextures.NormalFromHeight(128, (u, v) => ProceduralTextures.Ridged(u, v, 8, 3, 91), 2.5f, "foil_normal"));
            Material foil = ctx.Mats.Get("Foil", () => MatKit.Make("Foil", new Color(0.85f, 0.86f, 0.88f), 0.55f, 0.9f).WithNormal(crinkle, 1f));
            ctx.MeshObject("FoilSandwich", g, m, foil, seatPoint + Vector3.up * 0.025f, new Vector3(0f, ctx.Range(0f, 360f), 0f), Vector3.one, false);
        }

        /// <summary>A paper napkin lying flat: a cutout quad with wrinkled, torn edges.</summary>
        static void Napkin(BuildContext ctx, Transform g, string name, Vector3 pos, float size, float yaw)
        {
            Texture2D tex = ctx.Tex.Get("napkin_albedo", () => ProceduralTextures.Create(128, 128, (u, v) =>
            {
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float wobble = (ProceduralTextures.Fbm(u, v, 6, 3, 31) - 0.5f) * 0.12f;
                float alpha = Mathf.Clamp01((edge + wobble - 0.05f) / 0.02f);
                float fold = ProceduralTextures.Fbm(u, v, 3, 3, 32);
                float shade = 0.82f + fold * 0.16f;
                return new Color(shade, shade, shade * 0.97f, alpha);
            }, false, TextureWrapMode.Clamp, FilterMode.Trilinear, 4, "napkin_albedo"));
            Material mat = ctx.Mats.Get("Napkin", () => MatKit.Make("Napkin", Color.white, 0.15f, 0f).WithAlbedo(tex, 1f, 1f).Cutout(0.5f));
            ctx.FloorQuad(name, g, pos, new Vector2(size, size * 0.9f), yaw, mat);
        }

        // ── Sports bag ───────────────────────────────────────────────────────

        static void BuildSportsBag(BuildContext ctx, Transform g)
        {
            // Between bench 2 and the fence, leaning on the fence (top tilted toward +X).
            Vector3 pos = new Vector3(CourtSpec.FenceHalfX - 0.17f, 0.15f, CourtSpec.BenchEastZ[2] - 1.35f);
            Transform bag = ctx.Group("Bag", g, pos, new Vector3(0f, ctx.Range(-8f, 8f), -12f)).transform;

            Mesh m = MeshFactory.Box(new Vector3(0.26f, 0.3f, 0.46f), 3f, "SportsBag");
            int seed = ctx.RangeInt(1, 100000);
            MeshFactory.Displace(m, v =>
            {
                // Round the box off and let the top sag (a half-empty bag slumps).
                float n = ProceduralTextures.Fbm(v.z * 1.5f + 0.5f, v.y * 1.5f + v.x + 0.5f, 3, 3, seed) - 0.5f;
                Vector3 round = -new Vector3(v.x, 0f, v.z) * 0.12f * Mathf.Clamp01(Mathf.Abs(v.y) / 0.15f);
                float sag = v.y > 0.1f ? -0.05f * (1f - Mathf.Abs(v.z) / 0.23f) : 0f;
                return round + new Vector3(0f, sag, 0f) + new Vector3(n, n, n) * 0.015f;
            });
            Material fabric = ctx.Mats.Get("BagFabric", () => MatKit.Make("BagFabric", new Color(0.12f, 0.16f, 0.36f), 0.15f, 0f));
            GameObject body = ctx.MeshObject("Body", bag, m, fabric, Vector3.zero, Vector3.zero, Vector3.one, false);
            var col = body.AddComponent<BoxCollider>();
            col.size = new Vector3(0.26f, 0.3f, 0.46f);

            // Two carry straps looping over the top, a zipper line, a small logo patch.
            var b = new MeshFactory.Builder();
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? -0.12f : 0.12f;
                var path = MeshFactory.ArcPoints(new Vector3(0f, 0.1f, z), 0.16f, 0f, 180f, 12, 0f);
                for (int i = 0; i < path.Count; i++) path[i] = new Vector3(path[i].x, 0.1f + (path[i].z - z) * 0.9f, z);   // stand the arc up in the XY plane
                MeshFactory.AppendTube(b, path, 0.012f, 8, true, 1f);
            }
            Material strap = ctx.Mats.Flat("BagStrap", new Color(0.08f, 0.08f, 0.1f), 0.2f, 0f);
            ctx.MeshObject("Straps", bag, b.ToMesh("BagStraps"), strap, Vector3.zero, Vector3.zero, Vector3.one, false);
            ctx.Box("Zipper", bag, new Vector3(0f, 0.15f, 0f), new Vector3(0.012f, 0.006f, 0.4f), ctx.Mats.Flat("Zipper", new Color(0.7f, 0.7f, 0.72f), 0.6f, 0.8f), false);
            ctx.Box("LogoPatch", bag, new Vector3(0.131f, 0.02f, 0.05f), new Vector3(0.004f, 0.06f, 0.1f), ctx.Mats.Flat("BagLogo", new Color(0.9f, 0.85f, 0.2f), 0.3f, 0f), false);
        }

        // ── Shared materials ─────────────────────────────────────────────────

        static Material Cardboard(BuildContext ctx)
        {
            return ctx.Mats.Get("Cardboard", () =>
            {
                Texture2D tex = ctx.Tex.Get("cardboard_albedo", () => ProceduralTextures.Create(256, 256, (u, v) =>
                {
                    float fibre = ProceduralTextures.Fbm(u, v * 6f, 8, 3, 41);
                    float blotch = ProceduralTextures.Fbm(u, v, 3, 3, 42);
                    float grease = Mathf.Clamp01((blotch - 0.6f) * 5f);
                    Color c = new Color(0.72f, 0.58f, 0.4f) * (0.9f + fibre * 0.2f);
                    c = Color.Lerp(c, new Color(0.5f, 0.38f, 0.22f), grease * 0.7f);
                    c.a = 1f;
                    return c;
                }, false, TextureWrapMode.Repeat, FilterMode.Trilinear, 4, "cardboard_albedo"));
                return MatKit.Make("Cardboard", Color.white, 0.2f, 0f).WithAlbedo(tex, 1f, 1f);
            });
        }
    }
}
