using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Quoc.PuzzleQuest
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Project Quoc/Puzzle Quest/Puzzle Quest Controller")]
    public class PuzzleQuestController : MonoBehaviour
    {
        public static event Action OnPuzzleBossDefeated;
        public static bool IsPuzzleBossDefeated { get; set; } = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsPuzzleBossDefeated = false;
            OnPuzzleBossDefeated = null;
        }

        [Header("Events")]
        [SerializeField] private UnityEvent onBossDefeated;

        [Header("External Balance (optional)")]
        [Tooltip("When assigned, values in this asset override Inline Tuning.")]
        [SerializeField] private PuzzleQuestSettings settingsAsset;
        [SerializeField] private PuzzleQuestTuning inlineTuning = new PuzzleQuestTuning();

        [Header("Fairy Prefabs")]
        [HideInInspector][SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject waterFairyPrefab;
        [SerializeField] private GameObject fireFairyPrefab;
        [SerializeField] private GameObject windFairyPrefab;
        [HideInInspector][SerializeField] private GameObject enemyPrefab;

        [Header("Skill Visual Effects (optional)")]
        [SerializeField] private GameObject swordSkillVfxPrefab;
        [SerializeField] private GameObject waterSkillVfxPrefab;
        [SerializeField] private GameObject fireSkillVfxPrefab;
        [SerializeField] private GameObject windSkillVfxPrefab;
        [SerializeField] private PuzzleQuestActorLayout actorLayout = new PuzzleQuestActorLayout();

        [Header("Scene Battle Actors")]
        [SerializeField] internal bool useSceneBattleActors = true;
        [SerializeField] internal Transform playerSceneActor;
        [SerializeField] internal Transform enemySceneActor;
        [SerializeField] internal Transform playerPointA;
        [SerializeField] internal Transform playerPointB;
        [SerializeField] internal Transform enemyPointA;
        [SerializeField] internal Transform enemyPointB;

        [Header("Theme Assets")]
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private Sprite frameSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite swordIcon;
        [SerializeField] private Sprite waterIcon;
        [SerializeField] private Sprite fireIcon;
        [SerializeField] private Sprite windIcon;
        [SerializeField] private GameObject matchEffectPrefab;
        [SerializeField] private GameObject comboEffectPrefab;
        [SerializeField][Min(1)] private int comboEffectThreshold = 3;

        [Header("Audio")]
        [SerializeField] private AudioClip swapSound;
        [SerializeField] private AudioClip matchSound;
        [SerializeField] private AudioClip comboSound;
        [SerializeField] private AudioClip invalidSound;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip enemyAttackSound;
        [SerializeField] private AudioClip enemyHurtSound;
        [SerializeField] private AudioClip playerHurtSound;
        [SerializeField] private AudioClip victorySound;
        [Range(0f, 1f)][SerializeField] private float audioVolume = 0.75f;

        [Header("Scene UI (editable after Build Missing UI)")]
        [SerializeField] private bool buildMissingUiOnOpen = true;
        [SerializeField] private Canvas sceneCanvas;
        [SerializeField] private RectTransform boardGrid;
        [SerializeField] private RawImage battleRender;
        [SerializeField] private Image battleFlash;
        [SerializeField] private TMP_Text actionBanner;
        [SerializeField] private Slider playerHealthFill;
        [SerializeField] private TMP_Text playerHealthText;
        [SerializeField] private Slider enemyHealthFill;
        [SerializeField] private TMP_Text enemyHealthText;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text statusText;
        [Header("Dual Turn Panels (Optional)")]
        [SerializeField] private GameObject playerTurnPanel;
        [SerializeField] private GameObject enemyTurnPanel;
        [SerializeField] private TMP_Text enemyTurnText;
        [SerializeField] private TMP_Text enemyComboText;
        [SerializeField] private TMP_Text enemyStatusText;
        [SerializeField] private Material playerTurnMaterial;
        [SerializeField] private Material enemyTurnMaterial;
        [SerializeField][Min(0.1f)] private float turnMaterialDuration = 1.0f;
        [SerializeField] private RectTransform endOverlay;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultSubtitle;
        [SerializeField] private Button restartButton;
        [Header("Scene Return")]
        [SerializeField] private bool returnToPreviousTimelineSceneOnCompleted = true;
        [SerializeField] private float returnSceneDelay = 2.0f;
        [Header("Boss VFX Settings - Phase 1 (Bình Thường)")]
        [SerializeField] private PuzzleQuestBossSkillVfxSet phase1BossVfx = new PuzzleQuestBossSkillVfxSet();

        [Header("Boss VFX Settings - Phase 2 (Nâng Cấp - Thức Tỉnh)")]
        [SerializeField] private PuzzleQuestBossSkillVfxSet phase2BossVfx = new PuzzleQuestBossSkillVfxSet();

        [Header("Enemy AI & Combat Settings")]
        [SerializeField] private bool enemyOnlyMatch3 = false;
        [SerializeField] private bool enemyAttackMovesForward = true;

        private readonly Dictionary<Vector2Int, PuzzleQuestGemView> gemViews =
            new Dictionary<Vector2Int, PuzzleQuestGemView>();

        private PuzzleQuestBoardModel board;
        private PuzzleQuestActorPresenter actorPresenter;
        private AudioSource audioSource;
        private PuzzleQuestGemView selectedGem;
        private PuzzleQuestGemView selectionBeforePointerDown;
        private Vector2Int enemyPlannedMoveFrom;
        private Vector2Int enemyPlannedMoveTo;
        private bool lastEnemyMoveGrantedExtraTurn;
        private bool inputLocked = true;
        private bool gameEnded;
        private int playerHealth;
        private int enemyHealth;
        private int enemyMaxHealth;
        private int enemyShield;
        private bool isEnemyPhase2;
        private int enemyTurnCount;
        private Vector2 lastBoardSize;
        private Coroutine initializeCoroutine;
        private EventSystem runtimeEventSystem;
        private InputActionAsset runtimeUiActions;
        private bool createdRuntimeEventSystem;
        private bool runtimeStarted;

        internal Canvas SceneCanvas { get { return sceneCanvas; } }
        internal TMP_FontAsset UiFont { get { return uiFont; } }
        internal Sprite FrameSprite { get { return frameSprite; } }
        internal Sprite PanelSprite { get { return panelSprite; } }

        public PuzzleQuestTuning Tuning
        {
            get
            {
                if (settingsAsset != null && settingsAsset.tuning != null)
                {
                    return settingsAsset.tuning;
                }

                if (inlineTuning == null)
                {
                    inlineTuning = new PuzzleQuestTuning();
                }

                return inlineTuning;
            }
        }

        protected virtual void OnEnable()
        {
            if (Application.isPlaying)
            {
                BeginRuntimeInitialization();
            }
            else if (buildMissingUiOnOpen)
            {
                QueueEditorUiBuild();
            }
        }

        protected virtual void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            BeginRuntimeInitialization();
        }

        protected virtual void LateUpdate()
        {
            if (!Application.isPlaying || boardGrid == null || board == null || inputLocked)
            {
                return;
            }

            Vector2 currentSize = boardGrid.rect.size;
            if ((currentSize - lastBoardSize).sqrMagnitude > 1f)
            {
                LayoutAllGems();
            }
        }

        protected virtual void OnDestroy()
        {
            ResetRuntimeSession();
        }

        protected virtual void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= EditorBuildUiIfNeeded;
#endif
            ResetRuntimeSession();
        }

        protected virtual void OnValidate()
        {
            if (inlineTuning == null)
            {
                inlineTuning = new PuzzleQuestTuning();
            }

            inlineTuning.ClampValues();
            audioVolume = Mathf.Clamp01(audioVolume);

            if (actorLayout != null)
            {
                actorLayout.enemyNormalAttackMovesToPointB = enemyAttackMovesForward;
            }
        }

        [ContextMenu("Build Missing Puzzle Quest UI")]
        public void BuildMissingUi()
        {
            PuzzleQuestUIFactory.BuildMissing(this);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.F1))
            {
                StartCoroutine(DebugTriggerRockBarrage());
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.F2))
            {
                StartCoroutine(DebugTriggerOrbitAoe());
            }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.F3))
            {
                StartCoroutine(DebugTriggerPetrify());
            }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.F4))
            {
                StartCoroutine(DebugTriggerShield());
            }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.F5))
            {
                StartCoroutine(DebugTriggerPhase2());
            }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.F6))
            {
                DamageEnemy(150);
            }
