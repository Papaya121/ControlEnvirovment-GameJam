using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Bar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image delayedFillImage;

    [Header("Fill Animation")]
    [SerializeField] private bool useSmoothTransitions = true;
    [SerializeField, Min(0.01f)] private float fillDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float delayedFillDuration = 0.45f;
    [SerializeField, Min(0f)] private float delayedFillDelay = 0.12f;
    [SerializeField, Min(0f)] private float retargetThreshold = 0.00001f;
    [SerializeField] private bool delayDelayedFillOnlyOnDamage = true;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private AnimationCurve fillCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 6f, 1.2f),
        new Keyframe(0.2f, 0.55f, 1f, 1f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve delayedFillCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.8f),
        new Keyframe(0.45f, 0.2f, 0.4f, 0.8f),
        new Keyframe(1f, 1f, 5f, 0f));

    [Header("Visibility")]
    [SerializeField] private bool fadeOutOnMinOrMaxValue = false;
    [SerializeField, Min(0f)] private float edgeThreshold = 0.0005f;
    [SerializeField, Min(0.01f)] private float visibilityFadeDuration = 0.15f;
    [SerializeField] private CanvasGroup visibilityCanvasGroup;

    private float requestedFill = 1f;
    private float currentFill = 1f;
    private float currentDelayedFill = 1f;
    private bool visualsInitialized;

    private bool fillTweenActive;
    private float fillTweenFrom;
    private float fillTweenTo;
    private float fillTweenStartTime;

    private bool delayedTweenActive;
    private float delayedTweenFrom;
    private float delayedTweenTo;
    private float delayedTweenStartTime;

    private float currentVisibilityAlpha = 1f;
    private float targetVisibilityAlpha = 1f;
    private bool visibilityInitialized;

    public float ValueNormalized => requestedFill;

    protected virtual void Awake()
    {
        if (fillImage == null)
        {
            fillImage = GetComponent<Image>();
        }

        if (delayedFillImage == fillImage)
        {
            delayedFillImage = null;
        }

        if (visibilityCanvasGroup == null)
        {
            visibilityCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (fadeOutOnMinOrMaxValue && visibilityCanvasGroup == null)
        {
            visibilityCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    protected virtual void OnEnable()
    {
        if (!visualsInitialized)
        {
            float initial = fillImage != null ? fillImage.fillAmount : 1f;
            SetNormalized(initial, true);
        }
    }

    protected virtual void OnDisable()
    {
    }

    protected virtual void Update()
    {
        if (!visualsInitialized)
        {
            return;
        }

        float now = GetTime();
        EvaluateFillTween(now);
        EvaluateDelayedTween(now);
        EvaluateVisibility();
        ApplyFillValues();
        ApplyVisibility();
    }

    public void SetValue(float currentValue, float maxValue, bool instant = false)
    {
        float normalized = maxValue <= 0f ? 0f : currentValue / maxValue;
        SetNormalized(normalized, instant);
    }

    public void SetNormalized(float normalizedValue)
    {
        SetNormalized(normalizedValue, false);
    }

    public void SetNormalized(float normalizedValue, bool instant)
    {
        float normalized = Mathf.Clamp01(normalizedValue);

        if (!visualsInitialized || instant || !useSmoothTransitions)
        {
            SetVisualsInstant(normalized);
            return;
        }

        requestedFill = normalized;

        if (Mathf.Abs(requestedFill - fillTweenTo) >= retargetThreshold || !fillTweenActive)
        {
            float now = GetTime();
            EvaluateFillTween(now);
            fillTweenFrom = currentFill;
            fillTweenTo = requestedFill;
            fillTweenStartTime = now;
            fillTweenActive = true;
        }

        if (delayedFillImage != null)
        {
            if (Mathf.Abs(requestedFill - delayedTweenTo) >= retargetThreshold || !delayedTweenActive)
            {
                float now = GetTime();
                EvaluateDelayedTween(now);

                float delay = delayedFillDelay;
                if (delayDelayedFillOnlyOnDamage && requestedFill > currentDelayedFill)
                {
                    delay = 0f;
                }

                // For frequent ticks we apply delay only once and keep moving.
                if (delayedTweenActive)
                {
                    delay = 0f;
                }

                delayedTweenFrom = currentDelayedFill;
                delayedTweenTo = requestedFill;
                delayedTweenStartTime = now + delay;
                delayedTweenActive = true;
            }
        }
        else
        {
            currentDelayedFill = requestedFill;
        }

        UpdateVisibilityTarget(requestedFill);
        ApplyFillValues();
        ApplyVisibility();
    }

    private float GetTime()
    {
        return useUnscaledTime ? Time.unscaledTime : Time.time;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void EvaluateFillTween(float now)
    {
        if (!fillTweenActive)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, fillDuration);
        float t = Mathf.Clamp01((now - fillTweenStartTime) / duration);
        float eased = fillCurve == null ? t : fillCurve.Evaluate(t);
        currentFill = Mathf.LerpUnclamped(fillTweenFrom, fillTweenTo, eased);
        currentFill = Mathf.Clamp01(currentFill);

        if (t >= 1f)
        {
            currentFill = fillTweenTo;
            fillTweenActive = false;
        }
    }

    private void EvaluateDelayedTween(float now)
    {
        if (delayedFillImage == null)
        {
            currentDelayedFill = currentFill;
            delayedTweenActive = false;
            return;
        }

        if (!delayedTweenActive)
        {
            return;
        }

        if (now < delayedTweenStartTime)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, delayedFillDuration);
        float t = Mathf.Clamp01((now - delayedTweenStartTime) / duration);
        float eased = delayedFillCurve == null ? t : delayedFillCurve.Evaluate(t);
        currentDelayedFill = Mathf.LerpUnclamped(delayedTweenFrom, delayedTweenTo, eased);
        currentDelayedFill = Mathf.Clamp01(currentDelayedFill);

        if (t >= 1f)
        {
            currentDelayedFill = delayedTweenTo;
            delayedTweenActive = false;
        }
    }

    private void EvaluateVisibility()
    {
        if (!fadeOutOnMinOrMaxValue || visibilityCanvasGroup == null)
        {
            return;
        }

        if (!visibilityInitialized)
        {
            currentVisibilityAlpha = targetVisibilityAlpha;
            visibilityInitialized = true;
            return;
        }

        float duration = Mathf.Max(0.01f, visibilityFadeDuration);
        float step = GetDeltaTime() / duration;
        currentVisibilityAlpha = Mathf.MoveTowards(currentVisibilityAlpha, targetVisibilityAlpha, step);
    }

    private void ApplyFillValues()
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = currentFill;
        }

        if (delayedFillImage != null)
        {
            delayedFillImage.fillAmount = currentDelayedFill;
        }
    }

    private void ApplyVisibility()
    {
        if (!fadeOutOnMinOrMaxValue || visibilityCanvasGroup == null)
        {
            return;
        }

        visibilityCanvasGroup.alpha = currentVisibilityAlpha;
    }

    private void UpdateVisibilityTarget(float normalizedValue)
    {
        if (!fadeOutOnMinOrMaxValue || visibilityCanvasGroup == null)
        {
            targetVisibilityAlpha = 1f;
            return;
        }

        bool atMin = normalizedValue <= edgeThreshold;
        bool atMax = normalizedValue >= 1f - edgeThreshold;
        targetVisibilityAlpha = atMin || atMax ? 0f : 1f;
    }

    private void SetVisualsInstant(float normalizedValue)
    {
        requestedFill = Mathf.Clamp01(normalizedValue);
        currentFill = requestedFill;
        currentDelayedFill = requestedFill;

        fillTweenFrom = requestedFill;
        fillTweenTo = requestedFill;
        fillTweenStartTime = GetTime();
        fillTweenActive = false;

        delayedTweenFrom = requestedFill;
        delayedTweenTo = requestedFill;
        delayedTweenStartTime = GetTime();
        delayedTweenActive = false;

        UpdateVisibilityTarget(requestedFill);
        currentVisibilityAlpha = targetVisibilityAlpha;
        visibilityInitialized = true;

        visualsInitialized = true;
        ApplyFillValues();
        ApplyVisibility();
    }

    protected virtual void Reset()
    {
        fillImage = GetComponent<Image>();
    }
}
