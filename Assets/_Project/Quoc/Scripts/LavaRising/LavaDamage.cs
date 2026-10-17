using UnityEngine;

public class LavaDamage : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player Dead");

            // Gọi hàm chết ở đây
            // other.GetComponent<PlayerHealth>().Die();
        }
    }
}