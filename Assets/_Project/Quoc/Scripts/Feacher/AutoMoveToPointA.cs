using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class AutoMoveToPointA : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private Transform pointA;
    [SerializeField] private PlayableDirector timelineCutscene;
    [SerializeField] private GameObject cutsceneObject;

    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private float stopDistance = 0.08f;
    [SerializeField] private float moveSpeed = 4f;

    private Coroutine moveCoroutine;
    private Coroutine cutsceneCoroutine;
    private Rigidbody2D playerRb;

    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
        }

        CachePlayerRigidbody();
    }

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (player == null || pointA == null)
        {
            Debug.LogWarning("[AutoMoveToPointA] Missing Player or Point A.");
            return;
        }

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        moveCoroutine = StartCoroutine(MoveToPointA());
    }

    private IEnumerator MoveToPointA()
    {
        CachePlayerRigidbody();

        player.SetControlLocked(true);

        while (player != null
               && pointA != null
               && Mathf.Abs(player.transform.position.x - pointA.position.x) > stopDistance)
        {
            float direction = Mathf.Sign(pointA.position.x - player.transform.position.x);

            if (playerRb != null)
            {
                playerRb.linearVelocity = new Vector2(direction * moveSpeed, playerRb.linearVelocity.y);
            }
            else
            {
                player.transform.position = Vector3.MoveTowards(
                    player.transform.position,
                    new Vector3(pointA.position.x, player.transform.position.y, player.transform.position.z),
                    moveSpeed * Time.fixedDeltaTime);
            }

            yield return new WaitForFixedUpdate();
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector2(0f, playerRb.linearVelocity.y);
        }

        if (timelineCutscene != null)
        {
            timelineCutscene.Stop();
            timelineCutscene.time = 0;
            timelineCutscene.Play();

            if (cutsceneCoroutine != null)
            {
                StopCoroutine(cutsceneCoroutine);
            }

            cutsceneCoroutine = StartCoroutine(UnlockPlayerWhenCutsceneFinished());
        }
        else
        {
            player.SetControlLocked(false);
        }

        moveCoroutine = null;
    }

    private void CachePlayerRigidbody()
    {
        if (player == null)
        {
            playerRb = null;
            return;
        }

        if (playerRb == null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
        }
    }

    private void OnDisable()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        if (cutsceneCoroutine != null)
        {
            StopCoroutine(cutsceneCoroutine);
            cutsceneCoroutine = null;
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }

        if (player != null)
        {
            player.SetControlLocked(false);
        }
    }

    private IEnumerator UnlockPlayerWhenCutsceneFinished()
    {
        yield return null;

        while (timelineCutscene != null && timelineCutscene.state == PlayState.Playing)
        {
            yield return null;
        }

        if (player != null)
        {
            player.SetControlLocked(false);
        }

        GameObject target = cutsceneObject != null ? cutsceneObject : (timelineCutscene != null ? timelineCutscene.gameObject : null);
        if (target != null)
        {
            target.SetActive(false);
        }

        cutsceneCoroutine = null;
    }
}
