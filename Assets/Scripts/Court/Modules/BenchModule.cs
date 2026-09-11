using System;
using System.Collections.Generic;
using UnityEngine;

namespace BasketballCourt.Modules
{
    /// <summary>
    /// BenchModule – weathered park benches along the sidelines (positions from CourtSpec).
    /// <para>
    /// EACH BENCH (built in a local frame: length along X, front toward +Z, then turned to face the court):
    ///  • Two cast-iron end frames (rusty iron): two legs on foot plates, a seat support bar and a
    ///    backrest upright leaning back 12°.
    ///  • Four seat slats and three backrest slats of weathered wood, every slat with its own tint and a
    ///    tiny random twist so no two look identical, bolted to the frames with small dark bolt heads.
    ///  • Two BoxColliders per bench (seat and backrest) instead of one per slat.
    /// VARIATION (the "used for years" look):
    ///  • Bench 0 is missing a seat slat, bench 1 has a snapped backrest slat, bench 2 keeps a slat
    ///    that was once painted blue, and the west bench has sunk on one end.
    ///  • A low concrete curb block next to bench 2 with a half-empty water bottle standing on it.
    /// </para>
    /// </summary>
    public static class BenchModule
    {
        const float Length     = CourtSpec.BenchLength;   // 1.8
        const float SeatTop    = 0.45f;
        const float SlatW      = 0.09f;
        const float SlatT      = 0.035f;
        const float SlatGap    = 0.015f;
        const float FrameX     = Length * 0.5f - 0.10f;   // end frames sit 10 cm in from the ends
        const float SupportTop = SeatTop - SlatT;         // 0.415: top of the seat support bar
        const float BackLean   = 12f;                     // degrees the backrest leans back
        const float UprightLen = 0.55f;
        const int   WoodVariants = 6;

        public static void Build(BuildContext ctx, Transform parent)
        {
            // East benches face −X (the court is to their west): yaw −90 turns local +Z into world −X.
            for (int i = 0; i < CourtSpec.BenchEastZ.Length; i++)
            {
                Vector3 pos = new Vector3(CourtSpec.BenchEastX, 0f, CourtSpec.BenchEastZ[i]);
                BuildBench(ctx, parent, "Bench_East_" + i, pos, -90f, i);
            }
            // West bench faces +X.
            BuildBench(ctx, parent, "Bench_West", new Vector3(CourtSpec.BenchWestX, 0f, CourtSpec.BenchWestZ), 90f, 3);

            BuildCurbAndBottle(ctx, parent);
        }

        // ── One bench ────────────────────────────────────────────────────────

        static void BuildBench(BuildContext ctx, Transform parent, string name, Vector3 pos, float yaw, int index)
        {
            // Bench 3 (west) has sunk on one end: roll it 1.2° and drop it so the low end sinks into the slab
            // while the high end still stands on it.
            Vector3 euler = new Vector3(0f, yaw + ctx.Range(-1.5f, 1.5f), index == 3 ? 1.2f : 0f);
            if (index == 3) pos.y -= (Length * 0.5f) * Mathf.Sin(1.2f * Mathf.Deg2Rad);
            Transform b = ctx.Group(name, parent, pos, euler).transform;

            Material iron = ctx.Mats.RustyIron;
            for (int s = -1; s <= 1; s += 2) BuildEndFrame(ctx, b, s * FrameX, iron);

            BuildSeatSlats(ctx, b, index);
            BuildBackSlats(ctx, b, index);
            BuildColliders(ctx, b);
        }

        /// <summary>Legs, foot plates, seat support and the leaning backrest upright at x = `x`.</summary>
        static void BuildEndFrame(BuildContext ctx, Transform b, float x, Material iron)
        {
            float legH = SupportTop - 0.06f;   // legs stop under the support bar
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * 0.20f;
                ctx.Box("Leg", b, new Vector3(x, legH * 0.5f, z), new Vector3(0.05f, legH, 0.06f), iron, false);
                ctx.Box("Foot", b, new Vector3(x, 0.006f, z), new Vector3(0.09f, 0.012f, 0.10f), iron, false);
            }
            // Seat support bar: 6 cm tall, its top at SupportTop, spanning the seat depth.
            ctx.Box("SeatSupport", b, new Vector3(x, SupportTop - 0.03f, 0f), new Vector3(0.05f, 0.06f, 0.55f), iron, false);

