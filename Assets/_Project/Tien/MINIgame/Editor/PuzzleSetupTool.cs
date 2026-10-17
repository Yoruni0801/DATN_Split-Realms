using UnityEngine;
using UnityEngine.UI;
using Tien.Minigames; // Thêm dòng này để gọi được PuzzleManager

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

public class PuzzleSetupTool
{
#if UNITY_EDITOR
    [MenuItem("Tools/Tien/Auto Setup Puzzle Minigame")]
    public static void SetupPuzzleMinigame()
    {
        // 1. Tạo GameObject làm Trigger
        GameObject triggerObj = new GameObject("PuzzleTrigger");
        BoxCollider2D col = triggerObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        Tien.Minigames.PuzzleManager manager = triggerObj.AddComponent<Tien.Minigames.PuzzleManager>();

        // 2. Tìm hoặc tạo Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("PuzzleCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("[Auto Setup] Đã tạo mới PuzzleCanvas 1920x1080.");
        }
        else
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
        }

        // 3. Tạo PuzzleUI Panel
        GameObject puzzleUI = new GameObject("PuzzleUI");
        puzzleUI.transform.SetParent(canvas.transform, false);
        RectTransform puzzleUIRect = puzzleUI.AddComponent<RectTransform>();
        puzzleUIRect.anchorMin = Vector2.zero;
        puzzleUIRect.anchorMax = Vector2.one;
        puzzleUIRect.offsetMin = Vector2.zero;
        puzzleUIRect.offsetMax = Vector2.zero;
        Image bgImage = puzzleUI.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.8f); // Nền đen mờ

        // 4. Tạo Board (Lưới chứa 9 mảnh)
        GameObject board = new GameObject("Board");
        board.transform.SetParent(puzzleUI.transform, false);
        RectTransform boardRect = board.AddComponent<RectTransform>();
        boardRect.sizeDelta = new Vector2(600, 600);
        boardRect.anchoredPosition = Vector2.zero;
        GridLayoutGroup grid = board.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200, 200);
        grid.spacing = new Vector2(2, 2);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        // 5. Tạo Nút Close (Thoát)
        GameObject closeBtnObj = new GameObject("CloseBtn");
        closeBtnObj.transform.SetParent(puzzleUI.transform, false);
        RectTransform btnRect = closeBtnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 1);
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 1);
        btnRect.anchoredPosition = new Vector2(-50, -50); // Cách mép 50px
        btnRect.sizeDelta = new Vector2(50, 50);
        Image btnImage = closeBtnObj.AddComponent<Image>();
        btnImage.color = Color.red;
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        
        // Thêm Text "X" cho nút Close
        GameObject closeTextObj = new GameObject("Text");
        closeTextObj.transform.SetParent(closeBtnObj.transform, false);
        Text closeText = closeTextObj.AddComponent<Text>();
        closeText.text = "X";
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform textRect = closeTextObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Tự động gắn sự kiện OnClick gọi manager.ClosePuzzle
        UnityEventTools.AddPersistentListener(closeBtn.onClick, manager.ClosePuzzle);

        // 6. Tạo Prefab Mảnh Ghép
        GameObject pieceObj = new GameObject("PiecePrefab");
        pieceObj.AddComponent<RectTransform>().sizeDelta = new Vector2(200, 200);
        pieceObj.AddComponent<RawImage>();
        pieceObj.AddComponent<PuzzlePiece>();

        // Lưu thành file Prefab trong thư mục MINIgame
        string prefabPath = "Assets/_Project/Tien/MINIgame/PiecePrefab.prefab";
        if (!System.IO.Directory.Exists(Application.dataPath + "/_Project/Tien/MINIgame"))
        {
            System.IO.Directory.CreateDirectory(Application.dataPath + "/_Project/Tien/MINIgame");
            AssetDatabase.Refresh();
        }
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(pieceObj, prefabPath);
        Object.DestroyImmediate(pieceObj);

        // 7. Gắn References vào PuzzleManager
        manager.puzzleUI = puzzleUI;
        manager.boardTransform = board.transform;
        manager.piecePrefab = prefabAsset;

        // Ẩn UI đi ban đầu
        puzzleUI.SetActive(false);

        // 8. Tự động Setup UI Bảng Nhiệm Vụ (Góc phải màn hình)
        GameObject questUIManagerObj = new GameObject("QuestUIManager");
        Tien.Minigames.QuestUIManager questManager = questUIManagerObj.AddComponent<Tien.Minigames.QuestUIManager>();

        GameObject questPanel = new GameObject("QuestPanel");
        questPanel.transform.SetParent(canvas.transform, false);
        RectTransform questPanelRect = questPanel.AddComponent<RectTransform>();
        questPanelRect.anchorMin = new Vector2(1, 1);
        questPanelRect.anchorMax = new Vector2(1, 1);
        questPanelRect.pivot = new Vector2(1, 1);
        questPanelRect.anchoredPosition = new Vector2(-20, -20); // Cách góc trên phải 20px
        questPanelRect.sizeDelta = new Vector2(300, 100);
        Image questBg = questPanel.AddComponent<Image>();
        questBg.color = new Color(0, 0, 0, 0.7f);

        GameObject descTextObj = new GameObject("QuestDescriptionText");
        descTextObj.transform.SetParent(questPanel.transform, false);
        RectTransform descRect = descTextObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 0.5f);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.offsetMin = new Vector2(15, 0);
        descRect.offsetMax = new Vector2(-15, -10);
        Text descText = descTextObj.AddComponent<Text>();
        descText.text = "Chặt cây để xây cầu";
        descText.color = Color.white;
        descText.alignment = TextAnchor.UpperLeft;
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 24;

        GameObject progTextObj = new GameObject("QuestProgressText");
        progTextObj.transform.SetParent(questPanel.transform, false);
        RectTransform progRect = progTextObj.AddComponent<RectTransform>();
        progRect.anchorMin = new Vector2(0, 0);
        progRect.anchorMax = new Vector2(1, 0.5f);
        progRect.offsetMin = new Vector2(15, 10);
        progRect.offsetMax = new Vector2(-15, 0);
        Text progText = progTextObj.AddComponent<Text>();
        progText.text = "0 / 3";
        progText.color = Color.yellow;
        progText.alignment = TextAnchor.LowerRight;
        progText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        progText.fontSize = 28;

        questManager.questPanel = questPanel;
        questManager.questDescriptionText = descText;
        questManager.questProgressText = progText;

        questPanel.SetActive(false); // Ẩn ban đầu

        // 9. Tự động tạo cây cầu (Bridge)
        GameObject bridgeObj = new GameObject("Bridge (Auto Setup)");
        bridgeObj.transform.position = new Vector3(3, 0, 0); // Đặt ra chỗ khác để dễ nhìn
        SpriteRenderer bridgeRenderer = bridgeObj.AddComponent<SpriteRenderer>();
        Bridge bridgeScript = bridgeObj.AddComponent<Bridge>();
        bridgeScript.bridgeSpriteRenderer = bridgeRenderer;
        
        // Tự động gán luôn cái cầu này vào PuzzleManager
        manager.bridgeObject = bridgeObj;

        // 10. Tự động tạo 3 cái cây (Trees)
        for (int i = 0; i < 3; i++)
        {
            GameObject treeObj = new GameObject("Tree_" + (i + 1) + " (Auto Setup)");
            treeObj.transform.position = new Vector3(-3 + (i * 3), 0, 0); // Xếp 3 cây cách nhau ra
            
            // Thêm component ảnh cho cây
            treeObj.AddComponent<SpriteRenderer>();
            
            // Thêm va chạm 2D cho cây
            BoxCollider2D treeCol = treeObj.AddComponent<BoxCollider2D>();
            treeCol.isTrigger = true;
            treeCol.size = new Vector2(1.5f, 1.5f); // Size to to chút dễ va chạm
            
            // Gắn Script Tree
            treeObj.AddComponent<Tree>();
        }

        // 11. Đánh dấu Scene đã thay đổi để Unity cho phép Save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        // Chọn Trigger để người dùng dễ thấy
        Selection.activeGameObject = triggerObj;

        // Báo cáo thành công và ghi chú các việc còn thiếu
        Debug.Log("<color=cyan>[Auto Setup] Đã tạo xong TOÀN BỘ môi trường, UI, Cầu và Cây cho Mini Game!</color>");
        Debug.LogWarning("<b>=== CÁC BƯỚC CẦN LÀM THÊM ĐỂ HOÀN THIỆN ===</b>\n\n" +
                         "1. [ẢNH GHÉP]: Click vào <b>PuzzleTrigger</b>, kéo bức ảnh muốn chơi ghép vào ô <b>'Puzzle Image'</b>.\n" +
                         "2. [ẢNH CẦU]: Click vào <b>Bridge (Auto Setup)</b>, kéo 2 hình cầu gãy và cầu lành vào ô Broken Sprite và Repaired Sprite.\n" +
                         "3. [ẢNH CÂY]: Click vào 3 cái <b>Tree_ (Auto Setup)</b>, gắn hình cái cây vào component SpriteRenderer cho nó hiện ra.\n" +
                         "4. (Tùy chọn) Kéo cái <b>Bridge (Auto Setup)</b> vào ô 'Bridge Transform' của NPC mà bạn đã làm.\n\n" +
                         "<i>Xong rồi đó! Quá tiện lợi luôn!</i>");
    }
#endif
}
