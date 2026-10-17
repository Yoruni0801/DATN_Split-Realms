namespace Bao.Fluid2D
{
    /// <summary>
    /// Designer-facing quality tiers for RT resolution and safe runtime knobs.
    /// Does not mutate shared <see cref="Fluid2DSettings"/> assets.
    /// </summary>
    public enum Fluid2DQualityLevel
    {
        Low = 0,
        Balanced = 1,
        High = 2
    }

    /// <summary>
    /// Runtime quality overrides applied by <see cref="Fluid2DEnvironmentController"/>.
    /// Values are safe render/sim knobs only (resolution scale, substeps, max particles cap hint).
    /// </summary>
    public readonly struct Fluid2DQualityProfile
    {
        public readonly float ResolutionScale;
        public readonly int Substeps;
        public readonly string Label;

        public Fluid2DQualityProfile(float resolutionScale, int substeps, string label)
        {
            ResolutionScale = resolutionScale;
            Substeps = substeps;
            Label = label;
        }

        public static Fluid2DQualityProfile Get(Fluid2DQualityLevel level)
        {
            switch (level)
            {
                case Fluid2DQualityLevel.Low:
                    return new Fluid2DQualityProfile(0.50f, 2, "Low");
                case Fluid2DQualityLevel.High:
                    return new Fluid2DQualityProfile(1.00f, 4, "High");
                default:
                    return new Fluid2DQualityProfile(0.75f, 3, "Balanced");
            }
        }
    }
}
