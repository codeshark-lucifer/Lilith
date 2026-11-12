using Assets.Scripts.Runtime.DayNightCycle;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using global::DayNightCycleSystem;

namespace DayNightCycleSystem.EditorTools
{
    /// <summary>Menu bar > DayNightCycle. Must live in a folder named "Editor".</summary>
    public static class DayNightCycleMenu
    {
        private const string RootFolder = "Assets/DayNightCycle";
        private const string DemoFolder = RootFolder + "/Demo";
        private const string ProfilePath = DemoFolder + "/DayCycleProfile_Demo.asset";
        private const string LegacyProfilePath = "Assets/NightDayCycle/Demo/DayCycleProfile_Demo.asset";

        private static readonly Color MoonColor = new Color(0.66f, 0.73f, 1f);

        // ------------------------------------------------------------------ Menu items

        [MenuItem("DayNightCycle/Generate Demo Data")]
        public static void GenerateDemoData()
        {
            if (AssetDatabase.IsValidFolder(DemoFolder) &&
                !EditorUtility.DisplayDialog("DayNightCycle",
                    "Demo data already exists in " + DemoFolder + ".\nOverwrite its values? (Assets keep their references.)",
                    "Overwrite", "Cancel"))
                return;

            EnsureFolder(RootFolder);
            EnsureFolder(DemoFolder);

            // Phase(order, name, startHour, blendHours, isNight,
            //       sunColor, sunIntensity, moonIntensity,
            //       ambientColor, ambientIntensity, reflectionIntensity,
            //       fogColor, fogDensity, skyTint, skyExposure)
            var phases = new List<DayPhaseData>
            {
                Phase(0, "Midnight", 0f, 1f, true,
                    G("#FFFFFF", "#FFFFFF"), K(0f), K(0.3f),
                    G("#0A0F1F", "#0C1430"), K(0.7f), K(0.15f),
                    G("#070B18", "#0A1226"), K(0.012f),
                    G("#1B2440", "#26335A"), K(0.2f, 0.3f)),

                Phase(1, "Dawn", 5f, 1f, false,
                    G("#FF8A4A", "#FFD9A8"), K(0.2f, 0.9f), K(0.3f, 0f),
                    G("#26335A", "#D9A58C"), K(0.6f, 0.9f), K(0.15f, 0.6f),
                    G("#2A3556", "#E0A98A"), K(0.02f, 0.012f),
                    G("#3A4A7A", "#F0A878"), K(0.3f, 1f)),

                Phase(2, "Morning", 7f, 1.5f, false,
                    G("#FFD9A8", "#FFF1D6"), K(0.9f, 1.1f), K(0f),
                    G("#D9B79F", "#A9C4E8"), K(0.95f, 1f), K(0.6f, 1f),
                    G("#C8D3E0", "#B8D0EA"), K(0.008f),
                    G("#9FB8D8", "#8FB4E0"), K(1f, 1.3f)),

                Phase(3, "Noon", 11f, 1f, false,
                    G("#FFF8EE", "#FFFFFF"), K(1.25f), K(0f),
                    G("#A9C4E8", "#B9D6F2"), K(1.1f), K(1f),
                    G("#B9D6F2", "#B9D6F2"), K(0.005f),
                    G("#8FB4E0", "#7FAEE8"), K(1.35f)),

                Phase(4, "Afternoon", 15f, 1.5f, false,
                    G("#FFF1D6", "#FFD08A"), K(1.2f, 0.9f), K(0f),
                    G("#B9D6F2", "#D8C1A0"), K(1f), K(1f, 0.9f),
                    G("#B9D6F2", "#E0C8A8"), K(0.006f),
                    G("#7FAEE8", "#C8A888"), K(1.3f, 1.1f)),

                Phase(5, "Evening", 18f, 1f, false,
                    G("#FF9A4A", "#D6603A"), K(0.8f, 0.2f), K(0f, 0.2f),
                    G("#D8A070", "#5A4A6A"), K(0.9f, 0.7f), K(0.8f, 0.3f),
                    G("#E0A070", "#3A3050"), K(0.012f, 0.015f),
                    G("#E08A5A", "#3A3A6A"), K(1f, 0.4f)),

                Phase(6, "Night", 20f, 1f, true,
                    G("#FFFFFF", "#FFFFFF"), K(0f), K(0.25f, 0.3f),
                    G("#2A2F52", "#0F1530"), K(0.7f), K(0.3f, 0.15f),
                    G("#1A2040", "#0A1226"), K(0.012f),
                    G("#2A3560", "#1B2440"), K(0.4f, 0.2f)),
            };

            DayCycleProfile profile = LoadOrCreate<DayCycleProfile>(ProfilePath);
            profile.dayLengthSeconds = 240f;
            profile.startHour = 9f;
            profile.sunriseHour = 6f;
            profile.sunsetHour = 18f;
            profile.sunYaw = 170f;
            profile.phases = phases;
            EditorUtility.SetDirty(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log("[DayNightCycle] Demo data generated in " + DemoFolder +
                      ". Next: DayNightCycle > Add Lighting Manager To Scene.");
        }

        [MenuItem("DayNightCycle/Add Lighting Manager To Scene")]
        public static void AddManagerToScene()
        {
            LightingManager existing = FindFirst<LightingManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[DayNightCycle] A LightingManager already exists in this scene.");
                return;
            }

            DayCycleProfile profile = AssetDatabase.LoadAssetAtPath<DayCycleProfile>(ProfilePath);
            if (profile == null)
                profile = AssetDatabase.LoadAssetAtPath<DayCycleProfile>(LegacyProfilePath);
            if (profile == null)
                Debug.LogWarning("[DayNightCycle] No demo profile found. Run DayNightCycle > Generate Demo Data, then assign a profile.");

            Light sun = FindSun();
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(sun.gameObject, "Create Sun");
            }
            sun.lightmapBakeType = LightmapBakeType.Realtime; // baked lights can't change at runtime

