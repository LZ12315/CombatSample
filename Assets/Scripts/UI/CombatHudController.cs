using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Combat/Combat HUD Controller")]
public sealed class CombatHudController : MonoBehaviour
{
    [Header("Actors")]
    [SerializeField, Tooltip("Player health source. If empty, the component tries this GameObject, then the Player-tagged object.")]
    private ActorCombater playerCombater;

    [SerializeField, Tooltip("Fallback or fixed enemy health source.")]
    private ActorCombater enemyCombater;

    [SerializeField, Tooltip("When enabled, the top bar follows playerCombater.CombatTarget.")]
    private bool followPlayerCombatTarget = true;

    [SerializeField, Min(0.02f), Tooltip("How often the HUD checks for a changed combat target.")]
    private float targetRefreshInterval = 0.1f;

    [Header("Canvas")]
    [SerializeField, Tooltip("Optional existing canvas. Empty = create one under this object.")]
    private Canvas canvas;

    [SerializeField, Tooltip("Canvas sorting order when the controller creates a canvas.")]
    private int sortingOrder = 80;

    [Header("Style")]
    [SerializeField] private Color playerFillColor = new Color(0.45f, 1f, 0.78f, 1f);
    [SerializeField] private Color enemyFillColor = new Color(1f, 0.31f, 0.22f, 1f);
    [SerializeField] private Color delayedFillColor = new Color(0.55f, 0.08f, 0.08f, 0.9f);
    [SerializeField] private Color frameColor = new Color(0.92f, 0.96f, 1f, 0.5f);
    [SerializeField] private Color plateColor = new Color(0.015f, 0.018f, 0.024f, 0.78f);

    [Header("Layout")]
    [SerializeField] private Vector2 playerAnchoredPosition = new Vector2(52f, 54f);
    [SerializeField] private Vector2 playerBarSize = new Vector2(360f, 30f);
    [SerializeField] private Vector2 enemyAnchoredPosition = new Vector2(0f, -42f);
    [SerializeField] private Vector2 enemyBarSize = new Vector2(760f, 30f);

    private HealthBarView _playerView;
    private HealthBarView _enemyView;
    private ActorCombater _boundPlayer;
    private ActorCombater _boundEnemy;
    private float _targetRefreshTimer;
    private bool _initialized;

