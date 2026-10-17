using UnityEngine;
using System.Collections;

public class WormMove : MonoBehaviour
{
    [Header("Detect Player")]
    public float detectRadius = 3f;
    public Vector3 detectOffset; // 👈 chỉnh X Y trong Inspector

    [Header("Move")]
    public float upHeight = 2f;
    public float moveSpeed = 3f;

    [Header("Timing")]
    public float stayTime = 2f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool isMoving = false;

    void Start()
    {
        startPos = transform.position;
        targetPos = startPos + Vector3.up * upHeight;
    }

    void Update()
    {
        if (!isMoving && PlayerInRange())
        {
            StartCoroutine(Cycle());
        }
    }

    // ✅ Detect bằng TAG + OFFSET
    bool PlayerInRange()
    {
        Vector2 detectPos = transform.position + detectOffset;

        Collider2D[] hits = Physics2D.OverlapCircleAll(detectPos, detectRadius);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                return true;
            }
        }

        return false;
    }

    IEnumerator Cycle()
    {
        isMoving = true;

        // ⬆️ đi lên
        yield return MoveTo(targetPos);

        // 🟢 đứng trên
        yield return new WaitForSeconds(stayTime);

        // ⬇️ đi xuống
        yield return MoveTo(startPos);

        isMoving = false;
    }

    IEnumerator MoveTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.position = target;
    }

    // 🎯 Vẽ vùng detect đúng vị trí offset
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 detectPos = transform.position + detectOffset;
        Gizmos.DrawWireSphere(detectPos, detectRadius);
    }
}