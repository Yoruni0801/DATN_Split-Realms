using UnityEngine;

public class CrystalBounceTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasTriggered) return;

        // Kiểm tra xem vật va chạm có phải là quả cầu tuyết (chứa IceRockRoller) không
        if (collision.gameObject.GetComponent<IceRockRoller>() != null)
        {
            hasTriggered = true;
            TriggerBounce();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        if (other.gameObject.GetComponent<IceRockRoller>() != null)
        {
            hasTriggered = true;
            TriggerBounce();
        }
    }

    private void TriggerBounce()
    {
        if (SnowballDialogueManager.Instance != null)
        {
            SnowballDialogueManager.Instance.TriggerBounceDialogue();
        }
        else
        {
            Debug.LogWarning("[CrystalBounceTrigger] Không tìm thấy SnowballDialogueManager.Instance trong Scene!");
        }
    }
}
