using System;

public readonly struct BossThreatReaction
{
    public BossThreatReaction(bool accepted, BossThreatData threat, float reactionDelay)
    {
        Accepted = accepted;
        Threat = threat;
        ReactionDelay = reactionDelay;
    }

    public bool Accepted { get; }
    public BossThreatData Threat { get; }
    public float ReactionDelay { get; }
}

public sealed class BossThreatModel
{
    private readonly BossConfig config;
    private readonly Random random;

    public BossThreatModel(BossConfig config, int seed = 0)
    {
        this.config = config;
        random = seed == 0 ? new Random() : new Random(seed);
    }

    public BossThreatReaction Notify(
        BossStateId state,
        bool isAttacking,
        bool isPhaseTwo,
        BossThreatData threat)
    {
        if (state == BossStateId.Dead ||
            state == BossStateId.Stun ||
            state == BossStateId.Hurt ||
            isAttacking)
        {
            return new BossThreatReaction(false, threat, 0f);
        }

        float reactionChance = Clamp01(config.DefenseReactionChance + (isPhaseTwo ? config.PhaseTwoDefenseReactionBonus : 0f));
        if (NextFloat() > reactionChance)
        {
            return new BossThreatReaction(false, threat, 0f);
        }

        float estimatedTime = Max(0f, threat.EstimatedTimeToImpact);
        if (estimatedTime > 0f && estimatedTime < config.MinimumThreatTime)
        {
            return new BossThreatReaction(false, threat, 0f);
        }

        threat.EstimatedTimeToImpact = estimatedTime;
        float min = Min(config.ReactionDelayMin, config.ReactionDelayMax);
        float max = Max(config.ReactionDelayMin, config.ReactionDelayMax);
        if (threat.ThreatType == BossThreatType.Melee &&
            threat.VelocityX * threat.VelocityX + threat.VelocityY * threat.VelocityY > 0.01f)
        {
            min = 0.1f;
            max = 0.2f;
        }

        float delay = Lerp(min, max, NextFloat());
        if (isPhaseTwo)
        {
            delay *= config.PhaseTwoDecisionMultiplier;
        }

        return new BossThreatReaction(true, threat, delay);
    }

    private float NextFloat() => (float)random.NextDouble();
    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Min(float a, float b) => a < b ? a : b;
    private static float Max(float a, float b) => a > b ? a : b;
}
