using UnityEngine;

public class HideObjectTrigger2D : MonoBehaviour
{
    [SerializeField] private GameObject objectToHide;
    [SerializeField] private string playerTag = "Lara";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.transform.root.CompareTag(playerTag))
            return;

        if (objectToHide != null)
        {
            objectToHide.SetActive(false);
        }
    }
}