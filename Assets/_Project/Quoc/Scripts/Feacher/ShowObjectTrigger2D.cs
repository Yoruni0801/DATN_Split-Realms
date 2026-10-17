using UnityEngine;

public class ShowObjectTrigger2D : MonoBehaviour
{
    [SerializeField] private GameObject objectToShow;
    [SerializeField] private string playerTag = "Lara";

    private void Start()
    {
        if (objectToShow != null)
        {
            objectToShow.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.transform.root.CompareTag(playerTag))
            return;

        if (objectToShow != null)
        {
            objectToShow.SetActive(true);
        }
    }
}