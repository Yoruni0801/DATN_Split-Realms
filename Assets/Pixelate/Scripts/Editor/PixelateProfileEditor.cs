using UnityEditor;
using UnityEngine;

namespace Pixelate
{
    [CustomEditor(typeof(PixelateProfile))]
    public class PixelateProfileEditor : Editor
    {
        private enum ProfileSectionId
        {
            Unknown = 0,
            ColorPalette = 1,
            Lighting = 2,
        }

        [System.Serializable]
        private class NormalMapSectionSnapshot
        {
            public PixelateProfile.LightingSettings lighting = new PixelateProfile.LightingSettings();
        }

        private static ProfileSectionId copiedSectionId;
        private static string copiedSectionJson;
        private const string ProfileFoldoutStatePrefix = "Pixelate.Profile.Foldout.";
        private static readonly GUIContent AutoPaletteContent = new GUIContent("  Auto Generate Palette", "Creates a limited color palette from the capture.");
        private static readonly GUIContent CustomPaletteModeContent = new GUIContent("  Custom Palette", "Uses your palette texture to limit capture colors.");
        private static readonly GUIContent PaletteDetailContent = new GUIContent("Palette Detail", "Controls how many colors the auto palette keeps.");
        private static readonly GUIContent ColorCountContent = new GUIContent("Color Count", "The exact number of colors to keep in the palette.");
        private static readonly GUIContent CustomPaletteContent = new GUIContent("Custom Palette", "A 1-pixel-tall texture containing the colors to use.");
        private static readonly GUIContent PalettePreviewContent = new GUIContent("Palette Preview", "Shows the active palette colors.");
        private static readonly GUIContent LightingStepsContent = new GUIContent("Lighting Steps", "Controls how many stepped normal colors are exported.");
        private static readonly GUIContent MaterialPipelineContent = new GUIContent("Material Pipeline", "Choose the render pipeline your lit sprite material uses.");

        private bool colorExpanded = true;
        private bool lightingExpanded = true;

        private PixelateProfile Profile => (PixelateProfile)target;

        private void OnEnable()
        {
            colorExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(ProfileSectionId.ColorPalette), true);
            lightingExpanded = EditorPrefs.GetBool(GetFoldoutStateKey(ProfileSectionId.Lighting), true);
        }

        public override void OnInspectorGUI()
        {
            if (Profile.Sanitize())
            {
                EditorUtility.SetDirty(Profile);
            }
            serializedObject.Update();
            string paletteFingerprintBefore = GetPaletteSettingsFingerprint(Profile);

            bool sectionChanged = DrawColorPaletteSection();
            sectionChanged |= DrawLightingSection();

            bool changed = serializedObject.ApplyModifiedProperties();
            changed |= Profile.Sanitize();
            bool paletteChanged = paletteFingerprintBefore != GetPaletteSettingsFingerprint(Profile);
            if (sectionChanged || changed || paletteChanged)
            {
                EditorUtility.SetDirty(Profile);
            }
            if (paletteChanged)
            {
                PixelateCaptureManager.ClearGeneratedPalettesForProfile(Profile);
            }
        }

        private static string GetPaletteSettingsFingerprint(PixelateProfile profile)
        {
            PixelateProfile.PaletteSettings palette = profile != null && profile.sections != null
                ? profile.sections.palette
                : null;
            if (palette == null)
            {
                return string.Empty;
            }

            string customPaletteId = palette.customPalette != null
                ? GlobalObjectId.GetGlobalObjectIdSlow(palette.customPalette).ToString()
                : string.Empty;
            return $"{palette.enabled}|{palette.style}|{palette.autoDetail}|{customPaletteId}|{palette.colorCount}";
        }

