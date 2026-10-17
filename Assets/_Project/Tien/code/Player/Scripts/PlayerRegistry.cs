using System;

public static class PlayerRegistry
{
    public static Player Current { get; private set; }
    public static event Action<Player> OnCurrentChanged;

    public static void Register(Player player)
    {
        if (player == null || Current == player)
        {
            return;
        }

        Current = player;
        OnCurrentChanged?.Invoke(Current);
    }

    public static void Unregister(Player player)
    {
        if (Current == player)
        {
            Current = null;
            OnCurrentChanged?.Invoke(null);
        }
    }
}
