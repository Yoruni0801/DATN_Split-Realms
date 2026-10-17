using UnityEngine;

public class PuzzleManagerrr : MonoBehaviour
{
    public static PuzzleManagerrr instance;

    [Header("Board")]
    public RectTransform board;

    public int width = 6;
    public int height = 6;

    public float cellSize = 100f;

    public PipeBlock[,] grid;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        grid = new PipeBlock[width, height];

        PipeBlock[] pipes =
            FindObjectsByType<PipeBlock>(FindObjectsSortMode.None);

        foreach (PipeBlock pipe in pipes)
        {
            pipe.rect =
                pipe.GetComponent<RectTransform>();

            grid[
                pipe.gridPos.x,
                pipe.gridPos.y
            ] = pipe;

            pipe.rect.anchoredPosition =
                GridToPosition(pipe.gridPos);
        }
    }

    public Vector2 GridToPosition(
        Vector2Int gridPos
    )
    {
        return new Vector2(
            gridPos.x * cellSize,
            -gridPos.y * cellSize
        );
    }

    public Vector2Int PositionToGrid(
        Vector2 pos
    )
    {
        return new Vector2Int(
            Mathf.RoundToInt(pos.x / cellSize),
            Mathf.RoundToInt(-pos.y / cellSize)
        );
    }

    public bool IsInsideGrid(
        Vector2Int pos
    )
    {
        return pos.x >= 0 &&
               pos.y >= 0 &&
               pos.x < width &&
               pos.y < height;
    }
}
