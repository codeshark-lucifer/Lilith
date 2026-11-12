using UnityEngine;

/// <summary>
/// Legacy day/night implementation. Prefer
/// <see cref="Assets.Scripts.Runtime.DayNightCycle.LightingManager"/> with a DayCycleProfile.
/// This component disables itself when the newer manager is present to prevent both systems
/// from writing to the same RenderSettings values.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("References")]
    public Light sun;

    [Header("Time")]
    [Tooltip("Length of a full day in real seconds")]
    public float dayLengthSeconds = 120f;
    [Range(0f, 1f)] public float timeOfDay = 0.25f; // 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset

    [Header("Lighting")]
    public Gradient sunColor;
    public AnimationCurve sunIntensity = AnimationCurve.Linear(0, 0, 1, 1);
    public Gradient ambientColor;

    private void Awake()
    {
        if (Assets.Scripts.Runtime.DayNightCycle.LightingManager.Instance != null)
            enabled = false;
    }

    void Update()
    {
        if (Assets.Scripts.Runtime.DayNightCycle.LightingManager.Instance != null)
        {
            enabled = false;
            return;
        }

        if (sun == null || dayLengthSeconds <= 0f) return;

        // Advance time and wrap around at 1
        timeOfDay += Time.deltaTime / dayLengthSeconds;
        timeOfDay %= 1f;

        UpdateLighting();
    }

    void UpdateLighting()
    {
        // Rotate the sun: 0 -> -90 degrees (below horizon), 0.5 -> 90 degrees (overhead)
        float sunAngle = timeOfDay * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

        // Color and intensity from gradients/curves
        sun.color = sunColor.Evaluate(timeOfDay);
        sun.intensity = sunIntensity.Evaluate(timeOfDay);

        // Ambient light
        RenderSettings.ambientLight = ambientColor.Evaluate(timeOfDay);
    }

    void OnValidate()
    {
        // Lets you scrub timeOfDay in the editor and see the result
        if (sun != null) UpdateLighting();
    }
}