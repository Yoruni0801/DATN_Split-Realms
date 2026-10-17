#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;

namespace Tien.EditorTools
{
    /// <summary>
    /// Editor tool tạo UI Settings & Credits chuẩn Fantasy RPG cao cấp
    /// Các thanh Slider Âm Thanh thiết kế chuẩn xác theo mẫu Settings.prefab của Layer Lab.
    /// Cột Đồ Họa và Bảng Credits giữ nguyên toàn bộ tính năng và thẩm mỹ đỉnh cao.
    /// </summary>
    public static class SettingsUIBuilder
    {
        private const string PREFAB_DIR = "Assets/_Project/Tien/Pre";
        private const string ROOT_SPRITE_DIR = "Assets/UIA/Asset/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/Sprites/Component/";

        // Thư mục con
        private const string POPUP      = "Popup";
        private const string FRAME      = "Frame";
        private const string SLIDER     = "Slider";
        private const string BUTTON     = "Button";
        private const string ICON_MISC  = "IconMisc";
        private const string LABEL      = "Label-Title";
        private const string UI_ETC     = "UI_Etc";
        private const string ITEM_ICONS = "Icon_ItemIcons/NoShadow/128";

        // ============= SPRITE NAMES =============
        // Popup
        private const string SP_POPUP_BG           = "Popup_02_Single";
        private const string SP_POPUP_DECO         = "Popup_02_Single_Deco";
        private const string SP_POPUP_INNER_BG     = "Popup_02_White_Bg";
        
        // Frame
        private const string SP_PANEL_BG           = "PanelFrame_01_Bg";
        private const string SP_PANEL_DECO         = "PanelFrame_01_DecoLine";
        private const string SP_LIST_BG            = "Listframe_01~02_Bg";
        private const string SP_LIST_BORDER        = "Listframe_01~02_Border";
        private const string SP_LINE_DECO          = "LineFrame_02";
        private const string SP_LINE_FRAME_03      = "LineTextFrame_03_Line";
        
        // Slider (Chuẩn Settings.prefab của Layer Lab)
        private const string SP_SL_HANDLE_BG       = "Slider_Handle_Bg";
        private const string SP_SL_HANDLE_BORDER   = "Slider_Handle_Border";
        private const string SP_SL_HANDLE_FILL     = "Slider_Handle_Fill";
        private const string SP_SL_HANDLE_KNOB     = "Switch_Handle_On";
        private const string SP_SL_HANDLE_KNOB_ALT = "Slider_Handle_Handle";

        // Slider (Tapered cho Brightness)
        private const string SP_SL_TAPERED_BG      = "Slider_Border_Tapered_01_Bg";
        private const string SP_SL_TAPERED_BORDER  = "Slider_Border_Tapered_01_Border";
        private const string SP_SL_TAPERED_FILL    = "Slider_Border_Tapered_01_FillArea";

        // Button
        private const string SP_BTN_CIRCLE_RED     = "Button_Circle_01_Red";

        // Label / Ribbons
        private const string SP_RIBBON_PURPLE      = "Title_Ribbon_01_Purple";
        private const string SP_RIBBON_GREEN       = "Title_Ribbon_02_Green";
        private const string SP_RIBBON_BLUE        = "Title_Ribbon_02_Blue";

        // Icons
        private const string SP_ICO_CLOSE          = "Icon_Close";
        private const string SP_ICO_ARROW_PREV     = "Icon_ArrowSet_Prev";
        private const string SP_ICO_ARROW_NEXT     = "Icon_ArrowSet_Next";
        private const string SP_ICO_SOUND_ON       = "Icon_Setting_Sound_On";
        private const string SP_ICO_SOUND_OFF      = "Icon_Setting_Sound_Off";
        private const string SP_ICO_BGM_ON         = "Icon_Setting_Bgm_On";
        private const string SP_ICO_BGM_OFF        = "Icon_Setting_Bgm_Off";
        private const string SP_ICO_POTION_BLUE    = "icon_potion_blue";
        private const string SP_ICO_POTION_GREEN   = "icon_potion_green";
        private const string SP_ICO_LIGHTNING      = "icon_lightning_yellow";

