using System.Collections;
using UnityEngine;

namespace Bao.Bosses
{
    public class NidusAcidPool : MonoBehaviour
    {
        private float duration;
        private int damagePerSecond;
        private float radius;
        private float tickInterval = 1f;
        private float elapsed;
        private bool isPriming;
        private bool isCleaningUp;
        private NidusCocoonBoss owner;
        private Coroutine lifecycleRoutine;
        private SpriteRenderer[] renderers;
        private Vector3 baseScale;
        private MaterialPropertyBlock propertyBlock;
        private readonly System.Collections.Generic.HashSet<PlayerHealth> damagedExplosionTargets = new System.Collections.Generic.HashSet<PlayerHealth>();
        private readonly System.Collections.Generic.HashSet<PlayerHealth> damagedTickTargets = new System.Collections.Generic.HashSet<PlayerHealth>();

        public NidusCocoonBoss Owner => owner;
        public float Radius => radius;
        public bool IsPriming => isPriming;
        public bool IsAvailableForDetonation => !isPriming && !isCleaningUp && gameObject.activeInHierarchy;

        public void Initialize(float duration, int dps, float radius)
        {
            Initialize(duration, dps, radius, null);
        }

        public void Initialize(float duration, int dps, float radius, NidusCocoonBoss owner)
        {
            this.duration = Mathf.Max(0.4f, duration);
            this.damagePerSecond = Mathf.Max(0, dps);
            this.radius = Mathf.Max(0.2f, radius);
            this.owner = owner;
            elapsed = 0f;
            isPriming = false;
            isCleaningUp = false;

            baseScale = transform.localScale;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            propertyBlock = new MaterialPropertyBlock();

            owner?.RegisterAcidPool(this);

            if (lifecycleRoutine != null)
                StopCoroutine(lifecycleRoutine);
            lifecycleRoutine = StartCoroutine(LifecycleRoutine());
        }

        private IEnumerator LifecycleRoutine()
        {
            float nextTick = 0f;
            while (elapsed < duration && !isCleaningUp && !isPriming)
            {
                elapsed += Time.deltaTime;

                if (elapsed >= nextTick)
                {
                    ApplyTickDamage();
                    nextTick += tickInterval;
                }

                if (ShouldDetonateOnExpiry())
                {
                    isPriming = true;
                    lifecycleRoutine = null;
                    yield return PrimeAndDetonateRoutine(
                        owner.AcidPoolExpiryPrimeDuration,
                        owner.AcidPoolExpiryExplosionDamage,
                        owner.AcidPoolExpiryExplosionRadiusMultiplier,
                        owner.AcidPoolExpiryExplosionKnockbackForce);
                    yield break;
                }

                UpdateNormalVisuals();
                yield return null;
            }

            if (!isCleaningUp && !isPriming)
                yield return FadeAndDestroyRoutine(0.4f);
        }

        private bool ShouldDetonateOnExpiry()
        {
            if (owner == null || owner.IsDead)
                return false;

            float primeDuration = Mathf.Min(owner.AcidPoolExpiryPrimeDuration, duration);
            return elapsed >= duration - primeDuration;
        }

