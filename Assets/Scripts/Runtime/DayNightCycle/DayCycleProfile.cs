using System;
using System.Collections.Generic;
using UnityEngine;

namespace DayNightCycleSystem
{
    /// <summary>The whole day: clock speed, sun path and the list of phases.</summary>
    [CreateAssetMenu(fileName = "New Day Cycle Profile", menuName = "DayNightCycle/Day Cycle Profile")]
    public class DayCycleProfile : ScriptableObject
    {
        [Header("Clock")]
        [Tooltip("Real-life seconds for one full in-game day (24h).")]
        [Min(1f)] public float dayLengthSeconds = 240f;
        [Tooltip("In-game hour the game starts at (9 = 09:00 AM, 21 = 09:00 PM).")]
        [Range(0f, 24f)] public float startHour = 9f;

        [Header("Sun path")]
        [Range(0f, 24f)] public float sunriseHour = 6f;
        [Range(0f, 24f)] public float sunsetHour = 18f;
        [Tooltip("Compass direction the sun travels along.")]
        [Range(0f, 360f)] public float sunYaw = 170f;

        [Header("Phases (list order doesn't matter, they are sorted by start hour)")]
        public List<DayPhaseData> phases = new List<DayPhaseData>();

        private readonly List<DayPhaseData> sorted = new List<DayPhaseData>();
        private static readonly Comparison<DayPhaseData> ByStartHour =
            (a, b) => a.startHour.CompareTo(b.startHour);

        private void OnValidate()
        {
            dayLengthSeconds = Mathf.Max(1f, dayLengthSeconds);
            startHour = Mathf.Repeat(startHour, 24f);
            sunriseHour = Mathf.Repeat(sunriseHour, 24f);
            sunsetHour = Mathf.Repeat(sunsetHour, 24f);
            sunYaw = Mathf.Repeat(sunYaw, 360f);
        }

        private void RebuildSorted()
        {
            sorted.Clear();
            if (phases == null) return;
            foreach (DayPhaseData p in phases)
                if (p != null) sorted.Add(p);
            sorted.Sort(ByStartHour);
        }

        /// <summary>
        /// Finds the active phase for an in-game hour and returns the final blended look.
        /// At the start of a phase, the look blends from the end of the previous phase.
        /// </summary>
        public bool Evaluate(float hour, out DayPhaseData phase, out LightingSnapshot snapshot)
        {
            RebuildSorted();
            if (sorted.Count == 0)
            {
                phase = null;
                snapshot = default;
                return false;
            }

            hour = Mathf.Repeat(hour, 24f);

            // Last phase whose start is <= hour. Before the first start hour we are still in the last phase (wraps past midnight).
            int index = sorted.Count - 1;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].startHour <= hour) index = i;
                else break;
            }

            DayPhaseData current = sorted[index];
            DayPhaseData previous = sorted[(index - 1 + sorted.Count) % sorted.Count];
            DayPhaseData next = sorted[(index + 1) % sorted.Count];

            float duration = Mathf.Repeat(next.startHour - current.startHour, 24f);
            if (duration < 0.0001f) duration = 24f; // only one phase

            float elapsed = Mathf.Repeat(hour - current.startHour, 24f);
            float progress = Mathf.Clamp01(elapsed / duration);

            LightingSnapshot result = current.Sample(progress);

            float blendLength = Mathf.Min(current.blendHours, duration);
            if (previous != current && blendLength > 0.0001f && elapsed < blendLength)
            {
                float t = current.blendCurve != null ? current.blendCurve.Evaluate(elapsed / blendLength) : elapsed / blendLength;
                result = LightingSnapshot.Lerp(previous.Sample(1f), result, t);
            }

            phase = current;
            snapshot = result;
            return true;
        }

        /// <summary>
        /// Sun pitch angle: 0 = sunrise, 90 = noon, 180 = sunset, 270 = midnight.
        /// Daytime and nighttime each get their own span, so sunrise/sunset hours can be anything.
        /// </summary>
        public float SunAngle(float hour)
        {
            float dayLength = Mathf.Max(Mathf.Repeat(sunsetHour - sunriseHour, 24f), 0.01f);
            float nightLength = Mathf.Max(24f - dayLength, 0.01f);
            float sinceSunrise = Mathf.Repeat(hour - sunriseHour, 24f);

            if (sinceSunrise <= dayLength)
                return sinceSunrise / dayLength * 180f;

            return 180f + (sinceSunrise - dayLength) / nightLength * 180f;
        }
    }
}