        private bool DrawColorPaletteSection()
        {
            SerializedProperty paletteProp = serializedObject.FindProperty("sections").FindPropertyRelative("palette");
            SerializedProperty enabledProp = paletteProp.FindPropertyRelative("enabled");
            return DrawSection("Color Palette", ProfileSectionId.ColorPalette, () => colorExpanded, value => SetSectionExpanded(ProfileSectionId.ColorPalette, value), () =>
            {
                using (var changeScope = new EditorGUI.ChangeCheckScope())
                {
                    SerializedProperty styleProp = paletteProp.FindPropertyRelative("style");
                    GUIContent[] paletteText = { AutoPaletteContent, CustomPaletteModeContent };
                    int selectedStyle = styleProp.enumValueIndex;
                    if (selectedStyle != (int)PixelateCaptureManager.PaletteStyle.custom)
                    {
                        selectedStyle = (int)PixelateCaptureManager.PaletteStyle.auto;
                    }

                    selectedStyle = GUILayout.SelectionGrid(selectedStyle, paletteText, 1, EditorStyles.radioButton);
                    if (styleProp.enumValueIndex != selectedStyle)
                    {
                        Undo.RecordObject(Profile, "Change Pixelate Palette Mode");
                        styleProp.enumValueIndex = selectedStyle;
                    }

                    PixelateCaptureManager.PaletteStyle paletteStyle = enabledProp.boolValue
                        ? (PixelateCaptureManager.PaletteStyle)selectedStyle
                        : PixelateCaptureManager.PaletteStyle.none;

                    if (paletteStyle == PixelateCaptureManager.PaletteStyle.auto)
                    {
                        SerializedProperty autoPaletteDetailProp = paletteProp.FindPropertyRelative("autoDetail");
                        SerializedProperty colorCountProp = paletteProp.FindPropertyRelative("colorCount");
                        EditorGUILayout.PropertyField(autoPaletteDetailProp, PaletteDetailContent);
                        PixelateCaptureManager.AutoPaletteDetail autoPaletteDetail = (PixelateCaptureManager.AutoPaletteDetail)autoPaletteDetailProp.enumValueIndex;
                        if (autoPaletteDetail == PixelateCaptureManager.AutoPaletteDetail.custom)
                        {
                            EditorGUILayout.PropertyField(colorCountProp, ColorCountContent);
                        }
                        else
                        {
                            int displayedColorCount = PixelateCaptureManager.ResolveAutoPaletteDetailColorCount(
                                autoPaletteDetail,
                                colorCountProp.intValue);
                            using (new EditorGUI.DisabledScope(true))
                            {
                                EditorGUILayout.IntField(ColorCountContent, displayedColorCount);
                            }
                        }
                    }
                    else if (paletteStyle == PixelateCaptureManager.PaletteStyle.custom)
                    {
                        SerializedProperty customPaletteProp = paletteProp.FindPropertyRelative("customPalette");
                        EditorGUILayout.PropertyField(customPaletteProp, CustomPaletteContent);
                        Texture2D customPalette = customPaletteProp.objectReferenceValue as Texture2D;
                        DrawPalettePreview(customPalette);
                        DrawCustomPaletteImportSettingsWarning(customPalette);
                    }

                    return changeScope.changed;
                }
            }, true, () => enabledProp.boolValue, value =>
            {
                Undo.RecordObject(Profile, "Toggle Pixelate Palette");
                enabledProp.boolValue = value;
            });
        }

        private void DrawPalettePreview(Texture2D palette)
        {
            Rect controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            Rect previewRect = EditorGUI.PrefixLabel(controlRect, PalettePreviewContent);
            previewRect.y += 2f;
            previewRect.height -= 4f;

            GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(previewRect.x + 2f, previewRect.y + 2f, Mathf.Max(1f, previewRect.width - 4f), Mathf.Max(1f, previewRect.height - 4f));
            if (palette != null)
            {
                GUI.DrawTexture(innerRect, palette, ScaleMode.StretchToFill, false);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.LabelField(innerRect, "Assign a custom palette first", EditorStyles.miniLabel);
            }
        }