        private void UpdateNormalVisuals()
        {
            float lifeT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.18f));
            float bubble = 1f + Mathf.Sin(Time.time * 4.7f + GetInstanceID() * 0.013f) * 0.035f;
            float endFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Mathf.Max(0f, duration - 0.4f), duration, elapsed));

            transform.localScale = baseScale * Mathf.Max(0.05f, grow * bubble);
            SetRendererTint(new Color(0.58f, 1f, 0.20f, Mathf.Lerp(0.82f, 0.22f, 1f - endFade)));
        }

        private void ApplyTickDamage()
        {
            if (damagePerSecond <= 0) return;

            damagedTickTargets.Clear();
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.CompareTag("Player"))
                    continue;

                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    if (!damagedTickTargets.Add(playerHealth))
                        continue;
                    playerHealth.ApplyEnvironmentalDamageWithoutReaction(damagePerSecond);
                    continue;
                }

                Bao.Enemies.IDamageable damageable = hit.GetComponent<Bao.Enemies.IDamageable>();
                damageable?.TakeDamage(damagePerSecond);
            }
        }

        public bool PrimeAndDetonate(float primeDuration, int explosionDamage, float radiusMultiplier, float knockbackForce)
        {
            if (!IsAvailableForDetonation)
                return false;

            if (lifecycleRoutine != null)
            {
                StopCoroutine(lifecycleRoutine);
                lifecycleRoutine = null;
            }

            isPriming = true;
            StartCoroutine(PrimeAndDetonateRoutine(
                Mathf.Max(0.45f, primeDuration),
                Mathf.Max(0, explosionDamage),
                Mathf.Max(1f, radiusMultiplier),
                Mathf.Max(0f, knockbackForce)));
            return true;
        }

        private IEnumerator PrimeAndDetonateRoutine(float primeDuration, int explosionDamage, float radiusMultiplier, float knockbackForce)
        {
            Vector3 startScale = transform.localScale;
            float primeElapsed = 0f;

            while (primeElapsed < primeDuration)
            {
                if (owner == null || owner.IsDead)
                {
                    isPriming = false;
                    yield return FadeAndDestroyRoutine(0.22f);
                    yield break;
                }

                primeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(primeElapsed / primeDuration);
                float urgency = t * t;
                float pulseSpeed = Mathf.Lerp(7f, 24f, urgency);
                float pulse = 0.93f + Mathf.Sin(Time.time * pulseSpeed) * Mathf.Lerp(0.035f, 0.11f, urgency);
                float contract = Mathf.Lerp(1f, 0.72f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t)));

                transform.localScale = startScale * pulse * contract;

                Color tint = Color.Lerp(
                    new Color(0.25f, 1.25f, 0.03f, 0.92f),
                    new Color(0.90f, 1.75f, 0.12f, 1f),
                    urgency);
                SetRendererTint(tint);

                yield return null;
            }

            Detonate(explosionDamage, radius * radiusMultiplier, knockbackForce);
        }

        private void Detonate(int explosionDamage, float explosionRadius, float knockbackForce)
        {
            if (isCleaningUp) return;
            isCleaningUp = true;

            NidusOrganicSkillVFX.SpawnAcidDetonation(transform.position, explosionRadius);

            damagedExplosionTargets.Clear();
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.CompareTag("Player"))
                    continue;

                PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null && !damagedExplosionTargets.Add(playerHealth))
                    continue;

                Vector2 dir = ((Vector2)hit.transform.position - (Vector2)transform.position);
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
                dir.Normalize();

                PlayerHitResolver.TryApplyHitToPlayer(
                    hit,
                    explosionDamage,
                    ParryableAttackType.Projectile,
                    owner != null ? owner.transform : transform,
                    dir,
                    canBeBlocked: false);

                Rigidbody2D rb = hit.attachedRigidbody != null ? hit.attachedRigidbody : hit.GetComponentInParent<Rigidbody2D>();
                if (rb != null && knockbackForce > 0f)
                {
                    dir.y = Mathf.Max(dir.y, 0.25f);
                    rb.AddForce(dir.normalized * knockbackForce, ForceMode2D.Impulse);
                }
            }

            owner?.UnregisterAcidPool(this);
            owner = null;
            Destroy(gameObject);
        }

        public void FadeAndDestroy(float fadeDuration = 0.35f)
        {
            if (isCleaningUp) return;
            if (lifecycleRoutine != null)
            {
                StopCoroutine(lifecycleRoutine);
                lifecycleRoutine = null;
            }
            StartCoroutine(FadeAndDestroyRoutine(fadeDuration));
        }

        private IEnumerator FadeAndDestroyRoutine(float fadeDuration)
        {
            if (isCleaningUp && !isPriming)
                yield break;

            isCleaningUp = true;
            isPriming = false;
            Vector3 startScale = transform.localScale;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
                transform.localScale = startScale * Mathf.Lerp(1f, 0.62f, n);
                SetRendererAlpha(1f - n);
                yield return null;
            }

            owner?.UnregisterAcidPool(this);
            owner = null;
            Destroy(gameObject);
        }

        public void ForceCleanup()
        {
            if (gameObject == null) return;
            FadeAndDestroy(0.18f);
        }

        private void SetRendererTint(Color tint)
        {
            if (renderers == null) return;
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;

                sr.GetPropertyBlock(propertyBlock);
                if (sr.sharedMaterial != null)
                {
                    if (sr.sharedMaterial.HasProperty("_Color"))
                        propertyBlock.SetColor("_Color", tint);
                    if (sr.sharedMaterial.HasProperty("_BaseColor"))
                        propertyBlock.SetColor("_BaseColor", tint);
                    if (sr.sharedMaterial.HasProperty("_TintColor"))
                        propertyBlock.SetColor("_TintColor", tint);
                }
                sr.SetPropertyBlock(propertyBlock);

                Color c = sr.color;
                c.a = tint.a;
                sr.color = c;
            }
        }

        private void SetRendererAlpha(float alpha)
        {
            if (renderers == null) return;
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = Mathf.Clamp01(alpha);
                sr.color = c;
            }
        }

        private void OnDestroy()
        {
            owner?.UnregisterAcidPool(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
