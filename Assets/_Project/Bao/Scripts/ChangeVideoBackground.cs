using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class ChangeVideoBackground : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public VideoClip videoClip1;
    public VideoClip videoClip2;

    private bool isPlayingClip2;
    private bool hasFinishedClip2;

    private void OnEnable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
        }
        ApplyAudioVolume();
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
        }
    }

    void Start()
    {
        if (videoPlayer != null && videoClip1 != null)
        {
            isPlayingClip2 = false;
            videoPlayer.clip = videoClip1;
            videoPlayer.Play();
            ApplyAudioVolume();
        }
    }



    // Call this function to switch the video clip to videoClip2
    public void ChangeToVideoClip2()
    {
        if (videoPlayer != null && videoClip2 != null)
        {
            isPlayingClip2 = true;
            hasFinishedClip2 = false;
            videoPlayer.clip = videoClip2;
            videoPlayer.isLooping = false;
            videoPlayer.Play();
            ApplyAudioVolume();
        }
    }

    public IEnumerator WaitForClip2AndDoorEffect()
    {
        if (videoPlayer == null || videoClip2 == null)
        {
            yield break;
        }

        while (isPlayingClip2 && !hasFinishedClip2)
        {
            ApplyAudioVolume();
            yield return null;
        }
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        if (!isPlayingClip2 || source == null || source.clip != videoClip2)
        {
            return;
        }

        hasFinishedClip2 = true;
        isPlayingClip2 = false;
    }

    private void ApplyAudioVolume()
    {
        if (videoPlayer == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ApplyVolumeToVideoPlayerPublic(videoPlayer);
        }
        else
        {
            float bgm = PlayerPrefs.GetFloat("BGMVolume", 1f);
            bool isMuted = PlayerPrefs.GetInt("GameMuted", 0) == 1;
            float targetVol = (isMuted || bgm <= 0.001f) ? 0f : (bgm * bgm);

            int trackCount = Mathf.Max(8, (int)videoPlayer.audioTrackCount);
            for (ushort i = 0; i < trackCount; i++)
            {
                try
                {
                    if (isMuted || bgm <= 0.001f)
                    {
                        videoPlayer.SetDirectAudioMute(i, true);
                        videoPlayer.SetDirectAudioVolume(i, 0f);
                    }
                    else
                    {
                        videoPlayer.SetDirectAudioMute(i, false);
                        videoPlayer.EnableAudioTrack(i, true);
                        videoPlayer.SetDirectAudioVolume(i, targetVol);
                    }

                    AudioSource src = videoPlayer.GetTargetAudioSource(i);
                    if (src != null)
                    {
                        src.mute = isMuted || bgm <= 0.001f;
                        src.volume = targetVol;
                    }
                }
                catch { }
            }

            AudioSource attachedSrc = videoPlayer.GetComponent<AudioSource>();
            if (attachedSrc != null)
            {
                attachedSrc.mute = isMuted || bgm <= 0.001f;
                attachedSrc.volume = targetVol;
            }
        }
    }
}
