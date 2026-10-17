using UnityEngine;

// Duy chỉnh code lại thành chỉ đem thông tin máu của Player qua các scene 
public static class PlayerCarryData
{
    private const int UnsetHealth = -1;
    private static int currentHealth = UnsetHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        currentHealth = UnsetHealth;
    }

    public static bool TryGetCurrentHealth(out int health)
    {
        health = currentHealth;
        return currentHealth >= 0;
    }

    public static void SetCurrentHealth(int health)
    {
        currentHealth = Mathf.Max(0, health);
    }
    public static void ResetAll()
    {
        currentHealth = UnsetHealth;
    }
}
