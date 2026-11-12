using UnityEngine;
using UnityEditor;

namespace RealStep
{
    [CustomEditor(typeof(FootIK))]
    public class FootIKEditor : Editor
    {
        static readonly Color Accent = new Color(0.25f, 0.85f, 0.7f);
        static readonly Color SectionBg = new Color(0.26f, 0.28f, 0.34f);
        static readonly Color SliderBg = new Color(0.14f, 0.15f, 0.2f);
        static readonly Color HeaderBg = new Color(0.2f, 0.22f, 0.28f);

        static bool showMain = true, showRaycast = true, showSpeed = true;
        static bool showWeights = true, showBody = true;
        static bool showEvents, showDebug = true;

        SerializedProperty weight;
        SerializedProperty maxStep, autoMaxStep, footRadius, ground, offset;
        SerializedProperty hipsSpeed, feetPosSpeed, feetRotSpeed;
        SerializedProperty hipsWeight, leftFootWeight, rightFootWeight;
        SerializedProperty enableBodyTilt, tiltAmount, tiltSpeed, invertTilt;
        SerializedProperty showDebugProp;
        SerializedProperty onLeftGrounded, onLeftLifted, onRightGrounded, onRightLifted;

        GUIContent helpIcon;
        GUIContent rateIcon;
        GUIContent mainIcon;
        GUIContent raycastIcon;
        GUIContent speedIcon;
        GUIContent weightsIcon;
        GUIContent bodyIcon;
        GUIContent eventsIcon;
        GUIContent debugIcon;

        void OnEnable()
        {
            weight = serializedObject.FindProperty("Weight");
            maxStep = serializedObject.FindProperty("MaxStep");
            autoMaxStep = serializedObject.FindProperty("AutoMaxStep");
            footRadius = serializedObject.FindProperty("FootRadius");
            ground = serializedObject.FindProperty("Ground");
            offset = serializedObject.FindProperty("Offset");
            hipsSpeed = serializedObject.FindProperty("HipsPositionSpeed");
            feetPosSpeed = serializedObject.FindProperty("FeetPositionSpeed");
            feetRotSpeed = serializedObject.FindProperty("FeetRotationSpeed");
            hipsWeight = serializedObject.FindProperty("HipsWeight");
            leftFootWeight = serializedObject.FindProperty("LeftFootWeight");
            rightFootWeight = serializedObject.FindProperty("RightFootWeight");
            enableBodyTilt = serializedObject.FindProperty("EnableBodyTilt");
            tiltAmount = serializedObject.FindProperty("TiltAmount");
            tiltSpeed = serializedObject.FindProperty("TiltSpeed");
            invertTilt = serializedObject.FindProperty("InvertTilt");
            showDebugProp = serializedObject.FindProperty("ShowDebug");
            onLeftGrounded = serializedObject.FindProperty("OnLeftFootGrounded");
            onLeftLifted = serializedObject.FindProperty("OnLeftFootLifted");
            onRightGrounded = serializedObject.FindProperty("OnRightFootGrounded");
            onRightLifted = serializedObject.FindProperty("OnRightFootLifted");

            helpIcon = EditorGUIUtility.IconContent("_Help");
            rateIcon = EditorGUIUtility.IconContent("Favorite Icon");
            mainIcon = EditorGUIUtility.IconContent("AnimatorController Icon");
            raycastIcon = EditorGUIUtility.IconContent("d_Preset.Context");
            speedIcon = EditorGUIUtility.IconContent("d_Profiler.CPU");
            weightsIcon = EditorGUIUtility.IconContent("d_ScaleTool");
            bodyIcon = EditorGUIUtility.IconContent("d_RotateTool");
            eventsIcon = EditorGUIUtility.IconContent("Animation.EventMarker");
            debugIcon = EditorGUIUtility.IconContent("d_console.infoicon.sml");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            GUILayout.Space(2);
            DrawMainSection();
            DrawRaycastSection();
            DrawSpeedSection();
            DrawWeightsSection();
            DrawBodySection();
            DrawEventsSection();
            DrawDebugSection();
            GUILayout.Space(2);

            serializedObject.ApplyModifiedProperties();
        }

        void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, HeaderBg);

