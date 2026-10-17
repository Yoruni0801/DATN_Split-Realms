using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(HingeJoint2D))]
public class HingePlatform : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Thời gian chạm")]
    [Tooltip("Player chạm platform bao lâu thì kích hoạt")]
    [SerializeField] private float standTime = 3f;

    [Header("Upper Angle")]
    [Tooltip("Upper Angle ban đầu")]
    [SerializeField] private float startUpperAngle = 0f;

    [Tooltip("Upper Angle sau cùng")]
    [SerializeField] private float targetUpperAngle = -90f;

    [Header("Lower Angle")]
    [Tooltip("Lower Angle ban đầu")]
    [SerializeField] private float startLowerAngle = 0f;

    [Tooltip("Lower Angle sau cùng")]
    [SerializeField] private float targetLowerAngle = -90f;

    [Header("Thời gian thay đổi")]
    [Tooltip("Thời gian chuyển từ góc ban đầu tới góc đích")]
    [SerializeField] private float changeDuration = 2f;

    [Header("Quay lại ban đầu")]
    [Tooltip("Sau khi tới góc đích, chờ bao lâu trước khi quay lại")]
    [SerializeField] private float returnDelay = 3f;

    [Tooltip("Thời gian để quay về góc ban đầu")]
    [SerializeField] private float returnDuration = 2f;

    private HingeJoint2D hinge;

    // Lưu các collider của Player đang chạm platform
    private readonly HashSet<Collider2D> touchingPlayers
        = new HashSet<Collider2D>();

    private float standTimer;
    private float changeTimer;
    private float returnTimer;

    private PlatformState state = PlatformState.Waiting;

    private bool PlayerTouching => touchingPlayers.Count > 0;

    private enum PlatformState
    {
        Waiting,
        MovingToTarget,
        WaitingToReturn,
        Returning
    }

    private void Awake()
    {
        hinge = GetComponent<HingeJoint2D>();

        hinge.useLimits = true;

        SetLimits(startLowerAngle, startUpperAngle);
    }

    private void Update()
    {
        switch (state)
        {
            case PlatformState.Waiting:
                UpdateWaiting();
                break;

            case PlatformState.MovingToTarget:
                UpdateMovingToTarget();
                break;

            case PlatformState.WaitingToReturn:
                UpdateWaitingToReturn();
                break;

            case PlatformState.Returning:
                UpdateReturning();
                break;
        }
    }

    // =========================
    // PLAYER CHẠM LÀ ĐẾM NGAY
    // =========================
    private void UpdateWaiting()
    {
        if (PlayerTouching)
        {
            standTimer += Time.deltaTime;

            if (standTimer >= standTime)
            {
                standTimer = 0f;
                changeTimer = 0f;

                state = PlatformState.MovingToTarget;
            }
        }
        else
        {
            // Player rời khỏi trước khi đủ thời gian
            standTimer = 0f;
        }
    }

    // =========================
    // GÓC ĐẦU -> GÓC ĐÍCH
    // =========================
    private void UpdateMovingToTarget()
    {
        changeTimer += Time.deltaTime;

        float duration = Mathf.Max(0.01f, changeDuration);
        float t = Mathf.Clamp01(changeTimer / duration);

        t = Mathf.SmoothStep(0f, 1f, t);

        float currentLowerAngle = Mathf.Lerp(
            startLowerAngle,
            targetLowerAngle,
            t
        );

        float currentUpperAngle = Mathf.Lerp(
            startUpperAngle,
            targetUpperAngle,
            t
        );

        SetLimits(currentLowerAngle, currentUpperAngle);

        if (changeTimer >= duration)
        {
            SetLimits(targetLowerAngle, targetUpperAngle);

            returnTimer = 0f;

            state = PlatformState.WaitingToReturn;
        }
    }

    // =========================
    // CHỜ TRƯỚC KHI QUAY LẠI
    // =========================
    private void UpdateWaitingToReturn()
    {
        returnTimer += Time.deltaTime;

        if (returnTimer >= returnDelay)
        {
            returnTimer = 0f;
            changeTimer = 0f;

            state = PlatformState.Returning;
        }
    }

    // =========================
    // GÓC ĐÍCH -> GÓC BAN ĐẦU
    // =========================
    private void UpdateReturning()
    {
        changeTimer += Time.deltaTime;

        float duration = Mathf.Max(0.01f, returnDuration);
        float t = Mathf.Clamp01(changeTimer / duration);

        t = Mathf.SmoothStep(0f, 1f, t);

        float currentLowerAngle = Mathf.Lerp(
            targetLowerAngle,
            startLowerAngle,
            t
        );

        float currentUpperAngle = Mathf.Lerp(
            targetUpperAngle,
            startUpperAngle,
            t
        );

        SetLimits(currentLowerAngle, currentUpperAngle);

        if (changeTimer >= duration)
        {
            SetLimits(startLowerAngle, startUpperAngle);

            standTimer = 0f;
            changeTimer = 0f;
            returnTimer = 0f;

            state = PlatformState.Waiting;
        }
    }

    // =========================
    // SET HINGE LIMIT
    // =========================
    private void SetLimits(float lower, float upper)
    {
        JointAngleLimits2D limits = hinge.limits;

        limits.min = lower;
        limits.max = upper;

        hinge.limits = limits;
    }

    // =========================
    // PLAYER CHẠM PLATFORM
    // =========================
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag))
            return;

        // Vừa chạm là nhận ngay
        touchingPlayers.Add(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag))
            return;

        touchingPlayers.Add(collision.collider);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag))
            return;

        touchingPlayers.Remove(collision.collider);
    }
}