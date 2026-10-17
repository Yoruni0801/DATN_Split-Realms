using System.Collections.Generic;
using UnityEngine;

namespace Quoc.QuyVuong.Phase1
{
    public class QuyVuongWaterCage : MonoBehaviour
    {
        private static readonly List<QuyVuongWaterCage> activeCages = new List<QuyVuongWaterCage>();

        [Header("Settings")]
        [SerializeField] private float duration = 2.5f;

        private Transform targetPlayer;
        private IPlayerInputLock inputLock;
        private float timer = 0f;
        private bool isInitialized = false;
        private bool isReleased = false;

        public static bool HasActiveCageFor(Transform player)
        {
            if (player == null) return false;
            for (int i = activeCages.Count - 1; i >= 0; i--)
            {
                if (activeCages[i] == null)
                {
                    activeCages.RemoveAt(i);
                    continue;
                }
                if (activeCages[i].targetPlayer == player && !activeCages[i].isReleased)
                {
                    return true;
                }
            }
            return false;
        }

        public static void ClearAll()
        {
            for (int i = activeCages.Count - 1; i >= 0; i--)
            {
                if (activeCages[i] != null)
                {
                    activeCages[i].ReleaseAndDestroy();
                }
            }
            activeCages.Clear();
        }

        public void Initialize(Transform player, IPlayerInputLock playerInputLock, float cageDuration)
        {
            targetPlayer = player;
            inputLock = playerInputLock;
            duration = cageDuration;
            timer = 0f;
            isInitialized = true;
            isReleased = false;

            if (!activeCages.Contains(this))
            {
                activeCages.Add(this);
            }

            inputLock?.SetInputLocked(true);
        }

        private void Update()
        {
            if (!isInitialized || isReleased) return;

            if (targetPlayer == null)
            {
                ReleaseAndDestroy();
                return;
            }

            timer += Time.deltaTime;
            if (timer >= duration)
            {
                ReleaseAndDestroy();
            }
        }

        public void ReleaseAndDestroy()
        {
            if (isReleased) return;
            isReleased = true;

            if (inputLock != null)
            {
                inputLock.SetInputLocked(false);
            }
            else if (targetPlayer != null)
            {
                var lockComp = targetPlayer.GetComponent<IPlayerInputLock>()
                    ?? targetPlayer.GetComponentInChildren<IPlayerInputLock>()
                    ?? targetPlayer.GetComponentInParent<IPlayerInputLock>();
                lockComp?.SetInputLocked(false);
            }

            if (activeCages.Contains(this))
            {
                activeCages.Remove(this);
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!isReleased)
            {
                isReleased = true;
                if (inputLock != null)
                {
                    inputLock.SetInputLocked(false);
                }
                else if (targetPlayer != null)
                {
                    var lockComp = targetPlayer.GetComponent<IPlayerInputLock>()
                        ?? targetPlayer.GetComponentInChildren<IPlayerInputLock>()
                        ?? targetPlayer.GetComponentInParent<IPlayerInputLock>();
                    lockComp?.SetInputLocked(false);
                }
            }

            activeCages.Remove(this);
        }
    }
}
