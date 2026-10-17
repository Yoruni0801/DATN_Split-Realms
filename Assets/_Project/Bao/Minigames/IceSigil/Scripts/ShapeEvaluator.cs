using System.Collections.Generic;
using UnityEngine;

namespace Bao.IceSigil
{
    public readonly struct ShapeEvaluationResult
    {
        public readonly float coverage;
        public readonly float outsideDamage;
        public readonly float precision;
        public readonly float continuity;
        public readonly float closedRatio;
        public readonly float depthQuality;
        public readonly float score;
        public readonly bool passed;
        public readonly string failureReason;

        public ShapeEvaluationResult(
            float coverage,
            float outsideDamage,
            float continuity,
            float closedRatio,
            float depthQuality,
            bool passed,
            string failureReason)
        {
            this.coverage = coverage;
            this.outsideDamage = outsideDamage;
            precision = Mathf.Clamp01(1f - outsideDamage);
            this.continuity = continuity;
            this.closedRatio = closedRatio;
            this.depthQuality = depthQuality;
            score = Mathf.Clamp01(coverage * 0.45f + precision * 0.25f + continuity * 0.20f + depthQuality * 0.10f);
            this.passed = passed;
            this.failureReason = failureReason;
        }

        public static ShapeEvaluationResult Empty => new(0f, 0f, 0f, 0f, 0f, false, "Chưa có nét khắc");
    }

    public sealed class ShapeEvaluator
    {
        public const byte DepthReadyThreshold = 34;

        private readonly int resolution;
        private readonly bool[] visited;
        private readonly Queue<int> queue = new();

        public ShapeEvaluator(int resolution)
        {
            this.resolution = Mathf.Clamp(resolution, 128, 1024);
            visited = new bool[this.resolution * this.resolution];
        }

        public ShapeEvaluationResult Evaluate(
            ShapeTemplateRuntime template,
            byte[] playerMask,
            byte[] depthMask,
            byte[] damageMask,
            float totalCrackAmount)
        {
            if (template == null || playerMask == null || depthMask == null)
            {
                return ShapeEvaluationResult.Empty;
            }

            int targetPixels = 0;
            int coveredTargetPixels = 0;
            int deepTargetPixels = 0;
            int playerPixels = 0;
            int outsidePixels = 0;

            for (int i = 0; i < playerMask.Length; i++)
            {
                bool isTarget = template.TargetLineMask[i] > 0;
                bool isPlayer = playerMask[i] > 0;
                if (isTarget)
                {
                    targetPixels++;
                    if (isPlayer)
                    {
                        coveredTargetPixels++;
                        if (depthMask[i] >= DepthReadyThreshold)
                        {
                            deepTargetPixels++;
                        }
                    }
                }

                if (isPlayer)
                {
                    playerPixels++;
                    if (template.ValidCarveBandMask[i] == 0)
                    {
                        outsidePixels++;
                    }
                }
            }

            if (targetPixels == 0 || playerPixels == 0)
            {
                return ShapeEvaluationResult.Empty;
            }

            float coverage = coveredTargetPixels / (float)targetPixels;
            float outsideDamage = outsidePixels / (float)Mathf.Max(1, playerPixels);
            float depthQuality = deepTargetPixels / (float)targetPixels;
            float continuity = ComputeContinuity(template, playerMask);
            float closedRatio = template.ClosedShape && template.RequiredClosedRatio > 0f
                ? ComputeClosedRatio(template, playerMask, depthMask)
                : 1f;

            string failure = "";
            bool passed = true;
            if (coverage < template.RequiredCoverage)
            {
                passed = false;
                failure = "Đường khắc chưa phủ đủ hình mẫu";
            }
            else if (outsideDamage > template.MaxOutsideDamage)
            {
                passed = false;
                failure = "Nét khắc lệch ra ngoài vùng cho phép";
            }
            else if (continuity < template.RequiredContinuity)
            {
                passed = false;
                failure = "Đường khắc còn bị đứt đoạn";
            }
            else if (template.ClosedShape && closedRatio < template.RequiredClosedRatio)
            {
                passed = false;
                failure = "Hình chưa đủ khép kín để cạy";
            }
            else if (depthQuality < template.RequiredDepthCoverage)
            {
                passed = false;
                failure = "Rãnh khắc còn quá nông";
            }
            else if (totalCrackAmount >= 100f)
            {
                passed = false;
                failure = "Mặt băng đã vỡ";
            }

            return new ShapeEvaluationResult(coverage, outsideDamage, continuity, closedRatio, depthQuality, passed, failure);
        }

