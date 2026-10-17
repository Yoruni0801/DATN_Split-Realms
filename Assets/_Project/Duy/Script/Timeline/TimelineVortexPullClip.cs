using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum VortexMotionType
{
    StraightLine,       // Hút thẳng trực tiếp vào Điểm A
    VortexSpiral,       // Hút xoắn ốc theo hình lốc xoáy
    ArcCurve            // Hút theo đường cong vòng cung (Parabol)
}

public enum TimelineSpeedMode
{
    FitToClipDuration,  // Tốc độ co giãn vừa khít theo độ dài của Clip trên Timeline
    FixedSpeed,         // Tốc độ hút cố định theo mét/giây (Units/s)
    FixedDuration       // Thời gian hút hoàn thành cố định theo số giây
}

public enum VortexPlane
{
    XY_2D,
    XZ_3D,
    YZ
}

public enum VortexSpinDirection
{
    CounterClockwise,
    Clockwise
}

public enum TimelineVortexEndAction
{
    None,
    DeactivateItem,
    DestroyItem,
    ParentToTarget,
    CollectPickupIfApplicable
}

public enum VortexItemSourceMode
{
    DirectReferences,       // Kéo thả danh sách vật phẩm hoặc 1 vật phẩm trực tiếp
    ChildrenOfContainer,    // Hút toàn bộ vật phẩm con trong GameObject cha
    RadiusSearch,           // Tự động quét và hút tất cả vật phẩm trong bán kính
    AllCombined             // Kết hợp tất cả các nguồn trên
}

[Serializable]
public sealed class TimelineVortexPullClip : PlayableAsset, ITimelineClipAsset
{
    [Header("=== TRAJECTORY / MOTION TYPE (KIỂU CHUYỂN ĐỘNG) ===")]
    [Tooltip("Kiểu chuyển động hút: StraightLine (Đường thẳng), VortexSpiral (Lốc xoáy xoắn ốc), hoặc ArcCurve (Đường cong vòng cung).")]
    public VortexMotionType motionType = VortexMotionType.StraightLine;

    [Header("=== SPEED & TIMING (TỐC ĐỘ HÚT) ===")]
    [Tooltip("Chế độ tính tốc độ: FitToClipDuration (theo độ dài clip), FixedSpeed (theo mét/giây), FixedDuration (theo số giây cố định).")]
    public TimelineSpeedMode speedMode = TimelineSpeedMode.FitToClipDuration;

    [Tooltip("Hệ số nhân tốc độ hút (1 = bình thường, 2 = nhanh gấp đôi, 3 = nhanh gấp 3, 0.5 = chậm một nửa).")]
    [Min(0.01f)] public float speedMultiplier = 1f;

    [Tooltip("Tốc độ hút cố định (đơn vị: mét/giây) khi chọn chế độ FixedSpeed.")]
    [Min(0.1f)] public float fixedSpeed = 15f;

    [Tooltip("Thời gian hút hoàn thành (giây) khi chọn chế độ FixedDuration.")]
    [Min(0.05f)] public float fixedDuration = 1.2f;

    [Tooltip("Độ chênh lệch tốc độ bay ngẫu nhiên giữa các vật phẩm (để các vật bay nhanh chậm so le tự nhiên).")]
    [Range(0f, 1f)] public float speedVariation = 0.2f;

    [Header("=== TARGET POINT A (TÂM HÚT) ===")]
    [Tooltip("Điểm A - Điểm đích cần hút vào (Kéo Transform/GameObject đích trong Scene vào đây). Nếu để trống, sẽ dùng Transform được Bind trên Track.")]
    public ExposedReference<Transform> targetPointA;

    [Tooltip("Độ lệch vị trí tâm điểm A (Offset so với vị trí của Target Point A).")]
    public Vector3 targetOffset = Vector3.zero;

    [Header("=== DANH SÁCH NHIỀU VẬT PHẨM (KÉO NHIỀU OBJECT VÀO ĐÂY) ===")]
    [Tooltip("Danh sách động nhiều vật phẩm cần hút cùng lúc.")]
    public List<ExposedReference<Transform>> itemTargets = new List<ExposedReference<Transform>>();

