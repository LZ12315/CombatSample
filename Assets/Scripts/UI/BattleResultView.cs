using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation only: BattleFlowController supplies all real-time animation values.
/// The progress line represents the result countdown, never scene-loading progress.
/// </summary>
internal sealed class BattleResultView
{
    private const string FontResourcePath = "UI/BattleDeathFont";
    private static readonly Color Accent = new Color(0.65f, 0.22f, 0.2f, 1f);
    private static readonly Color VictoryAccent = new Color(0.72f, 0.6f, 0.34f, 1f);
    private readonly Text _title;
    private readonly Text _subtitle;
    private readonly Image _accentLine;
    private readonly Image _progressImage;
    private readonly GameObject _root;
    private readonly Image _shade;
    private readonly CanvasGroup _deathGroup;
    private readonly RectTransform _deathRect;
    private readonly Text _seconds;
    private readonly RectTransform _progress;
    private readonly CanvasGroup _statusGroup;
    private readonly Text _status;
    private int _shownSeconds = -1;

    private BattleResultView(GameObject root, Image shade, CanvasGroup deathGroup,
        Text seconds, RectTransform progress, CanvasGroup statusGroup, Text status,
        Text title, Text subtitle, Image accentLine, Image progressImage)
    {
        _title = title;
        _subtitle = subtitle;
        _accentLine = accentLine;
        _progressImage = progressImage;
        _root = root;
        _shade = shade;
        _deathGroup = deathGroup;
        _deathRect = (RectTransform)deathGroup.transform;
        _seconds = seconds;
        _progress = progress;
        _statusGroup = statusGroup;
        _status = status;
    }

    public static BattleResultView Create(Transform owner)
    {
        // Bundle an OFL font subset, without relying on installed system fonts.
        Font font = Resources.Load<Font>(FontResourcePath);
        if (font == null)
        {
            Debug.LogError($"[Battle Flow] Missing UI font Resources/{FontResourcePath}. The battle will still restart.", owner);
            return null;
        }

        var root = new GameObject("Battle Result Overlay", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(Image));
        root.SetActive(false);
        root.transform.SetParent(owner, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        Image shade = root.GetComponent<Image>();
        shade.color = Color.clear;
        shade.raycastTarget = false;
        // No GraphicRaycaster or EventSystem: this overlay never handles input.

        CanvasGroup deathGroup = CreateGroup(root.transform, "Result Presentation");
        Transform deathRoot = deathGroup.transform;
        RectTransform accentLine = CreateLine(deathRoot, "Accent", new Vector2(48f, 2f), 148f, Accent);

        Text title = CreateText(deathRoot, "Result Title", font, 58, 74f);
        title.text = "玩家已死亡";
        title.color = new Color(0.94f, 0.93f, 0.91f, 1f);

        Text subtitle = CreateText(deathRoot, "Result Subtitle", font, 16, 5f);
        subtitle.text = "DEFEATED";
        subtitle.color = new Color(0.6f, 0.59f, 0.57f, 1f);

        Text label = CreateText(deathRoot, "Restart Label", font, 18, -88f);
        label.text = "重新开始";
        label.color = new Color(0.62f, 0.62f, 0.62f, 1f);

        Text seconds = CreateText(deathRoot, "Restart Seconds", font, 34, -133f);
        RectTransform track = CreateLine(deathRoot, "Countdown Track",
            new Vector2(240f, 2f), -185f, new Color(1f, 1f, 1f, 0.12f));
        var fill = new GameObject("Countdown Remaining", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track, false);
        RectTransform progress = (RectTransform)fill.transform;
        progress.anchorMin = Vector2.zero;
        progress.anchorMax = Vector2.one;
        progress.offsetMin = progress.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = Accent;
        fillImage.raycastTarget = false;

        CanvasGroup statusGroup = CreateGroup(root.transform, "Restart Transition");
        Text status = CreateText(statusGroup.transform, "Restart Status", font, 24, 0f);
        status.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        return new BattleResultView(root, shade, deathGroup, seconds, progress, statusGroup, status,
            title, subtitle, accentLine.GetComponent<Image>(), fillImage);
    }

    public void SetOutcome(BattleOutcome outcome)
    {
        bool victory = outcome == BattleOutcome.Victory;
        _title.text = victory ? "战斗胜利" : "玩家已死亡";
        _subtitle.text = victory ? "VICTORY" : "DEFEATED";
        _title.color = victory ? new Color(0.96f, 0.91f, 0.77f, 1f) : new Color(0.94f, 0.93f, 0.91f, 1f);
        Color accent = victory ? VictoryAccent : Accent;
        _accentLine.color = accent;
        _progressImage.color = accent;
        _shownSeconds = -1;
    }

    public void ShowCountdown(int seconds, float remainingFraction, float elapsedSeconds)
    {
        _root.SetActive(true);
        float enter = Ease(elapsedSeconds / 0.28f);
        _deathGroup.alpha = enter;
        _deathRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-10f, 0f, enter));
        _statusGroup.alpha = 0f;
        SetShade(0.58f * enter);
        _progress.anchorMax = new Vector2(Mathf.Clamp01(remainingFraction), 1f);

        if (_shownSeconds != seconds)
        {
            _shownSeconds = seconds;
            _seconds.text = seconds.ToString("00");
        }
    }

    public void ShowRestarting(float elapsedSeconds)
    {
        _root.SetActive(true);
        float exit = Ease(elapsedSeconds / 0.22f);
        // Never reveal a death group that was skipped by a zero-length countdown.
        _deathGroup.alpha = Mathf.Min(_deathGroup.alpha, 1f - exit);
        _deathRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 8f, exit));
        _progress.anchorMax = new Vector2(0f, 1f);
        _status.text = "正在重新开始…";
        _statusGroup.alpha = Ease(elapsedSeconds / 0.25f);
        SetShade(Mathf.Lerp(0.58f, 1f, Ease(elapsedSeconds / 0.45f)));
    }

    public void ShowRestartFailed()
    {
        _root.SetActive(true);
        _deathGroup.alpha = 0f;
        _statusGroup.alpha = 1f;
        _status.text = "无法重新开始，请检查场景配置";
        SetShade(1f);
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
        _shownSeconds = -1;
    }

    private void SetShade(float alpha)
    {
        _shade.color = new Color(0f, 0f, 0f, alpha);
    }

    private static float Ease(float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
    }

    private static CanvasGroup CreateGroup(Transform parent, string name)
    {
        var groupObject = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        groupObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)groupObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, 500f);
        CanvasGroup group = groupObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    private static RectTransform CreateLine(Transform parent, string name, Vector2 size, float y, Color color)
    {
        var line = new GameObject(name, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)line.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static Text CreateText(Transform parent, string name, Font font, int fontSize, float y)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1400f, fontSize + 32f);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }
}