#endif
        }

        [ContextMenu("Debug: Trigger Rock Barrage")]
        public void ContextTriggerRockBarrage() { if (Application.isPlaying) StartCoroutine(DebugTriggerRockBarrage()); }

        [ContextMenu("Debug: Trigger Orbit AOE")]
        public void ContextTriggerOrbitAoe() { if (Application.isPlaying) StartCoroutine(DebugTriggerOrbitAoe()); }

        [ContextMenu("Debug: Trigger Petrify")]
        public void ContextTriggerPetrify() { if (Application.isPlaying) StartCoroutine(DebugTriggerPetrify()); }

        [ContextMenu("Debug: Trigger Stone Shield")]
        public void ContextTriggerShield() { if (Application.isPlaying) StartCoroutine(DebugTriggerShield()); }

        [ContextMenu("Debug: Trigger Phase 2")]
        public void ContextTriggerPhase2() { if (Application.isPlaying) StartCoroutine(DebugTriggerPhase2()); }

        private IEnumerator DebugTriggerRockBarrage()
        {
            if (actorPresenter == null) yield break;
            SetBanner("TEST: BÃO ĐÁ LIÊN HOÀN");
            SetStatus("Debug kích hoạt Rock Barrage!");
            yield return actorPresenter.PlayEnemyRockBarrage(6, 0.3f, delegate
            {
                PlayOneShot(enemyAttackSound);
                DamagePlayer(5);
            }, 0.3f);
        }

        private IEnumerator DebugTriggerOrbitAoe()
        {
            if (actorPresenter == null) yield break;
            SetBanner("TEST: VÀNH ĐAI CỔ THẠCH");
            SetStatus("Debug kích hoạt Orbit AOE Storm!");
            yield return actorPresenter.PlayEnemyOrbitAoe(0.4f, delegate
            {
                PlayOneShot(enemyAttackSound);
                DamagePlayer(25);
            }, 0.4f);
        }

        private IEnumerator DebugTriggerPetrify()
        {
            if (actorPresenter == null) yield break;
            SetBanner("TEST: ĐỊA CHẤN HÓA ĐÁ");
            SetStatus("Debug kích hoạt Hóa Đá 3 ô ngọc!");
            yield return actorPresenter.PlayEnemyEarthquake(0.35f, delegate
            {
                PlayOneShot(enemyAttackSound);
                PetrifyRandomGems(3);
            }, 0.35f);
        }

        private IEnumerator DebugTriggerShield()
        {
            if (actorPresenter == null) yield break;
            SetBanner("TEST: KHIÊN CỔ THẠCH");
            SetStatus("Debug kích hoạt Khiên Đá +60!");
            yield return actorPresenter.PlayEnemyShield(0.8f, delegate
            {
                enemyShield += 60;
                UpdateHealthUi();
            });
        }

        private IEnumerator DebugTriggerPhase2()
        {
            if (actorPresenter == null) yield break;
            isEnemyPhase2 = true;
            SetBanner("TEST: THỨC TỈNH CỔ ĐẠI (PHASE 2)");
            SetStatus("Debug kích hoạt chuyển Phase 2!");
            yield return actorPresenter.PlayEnemyPhaseTransition(delegate
            {
                enemyShield += 60;
                UpdateHealthUi();
            });
        }

        public Color GetGemColor(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Water:
                    return Tuning.water.color;
                case PuzzleGemType.Fire:
                    return Tuning.fire.color;
                case PuzzleGemType.Wind:
                    return Tuning.wind.color;
                default:
                    return new Color(0.92f, 0.73f, 0.32f, 1f);
            }
        }

        public Sprite GetGemIcon(PuzzleGemType type)
        {
            switch (type)
            {
                case PuzzleGemType.Water:
                    return waterIcon;
                case PuzzleGemType.Fire:
                    return fireIcon;
                case PuzzleGemType.Wind:
                    return windIcon;
                default:
                    return swordIcon;
            }
        }

        public void OnGemPointerDown(PuzzleQuestGemView gem)
        {
            if (inputLocked || gameEnded || gem == null)
            {
                return;
            }

            selectionBeforePointerDown = selectedGem;
            SelectGem(gem);
        }

        public void OnGemPointerUp(PuzzleQuestGemView gem, Vector2 dragDelta)
        {
            if (inputLocked || gameEnded || gem == null || board == null)
            {
                return;
            }

            Vector2Int target = gem.Cell;
            const float dragThreshold = 24f;
            if (dragDelta.sqrMagnitude >= dragThreshold * dragThreshold)
            {
                if (Mathf.Abs(dragDelta.x) >= Mathf.Abs(dragDelta.y))
                {
                    target.x += dragDelta.x >= 0f ? 1 : -1;
                }
                else
                {
                    target.y += dragDelta.y >= 0f ? 1 : -1;
                }
            }
            else if (selectionBeforePointerDown != null
                     && selectionBeforePointerDown != gem
                     && board.AreAdjacent(selectionBeforePointerDown.Cell, gem.Cell))
            {
                target = selectionBeforePointerDown.Cell;
            }
            else
            {
                return;
            }

            if (!board.AreAdjacent(gem.Cell, target))
            {
                PlayOneShot(invalidSound);
                SetStatus("Hãy chọn một ô kế bên.");
                return;
            }

            StartCoroutine(SwapAndResolve(gem.Cell, target));
        }

        internal void SetSceneCanvas(Canvas canvas)
        {
            sceneCanvas = canvas;
        }

        internal void AssignSceneUi(
            RectTransform assignedBoardGrid,
            RawImage assignedBattleRender,
            Image assignedBattleFlash,
            TMP_Text assignedActionBanner,
            Slider assignedPlayerHealthFill,
            TMP_Text assignedPlayerHealthText,
            Slider assignedEnemyHealthFill,
            TMP_Text assignedEnemyHealthText,
            TMP_Text assignedTurnText,
            TMP_Text assignedComboText,
            TMP_Text assignedStatusText,
            RectTransform assignedEndOverlay,
            TMP_Text assignedResultTitle,
            TMP_Text assignedResultSubtitle,
            Button assignedRestartButton)
        {
            boardGrid = assignedBoardGrid;
            battleRender = assignedBattleRender;
            battleFlash = assignedBattleFlash;
            actionBanner = assignedActionBanner;
            playerHealthFill = assignedPlayerHealthFill;
            playerHealthText = assignedPlayerHealthText;
            enemyHealthFill = assignedEnemyHealthFill;
            enemyHealthText = assignedEnemyHealthText;
            turnText = assignedTurnText;
            comboText = assignedComboText;
            statusText = assignedStatusText;
            endOverlay = assignedEndOverlay;
            resultTitle = assignedResultTitle;
            resultSubtitle = assignedResultSubtitle;
            restartButton = assignedRestartButton;
        }

        private IEnumerator InitializeGame()
        {
            runtimeStarted = true;
            inputLocked = true;
            gameEnded = false;
            ClearSelection();

            if (actorPresenter != null)
            {
                actorPresenter.Dispose();
                actorPresenter = null;
            }

            Tuning.ClampValues();

            if (buildMissingUiOnOpen)
            {
                BuildMissingUi();
            }

            EnsureEventSystem();

            if (boardGrid == null)
            {
                Debug.LogError("Puzzle Quest could not find its Board Grid.", this);
                yield break;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartScene);
                restartButton.onClick.AddListener(RestartScene);
            }

            if (endOverlay != null)
            {
                endOverlay.gameObject.SetActive(false);
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            int seed = Tuning.randomSeed;
            board = new PuzzleQuestBoardModel(
                Tuning.columns,
                Tuning.rows,
                Tuning.minimumMatch,
                seed);
            board.CreatePlayableBoard();

            playerHealth = Tuning.playerMaxHealth;
            enemyMaxHealth = Tuning.enemyMaxHealth;
            enemyHealth = enemyMaxHealth;
            enemyShield = 0;
            isEnemyPhase2 = false;
            enemyTurnCount = 0;
            gameEnded = false;

            SyncActorLayoutFromSceneActors();
            DisableAuthoredSceneActors();
            DisableSceneBattleActors();

            Canvas.ForceUpdateCanvases();
            CreateBoardViews();

            if (actorLayout != null)
            {
                actorLayout.enemyNormalAttackMovesToPointB = enemyAttackMovesForward;
            }

            actorPresenter = GetComponent<PuzzleQuestActorPresenter>();
            if (actorPresenter == null)
            {
                actorPresenter = gameObject.AddComponent<PuzzleQuestActorPresenter>();
            }

            actorPresenter.Initialize(
                battleRender,
                ResolvePlayerActorSource(),
                waterFairyPrefab,
                fireFairyPrefab,
                windFairyPrefab,
                ResolveEnemyActorSource(),
                actorLayout,
                swordSkillVfxPrefab,
                waterSkillVfxPrefab,
                fireSkillVfxPrefab,
                windSkillVfxPrefab,
                phase1BossVfx,
                phase2BossVfx);

            if (actorPresenter != null && actorPresenter.EnemyMaxHealth > 0)
            {
                enemyMaxHealth = actorPresenter.EnemyMaxHealth;
                enemyHealth = enemyMaxHealth;
            }

            UpdateHealthUi();

            SetBanner("PUZZLE QUEST");
            SetStatus("Kéo một ngọc sang ô kế bên.");
            SetTurn("LƯỢT CỦA BẠN");
            SetCombo(1);
            inputLocked = false;
            yield return null;
        }

        private IEnumerator SwapAndResolve(Vector2Int first, Vector2Int second)
        {
            if (inputLocked || board == null)
            {
                yield break;
            }

            inputLocked = true;
            ClearSelection();

            PuzzleQuestGemView firstView = gemViews[first];
            PuzzleQuestGemView secondView = gemViews[second];
            yield return AnimateTwoViews(
                firstView,
                GetCellPosition(second),
                secondView,
                GetCellPosition(first),
                Tuning.swapDuration);

            board.Swap(first, second);
            SwapViewCells(first, second);
            PlayOneShot(swapSound);

            PuzzleQuestMatchResult matches = board.FindMatches();
            if (!matches.HasMatches)
            {
                board.Swap(first, second);

                firstView = gemViews[first];
                secondView = gemViews[second];
                yield return AnimateTwoViews(
                    firstView,
                    GetCellPosition(second),
                    secondView,
                    GetCellPosition(first),
                    Tuning.invalidSwapDuration);
                SwapViewCells(first, second);

                PlayOneShot(invalidSound);
                SetStatus("Nước kéo này không tạo được hàng ghép.");
                inputLocked = false;
                yield break;
            }

            bool grantsExtraTurn = false;
            int cascade = 1;

            while (matches.HasMatches && !gameEnded)
            {
                SetCombo(cascade);
                if (Tuning.extraTurnMatchLength > 0)
                {
                    for (int i = 0; i < matches.Summaries.Count; i++)
                    {
                        if (matches.Summaries[i].LongestRun >= Tuning.extraTurnMatchLength)
                        {
                            grantsExtraTurn = true;
                            break;
                        }
                    }
                }

                if (cascade >= comboEffectThreshold && comboSound != null)
                {
                    PlayOneShot(comboSound);
                }
                else
                {
                    PlayOneShot(matchSound);
                }
                yield return PopMatches(matches.MatchedCells, cascade);
                List<PuzzleQuestBoardMove> moves = board.CollapseAndRefill(matches.MatchedCells);
                yield return AnimateCollapseAndRefill(matches.MatchedCells, moves);
                yield return ExecutePlayerActions(matches.Summaries, cascade);

                if (enemyHealth <= 0)
                {
                    yield return EndGame(true);
                    yield break;
                }

                matches = board.FindMatches();
                cascade++;
            }

            if (!board.HasPossibleSwap())
            {
                yield return ReshuffleBoard();
            }

            if (grantsExtraTurn)
            {
                SetBanner("THÊM LƯỢT");
                SetTurn("LƯỢT CỦA BẠN");
                SetStatus("Ghép từ 4 ô trở lên được thêm một lượt.");
            }
            else
            {
                yield return ExecuteEnemyTurn();
                if (playerHealth <= 0)
                {
                    yield return EndGame(false);
                    yield break;
                }
            }

            SetCombo(1);
            inputLocked = false;
        }

        private IEnumerator ExecutePlayerActions(
            List<PuzzleQuestMatchSummary> summaries,
            int cascade)
        {
            float multiplier = 1f + Mathf.Max(0, cascade - 1) * Tuning.cascadeDamageBonus;

            for (int i = 0; i < summaries.Count; i++)
            {
                PuzzleQuestMatchSummary summary = summaries[i];
                if (summary.Type == PuzzleGemType.Sword)
                {
                    int baseDamage = Tuning.swordDamage.Evaluate(
                        summary.LongestRun,
                        summary.TotalMatched);
                    int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
                    int attackIndex = summary.LongestRun >= 5 ? 3 : summary.LongestRun == 4 ? 2 : 1;

                    SetBanner("SWORD x" + summary.TotalMatched + "  •  " + damage + " DMG");

                    yield return actorPresenter.PlayPlayerAttack(
                        attackIndex,
                        Tuning.actionAnticipation,
                        delegate
                        {
                            PlayOneShot(attackSound);
                            DamageEnemy(damage);
                        },
                        Tuning.actionRecovery);
                }
                else
                {
                    PuzzleQuestElementTuning element = Tuning.GetElement(summary.Type);
                    PuzzleSkillTier tier = element.GetTier(summary.LongestRun);
                    int baseDamage = element.damage.Evaluate(
                        summary.LongestRun,
                        summary.TotalMatched);
                    int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
                    string state = element.GetAnimation(tier);

                    SetBanner(element.displayName + "  " + tier + "  •  " + damage + " DMG");

                    yield return actorPresenter.PlayElementSkill(
                        summary.Type,
                        tier,
                        state,
                        element.color,
                        Tuning.actionAnticipation,
                        delegate
                        {
                            if (summary.Type == PuzzleGemType.Fire)
                            {
                                PlayOneShot(attackSound);
                            }
                            DamageEnemy(damage);
                        },
                        Tuning.actionRecovery);
                }

                int currentMaxHp = enemyMaxHealth > 0 ? enemyMaxHealth : Tuning.enemyMaxHealth;
                int phase2Threshold = Mathf.FloorToInt(currentMaxHp * 0.5f);
                if (!isEnemyPhase2 && enemyHealth <= phase2Threshold && enemyHealth > 0)
                {
                    isEnemyPhase2 = true;
                    SetBanner("THỨC TỈNH CỔ ĐẠI (PHASE 2)");
                    SetStatus("Lithos Monolith thức tỉnh Phase 2! Sức mạnh tăng vọt!");
                    yield return actorPresenter.PlayEnemyPhaseTransition(delegate
                    {
                        enemyShield += 60;
                        UpdateHealthUi();
                    });
                }

                if (enemyHealth <= 0)
                {
                    yield break;
                }
            }
        }

        private IEnumerator ExecuteEnemyTurn()
        {
            inputLocked = true;
            SetTurn("LƯỢT CỦA QUÁI");

            int currentMaxHp = enemyMaxHealth > 0 ? enemyMaxHealth : Tuning.enemyMaxHealth;
            int phase2Threshold = Mathf.FloorToInt(currentMaxHp * 0.5f);
            if (!isEnemyPhase2 && enemyHealth <= phase2Threshold && enemyHealth > 0)
            {
                isEnemyPhase2 = true;
                SetBanner("THỨC TỈNH CỔ ĐẠI");
                SetStatus("Lithos Monolith thức tỉnh Phase 2! Sức mạnh tăng vọt!");
                yield return actorPresenter.PlayEnemyPhaseTransition(delegate
                {
                    enemyShield += 60;
                    UpdateHealthUi();
                });
            }

            bool enemyGetsExtraTurn;
            do
            {
                enemyTurnCount++;
                SetBanner("ENEMY PUZZLE");
                SetStatus("Lithos Monolith đang chọn nước đi.");
                yield return new WaitForSeconds(Tuning.enemyTurnDelay);

                enemyGetsExtraTurn = false;
                if (!TryFindEnemyMove(out enemyPlannedMoveFrom, out enemyPlannedMoveTo))
                {
                    yield return ReshuffleBoard();
                    if (!TryFindEnemyMove(out enemyPlannedMoveFrom, out enemyPlannedMoveTo))
                    {
                        yield break;
                    }
                }

                yield return ExecuteEnemyBoardMove(enemyPlannedMoveFrom, enemyPlannedMoveTo);

                if (playerHealth <= 0)
                {
                    yield break;
                }

                enemyGetsExtraTurn = lastEnemyMoveGrantedExtraTurn;
                if (enemyGetsExtraTurn)
                {
                    SetBanner("ENEMY THÊM LƯỢT");
                    SetStatus("Enemy ghép từ 4 ô trở lên nên được thêm lượt.");
                    yield return new WaitForSeconds(0.35f);
                }
            }
            while (enemyGetsExtraTurn && !gameEnded);

            if (!gameEnded && playerHealth > 0)
            {
                // Skill Địa Chấn Hóa Đá định kỳ
                int petrifyInterval = isEnemyPhase2 ? 2 : 3;
                if (enemyTurnCount % petrifyInterval == 0)
                {
                    int countToPetrify = isEnemyPhase2 ? 3 : 2;
                    SetBanner("ĐỊA CHẤN HÓA ĐÁ");
                    SetStatus("Rung chấn ma thuật đã hóa đá " + countToPetrify + " viên ngọc!");
                    yield return actorPresenter.PlayEnemyEarthquake(
                        Tuning.actionAnticipation,
                        delegate
                        {
                            PlayOneShot(enemyAttackSound);
                            PetrifyRandomGems(countToPetrify);
                        },
                        Tuning.actionRecovery,
                        isEnemyPhase2);
                }

                // Skill Khiên Cổ Thạch khi chưa có giáp
                if (enemyShield <= 0 && (enemyTurnCount % 4 == 0 || enemyHealth <= currentMaxHp * 0.4f))
                {
                    int shieldAmount = isEnemyPhase2 ? 80 : 50;
                    SetBanner("KHIÊN CỔ THẠCH  •  +" + shieldAmount + "");
                    SetStatus("Lithos Monolith dựng lớp khiên đá bảo vệ!");
                    yield return actorPresenter.PlayEnemyShield(
                        0.8f,
                        delegate
                        {
                            enemyShield += shieldAmount;
                            UpdateHealthUi();
                        },
                        isEnemyPhase2);
                }
            }

            if (playerHealth > 0)
            {
                SetTurn("LƯỢT CỦA BẠN");
                SetStatus("Kéo một ngọc sang ô kế bên.");
            }
        }

        public void OnPetrifiedGemClicked(PuzzleQuestGemView gemView)
        {
            PlayOneShot(invalidSound);
            SetStatus("Ô ngọc này đang bị HÓA ĐÁ! Hãy ghép các ô liền kề để giải phóng.");
        }

        private void PetrifyRandomGems(int count)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            foreach (var kvp in gemViews)
            {
                if (kvp.Value != null && !kvp.Value.IsPetrified)
                {
                    candidates.Add(kvp.Key);
                }
            }

            if (candidates.Count == 0) return;

            int toPetrify = Mathf.Min(count, candidates.Count);
            for (int i = 0; i < toPetrify; i++)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Count);
                Vector2Int cell = candidates[idx];
                candidates.RemoveAt(idx);

                if (gemViews.TryGetValue(cell, out PuzzleQuestGemView view) && view != null)
                {
                    view.SetPetrified(true);
                }
            }
        }

        private IEnumerator ExecuteEnemyBoardMove(Vector2Int first, Vector2Int second)
        {
            lastEnemyMoveGrantedExtraTurn = false;
            ClearSelection();

            PuzzleQuestGemView firstView = gemViews[first];
            PuzzleQuestGemView secondView = gemViews[second];
            SetStatus("Enemy đổi ô để tạo combo.");
            yield return AnimateTwoViews(
                firstView,
                GetCellPosition(second),
                secondView,
                GetCellPosition(first),
                Tuning.swapDuration);

            board.Swap(first, second);
            SwapViewCells(first, second);
            PlayOneShot(swapSound);

            PuzzleQuestMatchResult matches = board.FindMatches();
            int cascade = 1;
            while (matches.HasMatches && !gameEnded)
            {
                SetCombo(cascade);
                if (Tuning.extraTurnMatchLength > 0)
                {
                    for (int i = 0; i < matches.Summaries.Count; i++)
                    {
                        if (matches.Summaries[i].LongestRun >= Tuning.extraTurnMatchLength)
                        {
                            lastEnemyMoveGrantedExtraTurn = true;
                            break;
                        }
                    }
                }

                if (cascade >= comboEffectThreshold && comboSound != null)
                {
                    PlayOneShot(comboSound);
                }
                else
                {
                    PlayOneShot(matchSound);
                }
                yield return PopMatches(matches.MatchedCells, cascade);
                List<PuzzleQuestBoardMove> moves = board.CollapseAndRefill(matches.MatchedCells);
                yield return AnimateCollapseAndRefill(matches.MatchedCells, moves);
                yield return ExecuteEnemyActions(matches.Summaries, cascade);

                if (playerHealth <= 0)
                {
                    yield break;
                }

                matches = board.FindMatches();
                cascade++;
            }

            if (!board.HasPossibleSwap())
            {
                yield return ReshuffleBoard();
            }

            SetCombo(1);
        }

        private IEnumerator ExecuteEnemyActions(
            List<PuzzleQuestMatchSummary> summaries,
            int cascade)
        {
            float multiplier = 1f + Mathf.Max(0, cascade - 1) * Tuning.cascadeDamageBonus;

            for (int i = 0; i < summaries.Count; i++)
            {
                PuzzleQuestMatchSummary summary = summaries[i];
                int baseDamage = EvaluateSummaryDamage(summary);
                int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));

                // 1. Orbit AOE Storm nếu match dài >= 5 hoặc cascade cao ở Phase 2
                if (summary.LongestRun >= 5 || (isEnemyPhase2 && cascade >= 2))
                {
                    int finalDamage = Mathf.RoundToInt(damage * (isEnemyPhase2 ? 1.35f : 1.2f));
                    SetBanner("VÀNH ĐAI CỔ THẠCH  •  " + finalDamage + " DMG");
                    SetStatus("Lithos Monolith phóng bão sóng xung kích cổ đại!");

                    yield return actorPresenter.PlayEnemyOrbitAoe(
                        Tuning.actionAnticipation,
                        delegate
                        {
                            PlayOneShot(enemyAttackSound);
                            DamagePlayer(finalDamage);
                        },
                        Tuning.actionRecovery,
                        isEnemyPhase2);
                }
                // 2. Rock Barrage nếu match 4
                else if (summary.LongestRun == 4 || summary.TotalMatched >= 4)
                {
                    int rockCount = isEnemyPhase2 ? 6 : 4;
                    int dmgPerRock = Mathf.Max(1, damage / rockCount);
                    SetBanner("BÃO ĐÁ LIÊN HOÀN  •  " + damage + " DMG");
                    SetStatus("Lithos Monolith phóng loạt mảnh đá liên hoàn!");

                    yield return actorPresenter.PlayEnemyRockBarrage(
                        rockCount,
                        Tuning.actionAnticipation,
                        delegate
                        {
                            PlayOneShot(enemyAttackSound);
                            DamagePlayer(dmgPerRock);
                        },
                        Tuning.actionRecovery,
                        isEnemyPhase2);
                }
                // 3. Normal Rock Throw cho match 3
                else
                {
                    SetBanner("ĐÁ CÔNG KÍCH  •  " + damage + " DMG");
                    SetStatus("Lithos Monolith ném khối đá ma thuật!");

                    yield return actorPresenter.PlayEnemyAttack(
                        false,
                        Tuning.actionAnticipation,
                        delegate
                        {
                            PlayOneShot(enemyAttackSound);
                            DamagePlayer(damage);
                        },
                        Tuning.actionRecovery,
                        isEnemyPhase2);
                }

                if (playerHealth <= 0)
                {
                    yield break;
                }
            }
        }

        private bool TryFindEnemyMove(out Vector2Int first, out Vector2Int second)
        {
            first = Vector2Int.zero;
            second = Vector2Int.zero;
            if (board == null)
            {
                return false;
            }

            int bestScore = int.MinValue;
            bool found = false;

            // Nếu bật enemyOnlyMatch3: Ưu tiên tìm các nước đi CHỈ tạo match 3 viên (bỏ qua match 4, 5)
            if (enemyOnlyMatch3)
            {
                for (int row = 0; row < board.Rows; row++)
                {
                    for (int column = 0; column < board.Columns; column++)
                    {
                        Vector2Int current = new Vector2Int(column, row);
                        Vector2Int right = new Vector2Int(column + 1, row);
                        TryScoreEnemyMove(current, right, ref first, ref second, ref bestScore, ref found, true);

                        Vector2Int up = new Vector2Int(column, row + 1);
                        TryScoreEnemyMove(current, up, ref first, ref second, ref bestScore, ref found, true);
                    }
                }

                if (found)
                {
                    return true;
                }
            }

            // Quét bình thường nếu không bật enemyOnlyMatch3 hoặc nếu không có nước 3 viên thuần túy
            bestScore = int.MinValue;
            found = false;
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Vector2Int current = new Vector2Int(column, row);
                    Vector2Int right = new Vector2Int(column + 1, row);
                    TryScoreEnemyMove(current, right, ref first, ref second, ref bestScore, ref found, false);

                    Vector2Int up = new Vector2Int(column, row + 1);
                    TryScoreEnemyMove(current, up, ref first, ref second, ref bestScore, ref found, false);
                }
            }

            return found;
        }

        private void TryScoreEnemyMove(
            Vector2Int candidateA,
            Vector2Int candidateB,
            ref Vector2Int bestA,
            ref Vector2Int bestB,
            ref int bestScore,
            ref bool found,
            bool strictMatch3Only)
        {
            if (!board.AreAdjacent(candidateA, candidateB))
            {
                return;
            }

            board.Swap(candidateA, candidateB);
            PuzzleQuestMatchResult matches = board.FindMatches();
            if (matches.HasMatches && strictMatch3Only)
            {
                for (int i = 0; i < matches.Summaries.Count; i++)
                {
                    if (matches.Summaries[i].LongestRun >= 4 || matches.Summaries[i].TotalMatched >= 4)
                    {
                        board.Swap(candidateA, candidateB);
                        return; // Bỏ qua nước đi tạo từ 4 viên trở lên
                    }
                }
            }

            int score = matches.HasMatches ? ScoreEnemyMove(matches.Summaries) : int.MinValue;
            board.Swap(candidateA, candidateB);

            if (!matches.HasMatches || (found && score <= bestScore))
            {
                return;
            }

            bestA = candidateA;
            bestB = candidateB;
            bestScore = score;
            found = true;
        }

        private int ScoreEnemyMove(List<PuzzleQuestMatchSummary> summaries)
        {
            int score = 0;
            for (int i = 0; i < summaries.Count; i++)
            {
                PuzzleQuestMatchSummary summary = summaries[i];
                score += EvaluateSummaryDamage(summary);
                score += summary.TotalMatched * 3;
                if (Tuning.extraTurnMatchLength > 0
                    && summary.LongestRun >= Tuning.extraTurnMatchLength)
                {
                    score += 1000;
                }
            }

            return score;
        }

        private int EvaluateSummaryDamage(PuzzleQuestMatchSummary summary)
        {
            if (summary.Type == PuzzleGemType.Sword)
            {
                return Tuning.swordDamage.Evaluate(
                    summary.LongestRun,
                    summary.TotalMatched);
            }

            PuzzleQuestElementTuning element = Tuning.GetElement(summary.Type);
            return element.damage.Evaluate(
                summary.LongestRun,
                summary.TotalMatched);
        }

        private void DamageEnemy(int amount)
        {
            if (amount <= 0) return;

            if (enemyShield > 0)
            {
                int absorbed = Mathf.Min(enemyShield, amount);
                enemyShield -= absorbed;
                amount -= absorbed;
                SetStatus($"Khiên Cổ Thạch hấp thụ {absorbed} sát thương! Còn lại: {enemyShield}");
            }

            if (amount > 0)
            {
                enemyHealth = Mathf.Max(0, enemyHealth - amount);
            }

            UpdateHealthUi();
            if (actorPresenter != null)
            {
                actorPresenter.PlayEnemyHit();
            }
            PlayOneShot(enemyHurtSound);
        }

        private void DamagePlayer(int amount)
        {
            playerHealth = Mathf.Max(0, playerHealth - Mathf.Max(0, amount));
            UpdateHealthUi();
            if (actorPresenter != null)
            {
                actorPresenter.PlayPlayerHit();
            }
            PlayOneShot(playerHurtSound);
        }

        private IEnumerator EndGame(bool playerWon)
        {
            gameEnded = true;
            inputLocked = true;
            ClearSelection();

            if (playerWon)
            {
                IsPuzzleBossDefeated = true;
                SetBanner("CHIẾN THẮNG");
                actorPresenter.PlayEnemyDeath();
                PlayOneShot(victorySound);
                onBossDefeated?.Invoke();
                OnPuzzleBossDefeated?.Invoke();

                if (returnToPreviousTimelineSceneOnCompleted && TimelineSceneTravelManager.HasPreviousScene)
                {
                    TimelineSceneTravelManager.ReturnToPreviousScene();
                    yield break;
                }
            }
            else
            {
                SetBanner("THẤT BẠI");
                actorPresenter.PlayPlayerDeath();
            }

            yield return new WaitForSeconds(0.8f);

            if (resultTitle != null)
            {
                resultTitle.text = playerWon ? "CHIẾN THẮNG" : "THẤT BẠI";
                resultTitle.color = playerWon
                    ? new Color(1f, 0.82f, 0.32f, 1f)
                    : new Color(1f, 0.35f, 0.25f, 1f);
            }

            if (resultSubtitle != null)
            {
                resultSubtitle.text = playerWon
                    ? "Lithos Monolith đã bị đánh bại."
                    : "Tổ đội đã thất bại. Hãy thử lại.";
            }

            if (endOverlay != null)
            {
                endOverlay.gameObject.SetActive(!playerWon);
            }

            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(!playerWon);
            }
        }

        private void CreateBoardViews()
        {
            ClearBoardViews();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Vector2Int cell = new Vector2Int(column, row);
                    CreateGemView(cell, board[column, row], cell);
                }
            }

            LayoutAllGems();
        }

        private PuzzleQuestGemView CreateGemView(
            Vector2Int logicalCell,
            PuzzleGemType type,
            Vector2Int displayCell)
        {
            GameObject gemObject = new GameObject(
                "Gem " + logicalCell.x + "-" + logicalCell.y,
                typeof(RectTransform),
                typeof(PuzzleQuestGemView));
            RectTransform rect = gemObject.GetComponent<RectTransform>();
            rect.SetParent(boardGrid, false);

            PuzzleQuestGemView view = gemObject.GetComponent<PuzzleQuestGemView>();
            view.Initialize(this, logicalCell, type, GetGemIcon(type), GetGemColor(type), uiFont);
            SetGemSize(view);
            rect.anchoredPosition = GetCellPosition(displayCell);
            gemViews.Add(logicalCell, view);
            return view;
        }

        private IEnumerator PopMatches(HashSet<Vector2Int> matchedCells, int cascadeCount)
        {
            GameObject prefabToUse = (cascadeCount >= comboEffectThreshold && comboEffectPrefab != null) ? comboEffectPrefab : matchEffectPrefab;

            List<PuzzleQuestGemView> views = new List<PuzzleQuestGemView>();
            foreach (Vector2Int cell in matchedCells)
            {
                PuzzleQuestGemView view;
                if (gemViews.TryGetValue(cell, out view))
                {
                    views.Add(view);
                    view.CanvasGroup.blocksRaycasts = false;
                }

                if (prefabToUse != null)
                {
                    GameObject fx = Instantiate(prefabToUse, boardGrid);
                    RectTransform fxRect = fx.GetComponent<RectTransform>();
                    if (fxRect != null)
                    {
                        fxRect.anchoredPosition = GetCellPosition(cell);
                        fxRect.localScale = Vector3.one;
                    }
                    else
                    {
                        fx.transform.position = boardGrid.TransformPoint(GetCellPosition(cell));
                        fx.transform.localScale = Vector3.one;
                    }

                    Vector3 localPos = fx.transform.localPosition;
                    localPos.z = -50f;
                    fx.transform.localPosition = localPos;

                    Destroy(fx, 1.5f);
                }
            }

            // Giải phóng các ô bị Hóa Đá liền kề ô ghép
            Vector2Int[] adjacentDirs = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            bool unpetrifiedAny = false;
            foreach (Vector2Int cell in matchedCells)
            {
                for (int d = 0; d < adjacentDirs.Length; d++)
                {
                    Vector2Int neighbor = cell + adjacentDirs[d];
                    if (gemViews.TryGetValue(neighbor, out PuzzleQuestGemView neighborView) && neighborView != null && neighborView.IsPetrified)
                    {
                        neighborView.SetPetrified(false);
                        unpetrifiedAny = true;
                    }
                }
            }
            if (unpetrifiedAny)
            {
                SetStatus("Đã phá vỡ khối đá cổ đại!");
            }

            float duration = Tuning.matchPopDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = t < 0.35f
                    ? Mathf.Lerp(1f, 1.2f, t / 0.35f)
                    : Mathf.Lerp(1.2f, 0f, (t - 0.35f) / 0.65f);
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i] == null)
                    {
                        continue;
                    }

                    views[i].transform.localScale = Vector3.one * pulse;
                    views[i].CanvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                }

                yield return null;
            }
        }

        private IEnumerator AnimateCollapseAndRefill(
            HashSet<Vector2Int> matchedCells,
            List<PuzzleQuestBoardMove> moves)
        {
            Dictionary<Vector2Int, PuzzleQuestGemView> oldViews =
                new Dictionary<Vector2Int, PuzzleQuestGemView>(gemViews);
            gemViews.Clear();

            foreach (Vector2Int cell in matchedCells)
            {
                PuzzleQuestGemView removed;
                if (oldViews.TryGetValue(cell, out removed) && removed != null)
                {
                    DestroyRuntimeObject(removed.gameObject);
                }
            }

            List<PuzzleQuestGemView> movingViews = new List<PuzzleQuestGemView>();
            List<Vector2> starts = new List<Vector2>();
            List<Vector2> targets = new List<Vector2>();

            for (int i = 0; i < moves.Count; i++)
            {
                PuzzleQuestBoardMove move = moves[i];
                PuzzleQuestGemView view;
                if (move.IsNew)
                {
                    view = CreateGemViewWithoutRegistration(move.To, move.Type, move.From);
                }
                else if (!oldViews.TryGetValue(move.From, out view) || view == null)
                {
                    continue;
                }

                view.SetCell(move.To);
                view.gameObject.name = "Gem " + move.To.x + "-" + move.To.y;
                gemViews[move.To] = view;
                movingViews.Add(view);
                starts.Add(view.RectTransform.anchoredPosition);
                targets.Add(GetCellPosition(move.To));
            }

            float duration = Tuning.fallDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                for (int i = 0; i < movingViews.Count; i++)
                {
                    if (movingViews[i] != null)
                    {
                        movingViews[i].RectTransform.anchoredPosition = Vector2.LerpUnclamped(
                            starts[i],
                            targets[i],
                            t);
                    }
                }

                yield return null;
            }

            for (int i = 0; i < movingViews.Count; i++)
            {
                if (movingViews[i] == null)
                {
                    continue;
                }

                movingViews[i].RectTransform.anchoredPosition = targets[i];
                movingViews[i].ResetVisualState();
                PuzzleGemType type = board[movingViews[i].Cell.x, movingViews[i].Cell.y];
                movingViews[i].SetType(type, GetGemIcon(type), GetGemColor(type));
            }
        }

        private PuzzleQuestGemView CreateGemViewWithoutRegistration(
            Vector2Int logicalCell,
            PuzzleGemType type,
            Vector2Int displayCell)
        {
            GameObject gemObject = new GameObject(
                "Gem " + logicalCell.x + "-" + logicalCell.y,
                typeof(RectTransform),
                typeof(PuzzleQuestGemView));
            RectTransform rect = gemObject.GetComponent<RectTransform>();
            rect.SetParent(boardGrid, false);
            PuzzleQuestGemView view = gemObject.GetComponent<PuzzleQuestGemView>();
            view.Initialize(this, logicalCell, type, GetGemIcon(type), GetGemColor(type), uiFont);
            SetGemSize(view);
            rect.anchoredPosition = GetCellPosition(displayCell);
            return view;
        }

        private IEnumerator ReshuffleBoard()
        {
            inputLocked = true;
            SetBanner("RESHUFFLE");
            SetStatus("Không còn nước đi. Bàn cờ đang được xáo lại.");

            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                foreach (PuzzleQuestGemView view in gemViews.Values)
                {
                    view.CanvasGroup.alpha = alpha;
                }

                yield return null;
            }

            board.Reshuffle();
            foreach (KeyValuePair<Vector2Int, PuzzleQuestGemView> pair in gemViews)
            {
                PuzzleGemType type = board[pair.Key.x, pair.Key.y];
                pair.Value.SetType(type, GetGemIcon(type), GetGemColor(type));
                pair.Value.ResetVisualState();
            }

            yield return new WaitForSeconds(0.12f);
        }

        private IEnumerator AnimateTwoViews(
            PuzzleQuestGemView first,
            Vector2 firstTarget,
            PuzzleQuestGemView second,
            Vector2 secondTarget,
            float duration)
        {
            Vector2 firstStart = first.RectTransform.anchoredPosition;
            Vector2 secondStart = second.RectTransform.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                first.RectTransform.anchoredPosition = Vector2.LerpUnclamped(firstStart, firstTarget, t);
                second.RectTransform.anchoredPosition = Vector2.LerpUnclamped(secondStart, secondTarget, t);
                yield return null;
            }

            first.RectTransform.anchoredPosition = firstTarget;
            second.RectTransform.anchoredPosition = secondTarget;
        }

        private void SwapViewCells(Vector2Int first, Vector2Int second)
        {
            PuzzleQuestGemView firstView = gemViews[first];
            PuzzleQuestGemView secondView = gemViews[second];
            gemViews[first] = secondView;
            gemViews[second] = firstView;
            firstView.SetCell(second);
            secondView.SetCell(first);
            firstView.gameObject.name = "Gem " + second.x + "-" + second.y;
            secondView.gameObject.name = "Gem " + first.x + "-" + first.y;
        }

        private void LayoutAllGems()
        {
            if (boardGrid == null || board == null)
            {
                return;
            }

            lastBoardSize = boardGrid.rect.size;
            foreach (KeyValuePair<Vector2Int, PuzzleQuestGemView> pair in gemViews)
            {
                SetGemSize(pair.Value);
                pair.Value.RectTransform.anchoredPosition = GetCellPosition(pair.Key);
            }
        }

        private void SetGemSize(PuzzleQuestGemView view)
        {
            float size = GetCellSize();
            view.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            view.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            view.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.RectTransform.sizeDelta = new Vector2(size, size);
        }

        private float GetCellSize()
        {
            Vector2 size = boardGrid.rect.size;
            float width = Mathf.Max(100f, size.x);
            float height = Mathf.Max(100f, size.y);
            float spacing = Tuning.cellSpacing;
            float byWidth = (width - spacing * (board.Columns - 1)) / board.Columns;
            float byHeight = (height - spacing * (board.Rows - 1)) / board.Rows;
            return Mathf.Max(12f, Mathf.Min(byWidth, byHeight));
        }

        private Vector2 GetCellPosition(Vector2Int cell)
        {
            float cellSize = GetCellSize();
            float step = cellSize + Tuning.cellSpacing;
            float totalWidth = cellSize * board.Columns + Tuning.cellSpacing * (board.Columns - 1);
            float totalHeight = cellSize * board.Rows + Tuning.cellSpacing * (board.Rows - 1);
            return new Vector2(
                -totalWidth * 0.5f + cellSize * 0.5f + cell.x * step,
                -totalHeight * 0.5f + cellSize * 0.5f + cell.y * step);
        }

        private void SelectGem(PuzzleQuestGemView gem)
        {
            if (selectedGem == gem)
            {
                return;
            }

            if (selectedGem != null)
            {
                selectedGem.SetSelected(false);
            }

            selectedGem = gem;
            if (selectedGem != null)
            {
                selectedGem.SetSelected(true);
            }
        }

        private void ClearSelection()
        {
            if (selectedGem != null)
            {
                selectedGem.SetSelected(false);
            }

            selectedGem = null;
            selectionBeforePointerDown = null;
        }

        private void UpdateHealthUi()
        {
            if (playerHealthFill != null)
            {
                playerHealthFill.value = (float)playerHealth / Tuning.playerMaxHealth;
            }

            if (playerHealthText != null)
            {
                playerHealthText.text = playerHealth + " / " + Tuning.playerMaxHealth;
            }

            int currentMaxHp = enemyMaxHealth > 0 ? enemyMaxHealth : Tuning.enemyMaxHealth;

            if (enemyHealthFill != null)
            {
                enemyHealthFill.value = (float)enemyHealth / currentMaxHp;
            }

            if (enemyHealthText != null)
            {
                if (enemyShield > 0)
                {
                    enemyHealthText.text = $"{enemyHealth} [+{enemyShield}] / {currentMaxHp}";
                }
                else
                {
                    enemyHealthText.text = $"{enemyHealth} / {currentMaxHp}";
                }
            }
        }

        private void SetBanner(string value)
        {
            if (actionBanner != null)
            {
                actionBanner.text = value;
            }
        }

        private Coroutine turnMaterialCoroutine;
        private UnityEngine.UI.Image cachedPlayerTurnImage;
        private UnityEngine.UI.Image cachedEnemyTurnImage;
        private bool isTurnImageCached = false;
        private string currentTurnOwner = string.Empty;

        private void SetTurn(string value)
        {
            if (turnText != null) turnText.text = value;
            if (enemyTurnText != null) enemyTurnText.text = value;

            bool isPlayerTurn = (value == "LƯỢT CỦA BẠN");
            if (playerTurnPanel != null) playerTurnPanel.SetActive(isPlayerTurn);
            if (enemyTurnPanel != null) enemyTurnPanel.SetActive(!isPlayerTurn);

            if (currentTurnOwner != value)
            {
                currentTurnOwner = value;
                if (turnMaterialCoroutine != null)
                {
                    StopCoroutine(turnMaterialCoroutine);
                }
                if (gameObject.activeInHierarchy)
                {
                    turnMaterialCoroutine = StartCoroutine(TurnMaterialRoutine());
                }
            }
        }

        private IEnumerator TurnMaterialRoutine()
        {
            if (!isTurnImageCached)
            {
                if (playerTurnPanel != null) cachedPlayerTurnImage = playerTurnPanel.GetComponent<UnityEngine.UI.Image>();
                if (enemyTurnPanel != null) cachedEnemyTurnImage = enemyTurnPanel.GetComponent<UnityEngine.UI.Image>();
                isTurnImageCached = true;
            }

            if (cachedPlayerTurnImage != null) cachedPlayerTurnImage.material = playerTurnMaterial;
            if (cachedEnemyTurnImage != null) cachedEnemyTurnImage.material = enemyTurnMaterial;

            float elapsed = 0f;
            while (elapsed < turnMaterialDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (cachedPlayerTurnImage != null) cachedPlayerTurnImage.material = null;
            if (cachedEnemyTurnImage != null) cachedEnemyTurnImage.material = null;
        }

        private void SetCombo(int cascade)
        {
            string text = "COMBO x" + cascade;
            if (comboText != null) comboText.text = text;
            if (enemyComboText != null) enemyComboText.text = text;
        }

        private void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value;
            if (enemyStatusText != null) enemyStatusText.text = value;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip, audioVolume);
            }
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.gameObject.scene.IsValid())
            {
                eventSystem = FindSceneEventSystem();
            }

            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("PuzzleQuest EventSystem");
                eventSystemObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(eventSystemObject, gameObject.scene);
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                createdRuntimeEventSystem = true;
            }

            runtimeEventSystem = eventSystem;
            eventSystem.gameObject.SetActive(true);
            eventSystem.enabled = true;

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] != inputModule)
                {
                    modules[i].enabled = false;
                }
            }

            if (NeedsUiActions(inputModule))
            {
                AssignPuzzleQuestUiActions(inputModule);
            }

            inputModule.enabled = true;
            EventSystem.current = eventSystem;
            eventSystem.SetSelectedGameObject(null);
        }

        private bool NeedsUiActions(InputSystemUIInputModule inputModule)
        {
            return inputModule.actionsAsset == null
                   || inputModule.point == null
                   || inputModule.point.action == null
                   || inputModule.leftClick == null
                   || inputModule.leftClick.action == null;
        }

        private void AssignPuzzleQuestUiActions(InputSystemUIInputModule inputModule)
        {
            if (runtimeUiActions == null)
            {
                runtimeUiActions = CreatePuzzleQuestUiActions();
            }

            InputActionMap uiMap = runtimeUiActions.FindActionMap("UI", true);
            inputModule.actionsAsset = runtimeUiActions;
            inputModule.point = InputActionReference.Create(uiMap.FindAction("Point", true));
            inputModule.leftClick = InputActionReference.Create(uiMap.FindAction("Click", true));
            inputModule.rightClick = InputActionReference.Create(uiMap.FindAction("RightClick", true));
            inputModule.middleClick = InputActionReference.Create(uiMap.FindAction("MiddleClick", true));
            inputModule.scrollWheel = InputActionReference.Create(uiMap.FindAction("ScrollWheel", true));
            inputModule.move = InputActionReference.Create(uiMap.FindAction("Navigate", true));
            inputModule.submit = InputActionReference.Create(uiMap.FindAction("Submit", true));
            inputModule.cancel = InputActionReference.Create(uiMap.FindAction("Cancel", true));
        }

        private static InputActionAsset CreatePuzzleQuestUiActions()
        {
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            actions.name = "PuzzleQuest Runtime UI Actions";
            actions.hideFlags = HideFlags.DontSave;

            InputActionMap uiMap = actions.AddActionMap("UI");

            InputAction point = uiMap.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position");
            point.AddBinding("<Pen>/position");
            point.AddBinding("<Touchscreen>/touch*/position");

            InputAction click = uiMap.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            click.AddBinding("<Mouse>/leftButton");
            click.AddBinding("<Pen>/tip");
            click.AddBinding("<Touchscreen>/touch*/press");

            InputAction rightClick = uiMap.AddAction("RightClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            rightClick.AddBinding("<Mouse>/rightButton");

            InputAction middleClick = uiMap.AddAction("MiddleClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            middleClick.AddBinding("<Mouse>/middleButton");

            InputAction scroll = uiMap.AddAction("ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            scroll.AddBinding("<Mouse>/scroll");

            InputAction move = uiMap.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            InputAction submit = uiMap.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            InputAction cancel = uiMap.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");

            return actions;
        }

        private EventSystem FindSceneEventSystem()
        {
            EventSystem[] systems = Resources.FindObjectsOfTypeAll<EventSystem>();
            EventSystem fallback = null;
            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem candidate = systems[i];
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (candidate.gameObject.scene == gameObject.scene)
                {
                    if (candidate.isActiveAndEnabled)
                    {
                        return candidate;
                    }

                    fallback = candidate;
                }
                else if (fallback == null && candidate.isActiveAndEnabled)
                {
                    fallback = candidate;
                }
            }

            return fallback;
        }

        private void BeginRuntimeInitialization()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || initializeCoroutine != null)
            {
                return;
            }

            initializeCoroutine = StartCoroutine(InitializeGameRoutine());
        }

        private IEnumerator InitializeGameRoutine()
        {
            yield return InitializeGame();
            initializeCoroutine = null;
        }

        private void ResetRuntimeSession()
        {
            if (!runtimeStarted && initializeCoroutine == null)
            {
                return;
            }

            if (initializeCoroutine != null)
            {
                StopCoroutine(initializeCoroutine);
                initializeCoroutine = null;
            }

            StopAllCoroutines();
            inputLocked = true;
            gameEnded = false;
            board = null;
            ClearSelection();

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartScene);
            }

            if (actorPresenter != null)
            {
                actorPresenter.Dispose();
                actorPresenter = null;
            }

            ClearBoardViews();

            if (createdRuntimeEventSystem && runtimeEventSystem != null)
            {
                DestroyRuntimeObject(runtimeEventSystem.gameObject);
            }

            if (runtimeUiActions != null)
            {
                DestroyRuntimeObject(runtimeUiActions);
                runtimeUiActions = null;
            }

            runtimeEventSystem = null;
            createdRuntimeEventSystem = false;
            runtimeStarted = false;
        }

        private void ClearBoardViews()
        {
            gemViews.Clear();
            if (boardGrid == null)
            {
                return;
            }

            for (int index = boardGrid.childCount - 1; index >= 0; index--)
            {
                GameObject child = boardGrid.GetChild(index).gameObject;
                child.SetActive(false);
                DestroyRuntimeObject(child);
            }
        }

        private static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void DisableAuthoredSceneActors()
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == gameObject)
                {
                    continue;
                }

                string rootName = root.name;
                if (rootName == "PlayerBase"
                    || rootName == "Sunflower Fairy"
                    || rootName == "Sunflower Fairy Fire"
                    || rootName == "Sunflower Fairy Wind"
                    || rootName == "Lithos Monolith"
                    || rootName == "Directional Light")
                {
                    root.SetActive(false);
                }
            }
        }

        private void SyncActorLayoutFromSceneActors()
        {
            if (!useSceneBattleActors || actorLayout == null)
            {
                return;
            }

            FindSceneBattleReferencesIfNeeded();

            if (playerPointA != null)
            {
                actorLayout.playerPosition = transform.InverseTransformPoint(playerPointA.position);
            }
            else if (playerSceneActor != null)
            {
                actorLayout.playerPosition = transform.InverseTransformPoint(playerSceneActor.position);
            }

            if (playerPointB != null)
            {
                actorLayout.playerAttackPosition = transform.InverseTransformPoint(playerPointB.position);
            }

            if (playerSceneActor != null)
            {
                PlayerSceneScaleController scaleCtrl = playerSceneActor.GetComponent<PlayerSceneScaleController>();
                if (scaleCtrl != null && scaleCtrl.BasePlayerScale != Vector3.zero)
                {
                    actorLayout.playerScale = scaleCtrl.BasePlayerScale;
                }
                else
                {
                    actorLayout.playerScale = playerSceneActor.localScale;
                }
                actorLayout.playerEulerAngles = playerSceneActor.localEulerAngles;
            }

            if (enemyPointA != null)
            {
                actorLayout.enemyPosition = transform.InverseTransformPoint(enemyPointA.position);
            }
            else if (enemySceneActor != null)
            {
                actorLayout.enemyPosition = transform.InverseTransformPoint(enemySceneActor.position);
            }

            if (enemyPointB != null)
            {
                actorLayout.enemyAttackPosition = transform.InverseTransformPoint(enemyPointB.position);
            }

            if (enemySceneActor != null)
            {
                actorLayout.enemyScale = enemySceneActor.localScale;
                actorLayout.enemyEulerAngles = enemySceneActor.localEulerAngles;
            }
        }

        private void FindSceneBattleReferencesIfNeeded()
        {
            if (playerSceneActor == null)
            {
                playerSceneActor = FindSceneRoot("PlayerBase");
            }

            if (enemySceneActor == null)
            {
                enemySceneActor = FindSceneRoot("Lithos Monolith");
            }

            if (playerPointA == null)
            {
                playerPointA = FindSceneRoot("Player Point A");
            }

            if (playerPointB == null)
            {
                playerPointB = FindSceneRoot("Player Point B");
            }

            if (enemyPointA == null)
            {
                enemyPointA = FindSceneRoot("Enemy Point A");
            }

            if (enemyPointB == null)
            {
                enemyPointB = FindSceneRoot("Enemy Point B");
            }
        }

        private Transform FindSceneRoot(string objectName)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == objectName)
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private void DisableSceneBattleActors()
        {
            if (!Application.isPlaying || !useSceneBattleActors)
            {
                return;
            }

            if (playerSceneActor != null)
            {
                playerSceneActor.gameObject.SetActive(false);
            }

            if (enemySceneActor != null)
            {
                enemySceneActor.gameObject.SetActive(false);
            }
        }

        private GameObject ResolvePlayerActorSource()
        {
            if (useSceneBattleActors && playerSceneActor != null)
            {
                return playerSceneActor.gameObject;
            }

            return playerPrefab;
        }

        private GameObject ResolveEnemyActorSource()
        {
            if (useSceneBattleActors && enemySceneActor != null)
            {
                return enemySceneActor.gameObject;
            }

            return enemyPrefab;
        }

        private void RestartScene()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            StopAllCoroutines();
            inputLocked = true;
            gameEnded = false;
            if (actorPresenter != null)
            {
                actorPresenter.Dispose();
                actorPresenter = null;
            }

            if (endOverlay != null)
            {
                endOverlay.gameObject.SetActive(false);
            }

            initializeCoroutine = null;
            BeginRuntimeInitialization();
        }

        private void QueueEditorUiBuild()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= EditorBuildUiIfNeeded;
            UnityEditor.EditorApplication.delayCall += EditorBuildUiIfNeeded;
