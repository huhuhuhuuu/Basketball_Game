using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BasketballCourt
{
    /// <summary>
    /// Receives every asset (texture, mesh, material) the builder generates so the
    /// editor can persist them as project assets when the court is baked into the scene.
    /// At runtime no sink is used and the assets simply live in memory.
    /// </summary>
    public interface IAssetSink
    {
        void Save(List<Texture2D> textures, List<Mesh> meshes, List<Material> materials);
    }

    /// <summary>
    /// Shared state and object-creation helpers handed to every module.
    /// <para>
    /// Conventions:
    /// • pos / euler / scale passed to helpers are LOCAL to the parent you pass in.
    ///   Groups created with <see cref="Group"/> sit at their parent's origin (identity),
    ///   so for top-level module groups local == world.
    /// • Unity primitives: Cube is 1 m, Sphere Ø 1 m, Cylinder Ø 1 m × 2 m tall (y −1..+1),
    ///   Quad is 1 m × 1 m facing −Z (euler (90,0,0) makes it face up), Plane is 10 m × 10 m facing up.
    /// • Use <see cref="Rand"/> / <see cref="Range"/> for randomness so the scene is reproducible from the seed.
    /// </para>
    /// </summary>
    public sealed class BuildContext
    {
        public readonly Transform Root;
        public readonly System.Random Rng;
        public readonly TextureLibrary Tex;
        public readonly MaterialLibrary Mats;
        public readonly bool IsEditorBake;

        readonly List<Texture2D> _textures = new List<Texture2D>();
        readonly List<Mesh>      _meshes   = new List<Mesh>();
        readonly List<Material>  _materials= new List<Material>();

        public BuildContext(Transform root, int seed, bool isEditorBake)
        {
            Root = root;
            Rng = new System.Random(seed);
            IsEditorBake = isEditorBake;
            Tex  = new TextureLibrary(this);
            Mats = new MaterialLibrary(this);
        }

        // ── Random helpers ───────────────────────────────────────────────────

        /// <summary>Uniform float in [0,1).</summary>
        public float Rand() { return (float)Rng.NextDouble(); }
        public float Range(float min, float max) { return min + (max - min) * Rand(); }
        public int   RangeInt(int minInclusive, int maxExclusive) { return Rng.Next(minInclusive, maxExclusive); }
        public bool  Chance(float probability) { return Rand() < probability; }
        /// <summary>Random point in a disc of the given radius (XZ plane, y = 0).</summary>
        public Vector3 InDiscXZ(float radius)
        {
            float a = Rand() * Mathf.PI * 2f, r = Mathf.Sqrt(Rand()) * radius;
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        /// <summary>Random offset of ±amount on every axis.</summary>
        public Vector3 Jitter(float amount)
        {
            return new Vector3(Range(-amount, amount), Range(-amount, amount), Range(-amount, amount));
        }
        /// <summary>Random offset of ±amount on X and Z only.</summary>
        public Vector3 JitterXZ(float amount)
        {
            return new Vector3(Range(-amount, amount), 0f, Range(-amount, amount));
        }
        public Color Vary(Color c, float amount)
        {
            float k = 1f + Range(-amount, amount);
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }

        // ── Asset tracking ───────────────────────────────────────────────────

        public Texture2D Register(Texture2D t) { if (t != null && !_textures.Contains(t)) _textures.Add(t); return t; }
        public Mesh      Register(Mesh m)      { if (m != null && !_meshes.Contains(m))   _meshes.Add(m);   return m; }
        public Material  Register(Material m)  { if (m != null && !_materials.Contains(m)) _materials.Add(m); return m; }

        public void FlushTo(IAssetSink sink)
        {
            if (sink != null) sink.Save(_textures, _meshes, _materials);
        }

        /// <summary>Destroys every generated in-memory asset (used by Clear at runtime / un-baked editor builds).</summary>
        public void DestroyGeneratedAssets()
        {
            foreach (var m in _materials) SafeDestroy(m);
            foreach (var m in _meshes)    SafeDestroy(m);
            foreach (var t in _textures)  SafeDestroy(t);
            _materials.Clear(); _meshes.Clear(); _textures.Clear();
        }

        static void SafeDestroy(UnityEngine.Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.Contains(o)) return;   // persisted asset: leave it alone
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(o); return; }
#endif
            UnityEngine.Object.Destroy(o);
        }

        // ── Object creation ──────────────────────────────────────────────────

        /// <summary>Empty child object at the parent's origin.</summary>
        public GameObject Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            return go;
        }

        /// <summary>Empty child object with a local pose (useful as a mirrored frame for the second hoop).</summary>
        public GameObject Group(string name, Transform parent, Vector3 localPos, Vector3 localEuler)
        {
            var go = Group(name, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return go;
        }

        /// <summary>
        /// Unity primitive with the given local pose. Set collider=false for purely visual bits
        /// (leaves, decals, net strands) to keep the physics scene light.
        /// </summary>
        public GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat,
            bool collider = true, bool castShadows = true, bool receiveShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) DestroyComponent(c);
            }
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            ApplyRenderer(go, mat, castShadows, receiveShadows);
            return go;
        }

        /// <summary>Axis-aligned box (Cube primitive) given its centre and full size.</summary>
        public GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            bool collider = true, Vector3? euler = null)
        {
            return Primitive(PrimitiveType.Cube, name, parent, center, euler ?? Vector3.zero, size, mat, collider);
        }

        public GameObject Sphere(string name, Transform parent, Vector3 center, float diameter, Material mat,
            bool collider = true)
        {
            return Primitive(PrimitiveType.Sphere, name, parent, center, Vector3.zero, Vector3.one * diameter, mat, collider);
        }

        /// <summary>
        /// Cylinder primitive stretched between two points (a rod, rail, post or strand).
        /// Uses a capsule-free plain cylinder; ends are flat.
        /// </summary>
        public GameObject Tube(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat,
            bool collider = true, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) DestroyComponent(c);
            }
            go.transform.SetParent(parent != null ? parent : Root, false);
            Vector3 d = b - a;
            float len = d.magnitude;
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = len > 1e-5f
                ? Quaternion.FromToRotation(Vector3.up, d / len)
                : Quaternion.identity;
            go.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            ApplyRenderer(go, mat, castShadows, true);
            return go;
        }

        /// <summary>Vertical cylinder standing on `basePos` with the given height (convenience over Tube).</summary>
        public GameObject Post(string name, Transform parent, Vector3 basePos, float height, float radius, Material mat,
            bool collider = true)
        {
            return Tube(name, parent, basePos, basePos + Vector3.up * height, radius, mat, collider);
        }

        /// <summary>
        /// Flat quad lying on the floor (faces +Y). Size is (x, z) in metres.
        /// Meant for decals: no collider, no shadow casting.
        /// </summary>
        public GameObject FloorQuad(string name, Transform parent, Vector3 center, Vector2 size, float yawDeg, Material mat)
        {
            var go = Primitive(PrimitiveType.Quad, name, parent, center,
                new Vector3(90f, yawDeg, 0f), new Vector3(size.x, size.y, 1f), mat,
                collider: false, castShadows: false, receiveShadows: true);
            return go;
        }

        /// <summary>
        /// Custom mesh object. `meshCollider` adds a (non-convex) MeshCollider using the same mesh.
        /// </summary>
        public GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale,
            bool meshCollider = false, bool castShadows = true, bool receiveShadows = true)
        {
            Register(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = receiveShadows;
            if (meshCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
            return go;
        }

        public GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool meshCollider = false)
        {
            return MeshObject(name, parent, mesh, mat, Vector3.zero, Vector3.zero, Vector3.one, meshCollider);
        }

        /// <summary>Applies material / shadow flags to an existing renderer.</summary>
        public static void ApplyRenderer(GameObject go, Material mat, bool castShadows, bool receiveShadows)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
        }

        /// <summary>Marks a renderer as a two-sided-lit decal: no shadows, small render-queue offset.</summary>
        public static void MakeDecal(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        public static void DestroyComponent(Component c)
        {
            if (c == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(c);
            else UnityEngine.Object.DestroyImmediate(c);
        }
    }

    /// <summary>Named cache so two modules asking for "asphalt" share one texture.</summary>
    public sealed class TextureLibrary
    {
        readonly BuildContext _ctx;
        readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public TextureLibrary(BuildContext ctx) { _ctx = ctx; }

        public Texture2D Get(string key, Func<Texture2D> create)
        {
            Texture2D t;
            if (_cache.TryGetValue(key, out t) && t != null) return t;
            t = create();
            if (t != null)
            {
                if (string.IsNullOrEmpty(t.name)) t.name = key;
                _cache[key] = t;
                _ctx.Register(t);
            }
            return t;
        }
    }

    /// <summary>
    /// Named material cache plus the shared "look" materials several modules use
    /// (galvanised steel, weathered wood, asphalt...). Modules may also create
    /// their own one-off materials with <see cref="Get"/>.
    /// </summary>
    public sealed class MaterialLibrary
    {
        readonly BuildContext _ctx;
        readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        public MaterialLibrary(BuildContext ctx) { _ctx = ctx; }

        public Material Get(string key, Func<Material> create)
        {
            Material m;
            if (_cache.TryGetValue(key, out m) && m != null) return m;
            m = create();
            if (m != null)
            {
                if (string.IsNullOrEmpty(m.name) || m.name == "Standard") m.name = key;
                _cache[key] = m;
                _ctx.Register(m);
            }
            return m;
        }

        /// <summary>Flat-colour opaque material, cached by key.</summary>
        public Material Flat(string key, Color color, float smoothness = 0.35f, float metallic = 0f)
        {
            return Get(key, () => MatKit.Make(key, color, smoothness, metallic));
        }

        // ── Shared looks ─────────────────────────────────────────────────────

        /// <summary>Galvanised steel: light grey, slightly rough, with faint streaks. Fence posts, rails, hoop pole.</summary>
        public Material Galvanized
        {
            get
            {
                return Get("Galvanized", () =>
                {
                    var albedo = _ctx.Tex.Get("galvanized_albedo", () => ProceduralTextures.Galvanized(256, 11));
                    return MatKit.Make("Galvanized", new Color(0.78f, 0.79f, 0.8f), 0.55f, 0.85f)
                        .WithAlbedo(albedo, 1f, 1f);
                });
            }
        }

        /// <summary>Rusting dark steel for older ironwork (bench frames, trash can, brackets).</summary>
        public Material RustyIron
        {
            get
            {
                return Get("RustyIron", () =>
                {
                    var albedo = _ctx.Tex.Get("rusty_albedo", () => ProceduralTextures.RustyIron(512, 23));
                    var normal = _ctx.Tex.Get("rusty_normal", () => ProceduralTextures.RustyIronNormal(512, 23));
                    return MatKit.Make("RustyIron", Color.white, 0.32f, 0.55f)
                        .WithAlbedo(albedo, 1f, 1f).WithNormal(normal, 0.8f);
                });
            }
        }

        /// <summary>Weathered wooden planks (grey-brown, grain, knots) for bench slats.</summary>
        public Material WeatheredWood
        {
            get
            {
                return Get("WeatheredWood", () =>
                {
                    var albedo = _ctx.Tex.Get("wood_albedo", () => ProceduralTextures.WeatheredWood(512, 37));
                    var normal = _ctx.Tex.Get("wood_normal", () => ProceduralTextures.WeatheredWoodNormal(512, 37));
                    return MatKit.Make("WeatheredWood", Color.white, 0.22f, 0f)
                        .WithAlbedo(albedo, 1f, 1f).WithNormal(normal, 0.7f);
                });
            }
        }

        /// <summary>Plain grey concrete (slab sides, curbs, path).</summary>
        public Material Concrete
        {
            get
            {
                return Get("Concrete", () =>
                {
                    var albedo = _ctx.Tex.Get("concrete_albedo", () => ProceduralTextures.Concrete(512, 41));
                    return MatKit.Make("Concrete", Color.white, 0.18f, 0f).WithAlbedo(albedo, 2f, 2f);
                });
            }
        }

        /// <summary>The rim's powder-coated orange.</summary>
        public Material RimOrange
        {
            get { return Flat("RimOrange", CourtSpec.RimOrange, 0.5f, 0.3f); }
        }

        /// <summary>Black paint used for court lines, the backboard border and the shooter's square.</summary>
        public Material BlackPaint
        {
            get { return Flat("BlackPaint", CourtSpec.LinePaintBlack, 0.25f, 0f); }
        }

        /// <summary>Generic matte white (backboard face, paper).</summary>
        public Material MatteWhite
        {
            get { return Flat("MatteWhite", new Color(0.92f, 0.92f, 0.9f), 0.3f, 0f); }
        }

        /// <summary>Dark green painted steel (trash can, gate frame).</summary>
        public Material PaintedGreenSteel
        {
            get
            {
                return Get("PaintedGreenSteel", () =>
                {
                    var albedo = _ctx.Tex.Get("painted_green_albedo", () => ProceduralTextures.ChippedPaint(512, new Color(0.10f, 0.28f, 0.14f), 53));
                    return MatKit.Make("PaintedGreenSteel", Color.white, 0.45f, 0.35f).WithAlbedo(albedo, 1f, 1f);
                });
            }
        }
    }
}