    [Header("=== CÁC Ô KÉO THẢ TRỰC TIẾP (SLOTS NHANH 1 - 10) ===")]
    [Tooltip("Vật phẩm 1 (Kéo trực tiếp Transform vào đây).")]
    public ExposedReference<Transform> itemTarget;
    [Tooltip("Vật phẩm 2")] public ExposedReference<Transform> itemTarget2;
    [Tooltip("Vật phẩm 3")] public ExposedReference<Transform> itemTarget3;
    [Tooltip("Vật phẩm 4")] public ExposedReference<Transform> itemTarget4;
    [Tooltip("Vật phẩm 5")] public ExposedReference<Transform> itemTarget5;
    [Tooltip("Vật phẩm 6")] public ExposedReference<Transform> itemTarget6;
    [Tooltip("Vật phẩm 7")] public ExposedReference<Transform> itemTarget7;
    [Tooltip("Vật phẩm 8")] public ExposedReference<Transform> itemTarget8;
    [Tooltip("Vật phẩm 9")] public ExposedReference<Transform> itemTarget9;
    [Tooltip("Vật phẩm 10")] public ExposedReference<Transform> itemTarget10;

    [Header("=== NGUỒN PHỤ (CONTAINER & RADIUS SEARCH) ===")]
    [Tooltip("GameObject cha chứa các vật phẩm (Nếu gán, script sẽ tự động hút TẤT CẢ các vật phẩm con bên trong nó).")]
    public ExposedReference<Transform> itemsContainerParent;

    [Tooltip("Tự động quét thêm các vật phẩm trong bán kính.")]
    public bool autoFindItemsInRadius = false;

    [Tooltip("Bán kính tự động tìm vật phẩm quanh Điểm A.")]
    [Min(0f)] public float searchRadius = 8f;

    [Tooltip("LayerMask lọc vật phẩm (Layer của Items/Drops). Mặc định quét mọi Layer.")]
    public LayerMask searchLayerMask = ~0;

    [Tooltip("Tag lọc vật phẩm (Ví dụ: 'Item', 'Pickup', 'DropItem'). Để trống nếu không lọc theo tag.")]
    public string searchTagFilter = "";

    [Tooltip("Tự động nhận diện các component nhặt đồ của game (CrystalPickupItem, NidusDeathDropItem) trong scene.")]
    public bool autoDetectGamePickups = true;

    [Header("=== MULTI-ITEM DYNAMICS (CHUYỂN ĐỘNG NHIỀU VẬT) ===")]
    [Tooltip("Độ lệch thời gian hút giữa các vật phẩm (0: cùng bay 1 lúc; >0: các vật bị hút so le cuốn theo sau như một luồng tia năng lượng).")]
    [Range(0f, 0.8f)] public float staggerSpread = 0.2f;

    [Tooltip("Tự động phân bổ góc ban đầu so le cho lốc xoáy (chỉ áp dụng khi chọn VortexSpiral).")]
    public bool disperseAngles = true;

    [Tooltip("Độ biến thiên ngẫu nhiên nhẹ về bán kính giữa các vật phẩm.")]
    [Range(0f, 0.5f)] public float randomnessVariation = 0.1f;

    [Header("=== STRAIGHT / VORTEX CURVES & SETTINGS ===")]
    [Tooltip("Đường cong gia tốc hút dồn về tâm (0: bắt đầu, 1: chạm tâm A). Mặc định EaseIn/EaseInOut tăng tốc khi lao vào tâm.")]
    public AnimationCurve suctionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Mặt phẳng diễn ra chuyển động (XY_2D cho game 2D/2.5D, XZ_3D cho game 3D top-down).")]
    public VortexPlane plane = VortexPlane.XY_2D;

    [Tooltip("Chiều quay lốc xoáy (chỉ dùng cho VortexSpiral).")]
    public VortexSpinDirection spinDirection = VortexSpinDirection.CounterClockwise;

    [Tooltip("Số vòng xoay trôn ốc khi hút về tâm (chỉ dùng cho VortexSpiral).")]
    [Min(0f)] public float spiralRotations = 3.5f;

    [Tooltip("Ghi đè bán kính lốc ban đầu (chỉ dùng cho VortexSpiral).")]
    [Min(0f)] public float initialRadiusOverride = 0f;

    [Tooltip("Đường cong thu hẹp bán kính lốc (chỉ dùng cho VortexSpiral).")]
    public AnimationCurve spiralRadiusCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Độ võng / nâng bổng vòng cung (0 = đường thẳng tuyệt đối; >0 = bay cong vòng cung hình parabol).")]
    public float heightArc = 0f;

