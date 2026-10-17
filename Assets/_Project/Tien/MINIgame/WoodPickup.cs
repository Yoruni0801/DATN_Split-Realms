using UnityEngine;

public class WoodPickup : MonoBehaviour
{
    public float interactionRadius = 2f;
    public GameObject interactUI;

    private SpriteRenderer spriteRenderer;
    private bool isPlayerNear = false;
    private Transform playerTransform;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
        
        if (interactUI != null)
        {
            interactUI.SetActive(false);
        }
        
        // Cần đảm bảo có Collider2D để kiểm tra khoảng cách hoặc trigger
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = interactionRadius;
        }
    }

    void Update()
    {
        bool isClose = IsPlayerCloseEnough();

        if (isClose)
        {
            if (interactUI != null && !interactUI.activeSelf)
            {
                interactUI.SetActive(true);
            }
        }
        else
        {
            if (interactUI != null && interactUI.activeSelf)
            {
                interactUI.SetActive(false);
            }
        }
    }

    private bool IsPlayerCloseEnough()
    {
        if (isPlayerNear)
        {
            return true;
        }

        return playerTransform != null
            && Vector2.Distance(transform.position, playerTransform.position) <= interactionRadius;
    }

    private bool IsPlayer(Collider2D col)
    {
        if (col == null) return false;
        return col.CompareTag("Player") || col.GetComponentInParent<Player>() != null;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other)) isPlayerNear = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other)) isPlayerNear = false;
    }
}
