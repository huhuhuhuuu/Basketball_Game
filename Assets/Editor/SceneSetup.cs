using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SceneSetup
{
    [MenuItem("Basketball/Setup Scene")]
    static void SetupScene()
    {
        // ── 1. Fix the Rim ─────────────────────────────────────────────
        GameObject rim = GameObject.Find("Rim");
        if (rim == null)
        {
            Debug.LogError("Could not find an object named 'Rim' in the scene.");
            return;
        }

        // Disable the solid cylinder collider so the ball can pass through the centre
        Collider rimCol = rim.GetComponent<Collider>();
        if (rimCol != null)
        {
            rimCol.enabled = false;
            EditorUtility.SetDirty(rim);
        }

        // Clean up any segments from a previous run
        for (int i = 0; i < 4; i++)
        {
            GameObject old = GameObject.Find("RimSeg" + i);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
        }

        // Place 4 thin box colliders around the rim edge (N / S / E / W)
        float  radius = 0.23f;          // half of the 0.46 scale on Rim
        Vector3 center = rim.transform.position;

        (Vector3 offset, float yRot)[] segs =
        {
            (new Vector3( radius, 0,      0),  90f),
            (new Vector3(-radius, 0,      0),  90f),
            (new Vector3(0,       0,  radius),   0f),
            (new Vector3(0,       0, -radius),   0f),
        };

        for (int i = 0; i < segs.Length; i++)
        {
            GameObject seg = new GameObject("RimSeg" + i);
            seg.transform.position = center + segs[i].offset;
            seg.transform.rotation = Quaternion.Euler(0, segs[i].yRot, 0);

            BoxCollider bc = seg.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.05f, 0.05f, 0.38f);

            Undo.RegisterCreatedObjectUndo(seg, "Create RimSeg");
        }

        // ── 2. Score Trigger ───────────────────────────────────────────
        GameObject oldTrigger = GameObject.Find("ScoreTrigger");
        if (oldTrigger != null) UnityEngine.Object.DestroyImmediate(oldTrigger);

        GameObject trigger = new GameObject("ScoreTrigger");
        trigger.transform.position = center + new Vector3(0, -0.12f, 0);

        SphereCollider sc = trigger.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius    = 0.2f;

        trigger.AddComponent<ScoreDetector>();
        Undo.RegisterCreatedObjectUndo(trigger, "Create ScoreTrigger");

        // ── 3. GameManager ─────────────────────────────────────────────
        if (GameObject.Find("GameManager") == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Basketball scene setup complete! Press Play to test.");
    }
}
