using UnityEngine;
using System.Collections;
using UnityEngine.U2D;

public class CrumblingPlatform : MonoBehaviour
{
    [Header("Cấu hình Rung & Vỡ")]
    [Tooltip("Thời gian rung (giây) trước khi sàn vỡ hoàn toàn")]
    public float shakeDelay = 0.6f;
    [Tooltip("Thời gian (giây) chờ để sàn tự hồi phục lại (đặt <= 0 nếu không muốn hồi phục)")]
    public float respawnDelay = 4f;

    [Header("Hiệu ứng Rung")]
    public float shakeMagnitude = 0.08f;
    public float shakeSpeed = 35f;

    [Header("Mảnh Vỡ Debris")]
    public GameObject debrisPrefab;    // Prefab mảnh đá/băng nhỏ văng ra
    public int debrisCount = 5;
    public float debrisEjectForce = 4f;

    private Vector3 originPos;
    private SpriteRenderer sr;
    private SpriteShapeRenderer ssr;
    private Collider2D col;
    private bool triggered = false;
    private Coroutine crumbleCoroutine;

    private void Awake()
    {
        originPos = transform.position;
        sr = GetComponent<SpriteRenderer>();
        ssr = GetComponent<SpriteShapeRenderer>();
        col = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        // Khôi phục trạng thái nguyên vẹn khi bật lại GameObject/World
        transform.position = originPos;
        SetRendererEnabled(true);
        if (col != null) col.enabled = true;
        triggered = false;
    }

    private void OnDisable()
    {
        if (crumbleCoroutine != null)
        {
            StopCoroutine(crumbleCoroutine);
            crumbleCoroutine = null;
        }
        transform.position = originPos;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckTrigger(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckTrigger(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckTrigger(other.gameObject);
    }

    private void CheckTrigger(GameObject obj)
    {
        if (triggered) return;

        Player activePlayer = PlayerRegistry.Current;
        bool isPlayer = (activePlayer != null && (obj == activePlayer.gameObject || obj.transform.IsChildOf(activePlayer.transform)))
                        || obj.CompareTag("Player");

        if (isPlayer)
        {
            triggered = true;
            StartCoroutine(CrumbleSequence());
        }
    }

    private IEnumerator CrumbleSequence()
    {
        // 1. Rung rinh lắc ngang báo hiệu chuẩn bị sập sàn
        float t = 0f;
        while (t < shakeDelay)
        {
            float offsetX = Mathf.Sin(Time.time * shakeSpeed) * shakeMagnitude;
            transform.position = originPos + new Vector3(offsetX, 0, 0);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = originPos;

        // 2. Sinh ra các mảnh vỡ & Tắt Collider/Renderer để Player lọt hố
        SpawnDebris();
        SetRendererEnabled(false);
        if (col != null) col.enabled = false;

        // 3. Nếu đặt respawnDelay <= 0 thì vỡ vĩnh viễn không hồi lại
        if (respawnDelay <= 0) yield break;

        // 4. Chờ thời gian hồi phục
        yield return new WaitForSeconds(respawnDelay);

        // 5. Hiện mờ lại từ từ và bật lại Collider
        yield return StartCoroutine(FadeIn());

        if (col != null) col.enabled = true;
        triggered = false;
    }

    private void SpawnDebris()
    {
        if (debrisPrefab == null) return;

        Bounds bounds = GetPlatformBounds();

        for (int i = 0; i < debrisCount; i++)
        {
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float spawnY = bounds.max.y;
            Vector3 spawnPos = new Vector3(randomX, spawnY, transform.position.z);

            GameObject d = Instantiate(debrisPrefab, spawnPos, Quaternion.Euler(0, 0, Random.Range(0f, 360f)));

            Rigidbody2D rb = d.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 force = new Vector2(Random.Range(-3f, 3f), Random.Range(1f, debrisEjectForce));
                rb.AddForce(force, ForceMode2D.Impulse);
                rb.AddTorque(Random.Range(-10f, 10f), ForceMode2D.Impulse);
            }
            Destroy(d, 2f);
        }
    }

    private Bounds GetPlatformBounds()
    {
        if (sr != null) return sr.bounds;
        if (ssr != null) return ssr.bounds;
        if (col != null) return col.bounds;
        return new Bounds(transform.position, Vector3.one);
    }

    private void SetRendererEnabled(bool state)
    {
        if (sr != null) sr.enabled = state;
        if (ssr != null) ssr.enabled = state;
    }

    private IEnumerator FadeIn()
    {
        SetRendererEnabled(true);

        Color targetColor = Color.white;
        if (sr != null) targetColor = sr.color;

        float alpha = 0f;
        while (alpha < 1f)
        {
            alpha += Time.deltaTime * 2.5f;
            Color c = new Color(targetColor.r, targetColor.g, targetColor.b, Mathf.Clamp01(alpha));
            if (sr != null) sr.color = c;
            if (ssr != null) ssr.color = c;
            yield return null;
        }

        if (sr != null) sr.color = targetColor;
        if (ssr != null) ssr.color = targetColor;
    }
}