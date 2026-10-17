using UnityEngine;
using UnityEngine.UI;

public class Tile : MonoBehaviour
{
    public int correctIndex; // vị trí đúng

    private PuzzleManagerr manager;

    void Start()
    {
        manager = FindFirstObjectByType<PuzzleManagerr>();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        manager.TryMove(this);
    }
}
