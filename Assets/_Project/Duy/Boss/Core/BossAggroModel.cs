public sealed class BossAggroModel
{
    public bool IsAggro { get; private set; }
    public float Pressure { get; private set; }
    public int EscapeCount { get; private set; }

    public void Activate()
    {
        IsAggro = true;
        Pressure = 1f;
        EscapeCount = 0;
    }

    public void Clear()
    {
        IsAggro = false;
        Pressure = 0f;
        EscapeCount = 0;
    }

    public void AddPressure(float amount)
    {
        Pressure = Clamp01(Pressure + amount);
    }

    public void DecayPressure(float amount)
    {
        Pressure = Clamp01(Pressure - amount);
    }

    public void RegisterEscape()
    {
        EscapeCount++;
        AddPressure(0.15f);
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        return value > 1f ? 1f : value;
    }
}
