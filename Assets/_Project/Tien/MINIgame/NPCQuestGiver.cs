using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Tien.Minigames;

public class NPCQuestGiver : MonoBehaviour
{
    public enum NPCState
    {
        Idle_WaitingForTrigger,
        Cutscene_SpiritSpeaks,
        Cutscene_PanToNPC,
        RunningToPlayer_1,
        Idle_WaitingForHelp,
        WaitingForMonsters,
        RunningToPlayer_2,
        Idle_GivingQuest,
        WaitingForCameraPan,
        RunningToLumberyard,
        Cutscene_PanToLumberyard,
        WaitingForLumberyardMonsters,
        Idle_WaitingForWoodPickup,
        RunningToBridge,
        Idle_ReadyToRepair,
        RunningAway,
        QuestCompleted
    }

    [Header("Cài đặt NPC")]
    public NPCState currentState = NPCState.Idle_WaitingForTrigger;
    public float moveSpeed = 7f;
    public float stopDistance = 2f;
    public float activationRadius = 8f;

    [Header("Đối tượng liên quan")]
    public Transform playerTransform;
    public Transform bridgeTransform;

    [Tooltip("Vị trí xưởng gỗ mà NPC sẽ chạy tới")]
    public Transform lumberyardTransform;

    public Transform exitTransform;

    [Tooltip("Dấu chấm than trên đầu NPC")]
    public GameObject exclamationMark;
    [Tooltip("Dấu chấm than trên đầu Tinh Linh")]
    public GameObject spiritExclamationMark;
    [Tooltip("Minigame Sửa Cầu (MỚI)")]
    public BridgeRepairMinigame puzzleManager;

    [Header("Cài đặt Nhiệm vụ")]
    public int woodRequired = 3;

    [Header("UI Cài đặt")]
    public string questDescription = "Tiêu diệt quái và lấy gỗ";

    [Header("Ảnh đại diện hội thoại")]
    public Sprite playerAvatar;
    public Sprite spiritAvatar;
    public Sprite npcAvatar;

    [Header("Hội thoại")]
    public UnityEvent OnRequestHelpDialogue;
    public UnityEvent OnGiveQuestDialogue;
    public UnityEvent OnQuestCompleteDialogue;

    [Header("Camera (Cinemachine)")]
    public GameObject bridgeVirtualCamera;
    public GameObject npcVirtualCamera;
    public GameObject lumberyardVirtualCamera;
    public float cameraPanDuration = 2f;

    [Header("Danh sách Quái vật")]
    public List<GameObject> monstersToDefeat;
    public List<GameObject> lumberyardMonsters;
    public float lumberyardSpawnRadius = 15f;

    [Header("Hoạt ảnh (Spine)")]
    public AnimationSpine animationSpine;
    public AnimationSpine.State idleAnimState = AnimationSpine.State.Idle;
    public AnimationSpine.State walkAnimState = AnimationSpine.State.Run;
    public AnimationSpine.State jumpAnimState = AnimationSpine.State.Jump;
    public AnimationSpine.State fallAnimState = AnimationSpine.State.Fall;

    [Header("Nhảy qua Vật cản")]
    public float jumpForce = 12f;
    public float obstacleDetectDistance = 1.2f;
    public LayerMask obstacleLayer;
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;

    [Header("Khoảng cách chờ")]
    public float maxPlayerDistance = 15f;

    public static NPCQuestGiver Instance { get; private set; }

    private Rigidbody2D rb;
    private NPCQuestStateMachine stateMachine;
    private NPCQuestMovementController movementController;
    private NPCQuestDialogueDirector dialogueDirector;
    private NPCQuestObjectiveTracker objectiveTracker;
    private NPCQuestInteractionSensor interactionSensor;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animationSpine == null)
        {
            animationSpine = GetComponentInChildren<AnimationSpine>();
        }

        ResolvePlayer();
        SetInitialVisualState();

        movementController = new NPCQuestMovementController(this, rb, animationSpine);
        objectiveTracker = new NPCQuestObjectiveTracker(this);
        interactionSensor = new NPCQuestInteractionSensor(this);
        dialogueDirector = new NPCQuestDialogueDirector(this);
        stateMachine = new NPCQuestStateMachine(this, movementController, dialogueDirector);

        interactionSensor.SetupCollision();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        stateMachine?.Tick();
        movementController?.UpdateSpineAnimation();
    }

    public bool IsLumberyardCleared()
    {
        if (objectiveTracker != null)
        {
            return objectiveTracker.IsLumberyardCleared();
        }

        return AreMonstersDefeatedFallback(lumberyardMonsters);
    }

    public void TriggerPanCameraToBridge(float duration = 2f)
    {
        if (dialogueDirector == null)
        {
            dialogueDirector = new NPCQuestDialogueDirector(this);
        }

        StartCoroutine(dialogueDirector.JustPanCameraToBridge(duration));
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void SetInitialVisualState()
    {
        if (exclamationMark != null)
        {
            exclamationMark.SetActive(false);
        }

        if (spiritExclamationMark != null)
        {
            spiritExclamationMark.SetActive(false);
        }

        if (bridgeVirtualCamera != null)
        {
            bridgeVirtualCamera.SetActive(false);
        }

        if (npcVirtualCamera != null)
        {
            npcVirtualCamera.SetActive(false);
        }

        if (lumberyardVirtualCamera != null)
        {
            lumberyardVirtualCamera.SetActive(false);
        }
    }

    private bool AreMonstersDefeatedFallback(List<GameObject> monsters)
    {
        if (monsters == null || monsters.Count == 0)
        {
            return true;
        }

        foreach (GameObject monster in monsters)
        {
            if (monster != null)
            {
                return false;
            }
        }

        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        interactionSensor?.SetPlayerNear(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        interactionSensor?.SetPlayerNear(other, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        interactionSensor?.SetPlayerNear(other, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        interactionSensor?.SetPlayerNear(other, false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        interactionSensor?.SetPlayerNear(collision.collider, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        interactionSensor?.SetPlayerNear(collision.collider, false);
    }
}