        private void DrawCustomPaletteImportSettingsWarning(Texture2D customPalette)
        {
            if (customPalette == null)
            {
                return;
            }

            var issues = PixelateUtilities.GetPaletteValidationIssues(customPalette);
            if (issues.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(PixelateUtilities.FormatPaletteValidationWarning(issues), MessageType.Warning);

            if (PixelateUtilities.HasFixablePaletteImportSettingIssues(customPalette) == false)
            {
                return;
            }

            if (GUILayout.Button("Fix import settings", GUILayout.Height(25)))
            {
                if (PixelateUtilities.FixPaletteImportSettings(customPalette))
                {
                    PixelateCaptureManager.ClearGeneratedPalettesForProfile(Profile);
                    Repaint();
                }

                GUIUtility.ExitGUI();
            }
        }

        private bool DrawLightingSection()
        {
            SerializedProperty lightingProp = serializedObject.FindProperty("sections").FindPropertyRelative("lighting");
            SerializedProperty enabledProp = lightingProp.FindPropertyRelative("enabled");
            return DrawSection("Normal Map", ProfileSectionId.Lighting, () => lightingExpanded, value => SetSectionExpanded(ProfileSectionId.Lighting, value), () =>
            {
                using (var changeScope = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.IntSlider(lightingProp.FindPropertyRelative("lightingSteps"), 1, 10, LightingStepsContent);
                    SerializedProperty materialPipelineProp = lightingProp.FindPropertyRelative("materialPipeline");
                    EditorGUILayout.PropertyField(materialPipelineProp, MaterialPipelineContent);
                    string pipelineMismatch = GetMaterialPipelineMismatchWarning((PixelateCaptureManager.MaterialPipeline)materialPipelineProp.enumValueIndex);
                    if (pipelineMismatch != null)
                    {
                        EditorGUILayout.HelpBox(pipelineMismatch, MessageType.Warning);
                    }

                    return changeScope.changed;
                }
            }, true, () => enabledProp.boolValue, value =>
            {
                Undo.RecordObject(Profile, "Toggle Pixelate Normal Map");
                enabledProp.boolValue = value;
            });
        }

        private bool DrawSection(
            string title,
            ProfileSectionId sectionId,
            System.Func<bool> getExpanded,
            System.Action<bool> setExpanded,
            System.Func<bool> drawContent,
            bool hasToggle = false,
            System.Func<bool> getActive = null,
            System.Action<bool> setActive = null)
        {
            var section = new DelegatePixelateInspectorSection(
                title,
                getExpanded,
                setExpanded,
                drawContent,
                CollapseAllSections,
                ExpandAllSections,
                () => hasToggle,
                () => getActive?.Invoke() ?? true,
                setActive,
                () => sectionId != ProfileSectionId.Unknown,
                () => copiedSectionId == sectionId && string.IsNullOrEmpty(copiedSectionJson) == false,
                () => CopySection(sectionId),
                () => PasteSection(sectionId),
                () => ResetSection(sectionId),
                () => GetSectionDocumentationUrl(sectionId));

            return section.Draw();
        }

        private static string GetSectionDocumentationUrl(ProfileSectionId sectionId)
        {
            switch (sectionId)
            {
                case ProfileSectionId.ColorPalette:
                    return PixelateDocumentationLinks.ColorPalette;
                case ProfileSectionId.Lighting:
                    return PixelateDocumentationLinks.NormalMap;
                default:
                    return PixelateDocumentationLinks.Profiles;
            }
        }

        private void CopySection(ProfileSectionId sectionId)
        {
            copiedSectionJson = GetSectionJson(sectionId);
            copiedSectionId = string.IsNullOrEmpty(copiedSectionJson) ? ProfileSectionId.Unknown : sectionId;
        }

        private void PasteSection(ProfileSectionId sectionId)
        {
            if (copiedSectionId != sectionId || string.IsNullOrEmpty(copiedSectionJson))
            {
                return;
            }

            ApplySectionJson(sectionId, copiedSectionJson);
        }

        private void ResetSection(ProfileSectionId sectionId)
        {
            switch (sectionId)
            {
                case ProfileSectionId.ColorPalette:
                    ApplySectionJson(sectionId, EditorJsonUtility.ToJson(new PixelateProfile.PaletteSettings()));
                    break;
                case ProfileSectionId.Lighting:
                    ApplySectionJson(sectionId, EditorJsonUtility.ToJson(new NormalMapSectionSnapshot()));
                    break;
            }
        }

        private string GetSectionJson(ProfileSectionId sectionId)
        {
            serializedObject.ApplyModifiedProperties();
            switch (sectionId)
            {
                case ProfileSectionId.ColorPalette:
                    return EditorJsonUtility.ToJson(Profile.sections.palette);
                case ProfileSectionId.Lighting:
                    return EditorJsonUtility.ToJson(new NormalMapSectionSnapshot
                    {
                        lighting = Profile.sections.lighting,
                    });
                default:
                    return null;
            }
        }

        private void ApplySectionJson(ProfileSectionId sectionId, string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            Undo.RecordObject(Profile, "Paste Pixelate Profile Settings");
            switch (sectionId)
            {
                case ProfileSectionId.ColorPalette:
                    EditorJsonUtility.FromJsonOverwrite(json, Profile.sections.palette);
                    Profile.sections.palette.Sanitize();
                    break;
                case ProfileSectionId.Lighting:
                    ApplyNormalMapSectionSnapshot(JsonUtility.FromJson<NormalMapSectionSnapshot>(json));
                    break;
            }

            EditorUtility.SetDirty(Profile);
            serializedObject.Update();
            Repaint();
        }

        private void ApplyNormalMapSectionSnapshot(NormalMapSectionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.lighting != null)
            {
                Profile.sections.lighting.enabled = snapshot.lighting.enabled;
                Profile.sections.lighting.lightingSteps = snapshot.lighting.lightingSteps;
                Profile.sections.lighting.materialPipeline = snapshot.lighting.materialPipeline;
                Profile.sections.lighting.Sanitize();
            }
        }

