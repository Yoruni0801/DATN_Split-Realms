        using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class InazumaPuzzleManager : MonoBehaviour
{
    private const int QuarterTurnCount = 4;
    private const int SolvedQuarterTurn = 3;

    public InazumaCube cube1;
    public InazumaCube cube2;
    public InazumaCube cube3;
    public WaterDrain waterDrainScript;
    public WaterDrain[] waterSystems;

    [Header("Debug")]
    [SerializeField] private bool debugCompletionLogs = true;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showSolutionGuide = true;
    [SerializeField] private Color puzzleLinkColor = new Color(0.4f, 1f, 1f, 0.9f);
    [SerializeField] private Color solvedColor = new Color(0.2f, 1f, 0.3f, 0.9f);
    [SerializeField] private Color guideStartColor = new Color(1f, 0.88f, 0.2f, 0.95f);
    [SerializeField] private Color guideEndColor = new Color(1f, 0.35f, 0.2f, 0.95f);
    [SerializeField] private float guideHeight = 1.25f;
    [SerializeField] private float guideStepSpacing = 0.35f;
    [SerializeField] private float guideMarkerRadius = 0.18f;

    private bool isSolved;

    private void Update()
    {
        if (isSolved)
        {
            return;
        }

        if (cube1 == null || cube2 == null || cube3 == null)
        {
            return;
        }

        if (!cube1.IsBusy && !cube2.IsBusy && !cube3.IsBusy)
        {
            CheckPuzzleWin();
        }
    }

    public void OnCube1Hit()
    {
        if (IsUnavailableForHit())
        {
            return;
        }

        cube1.ExecuteRotation();
        cube3.ExecuteRotation();
    }

    public void OnCube2Hit()
    {
        if (IsUnavailableForHit())
        {
            return;
        }

        cube1.ExecuteRotation();
        cube2.ExecuteRotation();
        cube3.ExecuteRotation();
    }

    public void OnCube3Hit()
    {
        if (IsUnavailableForHit())
        {
            return;
        }

        cube3.ExecuteRotation();
        cube2.ExecuteRotation();
    }

    private bool IsUnavailableForHit()
    {
        return isSolved ||
               cube1 == null ||
               cube2 == null ||
               cube3 == null;
    }

    private void CheckPuzzleWin()
    {
        if (IsRotationCorrect(cube1.transform) &&
            IsRotationCorrect(cube2.transform) &&
            IsRotationCorrect(cube3.transform))
        {
            isSolved = true;
            WinSequence();
        }
    }

    private static bool IsRotationCorrect(Transform target)
    {
        float yAngle = target.localEulerAngles.y;
        return Mathf.Abs(yAngle - 270f) < 1.0f || Mathf.Abs(yAngle + 90f) < 1.0f;
    }

    private void WinSequence()
    {
        if (debugCompletionLogs)
        {
            Debug.Log("[InazumaPuzzleManager] Puzzle completed successfully.", this);
        }

        if (waterDrainScript != null)
        {
            waterDrainScript.StartDrain();
        }

        if (waterSystems != null)
        {
            foreach (var water in waterSystems)
            {
                if (water != null)
                {
                    water.StartDrain();
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        DrawCubeLink(cube1, cube2);
        DrawCubeLink(cube2, cube3);
        DrawCubeLink(cube1, cube3);
        DrawSolutionGuide();
    }

    private void DrawCubeLink(InazumaCube a, InazumaCube b)
    {
        if (a == null || b == null)
        {
            return;
        }

        Gizmos.color = isSolved ? solvedColor : puzzleLinkColor;
        Gizmos.DrawLine(a.transform.position, b.transform.position);
        Gizmos.DrawWireSphere(a.transform.position, 0.25f);
        Gizmos.DrawWireSphere(b.transform.position, 0.25f);
    }

    private void DrawSolutionGuide()
    {
        if (!showSolutionGuide || cube1 == null || cube2 == null || cube3 == null)
        {
            return;
        }

        if (!TryBuildSolution(out int[] steps))
        {
            return;
        }

        if (steps.Length == 0)
        {
            DrawSolvedLabel();
            return;
        }

        int cube1Count = 0;
        int cube2Count = 0;
        int cube3Count = 0;
        Vector3? previousPoint = null;

        for (int i = 0; i < steps.Length; i++)
        {
            int stepIndex = i + 1;
            int cubeIndex = steps[i];
            int stackIndex = GetAndIncrementStackIndex(cubeIndex, ref cube1Count, ref cube2Count, ref cube3Count);
            Transform targetCube = GetCubeTransform(cubeIndex);
            if (targetCube == null)
            {
                continue;
            }

            Vector3 point = targetCube.position + Vector3.up * (guideHeight + (stackIndex * guideStepSpacing));
            Color stepColor = Color.Lerp(guideStartColor, guideEndColor, steps.Length <= 1 ? 0f : i / (float)(steps.Length - 1));

            Gizmos.color = stepColor;
            Gizmos.DrawLine(targetCube.position, point);
            Gizmos.DrawWireSphere(point, guideMarkerRadius);

            Color fillColor = stepColor;
            fillColor.a *= 0.2f;
            Gizmos.color = fillColor;
            Gizmos.DrawSphere(point, guideMarkerRadius);

            if (previousPoint.HasValue)
            {
                Gizmos.color = stepColor;
                Gizmos.DrawLine(previousPoint.Value, point);
            }

            DrawStepLabel(stepIndex, cubeIndex, point, stepColor);
            previousPoint = point;
        }
    }

    private void DrawSolvedLabel()
    {
#if UNITY_EDITOR
        Vector3 labelPosition = transform.position + Vector3.up * (guideHeight + guideStepSpacing);
        Handles.color = solvedColor;
        Handles.Label(labelPosition, "Solved");
#endif
    }

    private void DrawStepLabel(int stepIndex, int cubeIndex, Vector3 point, Color labelColor)
    {
#if UNITY_EDITOR
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = labelColor;
        Handles.Label(point + Vector3.up * 0.08f, $"{stepIndex}: hit cube {cubeIndex}", style);
#endif
    }

    private static int GetAndIncrementStackIndex(int cubeIndex, ref int cube1Count, ref int cube2Count, ref int cube3Count)
    {
        if (cubeIndex == 1)
        {
            return cube1Count++;
        }

        if (cubeIndex == 2)
        {
            return cube2Count++;
        }

        return cube3Count++;
    }

    private Transform GetCubeTransform(int cubeIndex)
    {
        if (cubeIndex == 1)
        {
            return cube1 != null ? cube1.transform : null;
        }

        if (cubeIndex == 2)
        {
            return cube2 != null ? cube2.transform : null;
        }

        return cube3 != null ? cube3.transform : null;
    }

    private bool TryBuildSolution(out int[] steps)
    {
        steps = null;
        if (cube1 == null || cube2 == null || cube3 == null)
        {
            return false;
        }

        int startState = EncodeState(
            GetQuarterTurn(cube1.transform),
            GetQuarterTurn(cube2.transform),
            GetQuarterTurn(cube3.transform));
        int solvedState = EncodeState(SolvedQuarterTurn, SolvedQuarterTurn, SolvedQuarterTurn);

        bool[] visited = new bool[QuarterTurnCount * QuarterTurnCount * QuarterTurnCount];
        int[] parent = new int[visited.Length];
        int[] move = new int[visited.Length];
        int[] queue = new int[visited.Length];

        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = -1;
            move[i] = -1;
        }

        int head = 0;
        int tail = 0;
        queue[tail++] = startState;
        visited[startState] = true;

        while (head < tail)
        {
            int state = queue[head++];
            if (state == solvedState)
            {
                break;
            }

            for (int cubeIndex = 1; cubeIndex <= 3; cubeIndex++)
            {
                int nextState = ApplyMove(state, cubeIndex);
                if (visited[nextState])
                {
                    continue;
                }

                visited[nextState] = true;
                parent[nextState] = state;
                move[nextState] = cubeIndex;
                queue[tail++] = nextState;
            }
        }

        if (!visited[solvedState])
        {
            return false;
        }

        if (startState == solvedState)
        {
            steps = new int[0];
            return true;
        }

        int stepCount = 0;
        for (int state = solvedState; state != startState; state = parent[state])
        {
            stepCount++;
        }

        steps = new int[stepCount];
        int currentIndex = stepCount - 1;
        for (int state = solvedState; state != startState; state = parent[state])
        {
            steps[currentIndex--] = move[state];
        }

        return true;
    }

    private static int ApplyMove(int encodedState, int cubeIndex)
    {
        DecodeState(encodedState, out int a, out int b, out int c);

        if (cubeIndex == 1)
        {
            a = IncrementQuarterTurn(a);
            c = IncrementQuarterTurn(c);
        }
        else if (cubeIndex == 2)
        {
            a = IncrementQuarterTurn(a);
            b = IncrementQuarterTurn(b);
            c = IncrementQuarterTurn(c);
        }
        else
        {
            b = IncrementQuarterTurn(b);
            c = IncrementQuarterTurn(c);
        }

        return EncodeState(a, b, c);
    }

    private static int IncrementQuarterTurn(int value)
    {
        return (value + 1) % QuarterTurnCount;
    }

    private static int GetQuarterTurn(Transform target)
    {
        float normalizedAngle = Mathf.Repeat(target.localEulerAngles.y, 360f);
        int quarterTurn = Mathf.RoundToInt(normalizedAngle / 90f) % QuarterTurnCount;
        return quarterTurn < 0 ? quarterTurn + QuarterTurnCount : quarterTurn;
    }

    private static int EncodeState(int a, int b, int c)
    {
        return a + (b * QuarterTurnCount) + (c * QuarterTurnCount * QuarterTurnCount);
    }

    private static void DecodeState(int encodedState, out int a, out int b, out int c)
    {
        a = encodedState % QuarterTurnCount;
        encodedState /= QuarterTurnCount;
        b = encodedState % QuarterTurnCount;
        encodedState /= QuarterTurnCount;
        c = encodedState % QuarterTurnCount;
    }
}
