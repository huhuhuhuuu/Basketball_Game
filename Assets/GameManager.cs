using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private int score = 0;
    private TextMeshProUGUI scoreText;

    void Awake()
    {
        Instance = this;
        CreateUI();
    }

    void CreateUI()
    {
        GameObject canvasObj = new GameObject("ScoreCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject textObj = new GameObject("ScoreText");
        textObj.transform.SetParent(canvasObj.transform, false);

        scoreText = textObj.AddComponent<TextMeshProUGUI>();
        scoreText.text      = "Score: 0";
        scoreText.fontSize   = 48;
        scoreText.color      = Color.white;
        scoreText.fontStyle  = FontStyles.Bold;
        scoreText.alignment  = TextAlignmentOptions.TopLeft;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(0, 1);
        rt.pivot            = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(20, -20);
        rt.sizeDelta        = new Vector2(250, 70);
    }

    public void AddScore()
    {
        score++;
        Debug.Log("SCORE! Total: " + score);
        if (scoreText != null)
            scoreText.text = "Score: " + score;

        // Reset ball 1.5 seconds after scoring
        BallShooter shooter = FindFirstObjectByType<BallShooter>();
        if (shooter != null)
            Invoke(nameof(DelayedReset), 1.5f);
    }

    void DelayedReset()
    {
        BallShooter shooter = FindFirstObjectByType<BallShooter>();
        if (shooter != null) shooter.ResetBall();
    }
}
