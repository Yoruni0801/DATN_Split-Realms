using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using Quoc.Boss;
using EnemySystem;
using Bao.Enemies;

namespace Quoc.Scripts.Cutscene
{
    /// <summary>
    /// Component quản lý bật/tắt tính năng tìm/gây hấn (Aggro / Search Player) của Enemy hoặc Boss.
    /// Cho phép kéo thả GameObject hoặc Component bất kỳ trong Inspector, tự động đồng bộ theo Timeline.
    /// </summary>
    [DisallowMultipleComponent]
    public class GenericEnemyAggroController : MonoBehaviour
    {
        [Header("Target Enemy / Boss")]
        [Tooltip("Kéo thả GameObject của Enemy hoặc Boss vào đây (để trống sẽ tự tìm trên chính nó / parent / children)")]
        [SerializeField] private GameObject targetEnemyObject;

        [Header("Timeline Trigger (Optional)")]
        [Tooltip("Gán PlayableDirector nếu muốn tự động tắt Aggro khi Cutscene chạy và bật lại khi Cutscene kết thúc")]
        [SerializeField] private PlayableDirector director;

        [Header("Settings")]
        [Tooltip("Nếu tích chọn, khi Awake / Start script sẽ tự động Disable Aggro (khóa tìm player)")]
        [SerializeField] private bool disableAggroOnAwake = true;

        [Tooltip("In thông báo ra Console khi Aggro thay đổi trạng thái")]
        [SerializeField] private bool debugLog = false;

        private Component targetComponent;
        private BossController cachedBoss;
        private BossMovement cachedBossMovement;
        private StandaloneFlyingShooterEnemy cachedFlyingShooter;
        private BaoEnemyBase cachedBaoEnemy;
        private EnemyPatrol cachedEnemyPatrol;
        private WormMove cachedWormMove;

        private MethodInfo enableAggroMethod;
        private MethodInfo disableAggroMethod;
        private FieldInfo isAggroEnabledField;
        private PropertyInfo isAggroEnabledProp;

        private bool isAggroActive = true;

        private void Awake()
        {
            if (targetEnemyObject == null)
            {
                targetEnemyObject = gameObject;
            }

            if (director == null)
            {
                director = GetComponent<PlayableDirector>() ?? GetComponentInParent<PlayableDirector>();
            }

            ResolveTargetComponent();

            if (disableAggroOnAwake)
            {
                DisableAggro();
            }
        }

        private void Start()
        {
            ResolveTargetComponent();

            if (director != null && director.state == PlayState.Playing)
            {
                DisableAggro();
            }
            else if (disableAggroOnAwake)
            {
                DisableAggro();
            }
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.played += OnTimelinePlayed;
                director.stopped += OnTimelineStopped;
                director.paused += OnTimelinePaused;
            }