        private static string GetMaterialPipelineMismatchWarning(PixelateCaptureManager.MaterialPipeline selected)
        {
            bool projectUsesUrp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
                || UnityEngine.QualitySettings.renderPipeline != null;

            if (selected == PixelateCaptureManager.MaterialPipeline.urp && !projectUsesUrp)
            {
                return "Material Pipeline is set to URP, but the project appears to be using the Built-in render pipeline. Set it to Built-in, or assign a URP asset in Project Settings > Graphics and Quality.";
            }

            if (selected == PixelateCaptureManager.MaterialPipeline.builtIn && projectUsesUrp)
            {
                return "Material Pipeline is set to Built-in, but the project appears to be using a Scriptable Render Pipeline. Set it to URP, or remove the render pipeline asset from Project Settings > Graphics and Quality.";
            }

            return null;
        }

        private void CollapseAllSections()
        {
            SetAllSectionsExpanded(false);
        }

        private void ExpandAllSections()
        {
            SetAllSectionsExpanded(true);
        }

        private void SetAllSectionsExpanded(bool expanded)
        {
            SetSectionExpanded(ProfileSectionId.ColorPalette, expanded);
            SetSectionExpanded(ProfileSectionId.Lighting, expanded);
            Repaint();
        }

        private void SetSectionExpanded(ProfileSectionId sectionId, bool expanded)
        {
            switch (sectionId)
            {
                case ProfileSectionId.ColorPalette:
                    colorExpanded = expanded;
                    break;
                case ProfileSectionId.Lighting:
                    lightingExpanded = expanded;
                    break;
                default:
                    return;
            }

            EditorPrefs.SetBool(GetFoldoutStateKey(sectionId), expanded);
        }

        private static string GetFoldoutStateKey(ProfileSectionId sectionId)
        {
            return ProfileFoldoutStatePrefix + sectionId;
        }
    }
}
