using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PuzzleManagerr : MonoBehaviour
{
    public Transform board;
    public GameObject tilePrefab;
    public Sprite[] sprites;
    public GameObject puzzleUI;
    public DoorPuzzle door;

    [HideInInspector]
    public bool isSolved = false;

    private int gridSize = 3;
    private int emptyIndex;

    void Start()
    {
        CreateTiles();
        Shuffle();
    }

    void CreateTiles()
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            GameObject obj = Instantiate(tilePrefab, board);

            Image img = obj.GetComponent<Image>();
            img.sprite = sprites[i];

            obj.transform.SetSiblingIndex(i);

            // 🔥 gán vị trí đúng
            Tile tile = obj.GetComponent<Tile>();
            tile.correctIndex = i;
        }

        emptyIndex = sprites.Length - 1;

        // 👉 ô trống (chỉ ẩn, không disable)
        Transform emptyTile = board.GetChild(emptyIndex);

        emptyTile.GetComponent<Image>().enabled = false;
        emptyTile.GetComponent<Button>().interactable = false;
    }

    void Shuffle()
    {
        for (int i = 0; i < 100; i++)
        {
            List<int> neighbors = GetNeighbors(emptyIndex);
            int rand = neighbors[Random.Range(0, neighbors.Count)];

            Swap(rand, emptyIndex);
            emptyIndex = rand;
        }
    }

    public void TryMove(Tile tile)
    {
        int tileIndex = tile.transform.GetSiblingIndex();

        if (IsNeighbor(tileIndex, emptyIndex))
        {
            Swap(tileIndex, emptyIndex);

            emptyIndex = tileIndex;

            // 🔥 CHECK WIN
            if (CheckWin())
            {
                OnWin();
            }
        }
    }

    void Swap(int indexA, int indexB)
    {
        Transform tileA = board.GetChild(indexA);
        Transform tileB = board.GetChild(indexB);

        tileA.SetSiblingIndex(indexB);
        tileB.SetSiblingIndex(indexA);
    }

    bool IsNeighbor(int a, int b)
    {
        int rowA = a / gridSize;
        int colA = a % gridSize;

        int rowB = b / gridSize;
        int colB = b % gridSize;

        return (Mathf.Abs(rowA - rowB) + Mathf.Abs(colA - colB)) == 1;
    }

    List<int> GetNeighbors(int index)
    {
        List<int> result = new List<int>();

        int row = index / gridSize;
        int col = index % gridSize;

        if (row > 0) result.Add(index - gridSize);
        if (row < gridSize - 1) result.Add(index + gridSize);
        if (col > 0) result.Add(index - 1);
        if (col < gridSize - 1) result.Add(index + 1);

        return result;
    }

    // 🔥 WIN đúng theo ảnh gốc
    bool CheckWin()
    {
        foreach (Transform child in board)
        {
            Tile tile = child.GetComponent<Tile>();

            if (tile.correctIndex != child.GetSiblingIndex())
            {
                return false;
            }
        }

        return true;
    }

    void OnWin()
    {
        if (isSolved) return;

        isSolved = true;

        Debug.Log("WIN!");

        // hiện lại ô cuối
        Transform emptyTile = board.GetChild(emptyIndex);

        emptyTile.GetComponent<Image>().enabled = true;
        emptyTile.GetComponent<Button>().interactable = true;

        // tắt UI puzzle
        puzzleUI.SetActive(false);
        Time.timeScale = 1f;

        // mở cửa
        door.OpenDoor();
    }
}