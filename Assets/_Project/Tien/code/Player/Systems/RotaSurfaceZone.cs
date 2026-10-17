using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider2D))]
public class RotaSurfaceZone : MonoBehaviour
{
    public static event Action<Player> PlayerEnteredAnyZone;
    public static event Action PlayerExitedAllZones;

    private static readonly List<RotaSurfaceZone> allZones = new();
    private static readonly Dictionary<Player, int> activeContacts = new();
    private readonly Dictionary<Collider2D, Player> localContacts = new();

    [Header("Post Processing")]
    [SerializeField] private RotaPostProcessingController postProcessingController;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showLabel = true;
    [SerializeField] private bool showPlayerConnection = true;

    private Collider2D zoneCollider;
    private Player currentPlayer;

    public static IReadOnlyList<RotaSurfaceZone> AllZones => allZones;

    public static bool IsPlayerInAnyZone(Player player)
    {
        if (player == null)
            return false;

        if (activeContacts.TryGetValue(player, out int count) && count > 0)
            return true;

        Vector2 playerPos = player.transform.position;
        Collider2D playerCollider = player.GetComponent<Collider2D>();

        for (int i = 0; i < allZones.Count; i++)
        {
            RotaSurfaceZone zone = allZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null)
                continue;

            if (playerCollider != null && playerCollider.IsTouching(zone.zoneCollider))
                return true;

            if (zone.zoneCollider.OverlapPoint(playerPos) || zone.zoneCollider.bounds.Contains(playerPos))
                return true;
        }

        return false;
    }

    public static bool IsPositionInAnyZone(Vector2 position)
    {
        for (int i = 0; i < allZones.Count; i++)
        {
            RotaSurfaceZone zone = allZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null)
                continue;

            if (zone.zoneCollider.OverlapPoint(position) || zone.zoneCollider.bounds.Contains(position))
                return true;
        }

        return false;
    }

    private void Reset()
    {
        zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        if (postProcessingController == null)
            postProcessingController = GetComponent<RotaPostProcessingController>();
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
        {
            Debug.LogWarning(
                "[RotaSurfaceZone] Collider must be Trigger. Enabling isTrigger automatically.",
                this);
            zoneCollider.isTrigger = true;
        }

        if (postProcessingController == null)
            postProcessingController = GetComponent<RotaPostProcessingController>();
    }

    private void OnEnable()
    {
        if (!allZones.Contains(this))
            allZones.Add(this);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null || localContacts.ContainsKey(collision))
            return;

        Player player = collision.GetComponent<Player>();
        if (player == null)
            player = collision.GetComponentInParent<Player>();

        if (player == null || !player.CompareTag("Player"))
            return;

        localContacts.Add(collision, player);
        currentPlayer = player;

        activeContacts.TryGetValue(player, out int count);
        activeContacts[player] = count + 1;

        if (count == 0)
        {
            player.SetRotaMode(true);
            PlayerEnteredAnyZone?.Invoke(player);
        }

        if (postProcessingController != null)
        {
            postProcessingController.OnPlayerEnterZone();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision == null || !localContacts.TryGetValue(collision, out Player player))
            return;

        localContacts.Remove(collision);
        ReleaseContact(player);

        if (localContacts.Count == 0)
        {
            currentPlayer = null;
            if (postProcessingController != null)
            {
                postProcessingController.OnPlayerExitZone();
            }
        }
    }

    private void OnDisable()
    {
        allZones.Remove(this);

        if (localContacts.Count == 0)
        {
            currentPlayer = null;
            return;
        }

        Player[] players = new Player[localContacts.Count];
        int index = 0;
        foreach (Player player in localContacts.Values)
            players[index++] = player;

        localContacts.Clear();
        currentPlayer = null;

        for (int i = 0; i < players.Length; i++)
            ReleaseContact(players[i]);
    }

    private void OnDestroy()
    {
        allZones.Remove(this);
    }

    private static void ReleaseContact(Player player)
    {
        if (player == null || !activeContacts.TryGetValue(player, out int count))
            return;

        count--;

        if (count <= 0)
        {
            activeContacts.Remove(player);

            // Nếu người chơi vẫn còn nằm trong vùng ROTA (ví dụ vừa respawn hoặc teleport tới checkpoint trong zone)
            if (IsPlayerInAnyZone(player))
            {
                activeContacts[player] = 1;
                return;
            }

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            bool isDead = playerHealth != null && playerHealth.IsDead;

            // Khi player chết trong vùng ROTA, không xóa checkpoint minigame
            // để khi người chơi bấm hồi sinh (Respawn) vẫn khôi phục lại đúng trạng thái ROTA.
            if (!isDead)
            {
                Tien.Minigame.MinigameCheckpointState.ClearCheckpoint();
            }

            player.SetRotaMode(false);
            PlayerExitedAllZones?.Invoke();
        }
        else
        {
            activeContacts[player] = count;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        Collider2D col = zoneCollider != null ? zoneCollider : GetComponent<Collider2D>();
        if (col == null)
            return;

        bool invalid = !col.isTrigger;
        bool inside = Application.isPlaying && currentPlayer != null;

        Color fill = invalid
            ? new Color(1f, 0.15f, 0.15f, 0.18f)
            : inside
                ? new Color(0.15f, 1f, 0.3f, 0.16f)
                : new Color(0.1f, 0.8f, 1f, 0.10f);

        Color wire = invalid
            ? Color.red
            : inside
                ? Color.green
                : Color.cyan;

        if (col is BoxCollider2D box)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = fill;
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = old;
        }
        else
        {
            Gizmos.color = fill;
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }

        if (inside && showPlayerConnection && currentPlayer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(col.bounds.center, currentPlayer.transform.position);
        }

        if (!showLabel)
            return;

        Vector3 labelPosition = col.bounds.center;
        labelPosition.y = col.bounds.max.y + HandleUtility.GetHandleSize(labelPosition) * 0.15f;

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        style.normal.textColor = wire;

        string label = invalid
            ? "ROTA ZONE\nIs Trigger = OFF"
            : inside
                ? $"ROTA ZONE\nMODE: ON\nContacts: {localContacts.Count}"
                : "ROTA ZONE\nENTER = ON / EXIT = OFF";

        Handles.Label(labelPosition, label, style);
    }
#endif
}

