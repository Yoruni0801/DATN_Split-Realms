using UnityEngine;

namespace Bao.Bosses.QuyVuongPhase4
{
    /// <summary>
    /// Cọc gai Magma Spire cắm trên sàn đấu, tạo vùng làm chậm và chờ sóng thần kích nổ
    /// </summary>
    public class Phase4MagmaSpire : MonoBehaviour
    {
        private float slowRadius = 1.5f;
        private float slowAmount = 0.2f;
        private GameObject auraObj;

        public void Initialize(float radius, float slow, GameObject auraPrefab)
        {
            slowRadius = radius;
            slowAmount = slow;

            if (auraPrefab != null)
            {
                auraObj = Instantiate(auraPrefab, transform.position, Quaternion.identity, transform);
            }
        }

        private void OnDestroy()
        {
            if (auraObj != null)
            {
                Destroy(auraObj);
            }
        }
    }
}
