using UnityEngine;
using UnityEngine.EventSystems;

public class PipeDragUI :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private PipeBlock pipe;

    private RectTransform rect;

    private Canvas canvas;

    void Start()
    {
        pipe = GetComponent<PipeBlock>();

        rect = GetComponent<RectTransform>();

        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(
        PointerEventData eventData
    )
    {
        if (!pipe.movable)
            return;
    }

    public void OnDrag(
        PointerEventData eventData
    )
    {
        if (!pipe.movable)
            return;

        rect.anchoredPosition +=
            eventData.delta /
            canvas.scaleFactor;

        Vector2 pos =
            rect.anchoredPosition;

        if (pipe.moveHorizontal)
        {
            rect.anchoredPosition =
                new Vector2(
                    pos.x,
                    PuzzleManagerrr.instance
                    .GridToPosition(
                        pipe.gridPos
                    ).y
                );
        }

        if (pipe.moveVertical)
        {
            rect.anchoredPosition =
                new Vector2(
                    PuzzleManagerrr.instance
                    .GridToPosition(
                        pipe.gridPos
                    ).x,
                    pos.y
                );
        }
    }

    public void OnEndDrag(
        PointerEventData eventData
    )
    {
        Vector2Int newGrid =
            PuzzleManagerrr.instance
            .PositionToGrid(
                rect.anchoredPosition
            );

        if (CanMove(newGrid))
        {
            PuzzleManagerrr.instance.grid[
                pipe.gridPos.x,
                pipe.gridPos.y
            ] = null;

            pipe.gridPos = newGrid;

            PuzzleManagerrr.instance.grid[
                newGrid.x,
                newGrid.y
            ] = pipe;
        }

        rect.anchoredPosition =
            PuzzleManagerrr.instance
            .GridToPosition(
                pipe.gridPos
            );
    }

    bool CanMove(Vector2Int pos)
    {
        if (!PuzzleManagerrr.instance
            .IsInsideGrid(pos))
            return false;

        PipeBlock target =
            PuzzleManagerrr.instance.grid[
                pos.x,
                pos.y
            ];

        return target == null;
    }
}