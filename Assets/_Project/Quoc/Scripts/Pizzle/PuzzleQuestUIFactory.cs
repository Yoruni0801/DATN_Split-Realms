using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Quoc.PuzzleQuest
{
    internal static class PuzzleQuestUIFactory
    {
        private static readonly Color SkyColor = new Color(0.06f, 0.22f, 0.28f, 1f);
        private static readonly Color SandFarColor = new Color(0.72f, 0.43f, 0.2f, 1f);
        private static readonly Color SandNearColor = new Color(0.39f, 0.2f, 0.12f, 1f);
        private static readonly Color PanelColor = new Color(0.11f, 0.09f, 0.075f, 0.96f);
        private static readonly Color PanelEdgeColor = new Color(0.71f, 0.43f, 0.2f, 1f);
        private static readonly Color ParchmentText = new Color(1f, 0.91f, 0.72f, 1f);

        public static void BuildMissing(PuzzleQuestController controller)
        {
            Canvas canvas = controller.SceneCanvas;
            if (canvas == null)
            {
                Transform existingCanvas = controller.transform.Find("PuzzleQuest UI");
                canvas = existingCanvas != null ? existingCanvas.GetComponent<Canvas>() : null;
                if (canvas == null)
                {
                    GameObject canvasObject = new GameObject(
                        "PuzzleQuest UI",
                        typeof(RectTransform),
                        typeof(Canvas),
                        typeof(CanvasScaler),
                        typeof(GraphicRaycaster));
                    canvasObject.transform.SetParent(controller.transform, false);
                    canvas = canvasObject.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 50;

                    CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;
                }

                controller.SetSceneCanvas(canvas);
            }

            RectTransform root = canvas.GetComponent<RectTransform>();

            RectTransform background = EnsureImage(
                root,
                "Desert Background",
                Vector2.zero,
                Vector2.one,
                SkyColor,
                null,
                false);

            EnsureImage(
                background,
                "Sun Glow",
                new Vector2(0.42f, 0.65f),
                new Vector2(0.58f, 0.94f),
                new Color(1f, 0.63f, 0.24f, 0.28f),
                null,
                false);

            EnsureImage(
                background,
                "Far Dunes",
                new Vector2(0f, 0.46f),
                new Vector2(1f, 0.72f),
                SandFarColor,
                null,
                false);

            EnsureImage(
                background,
                "Near Dunes",
                new Vector2(0f, 0f),
                new Vector2(1f, 0.55f),
                SandNearColor,
                null,
                false);

            RectTransform battleStage = EnsurePanel(
                root,
                "Battle Stage",
                new Vector2(0.018f, 0.625f),
                new Vector2(0.982f, 0.985f),
                new Color(0.035f, 0.09f, 0.105f, 0.92f),
                controller.FrameSprite);

            RawImage battleRender = EnsureRawImage(
                battleStage,
                "Battle Render",
                new Vector2(0.015f, 0.02f),
                new Vector2(0.985f, 0.98f));

            Image battleFlash = EnsureImageComponent(
                battleStage,
                "Battle Flash",
                Vector2.zero,
                Vector2.one,
                Color.clear,
                null,
                false);
            battleFlash.raycastTarget = false;

            TMP_Text actionBanner = EnsureText(
                battleStage,
                "Action Banner",
                new Vector2(0.29f, 0.75f),
                new Vector2(0.71f, 0.98f),
                "PUZZLE QUEST",
                36f,
                ParchmentText,
                controller.UiFont,
                TextAlignmentOptions.Center);

            RectTransform playerPanel = EnsurePanel(
                root,
                "Player Status",
                new Vector2(0.035f, 0.82f),
                new Vector2(0.29f, 0.965f),
                PanelColor,
                controller.PanelSprite);
            BuildHealthPanel(
                playerPanel,
                "TỔ ĐỘI",
                "Player Health Fill",
                "Player Health Text",
                new Color(0.26f, 0.86f, 0.42f, 1f),
                controller.UiFont);

            RectTransform enemyPanel = EnsurePanel(
                root,
                "Enemy Status",
                new Vector2(0.71f, 0.82f),
                new Vector2(0.965f, 0.965f),
                PanelColor,
                controller.PanelSprite);
            BuildHealthPanel(
                enemyPanel,
                "LITHOS MONOLITH",
                "Enemy Health Fill",
                "Enemy Health Text",
                new Color(0.94f, 0.28f, 0.18f, 1f),
                controller.UiFont);

            RectTransform boardFrame = EnsurePanel(
                root,
                "Board Frame",
                new Vector2(0.305f, 0.025f),
                new Vector2(0.695f, 0.62f),
                new Color(0.045f, 0.075f, 0.075f, 0.98f),
                controller.FrameSprite);

            RectTransform boardGrid = EnsureRect(
                boardFrame,
                "Board Grid",
                new Vector2(0.035f, 0.035f),
                new Vector2(0.965f, 0.965f));

            // RectTransform partyPanel = EnsurePanel(
            //     root,
            //     "Party Skills",
            //     new Vector2(0.025f, 0.06f),
            //     new Vector2(0.285f, 0.59f),
            //     PanelColor,
            //     controller.PanelSprite);

            // EnsureText(
            //     partyPanel,
            //     "Party Title",
            //     new Vector2(0.06f, 0.84f),
            //     new Vector2(0.94f, 0.97f),
            //     "TỔ ĐỘI NGUYÊN TỐ",
            //     31f,
            //     ParchmentText,
            //     controller.UiFont,
            //     TextAlignmentOptions.Center);

            // BuildSkillRow(
            //     partyPanel,
            //     "Water Skill Row",
            //     new Vector2(0.07f, 0.61f),
            //     new Vector2(0.93f, 0.81f),
            //     "NƯỚC  |  U 3   I 4   O 5",
            //     new Color(0.12f, 0.68f, 1f, 1f),
            //     controller.UiFont);
            // BuildSkillRow(
            //     partyPanel,
            //     "Fire Skill Row",
            //     new Vector2(0.07f, 0.37f),
            //     new Vector2(0.93f, 0.57f),
            //     "LỬA  |  U 3   I 4   O 5",
            //     new Color(1f, 0.31f, 0.12f, 1f),
            //     controller.UiFont);
            // BuildSkillRow(
            //     partyPanel,
            //     "Wind Skill Row",
                // new Vector2(0.07f, 0.13f),
                // new Vector2(0.93f, 0.33f),
                // "GIÓ  |  U 3   I 4   O 5",
                // new Color(0.3f, 0.91f, 0.48f, 1f),
                // controller.UiFont);

            RectTransform infoPanel = EnsurePanel(
                root,
                "Turn Info",
                new Vector2(0.715f, 0.06f),
                new Vector2(0.975f, 0.59f),
                PanelColor,
                controller.PanelSprite);

            TMP_Text turnText = EnsureText(
                infoPanel,
                "Turn Text",
                new Vector2(0.08f, 0.78f),
                new Vector2(0.92f, 0.94f),
                "LƯỢT CỦA BẠN",
                34f,
                ParchmentText,
                controller.UiFont,
                TextAlignmentOptions.Center);

            TMP_Text comboText = EnsureText(
                infoPanel,
                "Combo Text",
                new Vector2(0.08f, 0.57f),
                new Vector2(0.92f, 0.73f),
                "COMBO x1",
                28f,
                new Color(1f, 0.64f, 0.24f, 1f),
                controller.UiFont,
                TextAlignmentOptions.Center);

            TMP_Text statusText = EnsureText(
                infoPanel,
                "Status Text",
                new Vector2(0.09f, 0.3f),
                new Vector2(0.91f, 0.54f),
                "Kéo một ngọc sang ô kế bên.",
                23f,
                Color.white,
                controller.UiFont,
                TextAlignmentOptions.Center);

            EnsureText(
                infoPanel,
                "Rules Text",
                new Vector2(0.09f, 0.06f),
                new Vector2(0.91f, 0.27f),
                "KIẾM = đánh thường\nGHÉP 4+ = thêm lượt",
                20f,
                new Color(0.82f, 0.78f, 0.68f, 1f),
                controller.UiFont,
                TextAlignmentOptions.Center);

            RectTransform endOverlay = EnsureImage(
                root,
                "End Overlay",
                Vector2.zero,
                Vector2.one,
                new Color(0.015f, 0.018f, 0.022f, 0.88f),
                null,
                false);

            RectTransform resultPanel = EnsurePanel(
                endOverlay,
                "Result Panel",
                new Vector2(0.31f, 0.28f),
                new Vector2(0.69f, 0.72f),
                PanelColor,
                controller.FrameSprite);

            TMP_Text resultTitle = EnsureText(
                resultPanel,
                "Result Title",
                new Vector2(0.08f, 0.62f),
                new Vector2(0.92f, 0.9f),
                "CHIẾN THẮNG",
                56f,
                ParchmentText,
                controller.UiFont,
                TextAlignmentOptions.Center);

            TMP_Text resultSubtitle = EnsureText(
                resultPanel,
                "Result Subtitle",
                new Vector2(0.1f, 0.42f),
                new Vector2(0.9f, 0.62f),
                "Lithos Monolith đã bị đánh bại.",
                25f,
                Color.white,
                controller.UiFont,
                TextAlignmentOptions.Center);

            Button restartButton = EnsureButton(
                resultPanel,
                "Restart Button",
                new Vector2(0.25f, 0.12f),
                new Vector2(0.75f, 0.36f),
                "CHƠI LẠI",
                controller.UiFont);

            if (!Application.isPlaying && endOverlay.gameObject.activeSelf)
            {
                endOverlay.gameObject.SetActive(false);
            }

            controller.AssignSceneUi(
                boardGrid,
                battleRender,
                battleFlash,
                actionBanner,
                playerPanel.Find("Player Health Bar").GetComponent<Slider>(),
                playerPanel.Find("Player Health Bar/Player Health Text").GetComponent<TMP_Text>(),
                enemyPanel.Find("Enemy Health Bar").GetComponent<Slider>(),
                enemyPanel.Find("Enemy Health Bar/Enemy Health Text").GetComponent<TMP_Text>(),
                turnText,
                comboText,
                statusText,
                endOverlay,
                resultTitle,
                resultSubtitle,
                restartButton);
        }

        private static void BuildHealthPanel(
            RectTransform parent,
            string title,
            string fillName,
            string textName,
            Color fillColor,
            TMP_FontAsset font)
        {
            EnsureText(
                parent,
                "Title",
                new Vector2(0.06f, 0.57f),
                new Vector2(0.94f, 0.92f),
                title,
                29f,
                ParchmentText,
                font,
                TextAlignmentOptions.Center);

            RectTransform bar = EnsureImage(
                parent,
                title == "TỔ ĐỘI" ? "Player Health Bar" : "Enemy Health Bar",
                new Vector2(0.08f, 0.18f),
                new Vector2(0.92f, 0.49f),
                new Color(0.025f, 0.028f, 0.028f, 1f),
                null,
                false);

            Slider slider = bar.GetComponent<Slider>();
            if (slider == null)
            {
                slider = bar.gameObject.AddComponent<Slider>();
            }
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.maxValue = 1f;
            slider.value = 1f;

            Image fill = EnsureImageComponent(
                bar,
                fillName,
                new Vector2(0.02f, 0.12f),
                new Vector2(0.98f, 0.88f),
                fillColor,
                null,
                false);
            
            fill.type = Image.Type.Simple;
            fill.raycastTarget = false;
            
            slider.fillRect = fill.rectTransform;

            EnsureText(
                bar,
                textName,
                Vector2.zero,
                Vector2.one,
                "100 / 100",
                21f,
                Color.white,
                font,
                TextAlignmentOptions.Center);
        }

        private static void BuildSkillRow(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            Color accent,
            TMP_FontAsset font)
        {
            RectTransform row = EnsureImage(
                parent,
                name,
                anchorMin,
                anchorMax,
                Color.Lerp(accent, new Color(0.04f, 0.04f, 0.035f, 1f), 0.76f),
                null,
                false);
            Outline outline = row.GetComponent<Outline>();
            if (outline == null)
            {
                outline = row.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = Color.Lerp(accent, Color.black, 0.25f);
            outline.effectDistance = new Vector2(2f, -2f);

            EnsureText(
                row,
                "Label",
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f),
                label,
                23f,
                Color.white,
                font,
                TextAlignmentOptions.Center);
        }

        private static RectTransform EnsurePanel(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            Sprite sprite)
        {
            RectTransform panel = EnsureImage(parent, name, anchorMin, anchorMax, color, sprite, true);
            Outline outline = panel.GetComponent<Outline>();
            if (outline == null)
            {
                outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = PanelEdgeColor;
                outline.effectDistance = new Vector2(3f, -3f);
            }

            return panel;
        }

        private static RectTransform EnsureRect(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<RectTransform>();
            }

            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);
            return rect;
        }

        private static RectTransform EnsureImage(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            Sprite sprite,
            bool sliced)
        {
            return EnsureImageComponent(parent, name, anchorMin, anchorMax, color, sprite, sliced).rectTransform;
        }

        private static Image EnsureImageComponent(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            Sprite sprite,
            bool sliced)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Image existingImage = existing.GetComponent<Image>();
                if (existingImage == null)
                {
                    existingImage = existing.gameObject.AddComponent<Image>();
                }

                return existingImage;
            }

            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);

            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage EnsureRawImage(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<RawImage>();
            }

            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);
            RawImage image = gameObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text EnsureText(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string value,
            float size,
            Color color,
            TMP_FontAsset font,
            TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_Text existingText = existing.GetComponent<TMP_Text>();
                if (existingText != null && existingText.font == null && font != null)
                {
                    existingText.font = font;
                }

                return existingText;
            }

            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);

            TMP_Text text = gameObject.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(10f, size * 0.5f);
            text.fontSizeMax = size;
            text.raycastTarget = false;
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static Button EnsureButton(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            TMP_FontAsset font)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, anchorMin, anchorMax);

            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0.76f, 0.39f, 0.14f, 1f);
            image.raycastTarget = true;
            Button button = gameObject.GetComponent<Button>();

            EnsureText(
                rect,
                "Label",
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f),
                label,
                28f,
                Color.white,
                font,
                TextAlignmentOptions.Center);
            return button;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
