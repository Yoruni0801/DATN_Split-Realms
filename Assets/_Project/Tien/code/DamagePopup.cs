using UnityEngine;
using TMPro;
using DG.Tweening; // 👉 GỌI DOTWEEN RA LÀM VIỆC

public class DamagePopup : MonoBehaviour
{
    private TMP_Text textMesh;

    private void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    // Thêm tham số isPlayerDamage (mặc định = false để không ảnh hưởng code cũ)
    public void Setup(float damageAmount, bool isCrit, bool isPlayerDamage = false)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
        
        // 1. CHỈNH MÀU SẮC VÀ KÍCH THƯỚC
        if (isPlayerDamage)
        {
            textMesh.color = Color.red; // Sát thương Player ăn đòn (Màu đỏ xót xa)
            textMesh.fontSize += 1f;
        }
        else if (isCrit)
        {
            textMesh.color = new Color(1f, 0.5f, 0f); // Chí mạng lên quái (Màu Cam/Vàng)
            textMesh.fontSize += 3f; // Phóng to chữ hơn
        }
        else
        {
            textMesh.color = Color.white; // Quái ăn đòn bình thường (Trắng)
        }

        // 2. KỊCH BẢN HOẠT ẢNH BẰNG DOTWEEN (Animation Sequence)
        Vector3 originalScale = transform.localScale;
        transform.localScale = Vector3.zero; // Ép về 0 để chuẩn bị phóng to

        // Bay lên cao và lệch sang ngang một chút
        float randomX = Random.Range(-1f, 1f);
        transform.DOMove(transform.position + new Vector3(randomX, 2f, 0f), 0.8f).SetEase(Ease.OutCubic);

        // Tạo một chuỗi hành động (Sequence)
        Sequence seq = DOTween.Sequence();
        
        // Hiện ra nảy to lên (Bouncy)
        seq.Append(transform.DOScale(originalScale * 1.5f, 0.2f).SetEase(Ease.OutBack));
        // Thu lại kích thước gốc
        seq.Append(transform.DOScale(originalScale, 0.1f));
        // Đứng im cho người chơi kịp đọc số (0.3 giây)
        seq.AppendInterval(0.3f);
        // Mờ dần Alpha về 0
        seq.Append(textMesh.DOFade(0f, 0.2f));
        
        // Xoay xong thì tự hủy Object để giải phóng RAM
        seq.OnComplete(() => Destroy(gameObject));
    }
}

public static class DamagePopupSystem
{
    private const string DefaultEnemyPopupResourcePath = "DamagePopup/EnemyDamagePopup";
    private const string DefaultPlayerPopupResourcePath = "DamagePopup/PlayerDamagePopup";
    private static GameObject defaultEnemyPopupPrefab;
    private static GameObject defaultPlayerPopupPrefab;

    public static void ShowEnemy(Vector3 position, float damage, bool isCrit = false)
    {
        Show(GetDefaultEnemyPopupPrefab(), position, damage, isCrit);
    }

    public static void ShowPlayer(Vector3 position, float damage)
    {
        Show(GetDefaultPlayerPopupPrefab(), position, damage, isPlayerDamage: true);
    }

    public static void Show(GameObject popupPrefab, Vector3 position, float damage, bool isCrit = false, bool isPlayerDamage = false)
    {
        if (damage <= 0f)
        {
            return;
        }

        if (popupPrefab == null)
        {
            return;
        }

        GameObject popup = Object.Instantiate(popupPrefab, position, Quaternion.identity);
        if (isPlayerDamage && popup.TryGetComponent(out PlayerDamagePopup playerPopup))
        {
            playerPopup.Setup(Mathf.RoundToInt(damage));
            return;
        }

        if (popup.TryGetComponent(out DamagePopup popupScript))
        {
            popupScript.Setup(damage, isCrit, isPlayerDamage);
            return;
        }

        if (popup.TryGetComponent(out PlayerDamagePopup fallbackPlayerPopup))
        {
            fallbackPlayerPopup.Setup(Mathf.RoundToInt(damage));
        }
    }

    private static GameObject GetDefaultEnemyPopupPrefab()
    {
        if (defaultEnemyPopupPrefab == null)
        {
            defaultEnemyPopupPrefab = Resources.Load<GameObject>(DefaultEnemyPopupResourcePath);
        }

        return defaultEnemyPopupPrefab;
    }

    private static GameObject GetDefaultPlayerPopupPrefab()
    {
        if (defaultPlayerPopupPrefab == null)
        {
            defaultPlayerPopupPrefab = Resources.Load<GameObject>(DefaultPlayerPopupResourcePath);
        }

        return defaultPlayerPopupPrefab;
    }
}

public static class DamagePopupSpawner
{
    public static void ShowEnemyDamage(GameObject popupPrefabOverride, Vector3 position, float damage, bool isCrit = false)
    {
        if (popupPrefabOverride != null)
        {
            DamagePopupSystem.Show(popupPrefabOverride, position, damage, isCrit);
            return;
        }

        DamagePopupSystem.ShowEnemy(position, damage, isCrit);
    }
}