            Rect accentLine = new Rect(rect.x, rect.yMax - 1, rect.width, 1);
            EditorGUI.DrawRect(accentLine, new Color(Accent.r, Accent.g, Accent.b, 0.4f));

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.95f, 0.97f, 1f) }
            };

            EditorGUI.LabelField(new Rect(rect.x + 10, rect.y, 200, rect.height), "RealStep", titleStyle);

            float btnSize = 24;
            float btnY = rect.y + (rect.height - btnSize) * 0.5f;

            Rect rateRect = new Rect(rect.xMax - btnSize - 6, btnY, btnSize, btnSize);
            Rect helpRect = new Rect(rateRect.x - btnSize - 4, btnY, btnSize, btnSize);

            if (GUI.Button(helpRect, helpIcon, EditorStyles.iconButton))
                OpenDocumentation();

            if (GUI.Button(rateRect, rateIcon, EditorStyles.iconButton))
                Application.OpenURL("https://assetstore.unity.com/packages/slug/322927");
        }

        void OpenDocumentation()
        {
            string[] guids = AssetDatabase.FindAssets("RealStep_Guide");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".html"))
                {
                    Application.OpenURL("file:///" + System.IO.Path.GetFullPath(path).Replace("\\", "/"));
                    return;
                }
            }
            Debug.LogWarning("[RealStep] Documentation file not found.");
        }

        void DrawMainSection()
        {
            showMain = DrawSectionHeader("Main", showMain, mainIcon);
            if (!showMain) return;
            BeginContent();
            DrawColorSlider(weight, "Weight", Accent);
            EndContent();
        }

        void DrawRaycastSection()
        {
            showRaycast = DrawSectionHeader("Raycast Settings", showRaycast, raycastIcon);
            if (!showRaycast) return;
            BeginContent();
            EditorGUILayout.PropertyField(autoMaxStep, new GUIContent("Auto Max Step"));
            EditorGUI.BeginDisabledGroup(autoMaxStep.boolValue);
            EditorGUILayout.PropertyField(maxStep, new GUIContent("Max Step"));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.PropertyField(footRadius, new GUIContent("Foot Radius"));
            EditorGUILayout.PropertyField(ground, new GUIContent("Ground Layer"));
            EditorGUILayout.PropertyField(offset, new GUIContent("Offset"));
            EndContent();
        }

        void DrawSpeedSection()
        {
            showSpeed = DrawSectionHeader("Speed", showSpeed, speedIcon);
            if (!showSpeed) return;
            BeginContent();
            EditorGUILayout.PropertyField(hipsSpeed, new GUIContent("Hips Position"));
            EditorGUILayout.PropertyField(feetPosSpeed, new GUIContent("Feet Position"));
            EditorGUILayout.PropertyField(feetRotSpeed, new GUIContent("Feet Rotation"));
            EndContent();
        }

        void DrawWeightsSection()
        {
            showWeights = DrawSectionHeader("Weights", showWeights, weightsIcon);
            if (!showWeights) return;
            BeginContent();
            DrawColorSlider(hipsWeight, "Hips", new Color(0.5f, 0.75f, 1f));
            DrawColorSlider(leftFootWeight, "Left Foot", new Color(1f, 0.65f, 0.35f));
            DrawColorSlider(rightFootWeight, "Right Foot", new Color(0.5f, 1f, 0.55f));
            EndContent();
        }

        void DrawBodySection()
        {
            showBody = DrawSectionHeader("Body Tilt", showBody, bodyIcon);
            if (!showBody) return;
            BeginContent();
            EditorGUILayout.PropertyField(enableBodyTilt, new GUIContent("Enable"));
            EditorGUI.BeginDisabledGroup(!enableBodyTilt.boolValue);
            DrawColorSlider(tiltAmount, "Tilt Amount", new Color(0.85f, 0.55f, 0.95f));
            EditorGUILayout.PropertyField(tiltSpeed, new GUIContent("Tilt Speed"));
            EditorGUILayout.PropertyField(invertTilt, new GUIContent("Invert Direction"));
            EditorGUI.EndDisabledGroup();
            EndContent();
        }

        void DrawEventsSection()
        {
            showEvents = DrawSectionHeader("Events", showEvents, eventsIcon);
            if (!showEvents) return;
            BeginContent();
            EditorGUILayout.PropertyField(onLeftGrounded);
            EditorGUILayout.PropertyField(onLeftLifted);
            EditorGUILayout.PropertyField(onRightGrounded);
            EditorGUILayout.PropertyField(onRightLifted);
            EndContent();
        }

        void DrawDebugSection()
        {
            showDebug = DrawSectionHeader("Debug", showDebug, debugIcon);
            if (!showDebug) return;
            BeginContent();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(showDebugProp, new GUIContent("Show Gizmos"));
            if (showDebugProp.boolValue)
            {
                GUIStyle s = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.3f, 1f, 0.55f) }, fontStyle = FontStyle.Bold };
                GUILayout.Label("ACTIVE", s, GUILayout.Width(48));
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                GUILayout.Space(4);
                Rect statusRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(statusRect, SliderBg);

                FootIK ik = (FootIK)target;
                bool lG = GetField<bool>(ik, "leftGrounded");
                bool rG = GetField<bool>(ik, "rightGrounded");
                float fw = GetField<float>(ik, "falloffWeight");

                GUIStyle statusStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    normal = { textColor = new Color(0.85f, 0.88f, 0.95f) },
                    richText = true
                };

                string lCol = lG ? "<color=#4DF5A0>L</color>" : "<color=#555>L</color>";
                string rCol = rG ? "<color=#4DF5A0>R</color>" : "<color=#555>R</color>";

                EditorGUI.LabelField(statusRect, $"{lCol}  {rCol}  W:{fw:F2}", statusStyle);
                Repaint();
            }

            EndContent();
        }

        bool DrawSectionHeader(string title, bool expanded, GUIContent icon)
        {
            GUILayout.Space(2);
            Rect rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SectionBg);

            Rect leftBar = new Rect(rect.x, rect.y, 2, rect.height);
            if (expanded)
                EditorGUI.DrawRect(leftBar, Accent);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = expanded ? new Color(0.92f, 0.94f, 1f) : new Color(0.6f, 0.63f, 0.7f) }
            };

            float iconX = rect.x + 8;
            if (icon != null && icon.image != null)
            {
                Rect iconRect = new Rect(iconX, rect.y + (rect.height - 14) * 0.5f, 14, 14);
                GUI.DrawTexture(iconRect, icon.image, ScaleMode.ScaleToFit);
                iconX += 18;
            }

            EditorGUI.LabelField(new Rect(iconX, rect.y, rect.width - iconX - 20, rect.height), title, headerStyle);

            GUIStyle arrowStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.53f, 0.6f) }
            };
            EditorGUI.LabelField(new Rect(rect.xMax - 22, rect.y, 16, rect.height),
                expanded ? "\u25BE" : "\u25B8", arrowStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                expanded = !expanded;
                Event.current.Use();
            }

            return expanded;
        }

        void DrawColorSlider(SerializedProperty prop, string label, Color color)
        {
            Rect total = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight + 2);
            Rect labelRect = new Rect(total.x, total.y, EditorGUIUtility.labelWidth, total.height);
            Rect sliderRect = new Rect(labelRect.xMax + 4, total.y + 1, total.width - labelRect.width - 4, total.height - 2);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            { normal = { textColor = new Color(0.82f, 0.85f, 0.92f) } };
            EditorGUI.LabelField(labelRect, label, labelStyle);

            Rect bg = new Rect(sliderRect.x, sliderRect.y + 1, sliderRect.width, sliderRect.height - 2);
            EditorGUI.DrawRect(bg, SliderBg);

            float fillW = bg.width * Mathf.Clamp01(prop.floatValue);
            Rect fill = new Rect(bg.x, bg.y, fillW, bg.height);
            EditorGUI.DrawRect(fill, new Color(color.r, color.g, color.b, 0.5f));

            Rect fillHighlight = new Rect(bg.x, bg.y, fillW, 1);
            EditorGUI.DrawRect(fillHighlight, new Color(color.r, color.g, color.b, 0.8f));

            EditorGUI.BeginChangeCheck();
            float val = GUI.HorizontalSlider(sliderRect, prop.floatValue, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = val;

            GUIStyle valStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.94f, 1f) }
            };
            EditorGUI.LabelField(new Rect(sliderRect.xMax - 38, sliderRect.y, 34, sliderRect.height),
                prop.floatValue.ToString("F2"), valStyle);
        }

        void BeginContent()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(2);
            EditorGUI.indentLevel++;
        }

        void EndContent()
        {
            EditorGUI.indentLevel--;
            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        T GetField<T>(FootIK ik, string field)
        {
            var f = typeof(FootIK).GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null ? (T)f.GetValue(ik) : default;
        }
    }
}
