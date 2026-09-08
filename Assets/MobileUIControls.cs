using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MobileUIControls : MonoBehaviour
{
    // ── Movement ──────────────────────────────────────────────────────────────
    bool  moveFwd, moveBack, moveLeft, moveRight;
    const float MoveSpeed = 4f;
    static readonly Vector3 MinPos = new Vector3(-6f, 1.5f,  0f);
    static readonly Vector3 MaxPos = new Vector3( 6f, 1.5f, 10f);

    // ── Timing bar ────────────────────────────────────────────────────────────
    float timingVal   = 0f;
    float timingDir   = 1f;
    const float TimingSpeed = 1.1f;   // full traversals per second
    const float BarHeight   = 280f;
    RectTransform timingIndicator;

    // Screen (pick) temporarily widens the green zone
    bool  screenBoost      = false;
    float screenBoostTimer = 0f;

    // ── Refs ──────────────────────────────────────────────────────────────────
    BallShooter     shooter;
    TextMeshProUGUI feedbackText;
    float           feedbackTimer = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        shooter = FindFirstObjectByType<BallShooter>();
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        MovePlayer();
        TickTimingBar();
        TickFeedback();
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    void MovePlayer()
    {
        if (shooter == null || shooter.HasShot) return;

        Vector3 pos = shooter.transform.position;
        if (moveFwd)   pos.z += MoveSpeed * Time.deltaTime;
        if (moveBack)  pos.z -= MoveSpeed * Time.deltaTime;
        if (moveRight) pos.x += MoveSpeed * Time.deltaTime;
        if (moveLeft)  pos.x -= MoveSpeed * Time.deltaTime;

        pos.x = Mathf.Clamp(pos.x, MinPos.x, MaxPos.x);
        pos.z = Mathf.Clamp(pos.z, MinPos.z, MaxPos.z);
        pos.y = 1.5f;

        shooter.transform.position = pos;
        shooter.UpdateStartPosition(pos);
    }

    // ── Timing bar ────────────────────────────────────────────────────────────

    void TickTimingBar()
    {
        timingVal += timingDir * TimingSpeed * Time.deltaTime;
        if (timingVal >= 1f) { timingVal = 1f; timingDir = -1f; }
        if (timingVal <= 0f) { timingVal = 0f; timingDir =  1f; }

        if (timingIndicator != null)
            timingIndicator.anchoredPosition = new Vector2(0f, timingVal * BarHeight);

        if (screenBoost)
        {
            screenBoostTimer -= Time.deltaTime;
            if (screenBoostTimer <= 0f) screenBoost = false;
        }
    }

    TimingZone CurrentZone()
    {
        float goodFloor = screenBoost ? 0.45f : 0.65f;
        if (timingVal >= goodFloor) return TimingZone.Good;
        if (timingVal >= 0.35f)    return TimingZone.OK;
        return TimingZone.Bad;
    }

    // ── Feedback text ─────────────────────────────────────────────────────────

    void ShowFeedback(string msg, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text  = msg;
        feedbackText.color = color;
        feedbackTimer = 1.4f;
    }

    void TickFeedback()
    {
        if (feedbackTimer <= 0f) return;
        feedbackTimer -= Time.deltaTime;
        if (feedbackTimer <= 0f && feedbackText != null)
            feedbackText.text = "";
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    void OnShoot()
    {
        if (shooter == null || shooter.HasShot) return;
        TimingZone zone = CurrentZone();
        shooter.ShootWithTiming(zone);
        switch (zone)
        {
            case TimingZone.Good: ShowFeedback("PERFECT!",  Color.green);                    break;
            case TimingZone.OK:   ShowFeedback("GOOD",      Color.yellow);                   break;
            default:              ShowFeedback("MISS...",   new Color(1f, 0.35f, 0.35f));    break;
        }
    }

    void OnPass()
    {
        ShowFeedback("PASS!", new Color(0.4f, 0.8f, 1f));
    }

    void OnScreen()
    {
        screenBoost      = true;
        screenBoostTimer = 2f;
        ShowFeedback("SCREEN!  +2s green zone", new Color(0.75f, 0.5f, 1f));
    }

    // ── UI builder ────────────────────────────────────────────────────────────

    void BuildUI()
    {
        GameObject root = new GameObject("MobileCanvas");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 10;
        CanvasScaler cs = root.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        root.AddComponent<GraphicRaycaster>();

        Transform cvs = root.transform;
        BuildDPad(cvs);
        BuildTimingBar(cvs);
        BuildActionButtons(cvs);
        BuildFeedbackText(cvs);
    }

    // ── D-pad ─────────────────────────────────────────────────────────────────

    void BuildDPad(Transform cvs)
    {
        const float S = 80f;   // button size
        const float G = 92f;   // grid step (center-to-center)
        float cx = 60f + G;    // cross center X from left
        float cy = 60f + G;    // cross center Y from bottom

        MakeHoldBtn(cvs, "▲", new Vector2(cx,     cy + G), S, () => moveFwd   = true, () => moveFwd   = false);
        MakeHoldBtn(cvs, "▼", new Vector2(cx,     cy - G), S, () => moveBack  = true, () => moveBack  = false);
        MakeHoldBtn(cvs, "◄", new Vector2(cx - G, cy    ), S, () => moveLeft  = true, () => moveLeft  = false);
        MakeHoldBtn(cvs, "►", new Vector2(cx + G, cy    ), S, () => moveRight = true, () => moveRight = false);
    }

    // ── Timing bar ────────────────────────────────────────────────────────────

    void BuildTimingBar(Transform cvs)
    {
        // Panel anchored bottom-right, to the left of action buttons
        // Action buttons occupy ~140px from right (40 pad + 100 wide)
        // Gap 20px → bar right edge at 160px from screen right
        GameObject panel = MakeRect("TimingBar", cvs);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1, 0);
        panelRT.anchorMax        = new Vector2(1, 0);
        panelRT.pivot            = new Vector2(1, 0);
        panelRT.anchoredPosition = new Vector2(-160f, 60f);
        panelRT.sizeDelta        = new Vector2(50f, BarHeight);

        panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.75f);

        // Color zones
        MakeZone(panel.transform, new Color(0.9f,  0.15f, 0.15f, 0.9f), 0f,    0.35f);  // red  — bad
        MakeZone(panel.transform, new Color(1f,    0.78f, 0.05f, 0.9f), 0.35f, 0.65f);  // yellow — ok
        MakeZone(panel.transform, new Color(0.15f, 0.85f, 0.25f, 0.9f), 0.65f, 1f);    // green — good

        // Text labels
        MakeBarLabel(panel.transform, "GOOD", new Vector2(0f, BarHeight - 18f), 10f);
        MakeBarLabel(panel.transform, "BAD",  new Vector2(0f, 4f),              10f);

        // "TIMING" above the bar
        GameObject hdr = MakeRect("Header", panel.transform);
        RectTransform hdrRT = hdr.GetComponent<RectTransform>();
        hdrRT.anchorMin        = new Vector2(0, 1);
        hdrRT.anchorMax        = new Vector2(1, 1);
        hdrRT.pivot            = new Vector2(0.5f, 0);
        hdrRT.anchoredPosition = new Vector2(0, 4);
        hdrRT.sizeDelta        = new Vector2(0, 22);
        TextMeshProUGUI hdrTxt = hdr.AddComponent<TextMeshProUGUI>();
        hdrTxt.text      = "TIMING";
        hdrTxt.fontSize  = 13;
        hdrTxt.color     = Color.white;
        hdrTxt.alignment = TextAlignmentOptions.Center;
        hdrTxt.fontStyle = FontStyles.Bold;

        // Moving white indicator line
        GameObject ind = MakeRect("Indicator", panel.transform);
        timingIndicator = ind.GetComponent<RectTransform>();
        timingIndicator.anchorMin        = new Vector2(0, 0);
        timingIndicator.anchorMax        = new Vector2(1, 0);
        timingIndicator.pivot            = new Vector2(0.5f, 0.5f);
        timingIndicator.sizeDelta        = new Vector2(0, 6);
        timingIndicator.anchoredPosition = new Vector2(0, 0);
        ind.AddComponent<Image>().color  = Color.white;
    }

    void MakeZone(Transform parent, Color color, float yMin, float yMax)
    {
        GameObject go = MakeRect("Zone", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, yMin);
        rt.anchorMax = new Vector2(1, yMax);
        rt.sizeDelta = Vector2.zero;
        go.AddComponent<Image>().color = color;
    }

    void MakeBarLabel(Transform parent, string text, Vector2 pos, float size)
    {
        GameObject go = MakeRect("Lbl_" + text, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0, 0);
        rt.anchorMax        = new Vector2(1, 0);
        rt.pivot            = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(0, size + 2);
        TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
        txt.text      = text;
        txt.fontSize  = size;
        txt.color     = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
    }

    // ── Action buttons ────────────────────────────────────────────────────────

    void BuildActionButtons(Transform cvs)
    {
        const float S   = 100f;   // size
        const float SP  = 15f;    // spacing between buttons
        const float PAD = 40f;    // right-edge padding

        float y0 = 60f;
        MakeTapBtn(cvs, "SHOOT",  PAD, y0,            S, new Color(1f,   0.55f, 0f   ), OnShoot);
        MakeTapBtn(cvs, "PASS",   PAD, y0 + S + SP,   S, new Color(0.2f, 0.6f,  1f   ), OnPass);
        MakeTapBtn(cvs, "SCREEN", PAD, y0 + 2*(S+SP), S, new Color(0.55f,0.3f,  0.95f), OnScreen);
    }

    // ── Feedback text (center-screen) ─────────────────────────────────────────

    void BuildFeedbackText(Transform cvs)
    {
        GameObject go = MakeRect("FeedbackText", cvs);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 120);
        rt.sizeDelta        = new Vector2(500, 90);
        feedbackText = go.AddComponent<TextMeshProUGUI>();
        feedbackText.text      = "";
        feedbackText.fontSize  = 58;
        feedbackText.color     = Color.white;
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.fontStyle = FontStyles.Bold;
    }

    // ── Widget factories ──────────────────────────────────────────────────────

    // Hold button (D-pad arrows)
    void MakeHoldBtn(Transform cvs, string label, Vector2 center, float size,
        System.Action onDown, System.Action onUp)
    {
        GameObject go = MakeRect("DBtn_" + label, cvs);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0, 0);
        rt.anchorMax        = new Vector2(0, 0);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta        = new Vector2(size, size);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);

        // Arrow text
        GameObject lGo = MakeRect("Lbl", go.transform);
        lGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        lGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
        lGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        TextMeshProUGUI lTxt = lGo.AddComponent<TextMeshProUGUI>();
        lTxt.text      = label;
        lTxt.fontSize  = 36;
        lTxt.color     = new Color(1f, 1f, 1f, 0.85f);
        lTxt.alignment = TextAlignmentOptions.Center;

        Color normal  = new Color(0f, 0f, 0f, 0.45f);
        Color pressed = new Color(0.3f, 0.3f, 0.3f, 0.7f);

        EventTrigger trig = go.AddComponent<EventTrigger>();
        AddEvt(trig, EventTriggerType.PointerDown, _ => { img.color = pressed; onDown(); });
        AddEvt(trig, EventTriggerType.PointerUp,   _ => { img.color = normal;  onUp();   });
        AddEvt(trig, EventTriggerType.PointerExit, _ => { img.color = normal;  onUp();   });
    }

    // Tap button (SHOOT / PASS / SCREEN) — fires on PointerDown for tight timing
    void MakeTapBtn(Transform cvs, string label, float rightPad, float bottomY,
        float size, Color color, System.Action onTap)
    {
        GameObject go = MakeRect("ABtn_" + label, cvs);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1, 0);
        rt.anchorMax        = new Vector2(1, 0);
        rt.pivot            = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-rightPad, bottomY);
        rt.sizeDelta        = new Vector2(size, size);

        Color normal  = new Color(color.r, color.g, color.b, 0.85f);
        Color pressed = new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, 1f);
        Image img = go.AddComponent<Image>();
        img.color = normal;

        // Button label
        GameObject lGo = MakeRect("Lbl", go.transform);
        lGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        lGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
        lGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
        TextMeshProUGUI lTxt = lGo.AddComponent<TextMeshProUGUI>();
        lTxt.text      = label;
        lTxt.fontSize  = 20;
        lTxt.color     = Color.white;
        lTxt.alignment = TextAlignmentOptions.Center;
        lTxt.fontStyle = FontStyles.Bold;

        EventTrigger trig = go.AddComponent<EventTrigger>();
        AddEvt(trig, EventTriggerType.PointerDown, _ => { img.color = pressed; onTap(); });
        AddEvt(trig, EventTriggerType.PointerUp,   _ => img.color = normal);
        AddEvt(trig, EventTriggerType.PointerExit, _ => img.color = normal);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void AddEvt(EventTrigger trig, EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> cb)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(cb);
        trig.triggers.Add(e);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
}
