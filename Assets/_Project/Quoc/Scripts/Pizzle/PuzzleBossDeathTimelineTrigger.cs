using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Quoc.PuzzleQuest;
using Bao.Bosses;

namespace Quoc.Enemy
{
    public class PuzzleBossDeathTimelineTrigger : MonoBehaviour
    {
        [Header("Cutscene Timeline Config")]
        [SerializeField] private PlayableDirector cutsceneTimeline;
        [SerializeField] private PlayableDirector previousBossTimeline;
        [SerializeField] private float delayBeforePlay = 0f;
        [SerializeField] private bool autoEnableTimelineObject = true;
        [SerializeField] private bool oneShot = true;

        private bool isCutscenePlayed = false;

        private void Awake()
        {
            if (cutsceneTimeline == null)
            {
                cutsceneTimeline = GetComponent<PlayableDirector>();
                if (cutsceneTimeline == null)
                {
                    cutsceneTimeline = GetComponentInChildren<PlayableDirector>(true);
                }
            }

            if (cutsceneTimeline != null)
            {
                cutsceneTimeline.playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            PuzzleQuestController.OnPuzzleBossDefeated += HandleBossDefeated;
            LithosMonolithBoss.OnGlobalLithosBossDied += HandleBossDefeated;

            if (PuzzleQuestController.IsPuzzleBossDefeated)
            {
                StartCoroutine(DelayedCheckBossDefeated());
            }
        }

        private void OnDisable()
        {
            PuzzleQuestController.OnPuzzleBossDefeated -= HandleBossDefeated;
            LithosMonolithBoss.OnGlobalLithosBossDied -= HandleBossDefeated;
        }

        private IEnumerator DelayedCheckBossDefeated()
        {
            yield return null;
            if (PuzzleQuestController.IsPuzzleBossDefeated && (!oneShot || !isCutscenePlayed))
            {
                HandleBossDefeated();
            }
        }

        private void HandleBossDefeated()
        {
            if (oneShot && isCutscenePlayed) return;

            if (cutsceneTimeline == null)
            {
                Debug.LogWarning($"[PuzzleBossDeathTimelineTrigger] Chưa gán PlayableDirector trên GameObject '{name}'.", this);
                return;
            }

            isCutscenePlayed = true;

            // Dừng timeline Boss trước đó nếu đang chạy để tránh đè lên BossDie
            StopPreviousBossTimeline();

            if (delayBeforePlay > 0f)
            {
                StartCoroutine(PlayCutsceneRoutine());
            }
            else
            {
                PlayTimeline();
            }
        }

        private void StopPreviousBossTimeline()
        {
            if (previousBossTimeline != null && previousBossTimeline.state == PlayState.Playing)
            {
                previousBossTimeline.Stop();
            }
            else
            {
                // Tự tìm TimelineBoss trong Scene nếu chưa gán
                GameObject bossTimelineObj = GameObject.Find("TimelineBoss");
                if (bossTimelineObj != null)
                {
                    PlayableDirector director = bossTimelineObj.GetComponent<PlayableDirector>();
                    if (director != null && director.state == PlayState.Playing)
                    {
                        director.Stop();
                    }
                }
            }
        }

        private IEnumerator PlayCutsceneRoutine()
        {
            yield return new WaitForSeconds(delayBeforePlay);
            PlayTimeline();
        }

        private void PlayTimeline()
        {
            if (cutsceneTimeline == null) return;

            if (autoEnableTimelineObject && !cutsceneTimeline.gameObject.activeSelf)
            {
                cutsceneTimeline.gameObject.SetActive(true);
            }

            cutsceneTimeline.time = 0;
            cutsceneTimeline.Play();
        }
    }
}
