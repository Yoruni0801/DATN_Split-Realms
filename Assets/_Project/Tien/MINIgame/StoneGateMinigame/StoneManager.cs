using UnityEngine;

namespace Tien.Minigames
{
    public class StoneManager : MonoBehaviour
    {
        public static StoneManager Instance { get; private set; }

        [Header("Thông tin Game")]
        public int stoneCount = 0;
        public bool isGateSolved = false;
        
        [Header("Tiến trình")]
        [Tooltip("Đã nhận nhiệm vụ từ NPC chưa?")]
        public bool isQuestStarted = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartQuest()
        {
            isQuestStarted = true;
            Debug.Log("Nhiệm vụ mở cổng đá đã bắt đầu!");
        }

        public void AddStone()
        {
            if (!isQuestStarted) 
            {
                Debug.Log("Chưa nhận nhiệm vụ, chưa thể nhặt đá.");
                return; // Không cho nhặt nếu chưa nhận quest
            }
            
            stoneCount++;
            Debug.Log($"Đã nhặt 1 khối đá! Tổng: {stoneCount}");
        }

        public bool UseStones(int amount)
        {
            if (stoneCount >= amount)
            {
                stoneCount -= amount;
                Debug.Log($"Đã dùng {amount} khối đá! Còn: {stoneCount}");
                return true;
            }
            return false;
        }
    }
}