        [MenuItem("Tools/UI/Build Settings & Credits Prefabs (FantasyRPG)")]
        public static void BuildAllPrefabs()
        {
            if (!Directory.Exists(PREFAB_DIR))
                Directory.CreateDirectory(PREFAB_DIR);

            TMP_FontAsset font = FindBestFont();

            GameObject canvasObj = CreateCanvas("Settings_Canvas_1080p");
            GameObject settingPanel = BuildSettingPanel(canvasObj.transform, font);
            GameObject creditsPanel = BuildCreditsPanel(canvasObj.transform, font);
            creditsPanel.SetActive(false);

            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasObj, $"{PREFAB_DIR}/Settings_Canvas_1080p.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(settingPanel, $"{PREFAB_DIR}/Settings_Panel_Full.prefab");
            PrefabUtility.SaveAsPrefabAsset(creditsPanel, $"{PREFAB_DIR}/Credits_Panel.prefab");

            GameObject.DestroyImmediate(canvasObj);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[SettingsUIBuilder] SUCCESS: GUI Pro-FantasyRPG Settings & Credits Prefabs Created!</color>");
            EditorUtility.DisplayDialog("Thành Công", "Đã tạo 3 Prefab Fantasy RPG tuyệt đẹp tại:\n" + PREFAB_DIR, "OK");
        }

        // ============================== CANVAS ==============================
        private static GameObject CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<Canvas>().sortingOrder = 80;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            return go;
        }

