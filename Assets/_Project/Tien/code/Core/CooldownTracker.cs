using System.Collections.Generic;

public sealed class CooldownTracker
{
    private readonly List<float> timers = new List<float>();

    public CooldownTracker(int slotCount = 0)
    {
        EnsureIndex(slotCount - 1);
    }

    public int Count => timers.Count;

    public float this[int index] => IsValidIndex(index) ? timers[index] : 0f;

    public bool IsReady(int index)
    {
        EnsureIndex(index);
        return IsValidIndex(index) && timers[index] <= 0f;
    }

    public void Start(int index, float duration)
    {
        EnsureIndex(index);
        timers[index] = duration;
    }

    public void Reset(int index)
    {
        if (IsValidIndex(index))
        {
            timers[index] = 0f;
        }
    }

    public void ResetAll()
    {
        for (int i = 0; i < timers.Count; i++)
        {
            timers[i] = 0f;
        }
    }

    public void Tick(float deltaTime)
    {
        for (int i = 0; i < timers.Count; i++)
        {
            if (timers[i] > 0f)
            {
                timers[i] -= deltaTime;
            }
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < timers.Count;
    }

    private void EnsureIndex(int index)
    {
        while (index >= timers.Count)
        {
            timers.Add(0f);
        }
    }
}
