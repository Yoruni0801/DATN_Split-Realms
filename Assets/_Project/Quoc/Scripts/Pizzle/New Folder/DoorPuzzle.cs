using UnityEngine;
using System.Collections;

public class DoorPuzzle : MonoBehaviour
{
    public GameObject puzzleUI;

    [Header("Door Move")]
    public Transform targetPoint;
    public float moveSpeed = 2f;

    private bool playerNear;
    private bool opened = false;

    void Update()
    {
        if (opened) return;

        // Nhấn E
        if (playerNear && Input.GetKeyDown(KeyCode.G))
        {
            puzzleUI.SetActive(true);
            Time.timeScale = 0f;        
        }
    }

    // 🔥 PuzzleManager gọi hàm này
    public void OpenDoor()
    {
        if (opened) return;

        opened = true;

        StartCoroutine(MoveDoor());
    }

    IEnumerator MoveDoor()
    {
        while (Mathf.Abs(transform.position.y - targetPoint.position.y) > 0.01f)
        {
            Vector3 newPos = transform.position;

            // 👉 chỉ move theo Y
            newPos.y = Mathf.MoveTowards(
                transform.position.y,
                targetPoint.position.y,
                moveSpeed * Time.deltaTime
            );

            transform.position = newPos;

            yield return null;
        }

        // set chính xác vị trí cuối
        Vector3 finalPos = transform.position;
        finalPos.y = targetPoint.position.y;

        transform.position = finalPos;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
        }
    }
}