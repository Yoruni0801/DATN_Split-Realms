public readonly struct DamageContext
{
    public DamageContext(
        float damage,
        object attacker = null,
        float hitPointX = 0f,
        float hitPointY = 0f,
        float hitDirectionX = 0f,
        float hitDirectionY = 0f)
    {
        Damage = damage;
        Attacker = attacker;
        HitPointX = hitPointX;
        HitPointY = hitPointY;
        HitDirectionX = hitDirectionX;
        HitDirectionY = hitDirectionY;
    }

    public float Damage { get; }
    public object Attacker { get; }
    public float HitPointX { get; }
    public float HitPointY { get; }
    public float HitDirectionX { get; }
    public float HitDirectionY { get; }
}
