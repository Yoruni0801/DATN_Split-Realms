using UnityEngine;

public class AutoDestroyEffect : MonoBehaviour
{
    [Tooltip("Thời gian tồn tại của hiệu ứng trước khi bị xóa (giây)")]
    public float lifeTime = 2f;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}