using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class LaserMirrorPressureSwitch2D : MonoBehaviour
{
    public enum SwitchFunctionality
    {
        RotateCrystal,
        PowerLaserBeam,
        RotateAndPower
    }

    [Header("Switch Mode")]
    [SerializeField] private SwitchFunctionality functionality = SwitchFunctionality.RotateAndPower;
    [SerializeField] private bool holdToKeepActive = false;

    [Header("Target")]
    [SerializeField] private LaserMirrorCrystal2D controlledCrystal;
    [SerializeField] private LaserMirrorPuzzle2D puzzle;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private bool rotateOnlyOnEnter = true;
    [SerializeField, Min(0.05f)] private float retriggerDelay = 0.25f;

    [Header("Visual Elements")]
    [SerializeField] private Transform pressVisual;
    [SerializeField] private SpriteRenderer switchRenderer;
    [SerializeField] private Sprite switchOffSprite;
    [SerializeField] private Sprite switchOnSprite;
    [SerializeField] private Vector3 pressedOffset = new Vector3(0f, -0.08f, 0f);

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isPressedBool = "IsPressed";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressClip;

    private Vector3 pressVisualStartPosition;
    private float nextTriggerTime;
    private int contacts;

    public void Configure(
        LaserMirrorPuzzle2D newPuzzle,
        LaserMirrorCrystal2D newControlledCrystal,
        Sprite offSprite = null,
        Sprite onSprite = null)
    {
        puzzle = newPuzzle;
        controlledCrystal = newControlledCrystal;
        if (offSprite != null) switchOffSprite = offSprite;
        if (onSprite != null) switchOnSprite = onSprite;
        CacheVisual();
        UpdateVisuals(false);
    }

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        CacheVisual();
        UpdateVisuals(false);
    }

    private void CacheVisual()
    {
        if (pressVisual == null)
        {
            Transform found = transform.Find("Visual");
            pressVisual = found != null ? found : transform;
        }

        if (switchRenderer == null && pressVisual != null)
        {
            switchRenderer = pressVisual.GetComponent<SpriteRenderer>();
            if (switchRenderer == null)
            {
                switchRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (pressVisual != null)
        {
            pressVisualStartPosition = pressVisual.localPosition;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        contacts++;
        SetPressed(true);

        if (functionality == SwitchFunctionality.PowerLaserBeam || functionality == SwitchFunctionality.RotateAndPower)
        {
            if (puzzle != null && !puzzle.IsBeamActive)
            {
                puzzle.SetBeamActive(true);
            }
        }

        if (rotateOnlyOnEnter && (functionality == SwitchFunctionality.RotateCrystal || functionality == SwitchFunctionality.RotateAndPower))
        {
            TryRotate();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (rotateOnlyOnEnter || !IsPlayer(other))
        {
            return;
        }

        if (functionality == SwitchFunctionality.RotateCrystal || functionality == SwitchFunctionality.RotateAndPower)
        {
            TryRotate();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        contacts = Mathf.Max(0, contacts - 1);
        if (contacts == 0)
        {
            SetPressed(false);
        }
    }

    private void TryRotate()
    {
        if (Time.time < nextTriggerTime)
        {
            return;
        }

        nextTriggerTime = Time.time + retriggerDelay;

        if (controlledCrystal != null)
        {
            controlledCrystal.RotateStep();
        }

        if (puzzle != null)
        {
            puzzle.RebuildBeam();
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        Transform root = other.transform.root;
        int playerLayer = LayerMask.NameToLayer(playerLayerName);

        return SafeCompareTag(other.gameObject, playerTag)
            || (other.attachedRigidbody != null && SafeCompareTag(other.attachedRigidbody.gameObject, playerTag))
            || (root != null && SafeCompareTag(root.gameObject, playerTag))
            || (playerLayer >= 0 && (other.gameObject.layer == playerLayer || (root != null && root.gameObject.layer == playerLayer)))
            || other.GetComponentInParent<Player>() != null;
    }

    private static bool SafeCompareTag(GameObject target, string tagName)
    {
        if (target == null || string.IsNullOrEmpty(tagName))
        {
            return false;
        }

        try
        {
            return target.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void SetPressed(bool pressed)
    {
        if (pressed && audioSource != null && pressClip != null)
        {
            audioSource.PlayOneShot(pressClip);
        }

        if (animator != null && !string.IsNullOrEmpty(isPressedBool))
        {
            animator.SetBool(isPressedBool, pressed);
        }
        else if (pressVisual != null)
        {
            pressVisual.localPosition = pressVisualStartPosition + (pressed ? pressedOffset : Vector3.zero);
        }

        UpdateVisuals(pressed);
    }

    private void UpdateVisuals(bool pressed)
    {
        if (switchRenderer != null)
        {
            if (pressed)
            {
                if (switchOnSprite != null) switchRenderer.sprite = switchOnSprite;
            }
            else
            {
                if (switchOffSprite != null) switchRenderer.sprite = switchOffSprite;
            }
        }
    }
}
