using UnityEngine;

namespace DayNightCycleSystem
{
    /// <summary>Every value the lighting system drives, at one moment in time.</summary>
    public struct LightingSnapshot
    {
        public Color sunColor;
        public float sunIntensity;
        public Color moonColor;
        public float moonIntensity;
        public Color ambientColor;
        public float ambientIntensity;
        public float reflectionIntensity;
        public Color fogColor;
        public float fogDensity;
        public Color skyTint;
        public float skyExposure;

        public static LightingSnapshot Lerp(LightingSnapshot a, LightingSnapshot b, float t)
        {
            t = Mathf.Clamp01(t);
            return new LightingSnapshot
            {
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                moonColor = Color.Lerp(a.moonColor, b.moonColor, t),
                moonIntensity = Mathf.Lerp(a.moonIntensity, b.moonIntensity, t),
                ambientColor = Color.Lerp(a.ambientColor, b.ambientColor, t),
                ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
                reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
                skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t),
            };
        }
    }

    /// <summary>
    /// One session of the day (Morning, Noon, Evening, Night...).
    /// It starts at startHour and lasts until the next phase's startHour.
    /// Gradients and curves run from the phase start (0) to the phase end (1).
    /// </summary>
        [CreateAssetMenu(fileName = "New Day Phase", menuName = "DayNightCycle/Day Phase")]
    public class DayPhaseData : ScriptableObject
    {
        [Header("Timing")]
        public string phaseName = "New Phase";
        [Tooltip("In-game hour (24h clock) when this phase begins. 21 = 09:00 PM.")]
        [Range(0f, 24f)] public float startHour = 6f;
        [Tooltip("Hours spent blending from the previous phase into this one.")]
        [Min(0f)] public float blendHours = 1f;
        [Tooltip("Shape of the blend from the previous phase (0 = old look, 1 = this phase).")]
        public AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Tick for gameplay checks, e.g. turn street lights on.")]
        public bool isNight;

        [Header("Sun")]
        public Gradient sunColor = new Gradient();
        public AnimationCurve sunIntensity = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Moon")]
        public Color moonColor = new Color(0.66f, 0.73f, 1f);
        public AnimationCurve moonIntensity = AnimationCurve.Constant(0f, 1f, 0f);

        [Header("Ambient")]
        public Gradient ambientColor = new Gradient();
        public AnimationCurve ambientIntensity = AnimationCurve.Constant(0f, 1f, 1f);
        public AnimationCurve reflectionIntensity = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Fog")]
        public Gradient fogColor = new Gradient();
        public AnimationCurve fogDensity = AnimationCurve.Constant(0f, 1f, 0.01f);

        [Header("Skybox")]
        public Gradient skyTint = new Gradient();
        public AnimationCurve skyExposure = AnimationCurve.Constant(0f, 1f, 1.3f);

        private void OnValidate()
        {
            startHour = Mathf.Repeat(startHour, 24f);
            blendHours = Mathf.Max(0f, blendHours);

            if (blendCurve == null || blendCurve.length == 0)
                blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        /// <param name="progress">0 = start of this phase, 1 = end of this phase.</param>
        public LightingSnapshot Sample(float progress)
        {
            progress = Mathf.Clamp01(progress);
            return new LightingSnapshot
            {
                sunColor = Eval(sunColor, progress),
                sunIntensity = Eval(sunIntensity, progress, 1f),
                moonColor = moonColor,
                moonIntensity = Eval(moonIntensity, progress, 0f),
                ambientColor = Eval(ambientColor, progress),
                ambientIntensity = Eval(ambientIntensity, progress, 1f),
                reflectionIntensity = Eval(reflectionIntensity, progress, 1f),
                fogColor = Eval(fogColor, progress),
                fogDensity = Eval(fogDensity, progress, 0.01f),
                skyTint = Eval(skyTint, progress),
                skyExposure = Eval(skyExposure, progress, 1.3f),
            };
        }

        private static Color Eval(Gradient g, float t) => g != null ? g.Evaluate(t) : Color.white;

        private static float Eval(AnimationCurve c, float t, float fallback) =>
            c != null && c.length > 0 ? c.Evaluate(t) : fallback;
    }
}