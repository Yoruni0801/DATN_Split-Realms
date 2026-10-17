using System;
using UnityEngine;

namespace Pixelate
{
    [CreateAssetMenu(fileName = "PixelateProfile", menuName = "Pixelate/Pixelate Profile", order = 1)]
    public class PixelateProfile : ScriptableObject
    {
        public PixelateProfileSections sections = new PixelateProfileSections();

        [SerializeField, HideInInspector] private PaletteSettings palette = new PaletteSettings();
        [SerializeField, HideInInspector] private RenderSettings render = new RenderSettings();
        [SerializeField, HideInInspector] private LightingSettings lighting = new LightingSettings();
        [SerializeField, HideInInspector] private bool migratedToSections;
        [SerializeField, HideInInspector] private bool migratedMaterialPipelineToLighting;

        public PaletteSettings Palette => sections.palette;
        public RenderSettings Render => sections.render;
        public LightingSettings Lighting => sections.lighting;

        private void OnEnable()
        {
            Sanitize();
        }

        private void OnValidate()
        {
            Sanitize();
        }

        public bool Sanitize()
        {
            bool changed = MigrateLegacyFieldsIfNeeded();
            changed |= MigrateMaterialPipelineToLightingIfNeeded();
            changed |= sections.Sanitize();
            return changed;
        }

        public void CopySettingsFrom(PixelateProfile source)
        {
            if (source == null)
            {
                return;
            }

            source.Sanitize();
            CopySettingsFrom(source.sections);
        }

        public void CopySettingsFrom(PixelateProfileSections source)
        {
            if (source == null)
            {
                return;
            }

            if (sections == null)
            {
                sections = new PixelateProfileSections();
            }
            source.Sanitize();
            sections.Sanitize();

            CopyPaletteSettings(source.palette, sections.palette);
            CopyLightingSettings(source.lighting, sections.lighting);
            CopyRenderSettings(source.render, sections.render);
            sections.Sanitize();
        }

        public void CopySettingsTo(PixelateProfileSections destination)
        {
            if (destination == null)
            {
                return;
            }

            Sanitize();
            destination.Sanitize();
            CopyPaletteSettings(sections.palette, destination.palette);
            CopyLightingSettings(sections.lighting, destination.lighting);
            CopyRenderSettings(sections.render, destination.render);
            destination.Sanitize();
        }

        private static void CopyPaletteSettings(PaletteSettings source, PaletteSettings destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.enabled = source.enabled;
            destination.style = source.style;
            destination.autoDetail = source.autoDetail;
            destination.customPalette = source.customPalette;
            destination.colorCount = source.colorCount;
            destination.Sanitize();
        }

        private static void CopyLightingSettings(LightingSettings source, LightingSettings destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.enabled = source.enabled;
            destination.lightingSteps = source.lightingSteps;
            destination.materialPipeline = source.materialPipeline;
            destination.Sanitize();
        }

        private static void CopyRenderSettings(RenderSettings source, RenderSettings destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.cellSize = source.cellSize;
            destination.linkCellSizeAxes = source.linkCellSizeAxes;
            destination.pivot = source.pivot;
            destination.pixelated = source.pixelated;
            destination.materialPipeline = source.materialPipeline;
            destination.Sanitize();
        }

        private bool MigrateLegacyFieldsIfNeeded()
        {
            if (sections == null)
            {
                sections = new PixelateProfileSections();
            }

            if (migratedToSections)
            {
                return false;
            }

            if (SectionsAreDefault() && HasLegacySettingsToMigrate())
            {
                sections.Sanitize();
                CopyPaletteSettings(palette ?? new PaletteSettings(), sections.palette);
                CopyRenderSettings(render ?? new RenderSettings(), sections.render);
                CopyLightingSettings(lighting ?? new LightingSettings(), sections.lighting);
            }
            migratedToSections = true;
            return true;
        }

        private bool MigrateMaterialPipelineToLightingIfNeeded()
        {
            if (migratedMaterialPipelineToLighting)
            {
                return false;
            }

            if (sections == null)
            {
                sections = new PixelateProfileSections();
            }
            if (sections.lighting == null)
            {
                sections.lighting = new LightingSettings();
            }

            if (HasLegacySettingsToMigrate())
            {
                sections.lighting.materialPipeline = sections.render != null
                    ? sections.render.materialPipeline
                    : PixelateCaptureManager.MaterialPipeline.urp;
            }
            migratedMaterialPipelineToLighting = true;
            return true;
        }

