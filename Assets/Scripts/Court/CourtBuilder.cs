using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using BasketballCourt.Modules;

namespace BasketballCourt
{
    /// <summary>
    /// Builds the whole outdoor court procedurally: floor + slab, painted lines, chain-link fence,
    /// two hoops, benches, props (balls, trash, food), wear (dirt, scratches, cracks, leaves) and
    /// the surroundings (grass, trees, light poles, path).
    /// <para>
    /// • In Play mode it builds itself on Awake (unless it was already baked into the scene).
    /// • In the editor, the inspector has "Build Court" / "Clear" buttons (see CourtBuilderEditor)
    ///   that bake the same objects into the scene and save generated textures/meshes/materials as assets.
    /// • Every dimension lives in <see cref="CourtSpec"/>; every look lives in the module files.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CourtBuilder : MonoBehaviour
    {
        public const string GeneratedRootName = "GeneratedCourt";

        [Header("Generation")]
        [Tooltip("Random seed for scatter (litter, leaves, wear). Same seed = same court.")]
        public int seed = 20260910;
        [Tooltip("Build automatically when entering Play mode.")]
        public bool buildOnAwake = true;
        [Tooltip("If the court was baked into the scene in the editor, rebuild it anyway on Play.")]
        public bool rebuildIfAlreadyBuilt = false;

        [Header("Modules")]
        public bool buildFloor = true;
        public bool buildLines = true;
        public bool buildFence = true;
        public bool buildHoops = true;
        public bool buildBenches = true;
        public bool buildProps = true;
        public bool buildWear = true;
        public bool buildSurroundings = true;

        [Header("Scene settings applied on Play")]
        public bool applySceneSettings = true;
        [Range(20f, 200f)] public float shadowDistance = 90f;

        BuildContext _ctx;

        /// <summary>True when a generated court already exists under this object.</summary>
        public bool IsBuilt { get { return transform.Find(GeneratedRootName) != null; } }

        void Awake()
        {
            if (!Application.isPlaying) return;
            if (applySceneSettings) ApplySceneSettings();
            if (!buildOnAwake) return;
            if (IsBuilt && !rebuildIfAlreadyBuilt) return;
            Build(null);
        }

        /// <summary>
        /// (Re)builds the court. Pass an <see cref="IAssetSink"/> to persist generated assets (editor bake);
        /// pass null to keep them in memory (Play mode).
        /// </summary>
        public GameObject Build(IAssetSink sink)
        {
            Clear();

            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);
            _ctx = new BuildContext(root.transform, seed, sink != null);

            float t0 = Time.realtimeSinceStartup;

            if (buildFloor)        Run("Floor",        () => FloorModule.Build(_ctx, _ctx.Group("Floor").transform));
            if (buildLines)        Run("Lines",        () => LinesModule.Build(_ctx, _ctx.Group("Lines").transform));
            if (buildHoops)        Run("Hoops",        () => HoopModule.Build(_ctx, _ctx.Group("Hoops").transform));
            if (buildFence)        Run("Fence",        () => FenceModule.Build(_ctx, _ctx.Group("Fence").transform));
            if (buildBenches)      Run("Benches",      () => BenchModule.Build(_ctx, _ctx.Group("Benches").transform));
            if (buildProps)        Run("Props",        () => PropsModule.Build(_ctx, _ctx.Group("Props").transform));
            if (buildWear)         Run("Wear",         () => WearModule.Build(_ctx, _ctx.Group("Wear").transform));
            if (buildSurroundings) Run("Surroundings", () => SurroundingsModule.Build(_ctx, _ctx.Group("Surroundings").transform));

            _ctx.FlushTo(sink);

            if (Application.isPlaying)
            {
                // Everything is static: let Unity merge draw calls.
                StaticBatchingUtility.Combine(root);
            }

            Debug.Log(string.Format("[CourtBuilder] Court built in {0:0.00}s ({1} objects).",
                Time.realtimeSinceStartup - t0, root.GetComponentsInChildren<Transform>(true).Length));
            return root;
        }

        static void Run(string label, System.Action build)
        {
            try { build(); }
            catch (System.Exception e)
            {
                Debug.LogError("[CourtBuilder] Module '" + label + "' failed: " + e);
            }
        }

        /// <summary>Removes the generated court (and in-memory assets) if present.</summary>
        public void Clear()
        {
            // Collect first: in Play mode Destroy() is deferred to the end of the frame,
            // so a Find-and-destroy loop would keep finding the same object.
            var stale = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.gameObject.name == GeneratedRootName) stale.Add(child.gameObject);
            }
            foreach (var go in stale)
            {
                go.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            if (_ctx != null)
            {
                _ctx.DestroyGeneratedAssets();
                _ctx = null;
            }
        }

        /// <summary>Outdoor-daylight render settings (sky ambient, long shadows, light haze).</summary>
        public void ApplySceneSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 400f;
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowCascades = 4;
        }
    }
}
