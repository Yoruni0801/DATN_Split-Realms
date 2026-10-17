using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Bao.IceSigil
{
    public sealed class CameraFeedbackController
    {
        private readonly RectTransform root;
        private Vector2 basePosition;
        private float shakeTime;
        private float shakeIntensity;

        public CameraFeedbackController(RectTransform root)
        {
            this.root = root;
            if (root != null)
            {
                basePosition = root.anchoredPosition;
            }
        }

        public void Shake(float intensity, float duration)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        public void Update(float deltaTime)
        {
            if (root == null)
            {
                return;
            }

            if (shakeTime <= 0f)
            {
                root.anchoredPosition = Vector2.Lerp(root.anchoredPosition, basePosition, deltaTime * 18f);
                return;
            }

            shakeTime -= deltaTime;
            Vector2 offset = Random.insideUnitCircle * shakeIntensity;
            root.anchoredPosition = basePosition + offset;
            shakeIntensity = Mathf.Lerp(shakeIntensity, 0f, deltaTime * 4.5f);
        }
    }

    public sealed class CarveVFXController
    {
        private readonly MonoBehaviour owner;
        private readonly RectTransform root;
        private float nextContactTime;

        private static readonly Color Cyan = new(0.42f, 1f, 1f, 0.95f);
        private static readonly Color Pale = new(0.86f, 1f, 1f, 0.86f);
        private static readonly Color Bad = new(1f, 0.42f, 0.34f, 0.92f);

        public CarveVFXController(MonoBehaviour owner, RectTransform root)
        {
            this.owner = owner;
            this.root = root;
        }

        public void EmitCarveContact(Vector2 localPosition, bool valid, bool deep, float speed)
        {
            if (owner == null || root == null)
            {
                return;
            }

            if (Time.unscaledTime < nextContactTime)
            {
                return;
            }

            nextContactTime = Time.unscaledTime + (valid ? 0.065f : 0.045f);
            int count = valid ? 1 : 3;
            if (speed > 1000f)
            {
                count += 1;
            }

            for (int i = 0; i < count; i++)
            {
                RectTransform particle = CreateParticle(valid ? (deep ? Pale : Cyan) : Bad, Random.Range(4f, valid ? 9f : 15f));
                particle.anchoredPosition = localPosition + Random.insideUnitCircle * 6f;
                Vector2 direction = (Random.insideUnitCircle + (valid ? Vector2.up * 0.25f : Random.insideUnitCircle)).normalized;
                owner.StartCoroutine(AnimateParticle(particle, direction, Random.Range(24f, valid ? 58f : 96f), Random.Range(0.18f, 0.38f)));
            }
        }

        public void EmitCrack(Vector2 localPosition, Vector2 direction, float severity)
        {
            if (owner == null || root == null)
            {
                return;
            }

            int branches = severity > 0.55f ? 3 : 2;
            for (int i = 0; i < branches; i++)
            {
                RectTransform line = CreateParticle(i == 0 ? Pale : Bad, Mathf.Lerp(2f, 4f, severity));
                line.pivot = new Vector2(0f, 0.5f);
                line.anchoredPosition = localPosition;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + Random.Range(-36f, 36f);
                line.localEulerAngles = new Vector3(0f, 0f, angle);
                line.sizeDelta = new Vector2(Mathf.Lerp(34f, 92f, severity) * Random.Range(0.55f, 1f), Mathf.Lerp(2f, 5f, severity));
                owner.StartCoroutine(FadeAndDestroy(line, 0.68f));
            }
        }

        public void EmitPryBurst(Vector2 localPosition, bool success)
        {
            int count = success ? 18 : 12;
            for (int i = 0; i < count; i++)
            {
                RectTransform particle = CreateParticle(success ? Pale : Bad, Random.Range(7f, success ? 18f : 14f));
                particle.anchoredPosition = localPosition + Random.insideUnitCircle * 12f;
                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction.sqrMagnitude < 0.01f)
                {
                    direction = Vector2.up;
                }

                owner.StartCoroutine(AnimateParticle(particle, direction, Random.Range(80f, success ? 190f : 130f), Random.Range(0.42f, 0.82f)));
            }
        }

        private RectTransform CreateParticle(Color color, float size)
        {
            GameObject gameObject = new("IceCarvingVFX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.sizeDelta = Vector2.one * size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static IEnumerator AnimateParticle(RectTransform rect, Vector2 direction, float distance, float duration)
        {
            Image image = rect.GetComponent<Image>();
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + direction * distance * (1f - Mathf.Pow(1f - t, 2f));
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.16f, t);
                rect.Rotate(0f, 0f, 240f * Time.unscaledDeltaTime);
                if (image != null)
                {
                    Color color = image.color;
                    color.a = 1f - t;
                    image.color = color;
                }

                yield return null;
            }

            if (rect != null)
            {
                Object.Destroy(rect.gameObject);
            }
        }

        private static IEnumerator FadeAndDestroy(RectTransform rect, float duration)
        {
            Image image = rect.GetComponent<Image>();
            float elapsed = 0f;
            Vector3 startScale = rect.localScale;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = new Vector3(Mathf.Lerp(0.3f, startScale.x, Mathf.Sin(t * Mathf.PI)), startScale.y, startScale.z);
                if (image != null)
                {
                    Color color = image.color;
                    color.a = 1f - t;
                    image.color = color;
                }

                yield return null;
            }

            if (rect != null)
            {
                Object.Destroy(rect.gameObject);
            }
        }
    }

    public sealed class CarveAudioController
    {
        private readonly AudioSource source;
        private readonly AudioClip lightScrape;
        private readonly AudioClip hardScrape;
        private readonly AudioClip crackTick;
        private readonly AudioClip prySuccess;
        private readonly AudioClip shatter;
        private float scrapeCooldown;

        public CarveAudioController(GameObject host)
        {
            source = host.GetComponent<AudioSource>();
            if (source == null)
            {
                source = host.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            lightScrape = CreateNoiseClip("ScrapeLight", 0.09f, 0.18f, 0.62f);
            hardScrape = CreateNoiseClip("ScrapeHard", 0.12f, 0.35f, 0.92f);
            crackTick = CreateTickClip("IceTick", 0.12f, 980f);
            prySuccess = CreateTickClip("PrySuccess", 0.22f, 540f);
            shatter = CreateNoiseClip("FullShatter", 0.42f, 0.65f, 1f);
        }

        public void Update(float deltaTime)
        {
            scrapeCooldown = Mathf.Max(0f, scrapeCooldown - deltaTime);
        }

        public void PlayScrape(bool heavy, float volume)
        {
            if (scrapeCooldown > 0f)
            {
                return;
            }

            source.PlayOneShot(heavy ? hardScrape : lightScrape, Mathf.Clamp01(volume));
            scrapeCooldown = heavy ? 0.08f : 0.06f;
        }

        public void PlayCrack(float volume = 0.65f)
        {
            source.PlayOneShot(crackTick, Mathf.Clamp01(volume));
        }

        public void PlayPrySuccess()
        {
            source.PlayOneShot(prySuccess, 0.72f);
        }

        public void PlayShatter()
        {
            source.PlayOneShot(shatter, 0.95f);
        }

        private static AudioClip CreateNoiseClip(string name, float duration, float gain, float brightness)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float last = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float envelope = Mathf.Sin(t * Mathf.PI);
                float raw = Random.Range(-1f, 1f);
                last = Mathf.Lerp(last, raw, brightness);
                samples[i] = last * envelope * gain;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateTickClip(string name, float duration, float frequency)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 18f);
                samples[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * 0.32f;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
