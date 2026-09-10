using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BasketballCourt;

namespace BasketballCourt.EditorTools
{
    /// <summary>
    /// Inspector buttons and a menu entry that bake the procedural court into the open scene
    /// so it can be inspected and tweaked without pressing Play. Generated textures, meshes and
    /// materials are written to Assets/Generated/Court so the baked scene survives a reload.
    /// </summary>
    [CustomEditor(typeof(CourtBuilder))]
    public class CourtBuilderEditor : Editor
    {
        const string GeneratedFolder = "Assets/Generated/Court";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var builder = (CourtBuilder)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Play mode builds the court automatically.\n" +
                "Build Court bakes it into this scene (and saves generated assets to " + GeneratedFolder + ") so you can look around in the Scene view.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Court", GUILayout.Height(28)))
                    BakeIntoScene(builder);
                using (new EditorGUI.DisabledScope(!builder.IsBuilt))
                {
                    if (GUILayout.Button("Clear", GUILayout.Height(28)))
                        ClearFromScene(builder);
                }
            }
        }

        [MenuItem("Basketball/Build Court In Scene")]
        static void MenuBuild()
        {
            var builder = FindFirstObjectByType<CourtBuilder>();
            if (builder == null)
            {
                var go = new GameObject("CourtBuilder");
                builder = go.AddComponent<CourtBuilder>();
                Undo.RegisterCreatedObjectUndo(go, "Create CourtBuilder");
            }
            BakeIntoScene(builder);
        }

        [MenuItem("Basketball/Clear Built Court")]
        static void MenuClear()
        {
            var builder = FindFirstObjectByType<CourtBuilder>();
            if (builder != null) ClearFromScene(builder);
        }

        static void BakeIntoScene(CourtBuilder builder)
        {
            if (Application.isPlaying)
            {
                builder.Build(null);
                return;
            }
            DeleteGeneratedFolder();
            var sink = new AssetFolderSink(GeneratedFolder);
            var root = builder.Build(sink);
            if (root != null)
            {
                // Flags are per-object, so mark the whole generated hierarchy static.
                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
            MarkDirty(builder);
        }

        static void ClearFromScene(CourtBuilder builder)
        {
            builder.Clear();
            if (!Application.isPlaying) DeleteGeneratedFolder();
            MarkDirty(builder);
        }

        static void MarkDirty(CourtBuilder builder)
        {
            if (Application.isPlaying) return;
            EditorUtility.SetDirty(builder);
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        static void DeleteGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.DeleteAsset(GeneratedFolder);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>Writes generated assets into a project folder so baked scenes keep their looks.</summary>
        sealed class AssetFolderSink : IAssetSink
        {
            readonly string _folder;
            public AssetFolderSink(string folder) { _folder = folder; }

            public void Save(List<Texture2D> textures, List<Mesh> meshes, List<Material> materials)
            {
                EnsureFolder(_folder);
                try
                {
                    AssetDatabase.StartAssetEditing();
                    var used = new HashSet<string>();
                    foreach (var t in textures) if (t != null && !AssetDatabase.Contains(t)) AssetDatabase.CreateAsset(t, Unique(used, "tex_" + t.name, ".asset"));
                    foreach (var m in meshes)   if (m != null && !AssetDatabase.Contains(m)) AssetDatabase.CreateAsset(m, Unique(used, "mesh_" + m.name, ".asset"));
                    foreach (var m in materials)if (m != null && !AssetDatabase.Contains(m)) AssetDatabase.CreateAsset(m, Unique(used, "mat_" + m.name, ".mat"));
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string Unique(HashSet<string> used, string baseName, string ext)
            {
                string safe = Sanitize(baseName);
                string candidate = safe;
                int i = 1;
                while (!used.Add(candidate)) candidate = safe + "_" + (i++);
                return _folder + "/" + candidate + ext;
            }

            static string Sanitize(string s)
            {
                if (string.IsNullOrEmpty(s)) return "asset";
                foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
                return s.Replace(' ', '_');
            }

            static void EnsureFolder(string folder)
            {
                if (AssetDatabase.IsValidFolder(folder)) return;
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                string leaf = Path.GetFileName(folder);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
