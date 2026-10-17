using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gắn lên GameObject cần gạt.
/// - Cần một Collider2D isTrigger làm vùng tương tác (có thể là collider riêng hoặc tự tạo).
/// - Khi Fairy bay vào vùng và người chơi nhấn F → lever xoay từ A → B (hoặc B → A nếu canToggle).
/// - Gắn spriteTarget = Transform của phần cần gạt (có thể là child riêng).
/// </summary>
public class LeverInteractable : MonoBehaviour
{
    [Header("Visual — Transform cần xoay")]
    [Tooltip("Transform của sprite cần gạt. Để trống = dùng transform của chính object này.")]
    [SerializeField] private Transform leverPivot;

    [Header("Góc xoay")]
    [SerializeField] private float angleOff = 0f;    // Góc khi chưa kéo
    [SerializeField] private float angleOn  = 60f;   // Góc khi đã kéo
    [SerializeField] private float rotateDuration = 0.35f;
    [SerializeField] private AnimationCurve rotateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Hành vi")]
    [Tooltip("Cho phép kéo ngược lại lần 2 (toggle). Bỏ tick = chỉ kéo 1 chiều.")]
    [SerializeField] private bool canToggle = true;

    [Header("Events")]
    public UnityEvent OnLeverActivated;   // Kéo từ Off → On
    public UnityEvent OnLeverDeactivated; // Kéo từ On → Off (chỉ khi canToggle)

    // ── Runtime ──────────────────────────────────────────────────────────────
    private bool _isOn;
    private bool _isBusy;
    private FairyPlayerCompanionController _fairyInRange;

    public bool IsBusy => _isBusy;

    private void Awake()
    {
        if (leverPivot == null) leverPivot = transform;

        // Đặt góc ban đầu
        SetAngleImmediate(angleOff);
    }

    private void Update()
    {
        if (_fairyInRange == null || _isBusy) return;
        if (!Input.GetKeyDown(KeyCode.F)) return;

        if (!_isOn)
        {
            StartCoroutine(RotateTo(angleOn, activate: true));
        }
        else if (canToggle)
        {
            StartCoroutine(RotateTo(angleOff, activate: false));
        }
    }

    // ── Trigger — detect Fairy ────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        var f = other.GetComponentInParent<FairyPlayerCompanionController>();
        if (f != null)
        {
            _fairyInRange = f;
            f.SetCurrentLever(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var f = other.GetComponentInParent<FairyPlayerCompanionController>();
        if (f != null && _fairyInRange == f)
        {
            _fairyInRange = null;
            f.SetCurrentLever(null);
        }
    }

    // ── Rotate coroutine ─────────────────────────────────────────────────
    private IEnumerator RotateTo(float targetAngle, bool activate)
    {
        _isBusy = true;

        float startAngle = leverPivot.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;

        float elapsed = 0f;
        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = rotateCurve.Evaluate(Mathf.Clamp01(elapsed / rotateDuration));
            float angle = Mathf.LerpAngle(startAngle, targetAngle, t);
            leverPivot.localEulerAngles = new Vector3(
                leverPivot.localEulerAngles.x,
                leverPivot.localEulerAngles.y,
                angle);
            yield return null;
        }

        SetAngleImmediate(targetAngle);
        _isOn = activate;
        _isBusy = false;

        if (activate)
        {
            OnLeverActivated?.Invoke();
            if (_fairyInRange != null)
            {
                var possession = _fairyInRange.GetComponentInParent<FairyPossessionController>();
                if (possession != null)
                {
                    possession.ReturnToPlayer();
                }

                // Kéo xong rồi thì báo fairy hết tương tác
                _fairyInRange.SetCurrentLever(null);
                _fairyInRange = null;
            }
        }
        else
        {
            OnLeverDeactivated?.Invoke();
        }
    }

    private void SetAngleImmediate(float angle)
    {
        Vector3 e = leverPivot.localEulerAngles;
        e.z = angle;
        leverPivot.localEulerAngles = e;
    }

    // ── API công khai ─────────────────────────────────────────────────────
    /// <summary>Kéo lever về On từ code (không cần Fairy/F).</summary>
    public void ActivateImmediate()
    {
        StopAllCoroutines();
        _isBusy = false;
        _isOn = true;
        SetAngleImmediate(angleOn);
        OnLeverActivated?.Invoke();
    }

    public bool IsOn => _isOn;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Preview vùng trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
