using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RhythmNote : MonoBehaviour
{
    [Header("Hit FX")]
    [SerializeField] private float hitDestroyDelay = 0.08f;
    [SerializeField] private float hitFlashDuration = 0.05f;
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private Vector3 hitScale = new Vector3(1.15f, 1.15f, 1f);

    public LaneType Lane { get; private set; }
    public float HitTime { get; private set; }
    public bool IsJudged { get; private set; }

    private RhythmGameManager gameManager;
    private Vector3 spawnPosition;
    private Vector3 hitPosition;
    private float spawnTime;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalScale;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (gameManager == null || IsJudged)
        {
            return;
        }

        float songTime = gameManager.CurrentSongTime;

        // Di chuyển note dựa hoàn toàn vào thời gian bài hát, không phụ thuộc frame rate.
        if (songTime <= spawnTime)
        {
            transform.position = spawnPosition;
        }
        else
        {
            float t = Mathf.InverseLerp(spawnTime, HitTime, songTime);
            transform.position = Vector3.Lerp(spawnPosition, hitPosition, t);
        }

        if (songTime > HitTime + gameManager.MissWindow)
        {
            gameManager.NotifyAutoMiss(this);
        }
    }

    public void Initialize(RhythmGameManager manager, NoteData data, Vector3 spawnPos, Vector3 targetPos, float currentSongTime)
    {
        gameManager = manager;
        Lane = data.lane;
        HitTime = data.hitTime;
        spawnPosition = spawnPos;
        hitPosition = targetPos;
        spawnTime = HitTime - manager.SpawnLeadTime;
        IsJudged = false;

        transform.position = currentSongTime <= spawnTime ? spawnPosition : Vector3.Lerp(spawnPosition, hitPosition, Mathf.InverseLerp(spawnTime, HitTime, currentSongTime));
    }

    public void MarkHit()
    {
        if (IsJudged)
        {
            return;
        }

        IsJudged = true;
        StartCoroutine(HitRoutine());
    }

    public void MarkMissed()
    {
        if (IsJudged)
        {
            return;
        }

        IsJudged = true;
        StartCoroutine(MissRoutine());
    }

    private IEnumerator HitRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
        }

        transform.localScale = hitScale;
        transform.position = hitPosition;
        yield return new WaitForSeconds(hitFlashDuration);
        Destroy(gameObject, hitDestroyDelay);
    }

    private IEnumerator MissRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.35f);
        }

        transform.localScale = originalScale * 0.9f;
        yield return new WaitForSeconds(0.05f);
        Destroy(gameObject);
    }
}
