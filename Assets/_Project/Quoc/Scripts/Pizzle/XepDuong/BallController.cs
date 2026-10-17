using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BallController : MonoBehaviour
{
    public RectTransform ballRect;

    public Vector2Int startPos;

    public Vector2Int goalPos;

    public float moveSpeed = 400f;

    private List<Vector2Int> path =
        new List<Vector2Int>();

    public void PlayBall()
    {
        StopAllCoroutines();

        ballRect.anchoredPosition =
            PuzzleManagerrr.instance
            .GridToPosition(startPos);

        path.Clear();

        bool success =
            FindPath(
                startPos,
                Vector2Int.right
            );

        if (success)
        {
            StartCoroutine(MoveBall());
        }
        else
        {
            Debug.Log("NO PATH");
        }
    }

    bool FindPath(
        Vector2Int current,
        Vector2Int dir
    )
    {
        path.Add(current);

        if (current == goalPos)
            return true;

        PipeBlock currentPipe =
            PuzzleManagerrr.instance.grid[
                current.x,
                current.y
            ];

        if (currentPipe == null)
            return false;

        if (!CanExit(currentPipe, dir))
            return false;

        Vector2Int next =
            current + dir;

        if (!PuzzleManagerrr.instance
            .IsInsideGrid(next))
            return false;

        PipeBlock nextPipe =
            PuzzleManagerrr.instance.grid[
                next.x,
                next.y
            ];

        if (nextPipe == null)
            return false;

        if (!CanEnter(nextPipe, dir))
            return false;

        Vector2Int nextDir =
            GetNextDirection(nextPipe, dir);

        return FindPath(next, nextDir);
    }

    bool CanExit(PipeBlock pipe, Vector2Int dir)
    {
        if (dir == Vector2Int.right)
            return pipe.right;

        if (dir == Vector2Int.left)
            return pipe.left;

        if (dir == Vector2Int.up)
            return pipe.up;

        if (dir == Vector2Int.down)
            return pipe.down;

        return false;
    }

    bool CanEnter(PipeBlock pipe, Vector2Int dir)
    {
        if (dir == Vector2Int.right)
            return pipe.left;

        if (dir == Vector2Int.left)
            return pipe.right;

        if (dir == Vector2Int.up)
            return pipe.down;

        if (dir == Vector2Int.down)
            return pipe.up;

        return false;
    }

    Vector2Int GetNextDirection(
        PipeBlock pipe,
        Vector2Int current
    )
    {
        if (current == Vector2Int.right)
        {
            if (pipe.up)
                return Vector2Int.up;

            if (pipe.down)
                return Vector2Int.down;
        }

        if (current == Vector2Int.left)
        {
            if (pipe.up)
                return Vector2Int.up;

            if (pipe.down)
                return Vector2Int.down;
        }

        if (current == Vector2Int.up)
        {
            if (pipe.left)
                return Vector2Int.left;

            if (pipe.right)
                return Vector2Int.right;
        }

        if (current == Vector2Int.down)
        {
            if (pipe.left)
                return Vector2Int.left;

            if (pipe.right)
                return Vector2Int.right;
        }

        return current;
    }

    IEnumerator MoveBall()
    {
        foreach (Vector2Int pos in path)
        {
            Vector2 target =
                PuzzleManagerrr.instance
                .GridToPosition(pos);

            while (
                Vector2.Distance(
                    ballRect.anchoredPosition,
                    target
                ) > 5f
            )
            {
                ballRect.anchoredPosition =
                    Vector2.MoveTowards(
                        ballRect.anchoredPosition,
                        target,
                        moveSpeed *
                        Time.deltaTime
                    );

                yield return null;
            }
        }

        Debug.Log("WIN");
    }
}