            Light moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.intensity = 0f;
            moon.lightmapBakeType = LightmapBakeType.Realtime;
            Undo.RegisterCreatedObjectUndo(moon.gameObject, "Create Moon");

            GameObject go = new GameObject("LightingManager");
            Undo.RegisterCreatedObjectUndo(go, "Add Lighting Manager");
            LightingManager manager = go.AddComponent<LightingManager>();

            var so = new SerializedObject(manager);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("sunLight").objectReferenceValue = sun;
            so.FindProperty("moonLight").objectReferenceValue = moon;
            so.ApplyModifiedProperties();

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        // ------------------------------------------------------------------ Asset helpers

        private static DayPhaseData Phase(int order, string name, float startHour, float blendHours, bool isNight,
            Gradient sun, AnimationCurve sunIntensity, AnimationCurve moonIntensity,
            Gradient ambient, AnimationCurve ambientIntensity, AnimationCurve reflection,
            Gradient fog, AnimationCurve fogDensity,
            Gradient sky, AnimationCurve skyExposure)
        {
            DayPhaseData asset = LoadOrCreate<DayPhaseData>($"{DemoFolder}/{order:00}_{name}.asset");

            asset.phaseName = name;
            asset.startHour = startHour;
            asset.blendHours = blendHours;
            asset.blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            asset.isNight = isNight;

            asset.sunColor = sun;
            asset.sunIntensity = sunIntensity;
            asset.moonColor = MoonColor;
            asset.moonIntensity = moonIntensity;
            asset.ambientColor = ambient;
            asset.ambientIntensity = ambientIntensity;
            asset.reflectionIntensity = reflection;
            asset.fogColor = fog;
            asset.fogDensity = fogDensity;
            asset.skyTint = sky;
            asset.skyExposure = skyExposure;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // Gradient from 2+ hex colors, evenly spaced from 0 to 1.
        private static Gradient G(params string[] hex)
        {
            if (hex == null || hex.Length == 0)
                hex = new[] { "#FFFFFF" };

            var keys = new GradientColorKey[hex.Length];
            for (int i = 0; i < hex.Length; i++)
            {
                if (!ColorUtility.TryParseHtmlString(hex[i], out Color c))
                    c = Color.white;
                float time = hex.Length == 1 ? 0f : i / (float)(hex.Length - 1);
                keys[i] = new GradientColorKey(c, time);
            }

            var g = new Gradient();
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // Curve from 1+ values, evenly spaced from 0 to 1 with smooth tangents.
        private static AnimationCurve K(params float[] values)
        {
            if (values == null || values.Length == 0)
                return AnimationCurve.Constant(0f, 1f, 0f);

            if (values.Length == 1) return AnimationCurve.Constant(0f, 1f, values[0]);

            var curve = new AnimationCurve();
            for (int i = 0; i < values.Length; i++)
                curve.AddKey(i / (float)(values.Length - 1), values[i]);
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        // ------------------------------------------------------------------ Scene helpers

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;

#if UNITY_2022_2_OR_NEWER
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>();
#else
            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>();
#endif
            foreach (Light l in lights)
                if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}