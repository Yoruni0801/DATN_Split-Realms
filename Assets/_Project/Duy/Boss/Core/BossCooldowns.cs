using System;

public sealed class BossCooldowns
{
    private readonly float[] timers;

    public BossCooldowns(int cooldownCount)
    {
        timers = new float[Math.Max(1, cooldownCount)];
    }

    public bool IsReady(int id)
    {
        return IsValidId(id) && timers[id] <= 0f;
    }

    public float GetRemaining(int id)
    {
        return IsValidId(id) ? timers[id] : 0f;
    }

    public void Start(int id, float duration)
    {
        if (!IsValidId(id))
        {
            return;
        }

        timers[id] = Math.Max(0f, duration);
    }

    public void Tick(float deltaTime)
    {
        float dt = Math.Max(0f, deltaTime);
        for (int i = 0; i < timers.Length; i++)
        {
            if (timers[i] > 0f)
            {
                timers[i] = Math.Max(0f, timers[i] - dt);
            }
        }
    }

    public void Clear()
    {
        Array.Clear(timers, 0, timers.Length);
    }

    private bool IsValidId(int id)
    {
        return id >= 0 && id < timers.Length;
    }
}
