using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(TimelineVortexPullClip))]
public class TimelineVortexPullClipEditor : Editor
{
    private SerializedProperty motionTypeProp;
    private SerializedProperty speedModeProp;
    private SerializedProperty speedMultiplierProp;
    private SerializedProperty fixedSpeedProp;
    private SerializedProperty fixedDurationProp;
    private SerializedProperty speedVariationProp;

    private SerializedProperty targetPointAProp;
    private SerializedProperty targetOffsetProp;
    private SerializedProperty itemTargetsProp;
    private SerializedProperty itemsContainerParentProp;
    private SerializedProperty autoFindItemsInRadiusProp;
    private SerializedProperty searchRadiusProp;
    private SerializedProperty searchLayerMaskProp;
    private SerializedProperty searchTagFilterProp;
    private SerializedProperty autoDetectGamePickupsProp;
    private SerializedProperty staggerSpreadProp;
    private SerializedProperty disperseAnglesProp;
    private SerializedProperty randomnessVariationProp;
    private SerializedProperty suctionCurveProp;
    private SerializedProperty planeProp;
    private SerializedProperty spinDirectionProp;
    private SerializedProperty spiralRotationsProp;
    private SerializedProperty initialRadiusOverrideProp;
    private SerializedProperty spiralRadiusCurveProp;
    private SerializedProperty heightArcProp;
    private SerializedProperty heightCurveProp;
    private SerializedProperty orientTowardsTargetProp;
    private SerializedProperty spinItemProp;
    private SerializedProperty itemSelfSpinMultiplierProp;
    private SerializedProperty shrinkOnAbsorbProp;
    private SerializedProperty scaleCurveProp;
    private SerializedProperty fadeAlphaProp;
    private SerializedProperty alphaCurveProp;
    private SerializedProperty endActionProp;
    private SerializedProperty restoreStateOnExitIfCancelledProp;
    private SerializedProperty applyInEditModeProp;

    private bool showDirectSlots = false;
    private bool showAdditionalSources = false;
    private bool showVisualSettings = true;
    private bool showMotionSettings = true;

