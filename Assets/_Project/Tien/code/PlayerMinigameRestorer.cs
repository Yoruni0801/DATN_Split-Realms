using UnityEngine;

namespace Tien.Minigame
{
    public class PlayerMinigameRestorer : MonoBehaviour
    {
        private void Start()
        {
            if (MinigameState.IsReturningFromMinigame)
            {
                transform.position = MinigameState.PlayerMapPosition;
                MinigameState.IsReturningFromMinigame = false;
            }
        }
    }
}
