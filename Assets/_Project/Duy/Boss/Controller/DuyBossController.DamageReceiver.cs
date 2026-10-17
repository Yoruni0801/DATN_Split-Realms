public partial class DuyBossController : IDamageReceiver
{
    public void TakeDamage(DamageContext context)
    {
        TakeDamage(context.Damage, context.Attacker as UnityEngine.Transform);
    }
}