    private void OnEnable()
    {
        motionTypeProp = serializedObject.FindProperty("motionType");
        speedModeProp = serializedObject.FindProperty("speedMode");
        speedMultiplierProp = serializedObject.FindProperty("speedMultiplier");
        fixedSpeedProp = serializedObject.FindProperty("fixedSpeed");
        fixedDurationProp = serializedObject.FindProperty("fixedDuration");
        speedVariationProp = serializedObject.FindProperty("speedVariation");

        targetPointAProp = serializedObject.FindProperty("targetPointA");
        targetOffsetProp = serializedObject.FindProperty("targetOffset");
        itemTargetsProp = serializedObject.FindProperty("itemTargets");
        itemsContainerParentProp = serializedObject.FindProperty("itemsContainerParent");
        autoFindItemsInRadiusProp = serializedObject.FindProperty("autoFindItemsInRadius");
        searchRadiusProp = serializedObject.FindProperty("searchRadius");
        searchLayerMaskProp = serializedObject.FindProperty("searchLayerMask");
        searchTagFilterProp = serializedObject.FindProperty("searchTagFilter");
        autoDetectGamePickupsProp = serializedObject.FindProperty("autoDetectGamePickups");
        staggerSpreadProp = serializedObject.FindProperty("staggerSpread");
        disperseAnglesProp = serializedObject.FindProperty("disperseAngles");
        randomnessVariationProp = serializedObject.FindProperty("randomnessVariation");
        suctionCurveProp = serializedObject.FindProperty("suctionCurve");
        planeProp = serializedObject.FindProperty("plane");
        spinDirectionProp = serializedObject.FindProperty("spinDirection");
        spiralRotationsProp = serializedObject.FindProperty("spiralRotations");
        initialRadiusOverrideProp = serializedObject.FindProperty("initialRadiusOverride");
        spiralRadiusCurveProp = serializedObject.FindProperty("spiralRadiusCurve");
        heightArcProp = serializedObject.FindProperty("heightArc");
        heightCurveProp = serializedObject.FindProperty("heightCurve");
        orientTowardsTargetProp = serializedObject.FindProperty("orientTowardsTarget");
        spinItemProp = serializedObject.FindProperty("spinItem");
        itemSelfSpinMultiplierProp = serializedObject.FindProperty("itemSelfSpinMultiplier");
        shrinkOnAbsorbProp = serializedObject.FindProperty("shrinkOnAbsorb");
        scaleCurveProp = serializedObject.FindProperty("scaleCurve");
        fadeAlphaProp = serializedObject.FindProperty("fadeAlpha");
        alphaCurveProp = serializedObject.FindProperty("alphaCurve");
        endActionProp = serializedObject.FindProperty("endAction");
        restoreStateOnExitIfCancelledProp = serializedObject.FindProperty("restoreStateOnExitIfCancelled");
        applyInEditModeProp = serializedObject.FindProperty("applyInEditMode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. MOTION TYPE & SPEED
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("CHẾ ĐỘ & TỐC ĐỘ HÚT", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(motionTypeProp, new GUIContent("Kiểu chuyển động"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(speedModeProp, new GUIContent("Chế độ tính tốc độ"));

        TimelineSpeedMode currentSpeedMode = (TimelineSpeedMode)speedModeProp.enumValueIndex;
        if (currentSpeedMode == TimelineSpeedMode.FixedSpeed)
        {
            EditorGUILayout.PropertyField(fixedSpeedProp, new GUIContent("Tốc độ hút (m/s)"));
            EditorGUILayout.PropertyField(speedMultiplierProp, new GUIContent("Hệ số nhân tốc độ"));
        }
        else if (currentSpeedMode == TimelineSpeedMode.FixedDuration)
        {
            EditorGUILayout.PropertyField(fixedDurationProp, new GUIContent("Thời gian hút (giây)"));
            EditorGUILayout.PropertyField(speedMultiplierProp, new GUIContent("Hệ số nhân tốc độ"));
        }
        else
        {
            EditorGUILayout.PropertyField(speedMultiplierProp, new GUIContent("Hệ số tốc độ (Speed Mult)"));
        }

        EditorGUILayout.PropertyField(speedVariationProp, new GUIContent("Độ lệch tốc độ giữa các vật"));

        EditorGUILayout.Space(6f);

        // 2. TARGET POINT A
        EditorGUILayout.LabelField("ĐIỂM ĐÍCH (ĐIỂM A)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetPointAProp, new GUIContent("Target Point A (Tâm hút)"));
        EditorGUILayout.PropertyField(targetOffsetProp, new GUIContent("Target Offset"));

        EditorGUILayout.Space(8f);

        // 3. MULTI-ITEM DRAG & DROP ZONE
        EditorGUILayout.LabelField("VẬT PHẨM CẦN HÚT (HỖ TRỢ KÉO NHIỀU OBJECT)", EditorStyles.boldLabel);
        DrawMultiItemDragDropBox();

        EditorGUILayout.Space(4f);
        DrawSelectionAddButton();

        EditorGUILayout.Space(4f);
        DrawItemTargetsList();

        EditorGUILayout.Space(6f);

        // 4. DIRECT SLOTS (SLOTS NHANH 1 - 10)
        showDirectSlots = EditorGUILayout.Foldout(showDirectSlots, $"Các Slot Kéo Thả Riêng Lẻ (Slots 1 - 10)", true);
        if (showDirectSlots)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemTarget"), new GUIContent("Vật phẩm 1"));
            for (int i = 2; i <= 10; i++)
            {
                SerializedProperty slotProp = serializedObject.FindProperty($"itemTarget{i}");
                if (slotProp != null)
                {
                    EditorGUILayout.PropertyField(slotProp, new GUIContent($"Vật phẩm {i}"));
                }
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6f);

        // 5. ADDITIONAL SOURCES (CONTAINER & RADIUS SEARCH)
        showAdditionalSources = EditorGUILayout.Foldout(showAdditionalSources, "Nguồn Phụ (GameObject Cha & Quét Bán Kính)", true);
        if (showAdditionalSources)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(itemsContainerParentProp, new GUIContent("Items Container Parent"));
            EditorGUILayout.PropertyField(autoFindItemsInRadiusProp, new GUIContent("Tự động quét trong bán kính"));
            if (autoFindItemsInRadiusProp.boolValue)
            {
                EditorGUILayout.PropertyField(searchRadiusProp, new GUIContent("Bán kính quét (m)"));
                EditorGUILayout.PropertyField(searchLayerMaskProp, new GUIContent("LayerMask lọc"));
                EditorGUILayout.PropertyField(searchTagFilterProp, new GUIContent("Tag lọc"));
                EditorGUILayout.PropertyField(autoDetectGamePickupsProp, new GUIContent("Tự nhận diện Drops / Pickups"));
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8f);

        // 6. MULTI-ITEM DYNAMICS & MOTION SETTINGS
        showMotionSettings = EditorGUILayout.Foldout(showMotionSettings, "Thông Số Chuyển Động & Gia Tốc", true);
        if (showMotionSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(staggerSpreadProp, new GUIContent("Stagger Spread (Hút so le)"));
            EditorGUILayout.PropertyField(randomnessVariationProp, new GUIContent("Độ lệch ngẫu nhiên"));
            EditorGUILayout.PropertyField(suctionCurveProp, new GUIContent("Suction Curve (Đường cong gia tốc)"));
            EditorGUILayout.PropertyField(planeProp, new GUIContent("Mặt phẳng chuyển động"));

            VortexMotionType currentMotion = (VortexMotionType)motionTypeProp.enumValueIndex;
            if (currentMotion == VortexMotionType.StraightLine)
            {
                EditorGUILayout.PropertyField(orientTowardsTargetProp, new GUIContent("Xoay đầu về Điểm A"));
                EditorGUILayout.PropertyField(heightArcProp, new GUIContent("Độ võng đường thẳng (Height Arc)"));
                if (heightArcProp.floatValue != 0f)
                {
                    EditorGUILayout.PropertyField(heightCurveProp, new GUIContent("Height Curve"));
                }
            }
            else if (currentMotion == VortexMotionType.ArcCurve)
            {
                EditorGUILayout.PropertyField(heightArcProp, new GUIContent("Độ cao vòng cung (Arc Height)"));
                EditorGUILayout.PropertyField(heightCurveProp, new GUIContent("Arc Curve"));
            }
            else // VortexSpiral
            {
                EditorGUILayout.PropertyField(disperseAnglesProp, new GUIContent("Phân bổ góc lốc xoáy"));
                EditorGUILayout.PropertyField(spinDirectionProp, new GUIContent("Chiều quay lốc"));
                EditorGUILayout.PropertyField(spiralRotationsProp, new GUIContent("Số vòng xoay"));
                EditorGUILayout.PropertyField(initialRadiusOverrideProp, new GUIContent("Ghi đè bán kính lốc"));
                EditorGUILayout.PropertyField(spiralRadiusCurveProp, new GUIContent("Đường cong bán kính"));
                EditorGUILayout.PropertyField(heightArcProp, new GUIContent("Độ nhấc bổng phễu lốc"));
                EditorGUILayout.PropertyField(heightCurveProp, new GUIContent("Height Curve"));
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8f);

        // 7. VISUAL & COMPLETION
        showVisualSettings = EditorGUILayout.Foldout(showVisualSettings, "Hiệu Ứng Hình Ảnh & Kết Thúc", true);
        if (showVisualSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spinItemProp, new GUIContent("Tự xoay vật phẩm (Spin Item)"));
            if (spinItemProp.boolValue)
            {
                EditorGUILayout.PropertyField(itemSelfSpinMultiplierProp, new GUIContent("Tốc độ tự xoay"));
            }

            EditorGUILayout.PropertyField(shrinkOnAbsorbProp, new GUIContent("Thu nhỏ khi vào tâm (Shrink)"));
            if (shrinkOnAbsorbProp.boolValue)
            {
                EditorGUILayout.PropertyField(scaleCurveProp, new GUIContent("Scale Curve"));
            }

            EditorGUILayout.PropertyField(fadeAlphaProp, new GUIContent("Làm mờ Alpha khi vào tâm"));
            if (fadeAlphaProp.boolValue)
            {
                EditorGUILayout.PropertyField(alphaCurveProp, new GUIContent("Alpha Curve"));
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(endActionProp, new GUIContent("Hành động khi hoàn tất"));
            EditorGUILayout.PropertyField(restoreStateOnExitIfCancelledProp, new GUIContent("Khôi phục khi hủy"));
            EditorGUILayout.PropertyField(applyInEditModeProp, new GUIContent("Xem trước trong Editor"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMultiItemDragDropBox()
    {
        Event currentEvent = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0f, 45f, GUILayout.ExpandWidth(true));

        GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };

        GUI.Box(dropArea, "⬇ KÉO & THẢ NHIỀU OBJECT TỪ HIERARCHY VÀO ĐÂY ⬇", boxStyle);

        switch (currentEvent.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(currentEvent.mousePosition))
                    break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    PlayableDirector director = TimelineEditor.inspectedDirector;
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        Transform t = null;
                        if (draggedObject is GameObject go) t = go.transform;
                        else if (draggedObject is Component comp) t = comp.transform;

                        if (t != null)
                        {
                            AddTransformToItemTargets(t, director);
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                    currentEvent.Use();
                }
                break;
        }
    }

    private void DrawSelectionAddButton()
    {
        EditorGUILayout.BeginHorizontal();

        int selectedCount = Selection.transforms != null ? Selection.transforms.Length : 0;
        GUIContent btnContent = new GUIContent($"➕ Thêm {selectedCount} Object đang chọn trong Hierarchy");

        using (new EditorGUI.DisabledScope(selectedCount == 0))
        {
            if (GUILayout.Button(btnContent, GUILayout.Height(26f)))
            {
                PlayableDirector director = TimelineEditor.inspectedDirector;
                foreach (Transform t in Selection.transforms)
                {
                    if (t != null)
                    {
                        AddTransformToItemTargets(t, director);
                    }
                }
                serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
            }
        }

        if (itemTargetsProp.arraySize > 0)
        {
            if (GUILayout.Button("🗑 Xóa danh sách", GUILayout.Width(110f), GUILayout.Height(26f)))
            {
                itemTargetsProp.ClearArray();
                serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawItemTargetsList()
    {
        int count = itemTargetsProp.arraySize;
        if (count == 0)
        {
            EditorGUILayout.HelpBox("Chưa có vật phẩm nào trong danh sách. Hãy kéo thả hoặc bấm nút thêm ở trên.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField($"Danh sách vật phẩm đã gán ({count} items):", EditorStyles.miniBoldLabel);

        PlayableDirector director = TimelineEditor.inspectedDirector;

        for (int i = 0; i < count; i++)
        {
            SerializedProperty elementProp = itemTargetsProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = elementProp.FindPropertyRelative("exposedName");

            Transform boundObj = null;
            if (director != null && !string.IsNullOrEmpty(nameProp.stringValue))
            {
                boundObj = director.GetReferenceValue(nameProp.stringValue, out bool idValid) as Transform;
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(elementProp, new GUIContent($"Item {i + 1}"), true);

            if (GUILayout.Button("✕", GUILayout.Width(24f), GUILayout.Height(18f)))
            {
                itemTargetsProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
                break;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void AddTransformToItemTargets(Transform targetTransform, PlayableDirector director)
    {
        if (targetTransform == null) return;

        string key = $"ItemRef_{targetTransform.name}_{System.Guid.NewGuid():N}";

        int newIndex = itemTargetsProp.arraySize;
        itemTargetsProp.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newElement = itemTargetsProp.GetArrayElementAtIndex(newIndex);

        SerializedProperty nameProp = newElement.FindPropertyRelative("exposedName");
        nameProp.stringValue = key;

        if (director != null)
        {
            director.SetReferenceValue(key, targetTransform);
            EditorUtility.SetDirty(director);
        }
    }
}
