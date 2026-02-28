using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WeatherCaster : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private WeatherElement selectedElement = WeatherElement.Rain;

    [Header("Raycast")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask mapLayerMask = ~0;
    [SerializeField, Min(5f)] private float maxRayDistance = 500f;
    [SerializeField] private bool useFallbackPlane = true;
    [SerializeField] private float fallbackPlaneY = 0f;
    [SerializeField] private bool blockInputWhenPointerOverUI = true;

    [Header("Rain")]
    [SerializeField] private GameObject rainPrefab;
    [SerializeField] private Vector3 rainSpawnOffset = Vector3.zero;
    [SerializeField] private Transform effectsParent;
    [SerializeField, Min(0f)] private float rainFollowSmoothing = 18f;
    [SerializeField, Min(0f)] private float waterMax = 100f;
    [SerializeField, Min(0f)] private float currentWater = 100f;
    [SerializeField, Min(0f)] private float waterDrainPerSecond = 10f;
    [SerializeField, Min(0f)] private float waterRegenerationPerSecond = 0f;
    [SerializeField, Min(0f)] private float waterRegenDelayAfterRainStop = 2f;

    [Header("Snow")]
    [SerializeField] private GameObject snowPrefab;
    [SerializeField] private Vector3 snowSpawnOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float snowFollowSmoothing = 18f;
    [SerializeField, Min(0f)] private float snowWaterDrainPerSecond = 12f;

    [Header("Lightning")]
    [SerializeField] private GameObject lightningPrefab;
    [SerializeField] private Vector3 lightningSpawnOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float lightningCooldown = 2f;

    [Header("Bars")]
    [SerializeField] private Bar waterBar;
    [SerializeField] private Bar lightningCooldownBar;

    [Header("Buttons")]
    [SerializeField] private WeatherAbilityButton[] abilityButtons;

    private GameObject activeSustainedWeatherInstance;
    private bool isSustainedWeatherCasting;
    private WeatherElement activeSustainedWeatherElement;
    private Vector3 sustainedWeatherTargetPosition;
    private float nextLightningTime;
    private float nextWaterRegenTime;

    public WeatherElement SelectedElement => selectedElement;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        waterMax = Mathf.Max(0f, waterMax);
        currentWater = Mathf.Clamp(currentWater, 0f, waterMax);

        BindButtons();
        UpdateButtonStates();
        UpdateBars(true);
    }

    private void Update()
    {
        if (GameStateManager.IsGameplayInputBlocked)
        {
            if (isSustainedWeatherCasting)
            {
                StopSustainedWeather();
            }

            UpdateBars(false);
            return;
        }

        HandleElementHotkeys();
        HandleWeatherInput();

        if (isSustainedWeatherCasting)
        {
            UpdateSustainedWeather();
        }
        else
        {
            RegenerateWater();
        }

        UpdateBars(false);
    }

    private void HandleElementHotkeys()
    {
        if (IsRainHotkeyPressed())
        {
            SelectRain();
        }
        else if (IsLightningHotkeyPressed())
        {
            SelectLightning();
        }
        else if (IsSnowHotkeyPressed())
        {
            SelectSnow();
        }
    }

    public void SelectElement(WeatherElement element)
    {
        if (selectedElement == element)
        {
            UpdateButtonStates();
            return;
        }

        selectedElement = element;
        if (selectedElement != WeatherElement.Rain && selectedElement != WeatherElement.Snow)
        {
            StopSustainedWeather();
        }

        UpdateButtonStates();
    }

    public void SelectRain()
    {
        SelectElement(WeatherElement.Rain);
    }

    public void SelectLightning()
    {
        SelectElement(WeatherElement.Lightning);
    }

    public void SelectSnow()
    {
        SelectElement(WeatherElement.Snow);
    }

    private void HandleWeatherInput()
    {
        bool pressed = IsLeftMousePressedThisFrame();
        bool held = IsLeftMouseHeld();
        bool released = IsLeftMouseReleasedThisFrame();

        if (released)
        {
            StopSustainedWeather();
        }

        if (selectedElement == WeatherElement.Rain || selectedElement == WeatherElement.Snow)
        {
            if (pressed && !ShouldBlockPointerInput())
            {
                TryStartSustainedWeather();
            }

            if (held && isSustainedWeatherCasting && ShouldBlockPointerInput())
            {
                StopSustainedWeather();
            }

            if (!held && isSustainedWeatherCasting)
            {
                StopSustainedWeather();
            }

            return;
        }

        if (pressed && !ShouldBlockPointerInput())
        {
            TryCastLightning();
        }
    }

    private void TryStartSustainedWeather()
    {
        if (currentWater <= 0f)
        {
            return;
        }

        if (!TryResolveSustainedWeatherPrefab(out GameObject prefab))
        {
            return;
        }

        if (!TryGetWorldPoint(out Vector3 point))
        {
            return;
        }

        sustainedWeatherTargetPosition = point + GetSustainedWeatherSpawnOffset();
        isSustainedWeatherCasting = true;

        if (activeSustainedWeatherInstance == null)
        {
            activeSustainedWeatherInstance = Instantiate(prefab, sustainedWeatherTargetPosition, Quaternion.identity, effectsParent);
            activeSustainedWeatherElement = selectedElement;
        }
        else
        {
            if (activeSustainedWeatherElement != selectedElement)
            {
                Destroy(activeSustainedWeatherInstance);
                activeSustainedWeatherInstance = Instantiate(prefab, sustainedWeatherTargetPosition, Quaternion.identity, effectsParent);
                activeSustainedWeatherElement = selectedElement;
                return;
            }

            activeSustainedWeatherInstance.SetActive(true);
            activeSustainedWeatherInstance.transform.position = sustainedWeatherTargetPosition;
        }
    }

    private void UpdateSustainedWeather()
    {
        if (currentWater <= 0f)
        {
            StopSustainedWeather();
            return;
        }

        if (!TryResolveSustainedWeatherPrefab(out GameObject prefab))
        {
            StopSustainedWeather();
            return;
        }

        if (TryGetWorldPoint(out Vector3 point))
        {
            sustainedWeatherTargetPosition = point + GetSustainedWeatherSpawnOffset();
        }

        if (activeSustainedWeatherInstance == null)
        {
            activeSustainedWeatherInstance = Instantiate(prefab, sustainedWeatherTargetPosition, Quaternion.identity, effectsParent);
            activeSustainedWeatherElement = selectedElement;
        }
        else if (activeSustainedWeatherElement != selectedElement)
        {
            Destroy(activeSustainedWeatherInstance);
            activeSustainedWeatherInstance = Instantiate(prefab, sustainedWeatherTargetPosition, Quaternion.identity, effectsParent);
            activeSustainedWeatherElement = selectedElement;
        }

        float followSmoothing = GetSustainedWeatherFollowSmoothing();
        float lerpFactor = followSmoothing <= 0f
            ? 1f
            : 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
        activeSustainedWeatherInstance.transform.position = Vector3.Lerp(activeSustainedWeatherInstance.transform.position, sustainedWeatherTargetPosition, lerpFactor);

        currentWater = Mathf.Max(0f, currentWater - GetSustainedWeatherWaterDrainPerSecond() * Time.deltaTime);
        if (currentWater <= 0f)
        {
            StopSustainedWeather();
        }
    }

    private void StopSustainedWeather()
    {
        bool hadActiveWeather = isSustainedWeatherCasting || activeSustainedWeatherInstance != null;
        isSustainedWeatherCasting = false;

        if (activeSustainedWeatherInstance != null)
        {
            Destroy(activeSustainedWeatherInstance);
            activeSustainedWeatherInstance = null;
        }

        activeSustainedWeatherElement = selectedElement;

        if (hadActiveWeather)
        {
            nextWaterRegenTime = Time.time + waterRegenDelayAfterRainStop;
        }
    }

    private void RegenerateWater()
    {
        if (waterRegenerationPerSecond <= 0f || waterMax <= 0f || currentWater >= waterMax)
        {
            return;
        }

        if (Time.time < nextWaterRegenTime)
        {
            return;
        }

        currentWater = Mathf.Min(waterMax, currentWater + waterRegenerationPerSecond * Time.deltaTime);
    }

    private void TryCastLightning()
    {
        if (lightningPrefab == null || Time.time < nextLightningTime)
        {
            return;
        }

        if (!TryGetWorldPoint(out Vector3 point))
        {
            return;
        }

        Instantiate(lightningPrefab, point + lightningSpawnOffset, Quaternion.identity, effectsParent);
        nextLightningTime = Time.time + lightningCooldown;
    }

    private void UpdateBars(bool instant)
    {
        if (waterBar != null)
        {
            float waterNormalized = waterMax <= 0f ? 0f : currentWater / waterMax;
            waterBar.SetNormalized(waterNormalized, instant);
        }

        if (lightningCooldownBar != null)
        {
            float cooldownNormalized = GetLightningCooldownNormalized();
            lightningCooldownBar.SetNormalized(cooldownNormalized, instant);
        }
    }

    private float GetLightningCooldownNormalized()
    {
        if (lightningCooldown <= 0f)
        {
            return 1f;
        }

        if (Time.time >= nextLightningTime)
        {
            return 1f;
        }

        float remaining = nextLightningTime - Time.time;
        return Mathf.Clamp01(1f - remaining / lightningCooldown);
    }

    private bool TryGetWorldPoint(out Vector3 point)
    {
        point = Vector3.zero;

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            return false;
        }

        Vector2 screenPosition = GetPointerScreenPosition();
        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, mapLayerMask, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            return true;
        }

        if (!useFallbackPlane)
        {
            return false;
        }

        Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneY, 0f));
        if (!fallbackPlane.Raycast(ray, out float enterDistance))
        {
            return false;
        }

        point = ray.GetPoint(enterDistance);
        return true;
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        return Input.mousePosition;
    }

    private bool IsLeftMousePressedThisFrame()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }

        return Input.GetMouseButtonDown(0);
    }

    private bool IsLeftMouseHeld()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }

        return Input.GetMouseButton(0);
    }

    private bool IsLeftMouseReleasedThisFrame()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasReleasedThisFrame;
        }

        return Input.GetMouseButtonUp(0);
    }

    private bool IsRainHotkeyPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
    }

    private bool IsLightningHotkeyPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
    }

    private bool IsSnowHotkeyPressed()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                return true;
            }
        }

        return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
    }

    private bool TryResolveSustainedWeatherPrefab(out GameObject prefab)
    {
        prefab = selectedElement switch
        {
            WeatherElement.Rain => rainPrefab,
            WeatherElement.Snow => snowPrefab,
            _ => null
        };

        return prefab != null;
    }

    private Vector3 GetSustainedWeatherSpawnOffset()
    {
        return selectedElement switch
        {
            WeatherElement.Rain => rainSpawnOffset,
            WeatherElement.Snow => snowSpawnOffset,
            _ => Vector3.zero
        };
    }

    private float GetSustainedWeatherFollowSmoothing()
    {
        return selectedElement switch
        {
            WeatherElement.Rain => rainFollowSmoothing,
            WeatherElement.Snow => snowFollowSmoothing,
            _ => 0f
        };
    }

    private float GetSustainedWeatherWaterDrainPerSecond()
    {
        return selectedElement switch
        {
            WeatherElement.Rain => waterDrainPerSecond,
            WeatherElement.Snow => snowWaterDrainPerSecond,
            _ => 0f
        };
    }

    private bool ShouldBlockPointerInput()
    {
        return blockInputWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void BindButtons()
    {
        if (abilityButtons == null)
        {
            return;
        }

        for (int i = 0; i < abilityButtons.Length; i++)
        {
            WeatherAbilityButton button = abilityButtons[i];
            if (button == null)
            {
                continue;
            }

            button.Bind(this);
        }
    }

    private void UpdateButtonStates()
    {
        if (abilityButtons == null)
        {
            return;
        }

        for (int i = 0; i < abilityButtons.Length; i++)
        {
            WeatherAbilityButton button = abilityButtons[i];
            if (button == null)
            {
                continue;
            }

            button.SetSelected(button.Element == selectedElement);
        }
    }

    private void OnValidate()
    {
        waterMax = Mathf.Max(0f, waterMax);
        currentWater = Mathf.Clamp(currentWater, 0f, waterMax);
        lightningCooldown = Mathf.Max(0.01f, lightningCooldown);
        snowFollowSmoothing = Mathf.Max(0f, snowFollowSmoothing);
        snowWaterDrainPerSecond = Mathf.Max(0f, snowWaterDrainPerSecond);
    }
}
