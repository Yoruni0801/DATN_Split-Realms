using System;
using UnityEngine;

namespace Pixelate
{
    [Serializable]
    public class PixelateProfileSections
    {
        public PixelateProfile.PaletteSettings palette = new PixelateProfile.PaletteSettings();
        public PixelateProfile.LightingSettings lighting = new PixelateProfile.LightingSettings();
        public PixelateProfile.RenderSettings render = new PixelateProfile.RenderSettings();

        public bool Sanitize()
        {
            bool changed = false;
            if (palette == null)
            {
                palette = new PixelateProfile.PaletteSettings();
                changed = true;
            }
            if (lighting == null)
            {
                lighting = new PixelateProfile.LightingSettings();
                changed = true;
            }
            if (render == null)
            {
                render = new PixelateProfile.RenderSettings();
                changed = true;
            }

            palette.Sanitize();
            lighting.Sanitize();
            render.Sanitize();
            return changed;
        }
    }

    [Serializable]
    public class PixelateManagerSections
    {
        public SetupSection setup = new SetupSection();
        public CameraSection camera = new CameraSection();
        [HideInInspector]
        public RenderSection render = new RenderSection();
        public AnimationSection animation = new AnimationSection();
        public PreviewSection preview = new PreviewSection();
        public ExportSettingsSection exportSettings = new ExportSettingsSection();

        public void Sanitize()
        {
            if (setup == null)
            {
                setup = new SetupSection();
            }
            if (camera == null)
            {
                camera = new CameraSection();
            }
            if (render == null)
            {
                render = new RenderSection();
            }
            if (animation == null)
            {
                animation = new AnimationSection();
            }
            if (preview == null)
            {
                preview = new PreviewSection();
            }
            if (exportSettings == null)
            {
                exportSettings = new ExportSettingsSection();
            }

            camera.Sanitize();
            render.Sanitize();
            animation.Sanitize();
            preview.Sanitize();
            exportSettings.Sanitize();
        }

        [Serializable]
        public class SetupSection
        {
            public GameObject target;
        }

        [Serializable]
        public class CameraSection
        {
            public Camera captureCamera;
            public Vector2Int cellSize = new Vector2Int(64, 64);
            public bool linkCellSizeAxes;
            public bool pixelated = true;

            public void Sanitize()
            {
                cellSize.x = Mathf.Max(1, cellSize.x);
                cellSize.y = Mathf.Max(1, cellSize.y);
            }
        }

        [Serializable]
        public class RenderSection
        {
            public Vector2Int cellSize = new Vector2Int(64, 64);
            public bool linkCellSizeAxes;
            public bool pixelated = true;

            public void Sanitize()
            {
                cellSize.x = Mathf.Max(1, cellSize.x);
                cellSize.y = Mathf.Max(1, cellSize.y);
            }
        }

        [Serializable]
        public class AnimationSection
        {
            public PixelateCaptureManager.SourceClipEntry[] sourceClipEntries;
            public int framesPerSecond = 12;
            public int currentFrame;

            public void Sanitize()
            {
                framesPerSecond = Mathf.Max(2, framesPerSecond);
                currentFrame = Mathf.Max(0, currentFrame);
                if (sourceClipEntries == null)
                {
                    return;
                }

                for (int i = 0; i < sourceClipEntries.Length; i++)
                {
                    sourceClipEntries[i].speed = Mathf.Max(0.01f, sourceClipEntries[i].speed);
                }
            }
        }

        [Serializable]
        public class PreviewSection
        {
            public Texture2D previewImage;
            public Texture2D previewNormalImage;

            public void Sanitize()
            {
            }
        }

        [Serializable]
        public class ExportSettingsSection
        {
            public Vector2 pivot = new Vector2(0.5f, 0.5f);
            public float pixelsPerUnit = 64f;
            [HideInInspector]
            public bool pixelated = true;
            public bool overrideCaptures = true;
            public string spriteSavePath = "";

            public void Sanitize()
            {
                pivot.x = Mathf.Clamp01(pivot.x);
                pivot.y = Mathf.Clamp01(pivot.y);
                pixelsPerUnit = Mathf.Max(0.01f, pixelsPerUnit);
            }
        }
    }
}
