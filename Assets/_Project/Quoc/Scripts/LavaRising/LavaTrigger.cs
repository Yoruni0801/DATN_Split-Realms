using UnityEngine;

public class LavaTrigger : MonoBehaviour
{
    public LavaRise lava;

    private bool triggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            triggered = true;

            lava.StartRising();

            Debug.Log("Lava Rising!");
        }
    }
}