    private void Reset()
    {
        playerCombater = GetComponentInParent<ActorCombater>();
    }

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!_initialized)
            return;

        BindPlayer(ResolvePlayerCombater());
        RefreshEnemyBinding(true);
    }

    private void OnDisable()
    {
        HideViewsInstant();
        UnbindPlayer();
        UnbindEnemy();
    }

    private void OnDestroy()
    {
        HideViewsInstant();
        UnbindPlayer();
        UnbindEnemy();
    }

    private void Update()
    {
        if (!_initialized)
            Initialize();

        float dt = Time.unscaledDeltaTime;
        _targetRefreshTimer -= dt;
        if (_targetRefreshTimer <= 0f)
        {
            _targetRefreshTimer = targetRefreshInterval;
            RefreshEnemyBinding(false);
        }

        SyncBoundHealth();
        _playerView?.Tick(dt);
        _enemyView?.Tick(dt);
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        playerCombater = ResolvePlayerCombater();
        canvas = ResolveCanvas();
        BuildHud(canvas.transform);
        _playerView.SetVisible(false, true);
        _enemyView.SetVisible(false, true);

        BindPlayer(playerCombater);
        RefreshEnemyBinding(true);
        _initialized = true;
    }

    private ActorCombater ResolvePlayerCombater()
    {
        if (playerCombater != null)
            return playerCombater;

        ActorCombater local = GetComponentInParent<ActorCombater>();
        if (local != null)
            return local;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            ActorCombater player = playerObject.GetComponentInParent<ActorCombater>();
            if (player != null)
                return player;

            player = playerObject.GetComponentInChildren<ActorCombater>();
            if (player != null)
                return player;
        }

        ActorCombater[] combaters = FindObjectsOfType<ActorCombater>();
        return combaters.Length == 1 ? combaters[0] : null;
    }

    private Canvas ResolveCanvas()
    {
        if (canvas != null)
            return canvas;

        Canvas existing = GetComponentInChildren<Canvas>();
        if (existing != null)
            return existing;

        GameObject canvasObject = new GameObject("Combat HUD Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas createdCanvas = canvasObject.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return createdCanvas;
    }

    private void BuildHud(Transform canvasRoot)
    {
        if (_playerView == null)
        {
            _playerView = HealthBarView.Create(
                canvasRoot,
                "Player Health",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                playerAnchoredPosition,
                playerBarSize,
                playerFillColor,
                delayedFillColor,
                plateColor,
                frameColor);
        }

        if (_enemyView == null)
        {
            _enemyView = HealthBarView.Create(
                canvasRoot,
                "Enemy Health",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                enemyAnchoredPosition,
                enemyBarSize,
                enemyFillColor,
                delayedFillColor,
                plateColor,
                frameColor);
        }
    }

    private void BindPlayer(ActorCombater combater)
    {
        if (_boundPlayer == combater)
            return;

        UnbindPlayer();
        _boundPlayer = combater;

        bool visible = _boundPlayer != null;
        _playerView.SetVisible(visible, true);
        if (!visible)
            return;

        _boundPlayer.HealthChanged += OnPlayerHealthChanged;
        _playerView.SetHealth(_boundPlayer.CurrentHealth, _boundPlayer.MaxHealth, true);
    }

    private void UnbindPlayer()
    {
        if (_boundPlayer != null)
            _boundPlayer.HealthChanged -= OnPlayerHealthChanged;
        _boundPlayer = null;
    }

    private void RefreshEnemyBinding(bool instant)
    {
        ActorCombater nextEnemy = ResolveEnemyCombater();
        if (nextEnemy == _boundEnemy)
        {
            _enemyView.SetVisible(nextEnemy != null && nextEnemy.gameObject.activeInHierarchy, instant);
            return;
        }

        BindEnemy(nextEnemy, instant);
    }

    private ActorCombater ResolveEnemyCombater()
    {
        if (followPlayerCombatTarget && _boundPlayer != null)
        {
            GameObject target = _boundPlayer.CombatTarget;
            if (target != null && target.activeInHierarchy)
            {
                ActorCombater targetCombater = target.GetComponentInParent<ActorCombater>();
                if (targetCombater != null && targetCombater != _boundPlayer)
                    return targetCombater;
            }
        }

        if (enemyCombater != null && enemyCombater.gameObject.activeInHierarchy)
            return enemyCombater;

        return null;
    }

    private void BindEnemy(ActorCombater combater, bool instant)
    {
        UnbindEnemy();
        _boundEnemy = combater;

        bool visible = _boundEnemy != null;
        _enemyView.SetVisible(visible, instant);
        if (!visible)
            return;

        _boundEnemy.HealthChanged += OnEnemyHealthChanged;
        _enemyView.SetHealth(_boundEnemy.CurrentHealth, _boundEnemy.MaxHealth, true);
    }

    private void UnbindEnemy()
    {
        if (_boundEnemy != null)
            _boundEnemy.HealthChanged -= OnEnemyHealthChanged;
        _boundEnemy = null;
    }

    private void HideViewsInstant()
    {
        _playerView?.SetVisible(false, true);
        _enemyView?.SetVisible(false, true);
    }

    private void SyncBoundHealth()
    {
        if (_boundPlayer != null)
            _playerView.SetHealth(_boundPlayer.CurrentHealth, _boundPlayer.MaxHealth, false);

        if (_boundEnemy != null)
            _enemyView.SetHealth(_boundEnemy.CurrentHealth, _boundEnemy.MaxHealth, false);
    }

    private void OnPlayerHealthChanged(ActorCombater combater, float current, float max)
    {
        _playerView.SetHealth(current, max, false);
    }

    private void OnEnemyHealthChanged(ActorCombater combater, float current, float max)
    {
        _enemyView.SetHealth(current, max, false);
        if (current <= 0f)
            _enemyView.SetVisible(false, false);
    }

    private sealed class HealthBarView
    {
        private const float ForegroundSpeed = 8f;
        private const float DelayedSpeed = 2.8f;
        private const float FadeSpeed = 8f;

        private readonly CanvasGroup _group;
        private readonly RectTransform _fill;
        private readonly RectTransform _delayedFill;
        private readonly RectTransform _accent;

        private float _targetValue = 1f;
        private float _displayValue = 1f;
        private float _delayedValue = 1f;
        private float _targetAlpha = 1f;

        public static HealthBarView Create(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color fillColor,
            Color delayedColor,
            Color plateColor,
            Color frameColor)
        {
            GameObject rootObject = CreateRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, size);
            CanvasGroup group = rootObject.AddComponent<CanvasGroup>();

            Image shadow = CreateImage("Shadow", rootObject.transform, plateColor.WithAlpha(0.35f));
            Stretch(shadow.rectTransform, new Vector2(-8f, -8f), new Vector2(8f, 8f));

            Image frame = CreateImage("Frame", rootObject.transform, frameColor);
            Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);

            Image plate = CreateImage("Plate", rootObject.transform, plateColor);
            Stretch(plate.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            Image delayed = CreateImage("Delayed Fill", rootObject.transform, delayedColor);
            Stretch(delayed.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            delayed.rectTransform.pivot = new Vector2(0f, 0.5f);

            Image fill = CreateImage("Fill", rootObject.transform, fillColor);
            Stretch(fill.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);

            Image accent = CreateImage("Accent", rootObject.transform, Color.white.WithAlpha(0.55f));
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0f, 1f);
            accentRect.anchoredPosition = new Vector2(0f, -3f);
            accentRect.sizeDelta = new Vector2(0f, 2f);

            Image leftCap = CreateImage("Left Cap", rootObject.transform, frameColor);
            RectTransform leftCapRect = leftCap.rectTransform;
            leftCapRect.anchorMin = new Vector2(0f, 0f);
            leftCapRect.anchorMax = new Vector2(0f, 1f);
            leftCapRect.pivot = new Vector2(0f, 0.5f);
            leftCapRect.anchoredPosition = new Vector2(-8f, 0f);
            leftCapRect.sizeDelta = new Vector2(4f, 10f);

            Image rightCap = CreateImage("Right Cap", rootObject.transform, frameColor);
            RectTransform rightCapRect = rightCap.rectTransform;
            rightCapRect.anchorMin = new Vector2(1f, 0f);
            rightCapRect.anchorMax = new Vector2(1f, 1f);
            rightCapRect.pivot = new Vector2(1f, 0.5f);
            rightCapRect.anchoredPosition = new Vector2(8f, 0f);
            rightCapRect.sizeDelta = new Vector2(4f, 10f);

            return new HealthBarView(group, fill.rectTransform, delayed.rectTransform, accent.rectTransform);
        }

        private HealthBarView(CanvasGroup group, RectTransform fill, RectTransform delayedFill, RectTransform accent)
        {
            _group = group;
            _fill = fill;
            _delayedFill = delayedFill;
            _accent = accent;
        }

        public void SetHealth(float current, float max, bool instant)
        {
            _targetValue = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (!instant)
                return;

            _displayValue = _targetValue;
            _delayedValue = _targetValue;
            ApplyFill();
        }

        public void SetVisible(bool visible, bool instant)
        {
            _targetAlpha = visible ? 1f : 0f;
            if (instant)
            {
                _group.alpha = _targetAlpha;
                _group.interactable = visible;
                _group.blocksRaycasts = false;
            }
        }

        public void Tick(float dt)
        {
            _displayValue = Mathf.MoveTowards(_displayValue, _targetValue, ForegroundSpeed * dt);
            if (_delayedValue > _displayValue)
                _delayedValue = Mathf.MoveTowards(_delayedValue, _displayValue, DelayedSpeed * dt);
            else
                _delayedValue = _displayValue;

            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, FadeSpeed * dt);
            _group.interactable = _group.alpha > 0.99f;
            _group.blocksRaycasts = false;

            ApplyFill();
        }

        private void ApplyFill()
        {
            SetHorizontalScale(_fill, _displayValue);
            SetHorizontalScale(_delayedFill, _delayedValue);
            SetHorizontalScale(_accent, _displayValue);
        }

        private static GameObject CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return obj;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetHorizontalScale(RectTransform rect, float value)
        {
            Vector3 scale = rect.localScale;
            scale.x = Mathf.Clamp01(value);
            rect.localScale = scale;
        }
    }
}

internal static class CombatHudColorExtensions
{
    public static Color WithAlpha(this Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
