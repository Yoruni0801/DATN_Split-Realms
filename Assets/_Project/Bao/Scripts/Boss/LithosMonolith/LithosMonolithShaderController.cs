using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Bao.Bosses
{
    /// <summary>
    /// Procedural VFX controller for Lithos Monolith Boss:
    /// - Hit flash (bright stone burst on damage)
    /// - Phase-dependent rune glow
    /// - Rock throw VFX (dust cloud + stone fragments)
    /// - Orbit glow aura (pulsating energy ring)
    /// - Screen shake
    /// - Death crumble VFX
    /// - Rune reveal burst
    /// </summary>
    public class LithosMonolithShaderController : MonoBehaviour
    {
        [Header("Hit Flash")]
        [SerializeField] private float hitFlashDuration = 0.12f;
        [SerializeField] private Color hitFlashColor = new Color(0.9f, 0.85f, 0.7f, 1f);

        [Header("Phase Colors")]
        [ColorUsage(true, true)]
        [SerializeField] private Color phase1GlowColor = new Color(0.8f, 0.7f, 0.5f, 1f);
        [ColorUsage(true, true)]
        [SerializeField] private Color phase2GlowColor = new Color(1.5f, 0.8f, 2.5f, 1f);

        [Header("Rune Glow")]
        [ColorUsage(true, true)]
        [SerializeField] private Color runeGlowColor = new Color(2f, 1.2f, 3f, 1f);
        [SerializeField] private float runePulseSpeed = 2f;

        [Header("Rock Throw VFX")]
        [SerializeField] private Color rockDustColor = new Color(0.7f, 0.6f, 0.4f, 0.8f);
        [SerializeField] private int throwParticleCount = 8;

        [Header("Orbit Glow")]
        [ColorUsage(true, true)]
        [SerializeField] private Color orbitGlowColor = new Color(1.5f, 1f, 2.5f, 1f);

        [Header("Crumble VFX")]
        [SerializeField] private Color crumbleColor = new Color(0.6f, 0.5f, 0.35f, 1f);
        [SerializeField] private int crumbleParticleCount = 25;

        // Internal
        private SpriteRenderer[] renderers;
        private LithosMonolithBoss boss;
        private Camera mainCam;
        private Coroutine hitFlashCoroutine;
        private Coroutine shakeCoroutine;
        private bool orbitGlowActive;

        private void Awake()
        {
            boss = GetComponentInParent<LithosMonolithBoss>();
            if (boss == null) boss = GetComponent<LithosMonolithBoss>();

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            mainCam = Camera.main;
        }

        private void Update()
        {
            if (boss == null) return;

            // Rune glow pulse in Phase 2
            if (boss.CurrentPhase == BossPhase.Phase2 && boss.RuneEffects != null)
            {
                float pulse = Mathf.Sin(Time.time * runePulseSpeed) * 0.3f + 0.7f;
                foreach (var rune in boss.RuneEffects)
                {
                    if (rune == null || !rune.activeSelf) continue;
                    SpriteRenderer sr = rune.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = runeGlowColor * pulse;
                        c.a = sr.color.a;
                        sr.color = c;
                    }
                }
            }
        }

        // ── PUBLIC VFX METHODS ──

        /// <summary>Trigger a stone-colored hit flash on all renderers.</summary>
        public void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        /// <summary>Spawn dust cloud VFX when throwing a rock.</summary>
        public void SpawnThrowVFX(Vector3 position)
        {
            StartCoroutine(ThrowVFXRoutine(position));
        }

        /// <summary>Trigger screen shake effect.</summary>
        public void TriggerScreenShake(float duration, float intensity)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ScreenShakeRoutine(duration, intensity));
        }

        /// <summary>Toggle the orbit glow state.</summary>
        public void TriggerOrbitGlow(bool active)
        {
            orbitGlowActive = active;
            if (active)
                StartCoroutine(OrbitGlowRoutine());
        }

        /// <summary>Spawn a rune reveal burst at position.</summary>
        public void SpawnRuneRevealVFX(Vector3 position)
        {
            StartCoroutine(RuneRevealRoutine(position));
        }

        /// <summary>Spawn crumbling debris for death.</summary>
        public void SpawnCrumbleVFX(Vector3 position)
        {
            StartCoroutine(CrumbleVFXRoutine(position));
        }

        /// <summary>Animate the death dissolve effect.</summary>
        public Coroutine StartDeathDissolve(float duration)
        {
            return StartCoroutine(DeathDissolveRoutine(duration));
        }

        // ── COROUTINES ──

        private IEnumerator HitFlashRoutine()
        {
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null) r.color = hitFlashColor;
                }
            }

            yield return new WaitForSeconds(hitFlashDuration);

            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null) r.color = Color.white;
                }
            }
        }

        private IEnumerator ThrowVFXRoutine(Vector3 position)
        {
            GameObject vfxRoot = new GameObject("Lithos_ThrowVFX");
            vfxRoot.transform.position = position;

            List<(SpriteRenderer sr, Vector2 velocity)> particles
                = new List<(SpriteRenderer, Vector2)>();

            for (int i = 0; i < throwParticleCount; i++)
            {
                GameObject p = new GameObject($"DustParticle_{i}");
                p.transform.SetParent(vfxRoot.transform);
                p.transform.position = position;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = Color.Lerp(rockDustColor, new Color(0.5f, 0.45f, 0.3f, 0.6f), Random.Range(0f, 0.5f));
                sr.sortingOrder = 95;

                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(2f, 6f);
                Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

                float scale = Random.Range(0.15f, 0.4f);
                p.transform.localScale = Vector3.one * scale;

                particles.Add((sr, vel));
            }

            float elapsed = 0f;
            float maxTime = 0.5f;
            while (elapsed < maxTime && vfxRoot != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / maxTime;

                for (int i = 0; i < particles.Count; i++)
                {
                    var p = particles[i];
                    if (p.sr != null)
                    {
                        p.sr.transform.position += (Vector3)(p.velocity * Time.deltaTime);
                        Color c = p.sr.color;
                        c.a = Mathf.Lerp(0.8f, 0f, t);
                        p.sr.color = c;
                        p.sr.transform.localScale *= (1f - Time.deltaTime * 3f);
                    }
                }
                yield return null;
            }

            if (vfxRoot != null) Destroy(vfxRoot);
        }

        private IEnumerator OrbitGlowRoutine()
        {
            // Create a pulsating ring
            GameObject ringObj = new GameObject("OrbitGlowRing");
            ringObj.transform.position = boss != null ? boss.CenterPosition : transform.position;

            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = CreateCircleSprite();
            ringSr.sortingOrder = -2;

            float elapsed = 0f;
            while (orbitGlowActive && ringObj != null)
            {
                elapsed += Time.deltaTime;
                if (boss != null)
                    ringObj.transform.position = boss.CenterPosition;

                float pulse = Mathf.Sin(elapsed * 4f) * 0.2f + 0.8f;
                float radius = boss != null ? boss.OrbitRadius * 2f : 5f;
                ringObj.transform.localScale = new Vector3(radius, radius, 1f) * pulse;

                Color c = orbitGlowColor;
                c.a = 0.15f * pulse;
                ringSr.color = c;

                yield return null;
            }

            if (ringObj != null) Destroy(ringObj);
        }

        private IEnumerator RuneRevealRoutine(Vector3 position)
        {
            GameObject burstRoot = new GameObject("RuneRevealBurst");
            burstRoot.transform.position = position;

            int count = 6;
            List<SpriteRenderer> particles = new List<SpriteRenderer>();

            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject($"RuneParticle_{i}");
                p.transform.SetParent(burstRoot.transform);
                p.transform.position = position;

                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = Color.Lerp(runeGlowColor, new Color(2f, 1f, 3f, 1f), Random.Range(0f, 0.5f));
                sr.sortingOrder = 100;
                p.transform.localScale = Vector3.one * Random.Range(0.1f, 0.3f);

                particles.Add(sr);
            }

            float elapsed = 0f;
            float maxTime = 0.6f;
            while (elapsed < maxTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / maxTime;

                for (int i = 0; i < particles.Count; i++)
                {
                    if (particles[i] == null) continue;
                    float angle = (360f / count) * i;
                    float dist = Mathf.Lerp(0f, 1.2f, Mathf.Sqrt(t));
                    particles[i].transform.position = position + new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * dist,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * dist, 0f);

                    Color c = particles[i].color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    particles[i].color = c;
                }
                yield return null;
            }

            Destroy(burstRoot);
        }

        private IEnumerator CrumbleVFXRoutine(Vector3 position)
        {
            GameObject crumbleRoot = new GameObject("Lithos_CrumbleVFX");
            crumbleRoot.transform.position = position;

            for (int i = 0; i < crumbleParticleCount; i++)
            {
                GameObject debris = new GameObject($"CrumbleDebris_{i}");
                debris.transform.SetParent(crumbleRoot.transform);
                debris.transform.position = position + (Vector3)(Random.insideUnitCircle * 2f);

                SpriteRenderer sr = debris.AddComponent<SpriteRenderer>();
                sr.sprite = CreateDiamondSprite();
                sr.color = Color.Lerp(crumbleColor, new Color(0.4f, 0.35f, 0.25f, 1f), Random.Range(0f, 0.5f));
                sr.sortingOrder = 100;

                float scale = Random.Range(0.15f, 0.5f);
                debris.transform.localScale = Vector3.one * scale;

                Rigidbody2D rb = debris.AddComponent<Rigidbody2D>();
                rb.gravityScale = Random.Range(1.5f, 4f);
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                rb.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(3f, 10f);
                rb.angularVelocity = Random.Range(-300f, 300f);
            }

            Destroy(crumbleRoot, 3f);
            yield return null;
        }

        private IEnumerator ScreenShakeRoutine(float duration, float intensity)
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) yield break;

            Vector3 originalPos = mainCam.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float dampedIntensity = intensity * (1f - elapsed / duration);
                mainCam.transform.position = originalPos + (Vector3)(Random.insideUnitCircle * dampedIntensity);
                yield return null;
            }

            mainCam.transform.position = originalPos;
        }

        private IEnumerator DeathDissolveRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (renderers != null)
                {
                    foreach (var r in renderers)
                    {
                        if (r != null)
                        {
                            Color c = r.color;
                            c.a = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));
                            c.r = Mathf.Lerp(c.r, crumbleColor.r * 1.5f, t * 0.3f);
                            c.g = Mathf.Lerp(c.g, crumbleColor.g, t * 0.3f);
                            r.color = c;
                        }
                    }
                }

                yield return null;
            }
        }

        // ── SPRITE GENERATORS ──

        private static Sprite _cachedCircle;
        private static Sprite CreateCircleSprite()
        {
            if (_cachedCircle != null) return _cachedCircle;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        float alpha = 1f - Mathf.Pow(dist / radius, 2f);
                        colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedCircle;
        }

        private static Sprite _cachedDiamond;
        private static Sprite CreateDiamondSprite()
        {
            if (_cachedDiamond != null) return _cachedDiamond;

            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half) / half;
                    float dy = Mathf.Abs(y - half) / half;
                    if (dx + dy <= 1f)
                    {
                        float alpha = 1f - (dx + dy);
                        colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            _cachedDiamond = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedDiamond;
        }
    }
}