            if (disableAggroOnAwake || (director != null && director.state == PlayState.Playing))
            {
                DisableAggro();
            }
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.played -= OnTimelinePlayed;
                director.stopped -= OnTimelineStopped;
                director.paused -= OnTimelinePaused;
            }
        }

        private void Update()
        {
            // Duy trì khóa liên tục trong suốt thời gian Timeline đang phát
            if (director != null && director.state == PlayState.Playing)
            {
                if (isAggroActive)
                {
                    DisableAggro();
                }
            }
        }

        private void OnTimelinePlayed(PlayableDirector pd)
        {
            if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Timeline Played -> Disable Aggro", this);
            DisableAggro();
        }

        private void OnTimelineStopped(PlayableDirector pd)
        {
            if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Timeline Stopped -> Enable Aggro", this);
            EnableAggro();
        }

        private void OnTimelinePaused(PlayableDirector pd)
        {
            // Nếu Timeline kết thúc (time >= duration)
            if (pd != null && pd.duration > 0 && pd.time >= pd.duration - 0.05f)
            {
                EnableAggro();
            }
        }

        /// <summary>
        /// Tìm kiếm component và các Method/Field/Property liên quan đến Aggro
        /// </summary>
        public void ResolveTargetComponent()
        {
            if (targetEnemyObject == null)
            {
                targetEnemyObject = gameObject;
            }

            // 1. Tìm BossController
            cachedBoss = targetEnemyObject.GetComponent<BossController>()
                ?? targetEnemyObject.GetComponentInChildren<BossController>()
                ?? targetEnemyObject.GetComponentInParent<BossController>();

            cachedBossMovement = targetEnemyObject.GetComponent<BossMovement>()
                ?? targetEnemyObject.GetComponentInChildren<BossMovement>()
                ?? targetEnemyObject.GetComponentInParent<BossMovement>();

            if (cachedBoss != null)
            {
                targetComponent = cachedBoss;
                return;
            }

            // 2. Tìm StandaloneFlyingShooterEnemy
            cachedFlyingShooter = targetEnemyObject.GetComponent<StandaloneFlyingShooterEnemy>()
                ?? targetEnemyObject.GetComponentInChildren<StandaloneFlyingShooterEnemy>()
                ?? targetEnemyObject.GetComponentInParent<StandaloneFlyingShooterEnemy>();
            if (cachedFlyingShooter != null)
            {
                targetComponent = cachedFlyingShooter;
                return;
            }

            // 3. Tìm BaoEnemyBase
            cachedBaoEnemy = targetEnemyObject.GetComponent<BaoEnemyBase>()
                ?? targetEnemyObject.GetComponentInChildren<BaoEnemyBase>()
                ?? targetEnemyObject.GetComponentInParent<BaoEnemyBase>();
            if (cachedBaoEnemy != null)
            {
                targetComponent = cachedBaoEnemy;
                return;
            }

            // 4. Tìm EnemyPatrol
            cachedEnemyPatrol = targetEnemyObject.GetComponent<EnemyPatrol>()
                ?? targetEnemyObject.GetComponentInChildren<EnemyPatrol>()
                ?? targetEnemyObject.GetComponentInParent<EnemyPatrol>();
            if (cachedEnemyPatrol != null)
            {
                targetComponent = cachedEnemyPatrol;
                return;
            }

            // 5. Tìm WormMove
            cachedWormMove = targetEnemyObject.GetComponent<WormMove>()
                ?? targetEnemyObject.GetComponentInChildren<WormMove>()
                ?? targetEnemyObject.GetComponentInParent<WormMove>();
            if (cachedWormMove != null)
            {
                targetComponent = cachedWormMove;
                return;
            }

            // 6. Quét Reflection trên tất cả MonoBehaviour (self, children, parent)
            MonoBehaviour[] components = targetEnemyObject.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var comp in components)
            {
                if (comp == null || comp == this) continue;

                var type = comp.GetType();

                var enableMethod = type.GetMethod("EnableAggro", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? type.GetMethod("SetAggroEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var disableMethod = type.GetMethod("DisableAggro", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (enableMethod != null || disableMethod != null)
                {
                    targetComponent = comp;
                    enableAggroMethod = enableMethod;
                    disableAggroMethod = disableMethod;
                    return;
                }

                var aggroField = type.GetField("isAggroEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (aggroField != null)
                {
                    targetComponent = comp;
                    isAggroEnabledField = aggroField;
                    return;
                }

                var aggroProp = type.GetProperty("isAggroEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (aggroProp != null && aggroProp.CanWrite)
                {
                    targetComponent = comp;
                    isAggroEnabledProp = aggroProp;
                    return;
                }
            }
        }

        /// <summary>
        /// Bật lại tính năng tìm Player / Aggro
        /// </summary>
        [ContextMenu("Enable Aggro (Unlock)")]
        public void EnableAggro()
        {
            SetAggroState(true);
        }

        /// <summary>
        /// Tắt tính năng tìm Player / Aggro
        /// </summary>
        [ContextMenu("Disable Aggro (Lock)")]
        public void DisableAggro()
        {
            SetAggroState(false);
        }

        /// <summary>
        /// Khóa Boss (tắt Target và Attack) trong lúc Cutscene
        /// </summary>
        public void LockBoss()
        {
            DisableAggro();
        }

        /// <summary>
        /// Mở khóa Boss khi Cutscene kết thúc
        /// </summary>
        public void UnlockBoss()
        {
            EnableAggro();
        }

        /// <summary>
        /// Thiết lập trạng thái khóa Cutscene
        /// </summary>
        public void SetCutsceneLocked(bool locked)
        {
            SetAggroState(!locked);
        }

        private void SetAggroState(bool state)
        {
            if (targetComponent == null)
            {
                ResolveTargetComponent();
            }

            isAggroActive = state;

            // 1. Trường hợp BossController
            if (cachedBoss != null)
            {
                if (state)
                {
                    cachedBoss.EnableAggro();
                }
                else
                {
                    cachedBoss.DisableAggro();
                    if (cachedBossMovement != null)
                    {
                        cachedBossMovement.StopMovement();
                    }
                }
                if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Set Boss Aggro = {state}", this);
                return;
            }

            // 2. Trường hợp StandaloneFlyingShooterEnemy
            if (cachedFlyingShooter != null)
            {
                if (state)
                {
                    cachedFlyingShooter.EnableAggro();
                }
                else
                {
                    cachedFlyingShooter.DisableAggro();
                }
                if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Set FlyingShooter Aggro = {state}", this);
                return;
            }

            // 3. Trường hợp BaoEnemyBase
            if (cachedBaoEnemy != null)
            {
                if (state)
                {
                    cachedBaoEnemy.EndTimelineControl(null);
                }
                else
                {
                    cachedBaoEnemy.BeginTimelineControl(null, true);
                }
                if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Set BaoEnemy Timeline Control = {!state}", this);
                return;
            }

            // 4. Trường hợp EnemyPatrol
            if (cachedEnemyPatrol != null)
            {
                cachedEnemyPatrol.enabled = state;
                if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Set EnemyPatrol enabled = {state}", this);
                return;
            }

            // 5. Trường hợp WormMove
            if (cachedWormMove != null)
            {
                cachedWormMove.enabled = state;
                if (debugLog) Debug.Log($"[{nameof(GenericEnemyAggroController)}] Set WormMove enabled = {state}", this);
                return;
            }

            // 6. Gọi Method nếu có
            if (state && enableAggroMethod != null)
            {
                var parameters = enableAggroMethod.GetParameters();
                if (parameters.Length == 0)
                {
                    enableAggroMethod.Invoke(targetComponent, null);
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
                {
                    enableAggroMethod.Invoke(targetComponent, new object[] { true });
                }
                return;
            }
            if (!state && disableAggroMethod != null)
            {
                disableAggroMethod.Invoke(targetComponent, null);
                return;
            }

            // 7. Set Field nếu có
            if (isAggroEnabledField != null)
            {
                isAggroEnabledField.SetValue(targetComponent, state);
                return;
            }

            // 8. Set Property nếu có
            if (isAggroEnabledProp != null && isAggroEnabledProp.CanWrite)
            {
                isAggroEnabledProp.SetValue(targetComponent, state, null);
                return;
            }

            if (targetComponent != null)
            {
                Debug.LogWarning($"[{nameof(GenericEnemyAggroController)}] Component {targetComponent.GetType().Name} trên {targetEnemyObject?.name} không hỗ trợ bật/tắt Aggro.", this);
            }
        }
    }
}