        private float ComputeContinuity(ShapeTemplateRuntime template, byte[] playerMask)
        {
            System.Array.Clear(visited, 0, visited.Length);
            int totalCovered = 0;
            int largest = 0;

            for (int i = 0; i < playerMask.Length; i++)
            {
                if (template.TargetLineMask[i] == 0 || playerMask[i] == 0)
                {
                    continue;
                }

                totalCovered++;
                if (visited[i])
                {
                    continue;
                }

                int component = FloodCoveredComponent(i, template, playerMask);
                if (component > largest)
                {
                    largest = component;
                }
            }

            return totalCovered > 0 ? largest / (float)totalCovered : 0f;
        }

        private int FloodCoveredComponent(int start, ShapeTemplateRuntime template, byte[] playerMask)
        {
            int count = 0;
            queue.Clear();
            queue.Enqueue(start);
            visited[start] = true;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                count++;
                int x = index % resolution;
                int y = index / resolution;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        int nx = x + ox;
                        int ny = y + oy;
                        if (nx < 0 || nx >= resolution || ny < 0 || ny >= resolution)
                        {
                            continue;
                        }

                        int next = ny * resolution + nx;
                        if (visited[next] || template.TargetLineMask[next] == 0 || playerMask[next] == 0)
                        {
                            continue;
                        }

                        visited[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }

            return count;
        }

        private float ComputeClosedRatio(ShapeTemplateRuntime template, byte[] playerMask, byte[] depthMask)
        {
            int innerPixels = 0;
            for (int i = 0; i < template.InnerAreaMask.Length; i++)
            {
                if (template.InnerAreaMask[i] > 0)
                {
                    innerPixels++;
                }
            }

            if (!template.ClosedShape || innerPixels == 0)
            {
                return 1f;
            }

            System.Array.Clear(visited, 0, visited.Length);
            queue.Clear();

            for (int x = 0; x < resolution; x++)
            {
                EnqueueOpenPixel(x);
                EnqueueOpenPixel((resolution - 1) * resolution + x);
            }

            for (int y = 1; y < resolution - 1; y++)
            {
                EnqueueOpenPixel(y * resolution);
                EnqueueOpenPixel(y * resolution + resolution - 1);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % resolution;
                int y = index / resolution;
                TryVisitOpen(x + 1, y);
                TryVisitOpen(x - 1, y);
                TryVisitOpen(x, y + 1);
                TryVisitOpen(x, y - 1);
            }

            int enclosedInnerPixels = 0;
            for (int i = 0; i < template.InnerAreaMask.Length; i++)
            {
                if (template.InnerAreaMask[i] > 0 && !visited[i])
                {
                    enclosedInnerPixels++;
                }
            }

            return enclosedInnerPixels / (float)innerPixels;

            void EnqueueOpenPixel(int index)
            {
                if (!visited[index] && !IsWall(index))
                {
                    visited[index] = true;
                    queue.Enqueue(index);
                }
            }

            void TryVisitOpen(int x, int y)
            {
                if (x < 0 || x >= resolution || y < 0 || y >= resolution)
                {
                    return;
                }

                int next = y * resolution + x;
                if (visited[next] || IsWall(next))
                {
                    return;
                }

                visited[next] = true;
                queue.Enqueue(next);
            }

            bool IsWall(int index)
            {
                return playerMask[index] > 0 && depthMask[index] >= DepthReadyThreshold;
            }
        }
    }
}
