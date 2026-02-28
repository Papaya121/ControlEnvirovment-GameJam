using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class WeatherAbilityButton : MonoBehaviour
{
    [SerializeField] private WeatherElement element = WeatherElement.Rain;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Graphic[] graphics;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float unselectedAlpha = 0.45f;

    private WeatherCaster weatherCaster;

    public WeatherElement Element => element;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Bind(WeatherCaster caster)
    {
        weatherCaster = caster;
    }

    public void SetSelected(bool isSelected)
    {
        float alpha = isSelected ? selectedAlpha : unselectedAlpha;
        ApplyAlpha(alpha);
    }

    private void HandleClicked()
    {
        weatherCaster?.SelectElement(element);
    }

    private void ApplyAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            return;
        }

        if (graphics != null && graphics.Length > 0)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }

            return;
        }

        Graphic ownGraphic = GetComponent<Graphic>();
        if (ownGraphic != null)
        {
            Color color = ownGraphic.color;
            color.a = alpha;
            ownGraphic.color = color;
        }
    }

    private void Reset()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
    }
}
