using System;
using UnityEngine;

namespace BasketballCourt
{
    /// <summary>
    /// Seeded, tileable noise and the texture generators shared by several modules.
    /// Everything here is deterministic for a given seed, so the scene looks the same every run.
    /// u/v passed to the sampling functions are in [0,1); textures wrap seamlessly when
    /// the frequency arguments are integers.
    /// </summary>
    public static class ProceduralTextures
    {
        // ── Hash / noise primitives ──────────────────────────────────────────

        /// <summary>Deterministic 0..1 hash of an integer lattice point.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / 2147483647f;
            }
        }

        static int Mod(int a, int n) { int r = a % n; return r < 0 ? r + n : r; }

        /// <summary>
        /// Smooth value noise. `x`,`y` are in lattice cells; the lattice repeats every `period` cells,
        /// so sampling with x = u * period gives a texture that tiles.
        /// </summary>
        public static float ValueNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period);
            int y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed);
            float c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        /// <summary>
        /// Fractal (multi-octave) tileable noise in 0..1. `baseFreq` = cells across the texture at octave 0.
        /// </summary>
        public static float Fbm(float u, float v, int baseFreq, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int freq = Mathf.Max(1, baseFreq);
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * freq, v * freq, freq, seed + o * 101);
                norm += amp;
                amp *= gain;
                freq *= 2;
            }
            return sum / norm;
        }

        /// <summary>Ridged variant of Fbm (sharp creases) – good for cracks and crumpled paper.</summary>
        public static float Ridged(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int freq = Mathf.Max(1, baseFreq);
            for (int o = 0; o < octaves; o++)
            {
                float n = ValueNoise(u * freq, v * freq, freq, seed + o * 131);
                n = 1f - Mathf.Abs(n * 2f - 1f);
                sum += amp * n * n;
                norm += amp;
                amp *= 0.5f;
                freq *= 2;
            }
            return sum / norm;
        }

        /// <summary>
        /// Tileable cellular (Worley) noise. Returns the distance to the nearest feature point (F1)
        /// in cell units (0..~1); `f2` receives the second-nearest distance. `f2 - f1` gives cell edges.
        /// </summary>
        public static float Cellular(float u, float v, int cells, int seed, out float f2)
        {
            float x = u * cells, y = v * cells;
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float f1 = 9f; f2 = 9f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = xi + i, cy = yi + j;
                int wx = Mod(cx, cells), wy = Mod(cy, cells);
                float px = cx + Hash(wx, wy, seed);
                float py = cy + Hash(wx, wy, seed + 7919);
                float dx = px - x, dy = py - y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
            return f1;
        }

        /// <summary>Soft radial falloff 1 at the centre → 0 at radius, with an optional noisy edge.</summary>
        public static float SoftBlob(float u, float v, float cu, float cv, float radius, float edgeNoise, int seed)
        {
            float dx = u - cu, dv = v - cv;
            float d = Mathf.Sqrt(dx * dx + dv * dv) / Mathf.Max(radius, 1e-4f);
            if (edgeNoise > 0f)
            {
                float ang = Mathf.Atan2(dv, dx) / (Mathf.PI * 2f) + 0.5f;
                d *= 1f + (Fbm(ang, 0.37f, 6, 3, seed) - 0.5f) * edgeNoise;
            }
            return Mathf.Clamp01(1f - d);
        }

        // ── Texture construction ─────────────────────────────────────────────

        /// <summary>
        /// Builds an RGBA32 texture by evaluating `pixel(u, v)` for every texel (u,v in 0..1).
        /// Use `linear = true` for normal maps and other non-colour data.
        /// </summary>
        public static Texture2D Create(int width, int height, Func<float, float, Color> pixel,
            bool linear = false, TextureWrapMode wrap = TextureWrapMode.Repeat,
            FilterMode filter = FilterMode.Trilinear, int aniso = 4, string name = null)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true, linear);
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    px[row + x] = pixel(u, v);
                }
            }
            tex.SetPixels32(px);
            tex.wrapMode = wrap;
            tex.filterMode = filter;
            tex.anisoLevel = aniso;
            if (!string.IsNullOrEmpty(name)) tex.name = name;
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>Evaluates a height function into a float grid (row-major, size×size).</summary>
        public static float[] HeightField(int size, Func<float, float, float> height)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                    h[y * size + x] = height((x + 0.5f) / size, v);
            }
            return h;
        }

        /// <summary>
        /// Tangent-space normal map from a tileable height grid. `strength` scales the slope
        /// (values around 2–8 look natural for 512–1024 px textures). Result is a linear texture and
        /// is encoded so it works with both the desktop (DXT5nm-style) and mobile Standard shader paths.
        /// </summary>
        public static Texture2D NormalFromField(float[] h, int size, float strength, string name = null)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int yUp = (y + 1) % size, yDn = (y - 1 + size) % size;
                for (int x = 0; x < size; x++)
                {
                    int xR = (x + 1) % size, xL = (x - 1 + size) % size;
                    float dx = (h[y * size + xR] - h[y * size + xL]) * strength;
                    float dy = (h[yUp * size + x] - h[yDn * size + x]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            if (!string.IsNullOrEmpty(name)) tex.name = name;
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>Convenience: normal map straight from a height function.</summary>
        public static Texture2D NormalFromHeight(int size, Func<float, float, float> height, float strength, string name = null)
        {
            return NormalFromField(HeightField(size, height), size, strength, name);
        }

        /// <summary>
        /// Fixes the classic cutout problem: auto-generated mipmaps average thin alpha shapes (wires, leaf
        /// edges) below the cutoff, so they vanish at a distance. This rescales each mip level's alpha so the
        /// fraction of texels that pass `cutoff` stays the same as in mip 0.
        /// </summary>
        public static Texture2D PreserveAlphaCoverage(Texture2D tex, float cutoff)
        {
            if (tex == null || tex.mipmapCount <= 1) return tex;
            float target = Coverage(tex.GetPixels(0), cutoff, 1f);
            for (int mip = 1; mip < tex.mipmapCount; mip++)
            {
                Color[] px = tex.GetPixels(mip);
                float lo = 1f, hi = 16f;
                for (int it = 0; it < 14; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Coverage(px, cutoff, mid) < target) lo = mid; else hi = mid;
                }
                float scale = (lo + hi) * 0.5f;
                for (int i = 0; i < px.Length; i++) px[i].a = Mathf.Clamp01(px[i].a * scale);
                tex.SetPixels(px, mip);
            }
            tex.Apply(false, false);
            return tex;
        }

        static float Coverage(Color[] px, float cutoff, float alphaScale)
        {
            if (px.Length == 0) return 0f;
            int n = 0;
            for (int i = 0; i < px.Length; i++) if (px[i].a * alphaScale >= cutoff) n++;
            return (float)n / px.Length;
        }

        /// <summary>Flat-colour normal (0.5,0.5,1) – handy when a material needs the keyword but no detail.</summary>
        public static Texture2D FlatNormal(int size = 4)
        {
            return Create(size, size, (u, v) => new Color(0.5f, 0.5f, 1f, 1f), true, name: "flat_normal");
        }

        // ── Shared material textures ─────────────────────────────────────────

        /// <summary>Light grey galvanised steel with faint vertical streaks and spangle mottling.</summary>
        public static Texture2D Galvanized(int size, int seed)
        {
            return Create(size, size, (u, v) =>
            {
                float spangle = Fbm(u, v, 16, 3, seed);
                float streak = Fbm(u * 0.15f, v, 2, 4, seed + 5);
                float g = 0.72f + (spangle - 0.5f) * 0.18f + (streak - 0.5f) * 0.08f;
                return new Color(g, g + 0.01f, g + 0.02f, 1f);
            }, name: "galvanized_albedo");
        }

        /// <summary>Dark painted iron with orange-brown rust blooming through, plus dirt streaks.</summary>
        public static Texture2D RustyIron(int size, int seed)
        {
            Color iron = new Color(0.22f, 0.22f, 0.23f);
            Color rustA = new Color(0.45f, 0.20f, 0.07f);
            Color rustB = new Color(0.62f, 0.33f, 0.12f);
            return Create(size, size, (u, v) =>
            {
                float blotch = Fbm(u, v, 4, 5, seed);
                float fine = Fbm(u, v, 32, 3, seed + 3);
                float rust = Mathf.Clamp01((blotch - 0.52f) * 4f + (fine - 0.5f) * 0.6f);
                float streak = Fbm(u * 0.2f, v, 3, 4, seed + 9);
                Color c = Color.Lerp(iron, Color.Lerp(rustA, rustB, fine), rust);
                c *= 0.85f + (streak - 0.5f) * 0.3f + (fine - 0.5f) * 0.2f;
                c.a = 1f;
                return c;
            }, name: "rusty_albedo");
        }

        public static Texture2D RustyIronNormal(int size, int seed)
        {
            return NormalFromHeight(size, (u, v) =>
            {
                float blotch = Fbm(u, v, 4, 5, seed);
                float pits = Fbm(u, v, 48, 2, seed + 3);
                float rust = Mathf.Clamp01((blotch - 0.52f) * 4f);
                return rust * pits * 0.6f;
            }, 4f, "rusty_normal");
        }

        /// <summary>Sun-bleached grey-brown planks: grain along U, a few knots, silvered edges.</summary>
        public static Texture2D WeatheredWood(int size, int seed)
        {
            Color dark = new Color(0.36f, 0.29f, 0.21f);
            Color light = new Color(0.62f, 0.55f, 0.45f);
            Color silver = new Color(0.60f, 0.60f, 0.57f);
            return Create(size, size, (u, v) =>
            {
                // grain: stretched noise along u
                float grain = Fbm(u * 1f, v * 8f, 2, 5, seed);
                float rings = Mathf.Abs(Mathf.Sin((v * 3f + grain * 0.8f) * Mathf.PI * 6f));
                float fibre = Fbm(u, v * 40f, 1, 3, seed + 2);
                float weather = Fbm(u, v, 3, 4, seed + 7);
                Color c = Color.Lerp(dark, light, rings * 0.7f + fibre * 0.3f);
                c = Color.Lerp(c, silver, Mathf.Clamp01((weather - 0.45f) * 2.2f));
                // knots
                float f2;
                float f1 = Cellular(u, v, 3, seed + 11, out f2);
                float knot = Mathf.Clamp01(1f - f1 * 9f);
                c = Color.Lerp(c, dark * 0.8f, knot * 0.8f);
                c.a = 1f;
                return c;
            }, name: "wood_albedo");
        }

        public static Texture2D WeatheredWoodNormal(int size, int seed)
        {
            return NormalFromHeight(size, (u, v) =>
            {
                float grain = Fbm(u * 1f, v * 8f, 2, 5, seed);
                float rings = Mathf.Abs(Mathf.Sin((v * 3f + grain * 0.8f) * Mathf.PI * 6f));
                float fibre = Fbm(u, v * 40f, 1, 3, seed + 2);
                return rings * 0.5f + fibre * 0.5f;
            }, 2.5f, "wood_normal");
        }

        /// <summary>Grey concrete with aggregate speckle and faint stains.</summary>
        public static Texture2D Concrete(int size, int seed)
        {
            return Create(size, size, (u, v) =>
            {
                float big = Fbm(u, v, 3, 4, seed);
                float speck = Fbm(u, v, 64, 2, seed + 1);
                float g = 0.62f + (big - 0.5f) * 0.16f + (speck - 0.5f) * 0.12f;
                return new Color(g, g, g * 0.98f, 1f);
            }, name: "concrete_albedo");
        }

        /// <summary>Painted metal where paint has chipped to show grey primer/rust underneath.</summary>
        public static Texture2D ChippedPaint(int size, Color paint, int seed)
        {
            Color under = new Color(0.35f, 0.3f, 0.26f);
            return Create(size, size, (u, v) =>
            {
                float wear = Fbm(u, v, 6, 5, seed);
                float fine = Fbm(u, v, 48, 2, seed + 4);
                float chip = Mathf.Clamp01((wear - 0.62f) * 8f + (fine - 0.5f) * 0.8f);
                float fade = 0.85f + (Fbm(u, v, 2, 3, seed + 8) - 0.5f) * 0.3f;
                Color c = Color.Lerp(paint * fade, under, chip);
                c.a = 1f;
                return c;
            }, name: "chipped_paint_albedo");
        }
    }
}
