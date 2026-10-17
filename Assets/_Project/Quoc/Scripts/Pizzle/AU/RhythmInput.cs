using UnityEngine;

public class RhythmInput : MonoBehaviour
{
    [SerializeField] private RhythmGameManager gameManager;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<RhythmGameManager>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            PressW();
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            PressA();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            PressS();
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            PressD();
        }
    }

    public void PressW()
    {
        PressLane(LaneType.W);
    }

    public void PressA()
    {
        PressLane(LaneType.A);
    }

    public void PressS()
    {
        PressLane(LaneType.S);
    }

    public void PressD()
    {
        PressLane(LaneType.D);
    }

    private void PressLane(LaneType lane)
    {
        if (gameManager == null)
        {
            Debug.LogWarning("[RhythmInput] Chưa gán RhythmGameManager.");
            return;
        }

        gameManager.PressLane(lane);
    }
}

