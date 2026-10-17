using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private float walkStepInterval = 0.4f;
    [SerializeField, Min(0.05f)] private float minFootstepInterval = 0.35f;
    [SerializeField, Range(0.1f, 1.5f)] private float footstepClipSpacing = 0.9f;

    [Header("Combat")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip blockSound;
    [SerializeField] private AudioClip parrySound;
    [SerializeField] private AudioClip parryFailSound;

    [Header("Elements")]
    [SerializeField] private AudioClip elementSwitchSound;

    private PlayerHealth health;
    private float nextFootstepTime;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        if (health == null) return;

        health.OnBlockedHit += PlayBlock;
        health.OnHurt += PlayHurt;
        health.OnDied += PlayDeath;
    }

    private void OnDisable()
    {
        if (health == null) return;

        health.OnBlockedHit -= PlayBlock;
        health.OnHurt -= PlayHurt;
        health.OnDied -= PlayDeath;
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.PlaySFXIfAvailable(clip, transform.position);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    public void PlayJump() => PlayOneShot(jumpSound);
    public void PlayDash() => PlayOneShot(dashSound);
    public void PlayAttack() => PlayOneShot(attackSound);
    public void PlayParry() => PlayOneShot(parrySound);
    public void PlayParryFail() => PlayOneShot(parryFailSound);
    public void PlayElementSwitch() => PlayOneShot(elementSwitchSound);

    public void TickFootsteps(
        float dt, bool grounded, Vector2 input,
        bool hurt, bool blocking, bool dashing)
    {
        bool shouldPlay = grounded
            && Mathf.Abs(input.x) > 0.1f
            && !hurt && !blocking && !dashing;

        if (!shouldPlay)
        {
            return;
        }

        AudioClip clip = walkSound;
        if (clip == null || Time.time < nextFootstepTime) return;

        PlayOneShot(clip);
        nextFootstepTime = Time.time + GetFootstepInterval(clip);
    }

    public void StopFootsteps()
    {
        nextFootstepTime = Mathf.Max(nextFootstepTime, Time.time);
    }

    private float GetFootstepInterval(AudioClip clip)
    {
        float clipInterval = clip.length * footstepClipSpacing;
        return Mathf.Max(walkStepInterval, minFootstepInterval, clipInterval);
    }

    private void PlayBlock() => PlayOneShot(blockSound);
    private void PlayHurt() => PlayOneShot(hurtSound);
    private void PlayDeath() => PlayOneShot(deathSound);
}
