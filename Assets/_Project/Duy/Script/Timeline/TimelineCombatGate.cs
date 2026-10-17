using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TimelineCombatGate : MonoBehaviour
{
    [System.Serializable]
    public struct EnemySpawnInfo
    {
        public GameObject prefab;
        public Transform spawnPoint;
    }

    [Header("Controller & Health")]
    [SerializeField] private TimelinePlayController playController;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Enemy Setup")]
    [SerializeField] private GameObject[] sceneEnemies;
    [SerializeField] private EnemySpawnInfo[] enemySpawns;

    [Header("Encounter Flow")]
    [Tooltip("Pause the Timeline when the encounter begins. Turn this off to let the Timeline keep playing while the player fights.")]
    [SerializeField] private bool pauseTimelineOnBegin = true;

    private readonly List<GameObject> activeTrackedEnemies = new List<GameObject>();
    private bool isEncounterActive;
    private bool encounterFailed;
    private bool didPauseTimeline;

    private void Awake()
    {
        if (playController == null)
        {
            playController = GetComponent<TimelinePlayController>();
            if (playController == null)
            {
                playController = GetComponentInParent<TimelinePlayController>();
            }
        }
    }

    private void OnEnable()
    {
        BindPlayerHealth();
    }

    private void OnDisable()
    {
        UnbindPlayerHealth();
    }

    private void Start()
    {
        if (playerHealth == null)
        {
            BindPlayerHealth();
        }
    }

    private void Update()
    {
        if (!isEncounterActive)
        {
            return;
        }

        if (AreAllEnemiesDefeated())
        {
            CompleteEncounter();
        }
    }

    public void BeginEncounter()
    {
        StartEncounter(pauseTimelineOnBegin);
    }

    /// <summary>
    /// Starts the encounter and hands control to the player without pausing the Timeline.
    /// Use this from a Signal Receiver or an appear effect callback when the cutscene should keep running.
    /// </summary>
    public void BeginEncounterWithoutPause()
    {
        StartEncounter(false);
    }

    private void StartEncounter(bool pauseTimeline)
    {
        if (playController == null)
        {
            playController = GetComponentInParent<TimelinePlayController>();
        }

        isEncounterActive = true;
        encounterFailed = false;

        activeTrackedEnemies.Clear();

        if (sceneEnemies != null)
        {
            for (int i = 0; i < sceneEnemies.Length; i++)
            {
                GameObject enemy = sceneEnemies[i];
                if (enemy != null)
                {
                    enemy.SetActive(true);
                    activeTrackedEnemies.Add(enemy);
                }
            }
        }

        if (enemySpawns != null)
        {
            for (int i = 0; i < enemySpawns.Length; i++)
            {
                EnemySpawnInfo info = enemySpawns[i];
                if (info.prefab != null)
                {
                    Vector3 pos = info.spawnPoint != null ? info.spawnPoint.position : transform.position;
                    Quaternion rot = info.spawnPoint != null ? info.spawnPoint.rotation : Quaternion.identity;
                    GameObject spawned = Instantiate(info.prefab, pos, rot);
                    activeTrackedEnemies.Add(spawned);
                }
            }
        }

        didPauseTimeline = false;

        if (playController != null)
        {
            if (pauseTimeline)
            {
                playController.PauseTimeline();
                didPauseTimeline = true;
            }

            playController.UnlockPlayerInput();
        }
    }

    private bool AreAllEnemiesDefeated()
    {
        for (int i = activeTrackedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemyObj = activeTrackedEnemies[i];
            if (enemyObj == null || !enemyObj.activeInHierarchy)
            {
                continue;
            }

            IEnemyTarget target = enemyObj.GetComponentInParent<IEnemyTarget>();
            if (target != null && !target.IsAlive)
            {
                continue;
            }

            TimelineNpcDamageReceiver damageReceiver = enemyObj.GetComponentInParent<TimelineNpcDamageReceiver>();
            if (damageReceiver != null && damageReceiver.IsDead)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void CompleteEncounter()
    {
        isEncounterActive = false;

        if (playController != null)
        {
            playController.LockPlayerInput();

            if (didPauseTimeline)
            {
                playController.ResumeTimeline();
            }
        }

        didPauseTimeline = false;
        activeTrackedEnemies.Clear();
    }

    private void HandlePlayerDied()
    {
        if (!isEncounterActive)
        {
            return;
        }

        isEncounterActive = false;
        encounterFailed = true;
        didPauseTimeline = false;

        if (playController != null)
        {
            playController.PauseTimeline();
            playController.ResetTrigger();
            TimelinePlayStateManager.ResetTimeline(playController.TimelineId);
        }

        ClearActiveEnemies();
    }

    private void HandlePlayerRespawned()
    {
        if (!encounterFailed)
        {
            return;
        }

        encounterFailed = false;

        if (playController != null)
        {
            playController.ForcePlayTimeline();
        }
    }

    private void ClearActiveEnemies()
    {
        if (sceneEnemies != null)
        {
            for (int i = 0; i < sceneEnemies.Length; i++)
            {
                if (sceneEnemies[i] != null)
                {
                    sceneEnemies[i].SetActive(false);
                }
            }
        }

        for (int i = 0; i < activeTrackedEnemies.Count; i++)
        {
            GameObject enemy = activeTrackedEnemies[i];
            if (enemy != null && (sceneEnemies == null || System.Array.IndexOf(sceneEnemies, enemy) < 0))
            {
                Destroy(enemy);
            }
        }

        activeTrackedEnemies.Clear();
    }

    private void BindPlayerHealth()
    {
        if (playerHealth == null)
        {
            Player player = TimelineTrigger2D.CurrentPlayer;
            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }
            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }
        }

        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
            playerHealth.OnDied += HandlePlayerDied;
            playerHealth.OnRespawned -= HandlePlayerRespawned;
            playerHealth.OnRespawned += HandlePlayerRespawned;
        }
    }

    private void UnbindPlayerHealth()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
            playerHealth.OnRespawned -= HandlePlayerRespawned;
        }
    }
}