    [Tooltip("Đường cong biến thiên độ cao/võng trong quá trình bay.")]
    public AnimationCurve heightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3.14f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -3.14f, 0f)
    );

    [Header("=== ITEM VISUAL TRANSFORMATIONS ===")]
    [Tooltip("Tự động xoay hướng đầu của vật phẩm hướng về Điểm A khi bay theo đường thẳng.")]
    public bool orientTowardsTarget = false;

    [Tooltip("Tự xoay tròn vật phẩm quanh trục của nó khi bay về tâm.")]
    public bool spinItem = true;

    [Tooltip("Hệ số nhân tốc độ tự xoay của vật phẩm.")]
    [Min(0f)] public float itemSelfSpinMultiplier = 3f;

    [Tooltip("Thu nhỏ dần kích thước (Scale) vật phẩm khi bị hút vào tâm.")]
    public bool shrinkOnAbsorb = true;

    [Tooltip("Đường cong thu nhỏ scale (1 -> 0).")]
    public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Làm mờ dần độ trong suốt (Alpha) của SpriteRenderer / CanvasGroup khi vào tâm.")]
    public bool fadeAlpha = false;

    [Tooltip("Đường cong làm mờ alpha (1 -> 0).")]
    public AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("=== COMPLETION & PLAYBACK ===")]
    [Tooltip("Hành động thực hiện với từng vật phẩm khi clip kết thúc / hút vào tâm thành công.")]
    public TimelineVortexEndAction endAction = TimelineVortexEndAction.DeactivateItem;

    [Tooltip("Khôi phục trạng thái ban đầu của vật phẩm nếu clip bị dừng đột ngột hoặc tua ngược.")]
    public bool restoreStateOnExitIfCancelled = true;

    [Tooltip("Cho phép hiển thị và tua xem trước trực tiếp trong Unity Editor mà không cần bấm Play.")]
    public bool applyInEditMode = true;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<TimelineVortexPullBehaviour> playable =
            ScriptPlayable<TimelineVortexPullBehaviour>.Create(graph);

        TimelineVortexPullBehaviour b = playable.GetBehaviour();
        IExposedPropertyTable resolver = graph.GetResolver();

        b.motionType = motionType;
        b.speedMode = speedMode;
        b.speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        b.fixedSpeed = Mathf.Max(0.1f, fixedSpeed);
        b.fixedDuration = Mathf.Max(0.05f, fixedDuration);
        b.speedVariation = Mathf.Clamp01(speedVariation);

        b.targetPointA = targetPointA.Resolve(resolver);

        // Thu thập toàn bộ các vật phẩm từ các ô kéo thả
        b.itemTargetList = new List<Transform>();

        void TryAdd(ExposedReference<Transform> itemRef)
        {
            Transform t = itemRef.Resolve(resolver);
            if (t != null && !b.itemTargetList.Contains(t))
            {
                b.itemTargetList.Add(t);
            }
        }

        // 1. Ô chính
        TryAdd(itemTarget);

        // 2. Các slot nhanh 2..10
        TryAdd(itemTarget2);
        TryAdd(itemTarget3);
        TryAdd(itemTarget4);
        TryAdd(itemTarget5);
        TryAdd(itemTarget6);
        TryAdd(itemTarget7);
        TryAdd(itemTarget8);
        TryAdd(itemTarget9);
        TryAdd(itemTarget10);

        // 3. Danh sách mảng động itemTargets
        if (itemTargets != null && itemTargets.Count > 0)
        {
            for (int i = 0; i < itemTargets.Count; i++)
            {
                TryAdd(itemTargets[i]);
            }
        }

        // Resolve container parent
        b.itemsContainerParent = itemsContainerParent.Resolve(resolver);

        b.autoFindItemsInRadius = autoFindItemsInRadius;
        b.searchRadius = Mathf.Max(0f, searchRadius);
        b.searchLayerMask = searchLayerMask;
        b.searchTagFilter = searchTagFilter;
        b.autoDetectGamePickups = autoDetectGamePickups;

        b.staggerSpread = Mathf.Clamp(staggerSpread, 0f, 0.8f);
        b.disperseAngles = disperseAngles;
        b.randomnessVariation = Mathf.Clamp(randomnessVariation, 0f, 0.5f);

        b.targetOffset = targetOffset;
        b.plane = plane;
        b.spinDirection = spinDirection;
        b.spiralRotations = Mathf.Max(0f, spiralRotations);
        b.initialRadiusOverride = Mathf.Max(0f, initialRadiusOverride);
        b.suctionCurve = suctionCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        b.spiralRadiusCurve = spiralRadiusCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0f);
        b.heightArc = heightArc;
        b.heightCurve = heightCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 0f);
        b.orientTowardsTarget = orientTowardsTarget;
        b.spinItem = spinItem;
        b.itemSelfSpinMultiplier = Mathf.Max(0f, itemSelfSpinMultiplier);
        b.shrinkOnAbsorb = shrinkOnAbsorb;
        b.scaleCurve = scaleCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0f);
        b.fadeAlpha = fadeAlpha;
        b.alphaCurve = alphaCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 0f);
        b.endAction = endAction;
        b.restoreStateOnExitIfCancelled = restoreStateOnExitIfCancelled;
        b.applyInEditMode = applyInEditMode;

        return playable;
    }
}
