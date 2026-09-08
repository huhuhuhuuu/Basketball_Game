using UnityEngine;

// Runs automatically after each scene loads — no GameObject needed.
// Creates MobileUIControls if it isn't already in the scene.
public static class SceneBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindFirstObjectByType<MobileUIControls>() == null)
            new GameObject("MobileUIControls").AddComponent<MobileUIControls>();
    }
}
