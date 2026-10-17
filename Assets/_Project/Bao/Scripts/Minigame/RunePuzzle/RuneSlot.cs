using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Minigame.RunePuzzle
{
    [RequireComponent(typeof(Image))]
    public class RuneSlot : MonoBehaviour, IDropHandler
    {
        public string expectedRuneId;
        private RuneStone currentStone;
        private Image slotImage;

        private void Awake()
        {
            slotImage = GetComponent<Image>();
        }

        public void UpdateVisualSprite(Sprite newSprite)
        {
            if (slotImage != null && newSprite != null)
            {
                slotImage.sprite = newSprite;
                slotImage.color = Color.white;
            }
        }

        public void TransitionToSprite(Sprite targetSprite)
        {
            StartCoroutine(TransitionEffect(targetSprite));
        }

        private System.Collections.IEnumerator TransitionEffect(Sprite targetSprite)
        {
            if (slotImage == null) yield break;

            // 1. POP UP & GLOW FLASH
            float elapsed = 0f;
            Vector3 originalScale = transform.localScale;
            Color originalColor = slotImage.color;
            
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.15f;
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease Out
                transform.localScale = Vector3.Lerp(originalScale, originalScale * 1.4f, ease);
                slotImage.color = Color.Lerp(originalColor, new Color(3f, 3f, 3f, 1f), ease); 
                yield return null;
            }

            // Đổi hình dạng
            if (targetSprite != null)
            {
                slotImage.sprite = targetSprite;
            }

            // 2. SMASH DOWN (Tạo cảm giác khảm mạnh)
            elapsed = 0f;
            while (elapsed < 0.1f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.1f;
                transform.localScale = Vector3.Lerp(originalScale * 1.4f, originalScale * 0.9f, t);
                yield return null;
            }

            // 3. REBOUND (Nảy nhẹ) & Giảm sáng
            elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.2f;
                float ease = 1f - Mathf.Pow(1f - t, 3f); // Ease Out Cubic
                transform.localScale = Vector3.Lerp(originalScale * 0.9f, originalScale, ease);
                slotImage.color = Color.Lerp(new Color(3f, 3f, 3f, 1f), Color.white, ease);
                yield return null;
            }

            transform.localScale = originalScale;
            slotImage.color = Color.white;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!RunePuzzleManager.Instance.CanDragRunes()) return;

            GameObject droppedObj = eventData.pointerDrag;
            if (droppedObj != null)
            {
                RuneStone stone = droppedObj.GetComponent<RuneStone>();
                if (stone != null)
                {
                    ReceiveStone(stone);
                }
            }
        }

        public void ReceiveStone(RuneStone stone)
        {
            if (currentStone == null)
            {
                Debug.Log($"<color=yellow>👉 Đã thả {stone.name} (ID: {stone.runeId}) vào lõm {gameObject.name} (Chờ ID: {expectedRuneId})</color>");
                
                currentStone = stone;
                stone.SetCurrentSlot(this); // Let RuneStone handle the snapping
                RunePuzzleManager.Instance.OnStonePlacedInSlot(this, stone);
            }
            else
            {
                Debug.Log($"<color=red>❌ Lõm {gameObject.name} đã có đá rồi!</color>");
            }
        }

        public bool HasStone()
        {
            return currentStone != null;
        }

        public bool IsCorrectRunePlaced()
        {
            if (currentStone == null) return false;
            return currentStone.runeId == expectedRuneId;
        }

        public void RemoveStone()
        {
            currentStone = null;
        }
    }
}