        private bool SectionsAreDefault()
        {
            if (sections == null)
            {
                return true;
            }

            PaletteSettings sectionPalette = sections.palette;
            RenderSettings sectionRender = sections.render;
            LightingSettings sectionLighting = sections.lighting;
            if (sectionPalette == null || sectionRender == null || sectionLighting == null)
            {
                return true;
            }

            return sectionPalette.enabled
                && sectionPalette.style == PixelateCaptureManager.PaletteStyle.auto
                && sectionPalette.autoDetail == PixelateCaptureManager.AutoPaletteDetail.medium
                && sectionPalette.customPalette == null
                && sectionPalette.colorCount == 16
                && (sectionRender.cellSize == new Vector2Int(64, 64) || sectionRender.cellSize == new Vector2Int(128, 128))
                && sectionRender.linkCellSizeAxes == false
                && sectionRender.pivot == new Vector2(0.5f, 0.5f)
                && sectionRender.pixelated
                && sectionRender.materialPipeline == PixelateCaptureManager.MaterialPipeline.urp
                && sectionLighting.enabled
                && (sectionLighting.lightingSteps == 2 || sectionLighting.lightingSteps == 4);
        }

        private bool HasLegacySettingsToMigrate()
        {
            return !PaletteMatchesDefault(palette)
                || !RenderMatchesDefault(render)
                || !LightingMatchesDefault(lighting);
        }

        private static bool PaletteMatchesDefault(PaletteSettings settings)
        {
            return settings == null
                || (settings.enabled
                    && settings.style == PixelateCaptureManager.PaletteStyle.auto
                    && settings.autoDetail == PixelateCaptureManager.AutoPaletteDetail.medium
                    && settings.customPalette == null
                    && settings.colorCount == 16);
        }

        private static bool RenderMatchesDefault(RenderSettings settings)
        {
            return settings == null
                || (settings.cellSize == new Vector2Int(64, 64)
                    && settings.linkCellSizeAxes == false
                    && settings.pivot == new Vector2(0.5f, 0.5f)
                    && settings.pixelated
                    && settings.materialPipeline == PixelateCaptureManager.MaterialPipeline.urp);
        }

        private static bool LightingMatchesDefault(LightingSettings settings)
        {
            return settings == null
                || (settings.enabled
                    && settings.lightingSteps == 2
                    && settings.materialPipeline == PixelateCaptureManager.MaterialPipeline.urp);
        }

        [Serializable]
        public class PaletteSettings
        {
            public bool enabled = true;
            public PixelateCaptureManager.PaletteStyle style = PixelateCaptureManager.PaletteStyle.auto;
            public PixelateCaptureManager.AutoPaletteDetail autoDetail = PixelateCaptureManager.AutoPaletteDetail.medium;
            public Texture2D customPalette;
            public int colorCount = 16;

            public PixelateCaptureManager.PaletteStyle EffectiveStyle
            {
                get
                {
                    if (enabled == false)
                    {
                        return PixelateCaptureManager.PaletteStyle.none;
                    }

                    return style == PixelateCaptureManager.PaletteStyle.none
                        ? PixelateCaptureManager.PaletteStyle.auto
                        : style;
                }
            }

            public void Sanitize()
            {
                colorCount = Mathf.Max(1, colorCount);
                if (style == PixelateCaptureManager.PaletteStyle.none)
                {
                    style = PixelateCaptureManager.PaletteStyle.auto;
                }
            }
        }

        [Serializable]
        public class RenderSettings
        {
            public Vector2Int cellSize = new Vector2Int(64, 64);
            public bool linkCellSizeAxes;
            public Vector2 pivot = new Vector2(0.5f, 0.5f);
            public bool pixelated = true;
            public PixelateCaptureManager.MaterialPipeline materialPipeline = PixelateCaptureManager.MaterialPipeline.urp;

            public void Sanitize()
            {
                cellSize.x = Mathf.Max(1, cellSize.x);
                cellSize.y = Mathf.Max(1, cellSize.y);
                pivot.x = Mathf.Clamp01(pivot.x);
                pivot.y = Mathf.Clamp01(pivot.y);
            }
        }

        [Serializable]
        public class LightingSettings
        {
            public bool enabled = true;
            public int lightingSteps = 2;
            public PixelateCaptureManager.MaterialPipeline materialPipeline = PixelateCaptureManager.MaterialPipeline.urp;

            public void Sanitize()
            {
                lightingSteps = Mathf.Clamp(lightingSteps, 1, 10);
            }
        }
    }
}
