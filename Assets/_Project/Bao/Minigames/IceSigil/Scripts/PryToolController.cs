using UnityEngine;

namespace Bao.IceSigil
{
    public enum PryAttemptState
    {
        None,
        Started,
        Holding,
        Success,
        TooLow,
        TooHigh
    }

    public readonly struct PryAttemptResult
    {
        public readonly PryAttemptState state;
        public readonly int pointIndex;
        public readonly Vector2 uv;
        public readonly float force;
        public readonly float safeMin;
        public readonly float safeMax;

        public bool IsFailure => state is PryAttemptState.TooLow or PryAttemptState.TooHigh;

        public PryAttemptResult(PryAttemptState state, int pointIndex, Vector2 uv, float force, float safeMin, float safeMax)
        {
            this.state = state;
            this.pointIndex = pointIndex;
            this.uv = uv;
            this.force = force;
            this.safeMin = safeMin;
            this.safeMax = safeMax;
        }
    }

    public sealed class PryToolController
    {
        public sealed class PryPointState
        {
            public Vector2 uv;
            public float risk;
            public bool completed;
            public float SafeMin { get; private set; }
            public float SafeMax { get; private set; }

            public void Configure(Vector2 uv, float risk, float baseMin, float baseMax)
            {
                this.uv = uv;
                this.risk = Mathf.Clamp01(risk);
                completed = false;
                float shrink = Mathf.Lerp(0f, 0.11f, this.risk);
                SafeMin = Mathf.Clamp01(baseMin + shrink);
                SafeMax = Mathf.Clamp01(baseMax - shrink);
            }
        }

        private readonly PryPointState[] points;
        private int activePoint = -1;
        private int successCount;
        private int requiredSuccesses = 3;
        private float forceOffset;

        public bool IsHolding => activePoint >= 0;
        public float Force { get; private set; }
        public int SuccessCount => successCount;
        public int RequiredSuccesses => requiredSuccesses;
        public PryPointState[] Points => points;

        public PryToolController(int maxPointCount = 5)
        {
            points = new PryPointState[Mathf.Clamp(maxPointCount, 2, 5)];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new PryPointState();
            }
        }

        public void Configure(ShapeTemplateRuntime template, float crackAmount)
        {
            activePoint = -1;
            successCount = 0;
            Force = 0f;
            requiredSuccesses = Mathf.Clamp(template.RequiredPrySuccesses, 1, points.Length);

            Vector2[] source = template.PryPoints;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 uv = source != null && i < source.Length ? source[i] : DefaultPoint(i);
                float risk = Mathf.Clamp01(crackAmount / 100f + i * 0.08f + Random.Range(0f, 0.12f));
                points[i].Configure(uv, risk, template.BaseSafeForceMin, template.BaseSafeForceMax);
            }
        }

        public PryAttemptResult Update(Vector2 uv, bool pressed, bool held, bool released, float time, float hitRadiusUv)
        {
            if (activePoint < 0 && pressed)
            {
                int point = FindPoint(uv, hitRadiusUv);
                if (point >= 0)
                {
                    activePoint = point;
                    forceOffset = Random.Range(0f, 1f);
                    Force = 0f;
                    PryPointState p = points[activePoint];
                    return new PryAttemptResult(PryAttemptState.Started, activePoint, p.uv, Force, p.SafeMin, p.SafeMax);
                }
            }

            if (activePoint >= 0 && held)
            {
                PryPointState p = points[activePoint];
                Force = Mathf.PingPong(time * Mathf.Lerp(0.82f, 1.35f, p.risk) + forceOffset, 1f);
                return new PryAttemptResult(PryAttemptState.Holding, activePoint, p.uv, Force, p.SafeMin, p.SafeMax);
            }

            if (activePoint >= 0 && released)
            {
                PryPointState p = points[activePoint];
                PryAttemptState state;
                if (Force >= p.SafeMin && Force <= p.SafeMax)
                {
                    p.completed = true;
                    successCount++;
                    state = PryAttemptState.Success;
                }
                else
                {
                    state = Force > p.SafeMax ? PryAttemptState.TooHigh : PryAttemptState.TooLow;
                }

                PryAttemptResult result = new(state, activePoint, p.uv, Force, p.SafeMin, p.SafeMax);
                activePoint = -1;
                Force = 0f;
                return result;
            }

            return default;
        }

        private int FindPoint(Vector2 uv, float radius)
        {
            int best = -1;
            float bestDistance = radius;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].completed)
                {
                    continue;
                }

                float distance = Vector2.Distance(uv, points[i].uv);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static Vector2 DefaultPoint(int index)
        {
            return index switch
            {
                0 => new Vector2(0.5f, 0.84f),
                1 => new Vector2(0.78f, 0.5f),
                2 => new Vector2(0.5f, 0.18f),
                3 => new Vector2(0.22f, 0.5f),
                _ => new Vector2(0.5f, 0.5f)
            };
        }
    }
}