#endif
        }

#if UNITY_EDITOR
        private void EditorBuildUiIfNeeded()
        {
            if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
            {
                return;
            }

            if (gameObject.scene.name == "PuzzleQuest")
            {
                BuildMissingUi();
                EnsureSceneBattleActors();
            }
        }

        [ContextMenu("Build Missing Scene Battle Actors")]
        public void EnsureSceneBattleActors()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (actorLayout == null)
            {
                actorLayout = new PuzzleQuestActorLayout();
            }

            playerPointA = EnsureScenePoint(playerPointA, "Player Point A", actorLayout.playerPosition);
            playerPointB = EnsureScenePoint(playerPointB, "Player Point B", actorLayout.playerAttackPosition);
            enemyPointA = EnsureScenePoint(enemyPointA, "Enemy Point A", actorLayout.enemyPosition);
            enemyPointB = EnsureScenePoint(enemyPointB, "Enemy Point B", actorLayout.enemyAttackPosition);

            playerSceneActor = EnsureSceneActor(
                playerSceneActor,
                playerPrefab,
                "PlayerBase",
                playerPointA,
                actorLayout.playerEulerAngles,
                actorLayout.playerScale);

            enemySceneActor = EnsureSceneActor(
                enemySceneActor,
                enemyPrefab,
                "Lithos Monolith",
                enemyPointA,
                actorLayout.enemyEulerAngles,
                actorLayout.enemyScale);

            useSceneBattleActors = true;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        private Transform EnsureScenePoint(Transform current, string objectName, Vector3 localPosition)
        {
            if (current == null)
            {
                current = FindSceneRoot(objectName);
            }

            if (current == null)
            {
                GameObject pointObject = new GameObject(objectName);
                UnityEditor.Undo.RegisterCreatedObjectUndo(pointObject, "Create " + objectName);
                pointObject.transform.position = transform.TransformPoint(localPosition);
                pointObject.transform.rotation = Quaternion.identity;
                pointObject.transform.localScale = Vector3.one;
                current = pointObject.transform;
            }

            return current;
        }

        private Transform EnsureSceneActor(
            Transform current,
            GameObject prefab,
            string objectName,
            Transform pointA,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (current == null)
            {
                current = FindSceneRoot(objectName);
            }

            if (current == null)
            {
                GameObject actorObject = null;
                if (prefab != null)
                {
                    actorObject = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, gameObject.scene) as GameObject;
                }

                if (actorObject == null)
                {
                    actorObject = new GameObject(objectName);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(actorObject, "Create " + objectName);
                }

                actorObject.name = objectName;
                current = actorObject.transform;
            }

            if (pointA != null)
            {
                current.position = pointA.position;
            }

            current.localEulerAngles = localEulerAngles;
            current.localScale = SafeEditorScale(localScale);
            current.gameObject.SetActive(true);
            return current;
        }

        private static Vector3 SafeEditorScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(PuzzleQuestController), true)]
    public sealed class PuzzleQuestControllerInspector : UnityEditor.Editor
    {
        private static Rect scenePanelRect = new Rect(12f, 72f, 280f, 248f);
        private UnityEditor.SerializedProperty actorLayoutProperty;

        private void OnEnable()
        {
            actorLayoutProperty = serializedObject.FindProperty("actorLayout");
        }

        public override void OnInspectorGUI()
        {
            UnityEditor.EditorGUILayout.HelpBox(
                "PUZZLE QUEST CONTROLLER\nBalance can be edited inline or overridden by a PuzzleQuestSettings asset. " +
                "UI RectTransforms remain editable under the generated PuzzleQuest UI hierarchy.",
                UnityEditor.MessageType.Info);

            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space();
            if (UnityEngine.GUILayout.Button("Build Missing Puzzle Quest UI"))
            {
                ((PuzzleQuestController)target).BuildMissingUi();
            }

            if (UnityEngine.GUILayout.Button("Build Missing Scene Battle Actors"))
            {
                ((PuzzleQuestController)target).EnsureSceneBattleActors();
            }
        }

        private void OnSceneGUI()
        {
            if (actorLayoutProperty == null)
            {
                actorLayoutProperty = serializedObject.FindProperty("actorLayout");
            }

            PuzzleQuestController controller = (PuzzleQuestController)target;
            Transform controllerTransform = controller.transform;

            serializedObject.Update();
            if (!Application.isPlaying
                && controller.useSceneBattleActors
                && (controller.playerSceneActor == null
                    || controller.enemySceneActor == null
                    || controller.playerPointA == null
                    || controller.playerPointB == null
                    || controller.enemyPointA == null
                    || controller.enemyPointB == null))
            {
                controller.EnsureSceneBattleActors();
                serializedObject.Update();
            }

            if (controller.useSceneBattleActors
                && controller.playerPointA != null
                && controller.playerPointB != null
                && controller.enemyPointA != null
                && controller.enemyPointB != null)
            {
                DrawSceneActorHandles(
                    controller.playerPointA,
                    controller.playerPointB,
                    controller.playerSceneActor,
                    "Player",
                    new Color(0.25f, 0.65f, 1f, 1f));

                DrawSceneActorHandles(
                    controller.enemyPointA,
                    controller.enemyPointB,
                    controller.enemySceneActor,
                    "Enemy",
                    new Color(1f, 0.42f, 0.25f, 1f));
            }
            else
            {
                UnityEditor.SerializedProperty playerA = actorLayoutProperty.FindPropertyRelative("playerPosition");
                UnityEditor.SerializedProperty playerB = actorLayoutProperty.FindPropertyRelative("playerAttackPosition");
                UnityEditor.SerializedProperty playerScale = actorLayoutProperty.FindPropertyRelative("playerScale");
                UnityEditor.SerializedProperty enemyA = actorLayoutProperty.FindPropertyRelative("enemyPosition");
                UnityEditor.SerializedProperty enemyB = actorLayoutProperty.FindPropertyRelative("enemyAttackPosition");
                UnityEditor.SerializedProperty enemyScale = actorLayoutProperty.FindPropertyRelative("enemyScale");

                DrawActorPathHandles(
                    controllerTransform,
                    playerA,
                    playerB,
                    playerScale,
                    "Player",
                    new Color(0.25f, 0.65f, 1f, 1f));

                DrawActorPathHandles(
                    controllerTransform,
                    enemyA,
                    enemyB,
                    enemyScale,
                    "Enemy",
                    new Color(1f, 0.42f, 0.25f, 1f));
            }

            UnityEditor.Handles.BeginGUI();
            scenePanelRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                scenePanelRect,
                DrawScenePanel,
                "Puzzle Quest Battle");
            UnityEditor.Handles.EndGUI();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScenePanel(int windowId)
        {
            UnityEditor.EditorGUILayout.LabelField("Player move to B", UnityEditor.EditorStyles.boldLabel);
            DrawLayoutProperty("playerSwordMovesToPointB", "Đánh thường");
            DrawLayoutProperty("playerFireMovesToPointB", "Skill lửa");
            DrawLayoutProperty("playerWaterMovesToPointB", "Skill nước");
            DrawLayoutProperty("playerWindMovesToPointB", "Skill gió");

            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Enemy move to B", UnityEditor.EditorStyles.boldLabel);
            DrawLayoutProperty("enemyNormalAttackMovesToPointB", "Đánh thường");
            DrawLayoutProperty("enemyAoeMovesToPointB", "AOE");
            DrawLayoutProperty("enemyWalkState", "Walk state");

            UnityEditor.EditorGUILayout.Space(4f);
            UnityEditor.EditorGUILayout.LabelField("Timing", UnityEditor.EditorStyles.boldLabel);
            DrawLayoutProperty("moveToAttackDuration", "Đi tới B");
            DrawLayoutProperty("returnToPointADuration", "Về A");

            GUI.DragWindow();
        }

        private void DrawLayoutProperty(string propertyName, string label)
        {
            UnityEditor.SerializedProperty property = actorLayoutProperty.FindPropertyRelative(propertyName);
            if (property != null)
            {
                UnityEditor.EditorGUILayout.PropertyField(property, new GUIContent(label));
            }
        }

        private static void DrawSceneActorHandles(
            Transform pointA,
            Transform pointB,
            Transform actor,
            string label,
            Color color)
        {
            Vector3 worldA = pointA.position;
            Vector3 worldB = pointB.position;
            float handleSizeA = UnityEditor.HandleUtility.GetHandleSize(worldA);
            float handleSizeB = UnityEditor.HandleUtility.GetHandleSize(worldB);

            Color previousColor = UnityEditor.Handles.color;
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawAAPolyLine(4f, worldA, worldB);
            UnityEditor.Handles.SphereHandleCap(
                0,
                worldA,
                Quaternion.identity,
                handleSizeA * 0.08f,
                EventType.Repaint);
            UnityEditor.Handles.CubeHandleCap(
                0,
                worldB,
                Quaternion.identity,
                handleSizeB * 0.1f,
                EventType.Repaint);
            UnityEditor.Handles.Label(worldA + Vector3.up * handleSizeA * 0.18f, label + " Point A");
            UnityEditor.Handles.Label(worldB + Vector3.up * handleSizeB * 0.18f, label + " Point B");

            UnityEditor.EditorGUI.BeginChangeCheck();
            Vector3 movedA = UnityEditor.Handles.PositionHandle(worldA, pointA.rotation);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(pointA, "Move " + label + " Point A");
                pointA.position = movedA;
                if (actor != null)
                {
                    UnityEditor.Undo.RecordObject(actor, "Move " + label);
                    actor.position = movedA;
                }
            }

            UnityEditor.EditorGUI.BeginChangeCheck();
            Vector3 movedB = UnityEditor.Handles.PositionHandle(worldB, pointB.rotation);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                UnityEditor.Undo.RecordObject(pointB, "Move " + label + " Point B");
                pointB.position = movedB;
            }

            if (actor != null)
            {
                UnityEditor.EditorGUI.BeginChangeCheck();
                Vector3 scaled = UnityEditor.Handles.ScaleHandle(
                    actor.localScale,
                    actor.position + Vector3.up * handleSizeA * 0.35f,
                    actor.rotation,
                    handleSizeA * 0.65f);
                if (UnityEditor.EditorGUI.EndChangeCheck())
                {
                    UnityEditor.Undo.RecordObject(actor, "Scale " + label);
                    actor.localScale = SafeSceneScale(scaled);
                }
            }

            UnityEditor.Handles.color = previousColor;
        }

        private static void DrawActorPathHandles(
            Transform origin,
            UnityEditor.SerializedProperty pointA,
            UnityEditor.SerializedProperty pointB,
            UnityEditor.SerializedProperty scale,
            string label,
            Color color)
        {
            Vector3 worldA = origin.TransformPoint(pointA.vector3Value);
            Vector3 worldB = origin.TransformPoint(pointB.vector3Value);
            float handleSizeA = UnityEditor.HandleUtility.GetHandleSize(worldA);
            float handleSizeB = UnityEditor.HandleUtility.GetHandleSize(worldB);

            Color previousColor = UnityEditor.Handles.color;
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawAAPolyLine(4f, worldA, worldB);
            UnityEditor.Handles.SphereHandleCap(
                0,
                worldA,
                Quaternion.identity,
                handleSizeA * 0.08f,
                EventType.Repaint);
            UnityEditor.Handles.CubeHandleCap(
                0,
                worldB,
                Quaternion.identity,
                handleSizeB * 0.1f,
                EventType.Repaint);
            UnityEditor.Handles.Label(worldA + Vector3.up * handleSizeA * 0.18f, label + " A");
            UnityEditor.Handles.Label(worldB + Vector3.up * handleSizeB * 0.18f, label + " B");

            UnityEditor.EditorGUI.BeginChangeCheck();
            Vector3 movedA = UnityEditor.Handles.PositionHandle(worldA, origin.rotation);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                pointA.vector3Value = origin.InverseTransformPoint(movedA);
            }

            UnityEditor.EditorGUI.BeginChangeCheck();
            Vector3 movedB = UnityEditor.Handles.PositionHandle(worldB, origin.rotation);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                pointB.vector3Value = origin.InverseTransformPoint(movedB);
            }

            UnityEditor.EditorGUI.BeginChangeCheck();
            Vector3 scaled = UnityEditor.Handles.ScaleHandle(
                scale.vector3Value,
                worldA + Vector3.up * handleSizeA * 0.35f,
                origin.rotation,
                handleSizeA * 0.65f);
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                scale.vector3Value = SafeSceneScale(scaled);
            }

            UnityEditor.Handles.color = previousColor;
        }

        private static Vector3 SafeSceneScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 0.001f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 0.001f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 0.001f : scale.z);
        }
    }
#endif
}