        // ============================== SETTING PANEL ==============================
        private static GameObject BuildSettingPanel(Transform parent, TMP_FontAsset font)
        {
            // Root overlay mờ huyền bí
            var root = MakeRect("Setting", parent, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddImg(root, null, new Color(0.01f, 0.01f, 0.03f, 0.88f));

            // ── Cửa sổ chính Fantasy RPG (1260 x 780) ──
            var window = MakeRect("Window", root.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(1260, 780));
            AddSlicedImg(window, LoadSprite(POPUP, SP_POPUP_BG), Color.white);

            // Nền tối bên trong tăng độ sâu
            var innerBg = MakeRect("InnerBg", window.transform, V2(0,0), V2(1,1), V2(0,0), V2(-24,-24));
            AddSlicedImg(innerBg, LoadSprite(POPUP, SP_POPUP_INNER_BG), new Color(0.07f, 0.05f, 0.12f, 0.95f));
            innerBg.GetComponent<Image>().raycastTarget = false;

            // Lớp viền hoa văn góc kim loại mạ vàng
            var deco = MakeRect("WindowDeco", window.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(deco, LoadSprite(POPUP, SP_POPUP_DECO), Color.white);
            deco.GetComponent<Image>().raycastTarget = false;

            // ── Ruy băng tiêu đề lớn ở đỉnh (Title_Ribbon_01_Purple) ──
            var ribbon = MakeRect("TitleRibbon", window.transform, V2(.5f,1), V2(.5f,1), V2(0,18), V2(560, 100));
            AddImg(ribbon, LoadSprite(LABEL, SP_RIBBON_PURPLE), Color.white);
            ribbon.GetComponent<Image>().preserveAspect = true;

            var titleTxt = MakeTMP("TitleText", ribbon.transform, "❖  CÀI ĐẶT HỆ THỐNG  ❖", 25, FontStyles.Bold, new Color(1f, 0.92f, 0.55f), font);
            SetRect(titleTxt, V2(.08f,.2f), V2(.92f,.85f), V2(0,0), V2(0,0));
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.enableWordWrapping = false;

            // ── Nút Đóng (Button_Circle_01_Red) ──
            var closeBtn = MakeRect("CloseButton", window.transform, V2(1,1), V2(1,1), V2(-22,-22), V2(64,64));
            AddImg(closeBtn, LoadSprite(BUTTON, SP_BTN_CIRCLE_RED), Color.white);
            closeBtn.AddComponent<Button>();
            var closeIcon = MakeRect("XIcon", closeBtn.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(30,30));
            AddImg(closeIcon, LoadSprite(ICON_MISC, SP_ICO_CLOSE), Color.white);

            // ── Thanh trang trí đáy ──
            var bottomLine = MakeRect("BottomLine", window.transform, V2(.5f,0), V2(.5f,0), V2(0,22), V2(960, 14));
            AddSlicedImg(bottomLine, LoadSprite(FRAME, SP_LINE_DECO), new Color(1f, 0.85f, 0.4f, 0.65f));

            // ── Box chứa 2 cột cân xứng (Tên 'box' để tương thích 100% MainMenuUIManager) ──
            var box = MakeRect("box", window.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,-16), V2(1180, 620));

            // ═══════════════════════════════════════════════════════════
            // CỘT 1: ÂM THANH (Tên 'Audio' – Thiết kế chuẩn theo Settings.prefab)
            // ═══════════════════════════════════════════════════════════
            var audioCol = MakeRect("Audio", box.transform, V2(0,0), V2(.485f,1), V2(0,0), V2(0,0));
            AddSlicedImg(audioCol, LoadSprite(FRAME, SP_PANEL_BG), new Color(0.11f, 0.08f, 0.18f, 0.96f));

            var audioDecLine = MakeRect("DecLine", audioCol.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(audioDecLine, LoadSprite(FRAME, SP_PANEL_DECO), new Color(1f, 0.85f, 0.5f, 0.7f));
            audioDecLine.GetComponent<Image>().raycastTarget = false;

            // Header Audio (Title_Ribbon_02_Green)
            var aRibbon = MakeRect("HeaderRibbon", audioCol.transform, V2(.5f,1), V2(.5f,1), V2(0,10), V2(400, 62));
            AddImg(aRibbon, LoadSprite(LABEL, SP_RIBBON_GREEN), Color.white);
            aRibbon.GetComponent<Image>().preserveAspect = true;
            var aHdrT = MakeTMP("Text", aRibbon.transform, "🔊  ÂM THANH & NHẠC", 19, FontStyles.Bold, new Color(1f, 0.96f, 0.8f), font);
            SetRect(aHdrT, V2(.12f,.12f), V2(.88f,.88f), V2(0,0), V2(0,0));
            aHdrT.alignment = TextAlignmentOptions.Center;

            // Slider 1: Master (Chuẩn Settings.prefab)
            Slider masterSl = BuildSettingsPrefabSlider("MasterGroup", audioCol.transform, -95, "Âm lượng tổng (Master):", font,
                new Color(0.25f, 0.75f, 1f), SP_ICO_POTION_BLUE, SP_ICO_SOUND_ON, SP_ICO_SOUND_OFF,
                out Image masterFill, out GameObject masterAct, out GameObject masterMute, out TextMeshProUGUI masterPct);

            // Slider 2: BGM (Chuẩn Settings.prefab)
            Slider bgmSl = BuildSettingsPrefabSlider("BGMGroup", audioCol.transform, -238, "Nhạc nền (Music / BGM):", font,
                new Color(0.35f, 0.95f, 0.55f), SP_ICO_POTION_GREEN, SP_ICO_BGM_ON, SP_ICO_BGM_OFF,
                out Image bgmFill, out GameObject bgmAct, out GameObject bgmMute, out TextMeshProUGUI bgmPct);

            // Slider 3: SFX (Chuẩn Settings.prefab)
            Slider sfxSl = BuildSettingsPrefabSlider("SFXGroup", audioCol.transform, -381, "Hiệu ứng âm thanh (Sound Fx):", font,
                new Color(1f, 0.80f, 0.25f), SP_ICO_LIGHTNING, SP_ICO_SOUND_ON, SP_ICO_SOUND_OFF,
                out Image sfxFill, out GameObject sfxAct, out GameObject sfxMute, out TextMeshProUGUI sfxPct);

            // ═══════════════════════════════════════════════════════════
            // CỘT 2: ĐỒ HỌA & HIỂN THỊ (Tên 'Hienthi' – Giữ nguyên như hiện tại)
            // ═══════════════════════════════════════════════════════════
            var gfxCol = MakeRect("Hienthi", box.transform, V2(.515f,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(gfxCol, LoadSprite(FRAME, SP_PANEL_BG), new Color(0.09f, 0.08f, 0.17f, 0.96f));

            var gfxDecLine = MakeRect("DecLine", gfxCol.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(gfxDecLine, LoadSprite(FRAME, SP_PANEL_DECO), new Color(1f, 0.85f, 0.5f, 0.7f));
            gfxDecLine.GetComponent<Image>().raycastTarget = false;

            // Header Graphics (Title_Ribbon_02_Blue)
            var gRibbon = MakeRect("HeaderRibbon", gfxCol.transform, V2(.5f,1), V2(.5f,1), V2(0,10), V2(400, 62));
            AddImg(gRibbon, LoadSprite(LABEL, SP_RIBBON_BLUE), Color.white);
            gRibbon.GetComponent<Image>().preserveAspect = true;
            var gHdrT = MakeTMP("Text", gRibbon.transform, "🎮  ĐỒ HỌA & HIỆU ỨNG", 19, FontStyles.Bold, new Color(1f, 0.96f, 0.8f), font);
            SetRect(gHdrT, V2(.12f,.12f), V2(.88f,.88f), V2(0,0), V2(0,0));
            gHdrT.alignment = TextAlignmentOptions.Center;

            // 6 Selector Rows (Khoảng cách đều đặn 62px)
            BuildRPGSelectorRow("ResolutionRow",  gfxCol.transform, -74,  "Độ phân giải:",          font, out Button rP, out Button rN, out TextMeshProUGUI rV);
            BuildRPGSelectorRow("ScreenModeRow",  gfxCol.transform, -136, "Chế độ hiển thị:",       font, out Button mP, out Button mN, out TextMeshProUGUI mV);
            BuildRPGSelectorRow("BloomRow",       gfxCol.transform, -198, "Phát sáng (Bloom FX):",  font, out Button bP, out Button bN, out TextMeshProUGUI bV);
            BuildRPGSelectorRow("ScreenShakeRow", gfxCol.transform, -260, "Rung màn hình (Shake):",  font, out Button sP, out Button sN, out TextMeshProUGUI sV);
            BuildRPGSelectorRow("FPSLimitRow",    gfxCol.transform, -322, "Giới hạn FPS (120):",    font, out Button fP, out Button fN, out TextMeshProUGUI fV);
            BuildRPGSelectorRow("VSyncRow",       gfxCol.transform, -384, "Đồng bộ dọc (VSync):",   font, out Button vP, out Button vN, out TextMeshProUGUI vV);

            // Slider Độ Sáng Màn Hình (Tapered Gold RPG Style)
            Slider brightSl = BuildBrightnessSlider("BrightnessRow", gfxCol.transform, -450, "Độ sáng màn hình:", font, out TextMeshProUGUI brightPct);

            // ═══════════════════════════════════════════════════════════
            // GẮN VÀ SERIALIZE SCRIPTS
            // ═══════════════════════════════════════════════════════════
            var audioMgr = root.AddComponent<AudioSettingsManager>();
            audioMgr.Master = masterSl; audioMgr.BGM = bgmSl; audioMgr.SFX = sfxSl;
            audioMgr.masterFill = masterFill; audioMgr.bgmFill = bgmFill; audioMgr.sfxFill = sfxFill;
            audioMgr.masterActiveIcon = masterAct; audioMgr.masterMuteIcon = masterMute;
            audioMgr.bgmActiveIcon = bgmAct; audioMgr.bgmMuteIcon = bgmMute;
            audioMgr.sfxActiveIcon = sfxAct; audioMgr.sfxMuteIcon = sfxMute;

            var grad = new Gradient();
            grad.SetKeys(
                new[]{ new GradientColorKey(new Color(0.95f, 0.30f, 0.30f), 0), new GradientColorKey(new Color(0.35f, 0.75f, 1f), 0.5f), new GradientColorKey(new Color(0.35f, 0.95f, 0.65f), 1) },
                new[]{ new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            audioMgr.masterGradient = grad; audioMgr.bgmGradient = grad; audioMgr.sfxGradient = grad;

            var gfxUI = root.AddComponent<GraphicsSettingsUI>();
            gfxUI.resPrevBtn = rP; gfxUI.resNextBtn = rN; gfxUI.resValueText = rV;
            gfxUI.modePrevBtn = mP; gfxUI.modeNextBtn = mN; gfxUI.modeValueText = mV;
            gfxUI.bloomPrevBtn = bP; gfxUI.bloomNextBtn = bN; gfxUI.bloomValueText = bV;
            gfxUI.shakePrevBtn = sP; gfxUI.shakeNextBtn = sN; gfxUI.shakeValueText = sV;
            gfxUI.fpsPrevBtn = fP; gfxUI.fpsNextBtn = fN; gfxUI.fpsValueText = fV;
            gfxUI.vsyncPrevBtn = vP; gfxUI.vsyncNextBtn = vN; gfxUI.vsyncValueText = vV;
            gfxUI.brightnessSlider = brightSl; gfxUI.brightnessValueText = brightPct;

            return root;
        }

        // ============================== CHUẨN SETTINGS.PREFAB SLIDER BUILDER ==============================
        /// <summary>
        /// Xây dựng thanh slider âm thanh chuẩn 100% theo mẫu Settings.prefab của Layer Lab
        /// </summary>
        private static Slider BuildSettingsPrefabSlider(string id, Transform parent, float y, string label,
            TMP_FontAsset font, Color fillTint, string leadIconName, string activeIconName, string muteIconName,
            out Image fillImg, out GameObject activeIcon, out GameObject muteIcon, out TextMeshProUGUI pctText)
        {
            var row = MakeRect(id, parent, V2(.035f,1), V2(.965f,1), V2(0,y), V2(0,116));

            // 1. Icon bên trái (Icon_Setting_Sound_On / Icon_Setting_Bgm_On)
            activeIcon = MakeRect("IconOn", row.transform, V2(0,.72f), V2(0,.72f), V2(24,0), V2(48,48));
            AddImg(activeIcon, LoadSprite(ICON_MISC, activeIconName) ?? LoadSprite(ITEM_ICONS, leadIconName), Color.white);

            muteIcon = MakeRect("IconOff", row.transform, V2(0,.72f), V2(0,.72f), V2(24,0), V2(48,48));
            AddImg(muteIcon, LoadSprite(ICON_MISC, muteIconName), new Color(0.95f, 0.35f, 0.35f));
            muteIcon.SetActive(false);

            // 2. Tên Label (Text TMP)
            var lbl = MakeTMP("Label", row.transform, label, 16, FontStyles.Bold, new Color(0.96f, 0.92f, 0.82f), font);
            SetRect(lbl, V2(0,.72f), V2(.65f,.72f), V2(54,0), V2(0,30));
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            // 3. Số % âm lượng
            pctText = MakeTMP("PctText", row.transform, "100%", 16, FontStyles.Bold, new Color(0.45f, 0.90f, 1f), font);
            SetRect(pctText, V2(.65f,.72f), V2(1,.72f), V2(-6,0), V2(0,30));
            pctText.alignment = TextAlignmentOptions.MidlineRight;

            // 4. Thanh Slider chuẩn Settings.prefab (Slider_Handle_Bg + Fill + Border + Switch_Handle_On)
            var slObj = MakeRect(id.Replace("Group",""), row.transform, V2(0,0), V2(1,.42f), V2(14,0), V2(-28,0));
            var slider = slObj.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;

            // Background (Slider_Handle_Bg)
            var bg = MakeRect("Bg", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(bg, LoadSprite(SLIDER, SP_SL_HANDLE_BG), new Color(0.16f, 0.12f, 0.25f));

            // Fill Area có Mask chuẩn như Settings.prefab
            var fillArea = MakeRect("FillArea", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(-12,0));
            var mask = fillArea.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var fillAreaImg = AddSlicedImg(fillArea, LoadSprite(SLIDER, SP_SL_HANDLE_FILL), Color.white);

            // Fill Image
            var fillObj = MakeRect("Fill", fillArea.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            fillImg = AddSlicedImg(fillObj, LoadSprite(SLIDER, SP_SL_HANDLE_FILL), fillTint);

            // Border viền thanh (Slider_Handle_Border)
            var border = MakeRect("Border", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(4,4));
            AddSlicedImg(border, LoadSprite(SLIDER, SP_SL_HANDLE_BORDER), Color.white);
            border.GetComponent<Image>().raycastTarget = false;

            // Handle Slide Area & Handle (Switch_Handle_On / Slider_Handle_Handle)
            var handleArea = MakeRect("Handle Slide Area", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            var handle = MakeRect("Handle", handleArea.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(44,44));
            var handleImg = AddImg(handle, LoadSprite(UI_ETC, SP_SL_HANDLE_KNOB) ?? LoadSprite(SLIDER, SP_SL_HANDLE_KNOB_ALT), Color.white);

            // Shadow Effect cho Handle như Settings.prefab
            var shadow = handle.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.35f);
            shadow.effectDistance = new Vector2(2, -3);

            slider.fillRect = fillImg.rectTransform;
            slider.handleRect = handleImg.rectTransform;
            slider.targetGraphic = handleImg;

            // 5. Đường kẻ phân cách phía dưới (ListLine như Settings.prefab)
            var listLine = MakeRect("ListLine", row.transform, V2(0,0), V2(1,0), V2(0,-2), V2(0,4));
            AddSlicedImg(listLine, LoadSprite(FRAME, SP_LINE_DECO), new Color(0.46f, 0.38f, 0.49f, 0.36f));
            listLine.GetComponent<Image>().raycastTarget = false;

            return slider;
        }

        // ============================== RPG SELECTOR ROW BUILDER ==============================
        private static void BuildRPGSelectorRow(string name, Transform parent, float y, string label,
            TMP_FontAsset font, out Button prevBtn, out Button nextBtn, out TextMeshProUGUI valText)
        {
            var row = MakeRect(name, parent, V2(.035f,1), V2(.965f,1), V2(0,y), V2(0,54));

            // Nhãn bên trái
            MakeTMP("Label", row.transform, label, 15, FontStyles.Bold, new Color(0.95f, 0.91f, 0.80f), font);
            SetRect(row.transform.Find("Label"), V2(0,.5f), V2(.42f,.5f), V2(4,0), V2(0,32));

            // Khung hộp chọn (Listframe_01~02_Bg + Border)
            var selBox = MakeRect("Selector", row.transform, V2(.43f,.06f), V2(.99f,.94f), V2(0,0), V2(0,0));
            AddSlicedImg(selBox, LoadSprite(FRAME, SP_LIST_BG), new Color(0.13f, 0.10f, 0.22f));
            
            var selBorder = MakeRect("SelBorder", selBox.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(selBorder, LoadSprite(FRAME, SP_LIST_BORDER), Color.white);
            selBorder.GetComponent<Image>().raycastTarget = false;

            // Nút mũi tên lùi (Trái)
            var leftObj = MakeRect("LeftBtn", selBox.transform, V2(0,.5f), V2(0,.5f), V2(22,0), V2(36,36));
            AddImg(leftObj, LoadSprite(ICON_MISC, SP_ICO_ARROW_PREV), Color.white);
            prevBtn = leftObj.AddComponent<Button>();

            // Nút mũi tên tiến (Phải)
            var rightObj = MakeRect("RightBtn", selBox.transform, V2(1,.5f), V2(1,.5f), V2(-22,0), V2(36,36));
            AddImg(rightObj, LoadSprite(ICON_MISC, SP_ICO_ARROW_NEXT), Color.white);
            nextBtn = rightObj.AddComponent<Button>();

            // Text giá trị ở giữa
            valText = MakeTMP("ValueText", selBox.transform, "—", 15, FontStyles.Bold, new Color(0.55f, 0.90f, 1f), font);
            SetRect(valText, V2(.14f,0), V2(.86f,1), V2(0,0), V2(0,0));
            valText.alignment = TextAlignmentOptions.Center;
        }

        // ============================== BRIGHTNESS SLIDER BUILDER ==============================
        private static Slider BuildBrightnessSlider(string name, Transform parent, float y, string label, TMP_FontAsset font, out TextMeshProUGUI valText)
        {
            var row = MakeRect(name, parent, V2(.035f,1), V2(.965f,1), V2(0,y), V2(0,60));

            // Nhãn và icon đèn nến
            var lbl = MakeTMP("Label", row.transform, "💡  " + label, 15, FontStyles.Bold, new Color(0.96f, 0.92f, 0.80f), font);
            SetRect(lbl, V2(0,1), V2(.55f,1), V2(4,-2), V2(0,24));

            valText = MakeTMP("Val", row.transform, "100%", 15, FontStyles.Bold, new Color(1f, 0.85f, 0.45f), font);
            SetRect(valText, V2(.75f,1), V2(.99f,1), V2(0,-2), V2(0,24));
            valText.alignment = TextAlignmentOptions.MidlineRight;

            var slObj = MakeRect("BrightnessSlider", row.transform, V2(0,0), V2(1,.52f), V2(0,0), V2(0,0));
            var slider = slObj.AddComponent<Slider>();
            slider.minValue = 1f; slider.maxValue = 100f; slider.value = 100f; slider.wholeNumbers = true;

            var bg = MakeRect("Bg", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(bg, LoadSprite(SLIDER, SP_SL_TAPERED_BG), new Color(0.16f, 0.12f, 0.25f));

            var border = MakeRect("Border", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(6,6));
            AddSlicedImg(border, LoadSprite(SLIDER, SP_SL_TAPERED_BORDER), Color.white);
            border.GetComponent<Image>().raycastTarget = false;

            var fillArea = MakeRect("Fill Area", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(-8,0));
            var fill = MakeRect("Fill", fillArea.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            var fillImg = AddSlicedImg(fill, LoadSprite(SLIDER, SP_SL_TAPERED_FILL), new Color(0.95f, 0.70f, 1f));

            var hArea = MakeRect("Handle Slide Area", slObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            var h = MakeRect("Handle", hArea.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(36,36));
            var hImg = AddImg(h, LoadSprite(UI_ETC, SP_SL_HANDLE_KNOB) ?? LoadSprite(SLIDER, SP_SL_HANDLE_KNOB_ALT), Color.white);

            slider.fillRect = fillImg.rectTransform;
            slider.handleRect = hImg.rectTransform;
            slider.targetGraphic = hImg;
            return slider;
        }

        // ============================== CREDITS PANEL ==============================
        private static GameObject BuildCreditsPanel(Transform parent, TMP_FontAsset font)
        {
            var root = MakeRect("CreditsPanel", parent, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddImg(root, null, new Color(0.01f, 0.01f, 0.03f, 0.88f));

            var window = MakeRect("Window", root.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(960, 700));
            AddSlicedImg(window, LoadSprite(POPUP, SP_POPUP_BG), Color.white);

            var innerBg = MakeRect("InnerBg", window.transform, V2(0,0), V2(1,1), V2(0,0), V2(-20,-20));
            AddSlicedImg(innerBg, LoadSprite(POPUP, SP_POPUP_INNER_BG), new Color(0.07f, 0.05f, 0.12f, 0.95f));
            innerBg.GetComponent<Image>().raycastTarget = false;

            var deco = MakeRect("Deco", window.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(deco, LoadSprite(POPUP, SP_POPUP_DECO), Color.white);
            deco.GetComponent<Image>().raycastTarget = false;

            // Ribbon tiêu đề
            var ribbon = MakeRect("TitleRibbon", window.transform, V2(.5f,1), V2(.5f,1), V2(0,16), V2(520, 90));
            AddImg(ribbon, LoadSprite(LABEL, SP_RIBBON_PURPLE), Color.white);
            ribbon.GetComponent<Image>().preserveAspect = true;

            var title = MakeTMP("Title", ribbon.transform, "❖  THÔNG TIN DỰ ÁN  ❖", 23, FontStyles.Bold, new Color(1f, 0.92f, 0.55f), font);
            SetRect(title, V2(.08f,.2f), V2(.92f,.85f), V2(0,0), V2(0,0));
            title.alignment = TextAlignmentOptions.Center;

            // Nút đóng
            var closeObj = MakeRect("CloseButton", window.transform, V2(1,1), V2(1,1), V2(-20,-20), V2(58,58));
            AddImg(closeObj, LoadSprite(BUTTON, SP_BTN_CIRCLE_RED), Color.white);
            var closeBtn = closeObj.AddComponent<Button>();
            var closeX = MakeRect("XIcon", closeObj.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,0), V2(26,26));
            AddImg(closeX, LoadSprite(ICON_MISC, SP_ICO_CLOSE), Color.white);

            // Bottom line
            var bottomLine = MakeRect("BottomLine", window.transform, V2(.5f,0), V2(.5f,0), V2(0,20), V2(760, 12));
            AddSlicedImg(bottomLine, LoadSprite(FRAME, SP_LINE_DECO), new Color(1f, 0.85f, 0.4f, 0.6f));

            // ScrollView
            var scrollObj = MakeRect("ScrollView", window.transform, V2(.5f,.5f), V2(.5f,.5f), V2(0,-18), V2(870, 520));
            var sr = scrollObj.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 35;

            var viewport = MakeRect("Viewport", scrollObj.transform, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            AddSlicedImg(viewport, LoadSprite(FRAME, SP_PANEL_BG), new Color(0.09f, 0.07f, 0.15f, 0.90f));
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var content = MakeRect("Content", viewport.transform, V2(0,1), V2(1,1), V2(0,0), V2(0,900));
            content.GetComponent<RectTransform>().pivot = V2(.5f,1);
            content.AddComponent<VerticalLayoutGroup>().spacing = 14;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.content = content.GetComponent<RectTransform>();

            var credText = MakeTMP("CreditsText", content.transform,
                "\n<size=28><color=#FFD700><b>❖ ĐỒ ÁN TỐT NGHIỆP ❖</b></color></size>\n" +
                "<size=20><color=#FFFFFF><b>CHUYÊN NGÀNH PHÁT TRIỂN GAME & ĐỒ HỌA</b></color></size>\n\n" +
                "<color=#6A538A>════════════════════════════════</color>\n\n" +
                "<size=22><color=#FFCC00><b>GIẢNG VIÊN HƯỚNG DẪN</b></color></size>\n" +
                "<size=19><color=#FFFFFF>ThS. Giảng Viên Phụ Trách</color></size>\n\n" +
                "<color=#6A538A>════════════════════════════════</color>\n\n" +
                "<size=22><color=#FFCC00><b>ĐỘI NGŨ PHÁT TRIỂN</b></color></size>\n\n" +
                "<b>Nguyễn Tiến</b> — <color=#66DDFF>Project Lead</color>\n" +
                "<b>Nguyễn Duy</b> — <color=#66DDFF>Gameplay & Audio</color>\n" +
                "<b>Vũ Hải</b> — <color=#66DDFF>UI/UX & Menu</color>\n" +
                "<b>Quốc</b> — <color=#66DDFF>Level Design</color>\n" +
                "<b>Bảo</b> — <color=#66DDFF>Boss & Minigame</color>\n\n" +
                "<color=#6A538A>════════════════════════════════</color>\n\n" +
                "<size=22><color=#FFCC00><b>TÀI NGUYÊN & CÔNG NGHỆ</b></color></size>\n\n" +
                "Layer Lab — <color=#99E6FF>GUI Pro-FantasyRPG</color>\n" +
                "Unity Technologies — <color=#99E6FF>Unity Engine 2022/2023</color>\n\n" +
                "<color=#6A538A>════════════════════════════════</color>\n\n" +
                "<size=19><color=#FFDD88><i>Cảm ơn các bạn đã trải nghiệm trò chơi!</i></color></size>\n",
                20, FontStyles.Normal, new Color(0.94f, 0.91f, 0.83f), font);
            credText.alignment = TextAlignmentOptions.Center;
            credText.lineSpacing = 14;

            var credMgr = root.AddComponent<Hai.Menu.CreditsUIManager>();
            var so = new SerializedObject(credMgr);
            so.FindProperty("scrollRect").objectReferenceValue = sr;
            so.FindProperty("contentRect").objectReferenceValue = content.GetComponent<RectTransform>();
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedProperties();

            return root;
        }

        // ============================== HELPERS ==============================
        private static Vector2 V2(float x, float y) => new Vector2(x, y);

        private static GameObject MakeRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go;
        }

        private static void SetRect(Component c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var rt = c.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static Image AddImg(GameObject go, Sprite sp, Color col)
        {
            var img = go.AddComponent<Image>();
            if (sp != null) img.sprite = sp;
            img.color = col;
            return img;
        }

        private static Image AddSlicedImg(GameObject go, Sprite sp, Color col)
        {
            var img = go.AddComponent<Image>();
            if (sp != null) { img.sprite = sp; img.type = Image.Type.Sliced; }
            img.color = col;
            return img;
        }

        private static TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float size, FontStyles style, Color col, TMP_FontAsset font)
        {
            var go = MakeRect(name, parent, V2(0,0), V2(1,1), V2(0,0), V2(0,0));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.color = col;
            if (font != null) tmp.font = font;
            return tmp;
        }

        private static TMP_FontAsset FindBestFont()
        {
            foreach (var g in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("Alata") || p.Contains("Josefin") || p.Contains("Play"))
                    return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            }
            var all = AssetDatabase.FindAssets("t:TMP_FontAsset");
            return all.Length > 0 ? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(all[0])) : null;
        }

        private static Sprite LoadSprite(string subFolder, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

            // 1. Thử load đường dẫn trực tiếp
            string directPath = ROOT_SPRITE_DIR + subFolder + "/" + spriteName + ".png";
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(directPath);
            if (sp != null) return sp;

            // 2. Tìm tất cả file Texture2D trong thư mục GUI Pro-FantasyRPG
            string[] guids = AssetDatabase.FindAssets(spriteName + " t:Texture2D", new[] { ROOT_SPRITE_DIR });
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string fn = Path.GetFileNameWithoutExtension(p);
                if (fn.Equals(spriteName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (s != null) return s;

                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(p);
                    foreach (var sa in subAssets)
                    {
                        if (sa is Sprite subSp && sa.name.Equals(spriteName, System.StringComparison.OrdinalIgnoreCase))
                            return subSp;
                    }
                }
            }

            // 3. Fallback toàn bộ thư mục
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (s != null) return s;
            }

            return null;
        }
    }
}
#endif
