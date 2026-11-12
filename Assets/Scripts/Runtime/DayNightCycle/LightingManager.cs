using System;
using UnityEngine;
using UnityEngine.Rendering;
using global::DayNightCycleSystem;

namespace Assets.Scripts.Runtime.DayNightCycle
{
    /// <summary>
    /// Controls the in-game day/night cycle.
    /// The clock behaves like real life (00:00 - 23:59, AM/PM, day counter) but only runs in Play mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightingManager : MonoBehaviour
    {
        public static LightingManager Instance { get; private set; }

        /// <summary>Fired when the active phase changes (Morning -> Noon ...).</summary>
        public event Action<DayPhaseData> PhaseChanged;
        /// <summary>Fired when the phase's isNight flag flips. true = night just started.</summary>
        public event Action<bool> NightStateChanged;
        /// <summary>Fired every in-game hour with the new hour (0-23).</summary>
        public event Action<int> HourChanged;
        /// <summary>Fired at midnight with the new day number.</summary>
        public event Action<int> NewDay;

        [Header("Data")]
        [SerializeField] private DayCycleProfile profile;

        [Header("Scene references")]
        [SerializeField] private Light sunLight;
        [Tooltip("Optional. Gives soft blue light at night.")]
        [SerializeField] private Light moonLight;

        [Header("Clock (in-game time)")]
        [SerializeField, Range(0f, 24f)] private float currentHour = 9f;
        [SerializeField, Min(1)] private int dayCount = 1;
        [SerializeField] private bool clockRunning = true;
        [Tooltip("1 = normal. 2 = twice as fast. 0 = frozen.")]
        [SerializeField, Min(0f)] private float timeScale = 1f;
        [Tooltip("On Play, jump to the profile's start hour instead of using Current Hour above.")]
        [SerializeField] private bool useProfileStartHour = true;

        [Header("What the manager controls")]
        [SerializeField] private bool controlAmbient = true;
        [SerializeField] private bool controlSkybox = true;
        [SerializeField] private bool controlFog = false;

        [Header("Tweaks")]
        [Tooltip("Sun/moon fade in over this many degrees above the horizon.")]
        [SerializeField, Min(0.1f)] private float horizonFadeDegrees = 10f;
        [Tooltip("How often to refresh reflections/GI in Play mode. 0 = never. Expensive, keep >= 1.")]
        [SerializeField, Min(0f)] private float giRefreshSeconds = 1f;

        private DayPhaseData currentPhase;
        private bool isNight;
        private bool hasNightState;
        private Material skyboxInstance;
        private Material originalSkybox;
        private float giTimer;

        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint"); // Procedural skybox
        private static readonly int TintId = Shader.PropertyToID("_Tint");       // 6 Sided / Cubemap skybox
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        // ---------------------------------------------------------------- Time data

        /// <summary>Hour on a 24h clock as a float (21.5 = 09:30 PM).</summary>
        public float Hour24 => currentHour;
        public int Hour => Mathf.FloorToInt(currentHour) % 24;
        public int Minute => Mathf.Min(59, Mathf.FloorToInt((currentHour - Mathf.Floor(currentHour)) * 60f));
        public int DayCount => dayCount;
        /// <summary>0 = midnight, 0.5 = noon.</summary>
        public float Normalized => currentHour / 24f;
        public DayPhaseData CurrentPhase => currentPhase;
        public bool IsNight => isNight;
        public bool ClockRunning => clockRunning;

        /// <summary>"09:30 PM"</summary>
        public string TimeString12
        {
            get
            {
                int h12 = Hour % 12;
                if (h12 == 0) h12 = 12;
                return $"{h12:00}:{Minute:00} {(Hour >= 12 ? "PM" : "AM")}";
            }
        }

        /// <summary>"21:30"</summary>
        public string TimeString24 => $"{Hour:00}:{Minute:00}";

        /// <summary>True if the clock is inside [fromHour, toHour). Works across midnight (e.g. 22 to 5).</summary>
        public bool IsBetween(float fromHour, float toHour)
        {
            return fromHour <= toHour
                ? currentHour >= fromHour && currentHour < toHour
                : currentHour >= fromHour || currentHour < toHour;
        }

        // ---------------------------------------------------------------- Controls

        public void SetTime(float hour24)
        {
            currentHour = Mathf.Repeat(hour24, 24f);
            ApplyLighting();
        }

        public void SetTime(int hour, int minute) => SetTime(hour + minute / 60f);

        /// <summary>Jump forward to the next time the clock hits targetHour (e.g. sleeping in bed until 7).</summary>
        public void SkipToHour(float targetHour)
        {
            targetHour = Mathf.Repeat(targetHour, 24f);
            if (targetHour <= currentHour)
            {
                dayCount++;
                NewDay?.Invoke(dayCount);
            }
            SetTime(targetHour);
            HourChanged?.Invoke(Hour);
        }

        public void SetClockRunning(bool running) => clockRunning = running;
        public void SetTimeScale(float scale) => timeScale = Mathf.Max(0f, scale);

        // ---------------------------------------------------------------- Unity messages

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            currentPhase = null;
            hasNightState = false;
            dayCount = Mathf.Max(1, dayCount);
            currentHour = Mathf.Repeat(currentHour, 24f);

            if (profile != null && useProfileStartHour)
                currentHour = Mathf.Repeat(profile.startHour, 24f);

            // Edit a runtime copy so we never change the skybox material asset on disk.
            if (controlSkybox && RenderSettings.skybox != null)
            {
                originalSkybox = RenderSettings.skybox;
                skyboxInstance = new Material(RenderSettings.skybox);
                RenderSettings.skybox = skyboxInstance;
            }
        }

