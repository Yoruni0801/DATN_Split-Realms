using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(Collider2D))]
public class ReloadSceneOnPlayerStep2D : MonoBehaviour
{
    private enum ResetMode
    {
        ReloadActiveScene,
        LoadFirstBuildScene,
        LoadSceneByName
    }

    [SerializeField] private string playerTag = "Player";
    [SerializeField] private ResetMode resetMode = ResetMode.ReloadActiveScene;
    [SerializeField] private string sceneName;
    [SerializeField] private float deathAnimationFallbackDelay = 1.5f;
    [SerializeField] private float deathFallGravityScale = 1f;
    [SerializeField] private Vector2 deathFallVelocity = new Vector2(0f, -5f);

    private bool isTriggered;

    private void Awake()
    {
        isTriggered = false;
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnEnable()
    {
        isTriggered = false;
        PlayerRespawnController.RespawnRequested += HandleRespawn;
    }

    private void OnDisable()
    {
        isTriggered = false;
        PlayerRespawnController.RespawnRequested -= HandleRespawn;
    }

    private void HandleRespawn(PlayerRespawnController controller)
    {
        isTriggered = false;
    }

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered || !IsPlayer(other))
        {
            return;
        }

        isTriggered = true;
        StartCoroutine(ResetAfterPlayerDeathRoutine(other));
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isTriggered || !IsPlayer(other))
        {
            return;
        }

        isTriggered = true;
        StartCoroutine(ResetAfterPlayerDeathRoutine(other));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isTriggered || !IsPlayer(collision.collider))
        {
            return;
        }

        isTriggered = true;
        StartCoroutine(ResetAfterPlayerDeathRoutine(collision.collider));
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(playerTag) && (other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag)))
        {
            return true;
        }

        return other.GetComponent<Player>() != null
            || other.GetComponentInParent<Player>() != null
            || other.GetComponent<PlayerHealth>() != null
            || other.GetComponentInParent<PlayerHealth>() != null;
    }

    private IEnumerator ResetAfterPlayerDeathRoutine(Collider2D playerCollider)
    {
        Transform playerRoot = ResolvePlayerRoot(playerCollider);
        PlayerHealth playerHealth = playerRoot != null ? playerRoot.GetComponent<PlayerHealth>() : null;
        AnimationSpine animationSpine = playerRoot != null ? playerRoot.GetComponent<AnimationSpine>() : null;

        if (playerRoot == null || playerHealth == null || playerHealth.IsDead)
        {
            isTriggered = false;
            yield break;
        }

        try
        {
            PreparePlayerFall(playerRoot);
            playerHealth.SetAutoRespawn(false);

            playerHealth.KillByFall();
            ApplyDeathFallPhysics(playerRoot);

            // Chờ animation chết kết thúc và dừng lại để hiển thị UI nút chơi lại
            // Không tự động hồi sinh người chơi, việc hồi sinh do PlayerRespawnController & UI nút chơi lại điều khiển
            yield return null;
        }
        finally
        {
            // Cho phép nhận lại trigger sau khi người chơi bấm nút chơi lại
        }
    }

    private Transform ResolvePlayerRoot(Collider2D playerCollider)
    {
        if (playerCollider == null)
        {
            return null;
        }

        Player player = playerCollider.GetComponent<Player>() ?? playerCollider.GetComponentInParent<Player>();
        if (player != null)
        {
            return player.transform;
        }

        if (playerCollider.attachedRigidbody != null && playerCollider.attachedRigidbody.CompareTag(playerTag))
        {
            return playerCollider.attachedRigidbody.transform;
        }

        return playerCollider.transform.root.CompareTag(playerTag)
            ? playerCollider.transform.root
            : playerCollider.transform;
    }

    private void PreparePlayerFall(Transform playerRoot)
    {
        Player player = playerRoot.GetComponent<Player>();
        if (player != null)
        {
            player.SetInputActive(false);
            if (!Tien.Minigame.MinigameCheckpointState.IsInMinigame)
            {
                player.SetRotaMode(false);
            }
        }
    }

    private void ApplyDeathFallPhysics(Transform playerRoot)
    {
        Rigidbody2D playerRigidbody = playerRoot.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.simulated = true;
        playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
        playerRigidbody.gravityScale = deathFallGravityScale;
        playerRigidbody.linearVelocity = deathFallVelocity;
        playerRigidbody.angularVelocity = 0f;
    }
}
