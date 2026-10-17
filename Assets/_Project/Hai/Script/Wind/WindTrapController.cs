using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WindTrapController : MonoBehaviour
{
    [Header("Wind Settings")]
    public ParticleSystem WindParticles;
    public Collider2D WindCollider;
    public float ActiveTime = 10f;
    public float InactiveTime = 1f;
    public float FadeTime = 1f;

    [Header("Push Settings")]
    public float pushForceX = 15f;  // lực đẩy ngang (ra xa)
    public float pushForceY = 12f;  // lực đẩy dọc (lên cao)

    private bool isActive = true;

    void Start()
    {
        StartCoroutine(WindCycle());
    }

    IEnumerator WindCycle()
    {
        while (true)
        {
            isActive = true;
            WindCollider.enabled = true;
            if (WindParticles) WindParticles.Play();
            yield return new WaitForSeconds(ActiveTime);

            isActive = false;
            WindCollider.enabled = false;
            if (WindParticles) WindParticles.Stop();
            yield return new WaitForSeconds(InactiveTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;

        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float dirX = other.transform.position.x > transform.position.x ? 1f : -1f;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(dirX * pushForceX, pushForceY), ForceMode2D.Impulse);

            // Disable player controller tạm thời
            StartCoroutine(DisablePlayerControl(other.gameObject));
        }
    }

    IEnumerator DisablePlayerControl(GameObject player)
    {
        // Tên script player controller của bạn là gì? Thay vào đây
        var playerScript = player.GetComponent<test>();
        if (playerScript != null)
        {
            playerScript.enabled = false;
            yield return new WaitForSeconds(0.5f); // disable 0.5s
            playerScript.enabled = true;
        }
    }
}