            // Backrest upright: leans back by BackLean degrees (rotation about X; negative = top toward −Z).
            Vector3 baseP = UprightBase();
            Vector3 up = UprightDir();
            Vector3 center = baseP + up * (UprightLen * 0.5f);
            center.x = x;
            ctx.Box("Upright", b, center, new Vector3(0.05f, UprightLen, 0.05f), iron, false, new Vector3(-BackLean, 0f, 0f));
            // A small curl at the top of the upright (cast-iron ornament).
            Vector3 top = baseP + up * UprightLen; top.x = x;
            ctx.Sphere("UprightCap", b, top, 0.07f, iron, false);
        }

        static Vector3 UprightBase() { return new Vector3(0f, SupportTop - 0.01f, -0.25f); }
        static Vector3 UprightDir()
        {
            float a = BackLean * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));      // along the upright
        }
        static Vector3 UprightNormal()
        {
            float a = BackLean * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));       // out of the upright's front face
        }

        /// <summary>Four seat slats from the front edge backwards; bench 0 is missing the third one.</summary>
        static void BuildSeatSlats(BuildContext ctx, Transform b, int index)
        {
            float zFront = 0.21f;
            for (int k = 0; k < 4; k++)
            {
                if (index == 0 && k == 2) continue;   // the missing slat
                float z = zFront - k * (SlatW + SlatGap);
                Vector3 center = new Vector3(0f, SupportTop + SlatT * 0.5f, z);
                Vector3 twist = new Vector3(ctx.Range(-0.6f, 0.6f), 0f, ctx.Range(-0.4f, 0.4f));
                Material wood = (index == 2 && k == 1) ? FadedBlueWood(ctx) : WoodVariant(ctx, index * 7 + k);
                ctx.Box("SeatSlat_" + k, b, center, new Vector3(Length, SlatT, SlatW), wood, false, twist);
                AddBolts(ctx, b, center + Vector3.up * (SlatT * 0.5f), Vector3.up);
            }
        }

        /// <summary>Three backrest slats on the front face of the uprights; bench 1 has a snapped one.</summary>
        static void BuildBackSlats(BuildContext ctx, Transform b, int index)
        {
            Vector3 baseP = UprightBase(), up = UprightDir(), n = UprightNormal();
            float[] along = { 0.12f, 0.27f, 0.42f };
            for (int k = 0; k < along.Length; k++)
            {
                Vector3 center = baseP + up * along[k] + n * (0.025f + SlatT * 0.5f);
                Vector3 rot = new Vector3(-BackLean, 0f, 0f);
                Material wood = WoodVariant(ctx, index * 7 + 4 + k);

                if (index == 1 && k == 1)
                {
                    // Snapped slat: two halves with a 4 cm gap, the loose half dropped and twisted a little.
                    float half = Length * 0.5f - 0.02f;
                    ctx.Box("BackSlat_" + k + "_L", b, center + new Vector3(-(half * 0.5f + 0.02f), 0f, 0f),
                        new Vector3(half, SlatW, SlatT), wood, false, rot);
                    ctx.Box("BackSlat_" + k + "_R", b, center + new Vector3(half * 0.5f + 0.02f, -0.02f, 0.01f),
                        new Vector3(half, SlatW, SlatT), wood, false, rot + new Vector3(0f, 0f, 3f));
                }
                else
                {
                    Vector3 twist = rot + new Vector3(ctx.Range(-0.4f, 0.4f), 0f, 0f);
                    ctx.Box("BackSlat_" + k, b, center, new Vector3(Length, SlatW, SlatT), wood, false, twist);
                }
                AddBolts(ctx, b, center + n * (SlatT * 0.5f), n);
            }
        }

        /// <summary>Two bolt heads (one per end frame) sitting on the slat's outer face.</summary>
        static void AddBolts(BuildContext ctx, Transform b, Vector3 faceCenter, Vector3 outward)
        {
            Material bolt = ctx.Mats.Flat("BenchBolt", new Color(0.22f, 0.2f, 0.18f), 0.4f, 0.7f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = faceCenter + new Vector3(s * FrameX, 0f, 0f);
                // Starts 1 cm inside the wood so a slightly twisted slat never leaves the head floating.
                ctx.Tube("Bolt", b, p - outward * 0.01f, p + outward * 0.008f, 0.007f, bolt, false, false);
            }
        }

        /// <summary>One collider for the seat volume and one (tilted) for the backrest.</summary>
        static void BuildColliders(BuildContext ctx, Transform b)
        {
            var seat = b.gameObject.AddComponent<BoxCollider>();
            seat.center = new Vector3(0f, SeatTop * 0.5f, 0f);
            seat.size = new Vector3(Length, SeatTop, 0.5f);

            Vector3 baseP = UprightBase(), up = UprightDir();
            Vector3 center = baseP + up * (UprightLen * 0.5f) + UprightNormal() * 0.03f;
            GameObject back = ctx.Group("BackrestCollider", b, center, new Vector3(-BackLean, 0f, 0f));
            var col = back.AddComponent<BoxCollider>();
            col.size = new Vector3(Length, UprightLen, 0.09f);
        }

        // ── Curb block + water bottle ────────────────────────────────────────

        static void BuildCurbAndBottle(BuildContext ctx, Transform parent)
        {
            // Beside bench 2 (east, z = +5.5), a bit further north along the fence.
            Vector3 curbPos = new Vector3(CourtSpec.BenchEastX, 0.075f, CourtSpec.BenchEastZ[2] + 1.45f);
            ctx.Box("CurbBlock", parent, curbPos, new Vector3(0.3f, 0.15f, 0.6f), ctx.Mats.Concrete, true, new Vector3(0f, 4f, 0f));

            Transform bottle = ctx.Group("WaterBottle", parent, new Vector3(curbPos.x + 0.02f, 0.15f, curbPos.z - 0.12f), new Vector3(0f, 30f, 0f)).transform;
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.028f, 0f), new Vector2(0.033f, 0.015f), new Vector2(0.033f, 0.15f),
                new Vector2(0.022f, 0.185f), new Vector2(0.014f, 0.2f), new Vector2(0.014f, 0.225f), new Vector2(0f, 0.225f),
            };
            Material plastic = ctx.Mats.Get("ClearPlastic", () =>
                MatKit.Make("ClearPlastic", new Color(0.85f, 0.9f, 0.95f, 0.35f), 0.92f, 0f).Transparent());
            ctx.MeshObject("Bottle", bottle, MeshFactory.Lathe(profile, 20, "Bottle"), plastic, Vector3.zero, Vector3.zero, Vector3.one, false, true, true);

            // Water inside (filled to ~40 %).
            var water = new List<Vector2>
            {
                new Vector2(0f, 0.002f), new Vector2(0.026f, 0.002f), new Vector2(0.03f, 0.015f), new Vector2(0.03f, 0.075f), new Vector2(0f, 0.075f),
            };
            Material waterMat = ctx.Mats.Get("BottleWater", () =>
                MatKit.Make("BottleWater", new Color(0.6f, 0.8f, 0.95f, 0.5f), 0.95f, 0f).Fade().QueueOffset(-1));
            ctx.MeshObject("Water", bottle, MeshFactory.Lathe(water, 20, "BottleWater"), waterMat, Vector3.zero, Vector3.zero, Vector3.one, false, false, true);

            // Blue cap and a paper label band.
            Material cap = ctx.Mats.Flat("BottleCap", new Color(0.15f, 0.4f, 0.85f), 0.5f, 0f);
            ctx.Tube("Cap", bottle, new Vector3(0f, 0.222f, 0f), new Vector3(0f, 0.245f, 0f), 0.016f, cap, false);
            Material label = ctx.Mats.Flat("BottleLabel", new Color(0.3f, 0.55f, 0.9f), 0.3f, 0f);
            ctx.Tube("Label", bottle, new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0.12f, 0f), 0.0335f, label, false);

            var col = bottle.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.115f, 0f);
            col.radius = 0.033f;
            col.height = 0.23f;
        }

        // ── Wood materials ───────────────────────────────────────────────────

        /// <summary>A handful of tint variants of the shared weathered-wood material, cached by index.</summary>
        static Material WoodVariant(BuildContext ctx, int i)
        {
            int v = ((i % WoodVariants) + WoodVariants) % WoodVariants;
            return ctx.Mats.Get("WoodVar_" + v, () =>
            {
                Material m = new Material(ctx.Mats.WeatheredWood);
                m.name = "WoodVar_" + v;
                return m.WithColor(ctx.Vary(new Color(0.95f, 0.93f, 0.9f), 0.12f));
            });
        }

        /// <summary>A slat that still carries a faded blue paint job.</summary>
        static Material FadedBlueWood(BuildContext ctx)
        {
            return ctx.Mats.Get("WoodFadedBlue", () =>
            {
                Material m = new Material(ctx.Mats.WeatheredWood);
                m.name = "WoodFadedBlue";
                return m.WithColor(new Color(0.55f, 0.68f, 0.95f)).WithSmoothness(0.3f);
            });
        }
    }
}
