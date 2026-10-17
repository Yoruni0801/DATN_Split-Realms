using UnityEngine;

/// <summary>
/// Component đánh dấu để KeepObjectOnLoad bỏ qua PlayableDirector này,
/// không ẩn UI HUD khi Timeline này đang chạy (dùng cho VFX, Animation môi trường, bẫy, v.v.).
/// </summary>
[DisallowMultipleComponent]
public class IgnoreHUDHide : MonoBehaviour
{
}