        private void Start()
        {
            ApplyLighting();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (RenderSettings.skybox == skyboxInstance)
                RenderSettings.skybox = originalSkybox;

            if (skyboxInstance != null) Destroy(skyboxInstance);
        }

        private void Update()
        {
            if (profile == null) return;

            if (clockRunning) AdvanceClock(Time.deltaTime);
            ApplyLighting();

            if (giRefreshSeconds > 0f)
            {
                giTimer += Time.deltaTime;
                if (giTimer >= giRefreshSeconds)
                {
                    giTimer = 0f;
                    DynamicGI.UpdateEnvironment();
                }
            }
        }

        // Lets you scrub "Current Hour" in the Inspector and preview the lighting in Edit mode.
        private void OnValidate()
        {
            if (profile == null) return;
            if (sunLight == null && RenderSettings.sun != null) sunLight = RenderSettings.sun;
            ApplyLighting();
        }

        // ---------------------------------------------------------------- Internals

        private void AdvanceClock(float deltaTime)
        {
            if (profile == null || profile.dayLengthSeconds <= 0f || deltaTime <= 0f) return;

            int previousHour = Hour;
            currentHour += deltaTime * (24f / profile.dayLengthSeconds) * timeScale;

            while (currentHour >= 24f)
            {
                currentHour -= 24f;
                dayCount++;
                NewDay?.Invoke(dayCount);
            }

            currentHour = Mathf.Repeat(currentHour, 24f);

            if (Hour != previousHour) HourChanged?.Invoke(Hour);
        }

        private void ApplyLighting()
        {
            if (profile == null || !profile.Evaluate(currentHour, out DayPhaseData phase, out LightingSnapshot s))
                return;

            if (phase != currentPhase)
            {
                currentPhase = phase;
                PhaseChanged?.Invoke(phase);
            }

            if (!hasNightState || phase.isNight != isNight)
            {
                hasNightState = true;
                isNight = phase.isNight;
                NightStateChanged?.Invoke(isNight);
            }

            // Sun and moon travel on opposite sides of the sky.
            float angle = profile.SunAngle(currentHour);
            float elevation = Mathf.Asin(Mathf.Sin(angle * Mathf.Deg2Rad)) * Mathf.Rad2Deg; // + above horizon, - below
            float sunFade = Mathf.Clamp01(elevation / horizonFadeDegrees);   // sun never lights from below the ground
            float moonFade = Mathf.Clamp01(-elevation / horizonFadeDegrees); // moon only when the sun is down

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(angle, profile.sunYaw, 0f);
                sunLight.color = s.sunColor;
                sunLight.intensity = s.sunIntensity * sunFade;
                RenderSettings.sun = sunLight;
            }

            if (moonLight != null)
            {
                moonLight.transform.rotation = Quaternion.Euler(angle + 180f, profile.sunYaw, 0f);
                moonLight.color = s.moonColor;
                moonLight.intensity = s.moonIntensity * moonFade;
            }

            if (controlAmbient)
            {
                // Flat ambient: the skybox no longer lights the scene, so objects really do get dark.
                // Intensity is multiplied into the color because the ambient intensity multiplier only applies to Skybox mode.
                Color ambient = s.ambientColor * s.ambientIntensity;
                ambient.a = 1f;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
                RenderSettings.reflectionIntensity = s.reflectionIntensity;
            }

            if (controlFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = s.fogColor;
                RenderSettings.fogDensity = s.fogDensity;
            }

            if (skyboxInstance != null)
            {
                if (skyboxInstance.HasProperty(SkyTintId)) skyboxInstance.SetColor(SkyTintId, s.skyTint);
                else if (skyboxInstance.HasProperty(TintId)) skyboxInstance.SetColor(TintId, s.skyTint);

                if (skyboxInstance.HasProperty(ExposureId)) skyboxInstance.SetFloat(ExposureId, s.skyExposure);
            }
        }
    }
}