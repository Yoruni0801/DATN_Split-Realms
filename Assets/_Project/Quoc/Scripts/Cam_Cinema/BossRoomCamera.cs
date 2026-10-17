using UnityEngine;
using Unity.Cinemachine;

public class BossRoomCamera : MonoBehaviour
{
    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineCamera bossCamera;

    [Header("Boss")]
    [SerializeField] private GameObject boss;

    [Header("Doors")]
    [SerializeField] private GameObject entranceDoor;
    [SerializeField] private GameObject exitDoor;

    [Header("Boss Room Bounds")]
    [Tooltip("BoxCollider2D chặn mép trái phòng boss")]
    [SerializeField] private GameObject leftWall;

    [Tooltip("BoxCollider2D chặn mép phải phòng boss")]
    [SerializeField] private GameObject rightWall;

    [Header("Settings")]
    [SerializeField] private int normalCameraPriority = 10;
    [SerializeField] private int bossCameraPriority = 20;

    [Tooltip("Nếu bật, Boss sẽ bị tắt cho tới khi Player bước vào phòng.")]
    [SerializeField] private bool activateBossOnEnter = true;

    private bool fightStarted;
    private bool fightFinished;

    private void Awake()
    {
        // Camera thường hoạt động trước
        playerCamera.Priority = normalCameraPriority;
        bossCamera.Priority = 0;

        // Boss chưa hoạt động
        if (activateBossOnEnter && boss != null)
            boss.SetActive(false);

        // Cửa ban đầu mở
        if (entranceDoor != null)
            entranceDoor.SetActive(false);

        if (exitDoor != null)
            exitDoor.SetActive(false);

        // Invisible wall ban đầu tắt
        SetBossRoomWalls(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fightStarted || fightFinished)
            return;

        if (!other.CompareTag("Player"))
            return;

        StartBossFight();
    }

    public void StartBossFight()
    {
        if (fightStarted || fightFinished)
            return;

        fightStarted = true;

        // Bật Boss
        if (boss != null)
            boss.SetActive(true);

        // Đóng cửa
        if (entranceDoor != null)
            entranceDoor.SetActive(true);

        if (exitDoor != null)
            exitDoor.SetActive(true);

        // Bật tường chặn 2 bên
        SetBossRoomWalls(true);

        // Chuyển sang Boss Camera
        playerCamera.Priority = normalCameraPriority;
        bossCamera.Priority = bossCameraPriority;

        Debug.Log("Boss fight started.");
    }

    public void EndBossFight()
    {
        if (!fightStarted || fightFinished)
            return;

        fightStarted = false;
        fightFinished = true;

        // Trả về camera thường
        bossCamera.Priority = 0;
        playerCamera.Priority = normalCameraPriority;

        // Mở cửa
        if (entranceDoor != null)
            entranceDoor.SetActive(false);

        if (exitDoor != null)
            exitDoor.SetActive(false);

        // Bỏ tường chặn
        SetBossRoomWalls(false);

        Debug.Log("Boss defeated.");
    }

    private void SetBossRoomWalls(bool active)
    {
        if (leftWall != null)
            leftWall.SetActive(active);

        if (rightWall != null)
            rightWall.SetActive(active);
    }
}